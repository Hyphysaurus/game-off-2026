using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The claw machine's small panel while it is steered (D-0229): its heading, the seconds left before the claw drops by
    /// itself, and the keys. In the screen's top right corner, which the HUD leaves free (the hotbar and the ammo are at
    /// the bottom, the objective card top left, the toasts top middle), and away from the glass's front, where the prizes
    /// are. Made in code the first time it shows (as the armory bench's screen is, with the same UI kit), kept hidden
    /// between rounds, destroyed with the machine. The seconds' words are set only when the whole second changes, so
    /// nothing is made every frame. The words are the settings' (placeholders). Not a component: ClawGame owns one.
    /// THE PIXEL LOOK (D-0243, PixelKit > Pixel On), his mockup's claw panel: a pink-framed pixel panel in the same corner;
    /// the trophy icon and the heading in pink pixel capitals, the seconds big and gold at the right; under a rule, the
    /// keys as glyphs (or cream key caps) with their words ("[MOVE] STEER - [E][Space] DROP - [Esc] GIVE UP"), read from
    /// the same words (each &lt;color&gt; part is a key); the pad's keys (the part with no words after it) on a lilac line
    /// under them. The panel is as wide as its words need (at least his mockup's 520).
    /// </summary>
    internal sealed class ClawGameHud
    {
        private const float Width = 680f, Height = 118f, Inset = 32f;
        private const int Sorting = 40;   // over the game, under the toasts and the key sheet
        // The pixel look (D-0243), his mockup's numbers: the narrowest panel, the padding inside the frame (frame 4 + 18
        // sides, + 14 top, + 16 bottom), the heading's height, the gaps, the room kept for the seconds.
        private const float PixMinWide = 520f, PixPadX = 22f, PixPadTop = 18f, PixPadBottom = 20f, PixHead = 40f, PixGap = 12f, PixRuleGap = 10f, PixNoteGap = 8f, PixTime = 64f;

        private GameObject _root;
        private TextMeshProUGUI _title, _time, _keys;
        private int _shown = int.MinValue;
        // The pixel look.
        private bool _pixel;
        private RectTransform _panel, _rule, _noteRt;
        private Image _trophy;
        private PixelKit.KeyRow _keyRow;
        private TextMeshProUGUI _note;

        /// <summary>Shows the panel with these words (the seconds come with <see cref="Seconds"/>).</summary>
        public void Show(string title, string keys)
        {
            if (_root == null) Build();
            if (!_root.activeSelf) _root.SetActive(true);   // first: the pixel key row's new words are made awake, so they can be measured
            _title.text = title ?? "";
            if (_pixel) LayOut(keys ?? "");
            else _keys.text = keys ?? "";
            _shown = int.MinValue;
        }

        /// <summary>The seconds left: written only when the whole second changes.</summary>
        public void Seconds(int seconds, string format)
        {
            if (_root == null || seconds == _shown) return;
            _shown = seconds;
            _time.text = ClawGameSettings.Fill(format, seconds);
        }

        public void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        /// <summary>Gone for good (the machine is).</summary>
        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        /// <summary>The canvas, scaled with the screen's height like the HUD, and the panel at the bottom middle.</summary>
        private void Build()
        {
            _root = new GameObject("ClawGameHud", typeof(RectTransform));
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Sorting;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = BuildUiKit.HudMatch;
            if (PixelKit.On) { BuildPixel(); _root.SetActive(false); return; }

            RectTransform panel = BuildUiKit.Rect("Panel", _root.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-Inset, -Inset), new Vector2(Width, Height));
            BuildUiKit.Panel(panel, BuildUiKit.PanelColor);
            RectTransform top = BuildUiKit.Rect("Title", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-70f, -14f), new Vector2(-180f, 44f));
            _title = BuildUiKit.Label(top, "", 30f, TextAlignmentOptions.MidlineLeft);
            _title.fontStyle = FontStyles.Bold;
            RectTransform time = BuildUiKit.Rect("Time", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -6f), new Vector2(140f, 64f));
            _time = BuildUiKit.Label(time, "", 54f, TextAlignmentOptions.MidlineRight);
            _time.fontStyle = FontStyles.Bold;
            _time.color = new Color(1f, 0.82f, 0.4f);
            RectTransform keys = BuildUiKit.Rect("Keys", panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-36f, 34f));
            _keys = BuildUiKit.Label(keys, "", 18f, TextAlignmentOptions.MidlineLeft);
            _keys.color = new Color(1f, 1f, 1f, 0.8f);
            _root.SetActive(false);
        }

        /// <summary>The pixel panel (D-0243): its parts, placed by <see cref="LayOut"/> when its words are set.</summary>
        private void BuildPixel()
        {
            _pixel = true;
            Vector2 topLeft = new Vector2(0f, 1f), topRight = new Vector2(1f, 1f), left = new Vector2(0f, 0.5f), right = new Vector2(1f, 0.5f);
            _panel = BuildUiKit.Rect("Panel", _root.transform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-PixelKit.Edge, -PixelKit.Edge), new Vector2(PixMinWide, 160f));
            PixelKit.Panel(_panel, PixelKit.Frame.Normal);
            var head = BuildUiKit.Rect("Heading", _panel, topLeft, topRight, new Vector2(0.5f, 1f), new Vector2(0f, -PixPadTop), new Vector2(-PixPadX * 2f, PixHead));
            var trophy = BuildUiKit.Rect("Trophy", head, left, left, left, Vector2.zero, Vector2.zero);
            _trophy = PixelKit.Icon(trophy, IconSet.Trophy, 2);
            var title = BuildUiKit.Rect("Title", head, left, left, left, Vector2.zero, new Vector2(300f, PixHead));
            _title = PixelKit.Text(title, PixelKit.Big, PixelKit.Pink, true, TextAlignmentOptions.MidlineLeft, true);
            var time = BuildUiKit.Rect("Time", head, right, right, right, Vector2.zero, new Vector2(PixTime * 2f, PixHead));
            _time = PixelKit.Text(time, PixelKit.Huge, PixelKit.Gold, true, TextAlignmentOptions.MidlineRight);
            _rule = BuildUiKit.Rect("Rule", _panel, topLeft, topRight, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-PixPadX * 2f, 2f));
            PixelKit.Block(_rule, PixelKit.BandTop);
            _keyRow = new PixelKit.KeyRow(_panel, "Keys", PixelKit.Body, PixelKit.Cream, PixelKit.Glyph, 10f, 12f, 12f);
            _keyRow.Root.anchorMin = _keyRow.Root.anchorMax = topLeft;
            _noteRt = BuildUiKit.Rect("Pad", _panel, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(300f, 24f));
            _note = PixelKit.Text(_noteRt, PixelKit.Body, PixelKit.Lilac, false, TextAlignmentOptions.MidlineLeft);
        }

        /// <summary>The pixel panel round its words: the heading, the rule, the key row, the pad's line; as wide as the
        /// widest of them needs (his mockup's 520 at least).</summary>
        private void LayOut(string keys)
        {
            float titleWide = PixelKit.Wide(_title, _title.text);
            float iconWide = _trophy.enabled ? _trophy.rectTransform.sizeDelta.x + PixGap : 0f;
            ((RectTransform)_title.transform).anchoredPosition = new Vector2(iconWide, 0f);
            ((RectTransform)_title.transform).sizeDelta = new Vector2(titleWide, PixHead);
            float inner = iconWide + titleWide + PixGap * 2f + PixTime;

            float y = PixPadTop + PixHead + PixGap;
            _rule.anchoredPosition = new Vector2(0f, -y);
            y += 2f + PixRuleGap;
            _keyRow.Tagged(keys);
            _keyRow.Root.anchoredPosition = new Vector2(PixPadX, -(y + PixelKit.Glyph * 0.5f));
            inner = Mathf.Max(inner, _keyRow.Width);
            y += PixelKit.Glyph;
            string note = _keyRow.Note;
            bool any = !string.IsNullOrEmpty(note);
            _noteRt.gameObject.SetActive(any);
            if (any)
            {
                _note.text = note;
                float wide = PixelKit.Wide(_note, note);
                y += PixNoteGap;
                _noteRt.anchoredPosition = new Vector2(PixPadX, -y);
                _noteRt.sizeDelta = new Vector2(wide, 24f);
                inner = Mathf.Max(inner, wide);
                y += 24f;
            }
            y += PixPadBottom;
            _panel.sizeDelta = new Vector2(Mathf.Round(Mathf.Max(PixMinWide, inner + PixPadX * 2f)), Mathf.Round(y));
        }
    }
}
