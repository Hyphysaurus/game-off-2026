using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The Flat Zone letters' strip on screen (D-0208): S H I F T in the top left corner on a sticker card in the toasts'
    /// style (cream card, thick dark outline, a drop shadow), each letter grey until it is found and lit in his title's
    /// colours once it is (a found letter pops in as it lights). It shows while a zone with letters holds the player,
    /// and for a few seconds after a letter is found (FlatView > Strip Seconds); hidden under the menus, the key sheet
    /// and the console. A zone whose letters are all switched off shows no strip. It only reads (FlatZone.Current and
    /// FlatLetter.Mask). One for the whole game, made by the first zone that takes a player; built in code like the
    /// other HUDs.
    /// </summary>
    public sealed class FlatHud : MonoBehaviour
    {
        private const string FontPath = "HUD/Fredoka SDF";
        // At 1920 by 1080, by the screen's height (D-0133): the card's corner, a letter's cell, its size, the card's pad.
        private const float Left = 28f, Top = 26f, Cell = 50f, Gap = 6f, Letters = 52f, Pad = 22f, High = 84f;
        private const float Outline = 4f, Shadow = 6f, Round = 22f, LetterDrop = 4f;
        private const float FadeSeconds = 0.25f, PopSeconds = 0.35f, PopFrom = 1.7f;
        private static readonly Color Ink = new Color32(0x1f, 0x1b, 0x2e, 0xff);
        private static readonly Color Card = new Color32(0xff, 0xf7, 0xe0, 0xff);        // the "got it" toast's cream
        private static readonly Color Unfound = new Color(0x1f / 255f, 0x1b / 255f, 0x2e / 255f, 0.2f);
        // Lit: his title's colours (the sheet's pink, purple and cyan), one a letter.
        private static readonly Color[] Lit =
        {
            new Color32(0xff, 0x72, 0xcc, 0xff), new Color32(0xb2, 0x4d, 0xff, 0xff), new Color32(0x73, 0xf1, 0xff, 0xff),
            new Color32(0xff, 0x93, 0xd8, 0xff), new Color32(0x94, 0xf4, 0xff, 0xff),
        };

        private static FlatHud _instance;
        private CanvasGroup _group;
        private RectTransform[] _cells;
        private TextMeshProUGUI[] _faces, _drops;
        private float[] _litAt;
        private FlatZone _last;
        private float _flashUntil = float.NegativeInfinity;
        private int _shownPresent = -1, _shownFound = -1;
        private FlatZone _shownZone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        /// <summary>The strip, made on the first call (kept across scene loads, like the toasts). Batch runs make none.</summary>
        public static FlatHud Get()
        {
            if (_instance != null) return _instance;
            if (Application.isBatchMode || !Application.isPlaying) return null;
            var go = new GameObject("FlatHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FlatHud>();
            _instance.Build();
            return _instance;
        }

        /// <summary>A letter of <paramref name="zone"/> was just found: the strip shows that zone for Strip Seconds more,
        /// even once the zone has let the player go.</summary>
        public static void Flash(FlatZone zone)
        {
            FlatHud hud = Get();
            if (hud == null || zone == null) return;
            hud._last = zone;
            hud._flashUntil = Time.unscaledTime + Mathf.Max(0f, zone.Settings.stripSeconds);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            FlatZone zone = FlatZone.Current;
            if (zone == null && now < _flashUntil && _last != null) zone = _last;
            int present = 0, found = 0;
            if (zone != null) FlatLetter.Mask(zone, out present, out found);
            bool menus = PauseMenu.IsOpen || TitleMenu.IsOpen || KeyHelp.IsOpen || DebugConsole.IsOpen;
            bool want = !menus && present != 0;
            _group.alpha = Mathf.MoveTowards(_group.alpha, want ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            if (_group.alpha <= 0f) { _shownZone = null; return; }   // read afresh when it shows again (another slot, another zone)
            if (present != 0 && (zone != _shownZone || present != _shownPresent || found != _shownFound))
            {
                bool sameZone = zone == _shownZone;
                for (int i = 0; i < _cells.Length; i++)
                {
                    int bit = 1 << i;
                    bool lit = (found & bit) != 0;
                    bool there = (present & bit) != 0;
                    if (lit && sameZone && (_shownFound & bit) == 0) _litAt[i] = now;   // just found: it pops in
                    _faces[i].color = lit ? Lit[i % Lit.Length] : Unfound;
                    _drops[i].enabled = lit;
                    _cells[i].gameObject.SetActive(there);
                }
                _shownZone = zone; _shownPresent = present; _shownFound = found;
            }
            for (int i = 0; i < _cells.Length; i++)
            {
                float t = Mathf.Clamp01((now - _litAt[i]) / PopSeconds);
                if (t >= 1f && _cells[i].localScale.x == 1f) continue;   // still: nothing is touched (no canvas rebuild)
                float scale = t < 1f ? Mathf.LerpUnclamped(PopFrom, 1f, EaseOutBack(t)) : 1f;
                _cells[i].localScale = new Vector3(scale, scale, 1f);
            }
        }

        private static float EaseOutBack(float t) { const float k = 1.70158f; t -= 1f; return 1f + (k + 1f) * t * t * t + k * t * t; }

        private void Build()
        {
            var font = Resources.Load<TMP_FontAsset>(FontPath);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 36;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; _group.alpha = 0f;

            int n = FlatLetter.Word.Length;
            float wide = Pad * 2f + n * Cell + (n - 1) * Gap;
            var card = BuildUiKit.Rect("Letters", canvasGo.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Left, -Top), new Vector2(wide, High));
            HudShapes.RoundedImage(BuildUiKit.Rect("Shadow", card, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -Shadow), Vector2.zero), Ink, Round);
            HudShapes.RoundedImage(BuildUiKit.Rect("Outline", card, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Ink, Round);
            HudShapes.RoundedImage(BuildUiKit.Rect("Fill", card, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-Outline * 2f, -Outline * 2f)), Card, Round - Outline);

            _cells = new RectTransform[n]; _faces = new TextMeshProUGUI[n]; _drops = new TextMeshProUGUI[n]; _litAt = new float[n];
            for (int i = 0; i < n; i++)
            {
                _litAt[i] = float.NegativeInfinity;
                var cell = BuildUiKit.Rect(FlatLetter.Word[i].ToString(), card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Pad + Cell * 0.5f + i * (Cell + Gap), 0f), new Vector2(Cell, High));
                _cells[i] = cell;
                // A lit letter stands on a dark copy of itself, a little lower: a sticker letter that reads on the cream.
                _drops[i] = Letter(cell, "Drop", FlatLetter.Word[i], font, Ink, new Vector2(0f, -LetterDrop));
                _faces[i] = Letter(cell, "Face", FlatLetter.Word[i], font, Unfound, Vector2.zero);
                _drops[i].enabled = false;
            }
        }

        private static TextMeshProUGUI Letter(RectTransform cell, string name, char letter, TMP_FontAsset font, Color colour, Vector2 offset)
        {
            var rt = BuildUiKit.Rect(name, cell, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), offset, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = letter.ToString(); t.fontSize = Letters; t.fontStyle = FontStyles.Bold; t.color = colour;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }
    }
}
