using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The button-prompt footer of his pixel menu mockup (D-0226): "[arrows] MOVE | [Enter] SELECT | [Esc] BACK", with the
    /// glyphs of the hands used last: the keyboard and mouse, or a pad (a PlayStation pad's own glyphs, any other pad the
    /// Xbox ones). Which hands: at first the game's own rule (DrivingHud.UsingPad: the pad touched more lately than the
    /// keyboard), then whichever was used since: a key, a click or a mouse move is the keyboard; a pad button, its D-pad
    /// or a stick pushed is the pad. The glyphs, the words and the sizes are IconSet's. The menus own it: the title (on
    /// its model: Move and Select on the main page, Back too on the others), the pause menu (all three; Back alone over
    /// the Sticker Chart page). It lies on its owner's canvas, or on a canvas of its own when it must stay over another
    /// page (the pause menu's, over the chart). Laid out when shown or when the hands change; nothing is made or
    /// allocated per frame. Off: IconSet > Footer On (then Make gives null and the menus show none).
    /// Its words are in the pixel HUD's bold pixel font when IconSet > Font is empty and PixelKit > Pixel On and Menu Words
    /// are ticked (D-0243), at the nearest multiple of 8 to Words Size.
    /// </summary>
    public sealed class PromptBar : MonoBehaviour
    {
        /// <summary>Which prompts show.</summary>
        [System.Flags]
        public enum Prompts { None = 0, Move = 1, Select = 2, Back = 4, MoveSelect = Move | Select, All = Move | Select | Back }

        private const float StickPush = 0.5f, MouseMove = 3f;   // how far a stick or the mouse must move to count as used (share; pixels a frame)

        // The hands used last, for every footer (one rule for the whole game).
        private static IconSet.Device _hands;
        private static bool _handsKnown;
        private static int _sensedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _handsKnown = false; _sensedFrame = -1; }

        private IconSet _set;
        private GameObject _host;           // its own canvas (null: it lies on its owner's)
        private RectTransform _row;
        private readonly RectTransform[] _glyphRt = new RectTransform[3], _wordRt = new RectTransform[3], _barRt = new RectTransform[2];
        private readonly Image[] _glyph = new Image[3];
        private readonly TextMeshProUGUI[] _word = new TextMeshProUGUI[3], _bar = new TextMeshProUGUI[2];
        private Prompts _showing;
        private IconSet.Device _drawn;
        // The parts on show in reading order (at most three glyphs, three words, two separators): each one's width and
        // the space before it. Kept, so a layout makes nothing.
        private readonly RectTransform[] _lay = new RectTransform[8];
        private readonly float[] _wide = new float[8], _before = new float[8];

        /// <summary>
        /// A hidden footer under <paramref name="parent"/>: on that canvas, or (<paramref name="ownSorting"/> 0 or more) on a
        /// canvas of its own drawn at that sorting order (1920 by 1080, by the screen's height, as the model menus are).
        /// Null when there is no IconSet or its Footer On is unticked.
        /// </summary>
        public static PromptBar Make(Transform parent, string name, int ownSorting = -1)
        {
            IconSet set = IconSet.Get();
            if (set == null || !set.footerOn || parent == null) return null;
            GameObject host = null;
            Transform under = parent;
            if (ownSorting >= 0)
            {
                host = new GameObject(name, typeof(RectTransform));
                host.transform.SetParent(parent, false);
                var canvas = host.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = ownSorting;
                var scaler = host.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 1f;
                under = host.transform;
            }
            var row = new GameObject(host != null ? "Row" : name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(under, false);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(10f, set.glyphSize);
            var bar = row.gameObject.AddComponent<PromptBar>();
            bar._set = set; bar._host = host; bar._row = row;
            bar.Build();
            bar.Hide();
            return bar;
        }

        private void Build()
        {
            for (int i = 0; i < 3; i++)
            {
                _glyphRt[i] = Part("Glyph" + i);
                _glyph[i] = _glyphRt[i].gameObject.AddComponent<Image>();
                _glyph[i].raycastTarget = false; _glyph[i].preserveAspect = true;
                _wordRt[i] = Part("Words" + i);
                _word[i] = Words(_wordRt[i], _set.wordsColour);
            }
            for (int i = 0; i < 2; i++)
            {
                _barRt[i] = Part("Separator" + i);
                _bar[i] = Words(_barRt[i], _set.separatorColour);
            }
        }

        private RectTransform Part(string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(_row, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            return rt;
        }

        private TextMeshProUGUI Words(RectTransform rt, Color colour)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_set.font != null) t.font = _set.font;
            t.fontSize = _set.wordsSize; t.color = colour; t.raycastTarget = false;
            if (_set.font == null && PixelKit.MenuWords)
            {
                // His pixel mockup's footer (D-0243): the bold pixel font at its nearest sharp size (Words Size 22 -> 24).
                t.font = PixelKit.Bold;
                t.fontSize = Mathf.Max(PixelKit.Body, Mathf.Round(_set.wordsSize / 8f) * 8f);
            }
            t.fontStyle = FontStyles.Bold | (_set.capitals ? FontStyles.UpperCase : FontStyles.Normal);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>The footer's middle, canvas pixels from the middle of its canvas.</summary>
        public void At(Vector2 fromMiddle) => _row.anchoredPosition = fromMiddle;

        /// <summary>Under the model's hint place (Anchor_Hint, canvas pixels from the canvas's middle): IconSet > Below Hint lower.</summary>
        public void UnderHint(Vector2 anchor) => At(anchor - new Vector2(0f, _set.belowHint));

        /// <summary>How far the hint line moves up from Anchor_Hint to make room for the footer (IconSet > Hint Lift).</summary>
        public float HintLift => _set.hintLift;

        /// <summary>At IconSet > From Bottom up from the bottom of a 1080-high canvas matched by height (its own canvas).</summary>
        public void AtBottom() => At(new Vector2(0f, -540f + _set.fromBottom));

        /// <summary>Shows these prompts (None hides it), in the glyphs of the hands used last.</summary>
        public void Show(Prompts which)
        {
            if (which == Prompts.None) { Hide(); return; }
            _showing = which;
            if (_host != null) _host.SetActive(true);
            _row.gameObject.SetActive(true);
            Draw(Sense());
        }

        public void Hide()
        {
            _showing = Prompts.None;
            _row.gameObject.SetActive(false);
            if (_host != null) _host.SetActive(false);
        }

        private void Update()
        {
            IconSet.Device now = Sense();
            if (now != _drawn) Draw(now);
        }

        /// <summary>The glyphs for <paramref name="hands"/>, the words, and the row laid out in the middle.</summary>
        private void Draw(IconSet.Device hands)
        {
            _drawn = hands;
            // First what shows and how wide each part is (into _lay, in reading order), then each part placed.
            int parts = 0, bars = 0;
            bool first = true;
            for (int i = 0; i < 3; i++)
            {
                bool on = (_showing & (Prompts)(1 << i)) != 0;
                _glyphRt[i].gameObject.SetActive(false);
                _wordRt[i].gameObject.SetActive(on);
                if (!on) continue;
                if (!first && !string.IsNullOrEmpty(_set.separator) && bars < 2)
                {
                    TextMeshProUGUI bar = _bar[bars];
                    bar.text = _set.separator;
                    _lay[parts] = _barRt[bars]; _wide[parts] = bar.GetPreferredValues(bar.text).x; _before[parts] = _set.itemGap; parts++;
                    bars++;
                }
                float before = first ? 0f : _set.itemGap;
                first = false;
                Sprite glyph = _set.Glyph(hands, i);
                _glyph[i].sprite = glyph;
                if (glyph != null)
                {
                    _glyphRt[i].gameObject.SetActive(true);
                    _lay[parts] = _glyphRt[i]; _wide[parts] = _set.glyphSize * glyph.rect.width / Mathf.Max(1f, glyph.rect.height); _before[parts] = before; parts++;
                    before = _set.glyphGap;
                }
                TextMeshProUGUI word = _word[i];
                word.text = i == 0 ? _set.moveWords : i == 1 ? _set.selectWords : _set.backWords;
                _lay[parts] = _wordRt[i]; _wide[parts] = word.GetPreferredValues(word.text).x; _before[parts] = before; parts++;
            }
            for (int b = 0; b < 2; b++) _barRt[b].gameObject.SetActive(b < bars);
            float total = 0f;
            for (int p = 0; p < parts; p++) total += _before[p] + _wide[p];
            float x = -total * 0.5f;
            for (int p = 0; p < parts; p++)
            {
                x += _before[p];
                _lay[p].anchoredPosition = new Vector2(x, 0f);
                _lay[p].sizeDelta = new Vector2(_wide[p], _set.glyphSize);
                x += _wide[p];
            }
            _row.sizeDelta = new Vector2(total, _set.glyphSize);
        }

        /// <summary>The hands used last (once a frame for every footer): the game's own rule at first, then the last used.</summary>
        private static IconSet.Device Sense()
        {
            if (_sensedFrame == Time.frameCount && _handsKnown) return _hands;
            _sensedFrame = Time.frameCount;
            Gamepad pad = Gamepad.current;
            if (!_handsKnown) { _hands = DrivingHud.UsingPad() ? PadKind(pad) : IconSet.Device.Keyboard; _handsKnown = true; }
            if (pad != null && PadUsed(pad)) _hands = PadKind(pad);
            else if (KeysUsed()) _hands = IconSet.Device.Keyboard;
            return _hands;
        }

        private static IconSet.Device PadKind(Gamepad pad) => pad is DualShockGamepad ? IconSet.Device.PlayStation : pad != null ? IconSet.Device.Xbox : IconSet.Device.Keyboard;

        /// <summary>A pad button, its D-pad or a stick used this frame.</summary>
        private static bool PadUsed(Gamepad pad)
        {
            if (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame) return true;
            if (pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame) return true;
            if (pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame) return true;
            if (pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame) return true;
            return pad.leftStick.ReadValue().sqrMagnitude > StickPush * StickPush || pad.rightStick.ReadValue().sqrMagnitude > StickPush * StickPush;
        }

        /// <summary>A key, a mouse button or the mouse moved this frame.</summary>
        private static bool KeysUsed()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.anyKey.wasPressedThisFrame) return true;
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) return true;
            return mouse.delta.ReadValue().sqrMagnitude > MouseMove * MouseMove;
        }
    }
}
