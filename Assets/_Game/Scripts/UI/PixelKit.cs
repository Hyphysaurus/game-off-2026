using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// His pixel HUD (D-0243, from his pixel menu mockup): the palette, the pixel font (Pixel Operator 8, CC0) and the parts
    /// every in-game panel is made of, built in code like HudShapes. A panel is an outer frame (pink normal, cyan selected,
    /// gold reward, deep purple quiet) with stepped pixel corners round a dark purple fill with lighter bands; a bar is a row
    /// of cells; a slot is a square frame; a key prompt is a Kenney key glyph (or a cream key cap) beside bold capital words,
    /// gold dashes between. The frames are tiny sprites drawn once in code (one texture pixel = one canvas pixel at 1080
    /// high, Point filtered, 9-sliced), so they stay sharp at any panel size. Resources/PixelKit (made by Shift Jam > UI >
    /// Make HUD Fonts) holds the switch and the two font assets. OFF SWITCH: untick Pixel On there, or delete the asset:
    /// every HUD looks as it did before (each HUD reads it when it is made). Without the font assets it is off too.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Pixel Kit", fileName = "PixelKit")]
    public sealed class PixelKit : ScriptableObject
    {
        public const string ResourcePath = "PixelKit";
        /// <summary>The font assets' names in Resources/HUD (Make HUD Fonts makes them).</summary>
        public const string FontName = "Pixel Operator 8", BoldFontName = "Pixel Operator 8 Bold";

        // The font's sharp sizes: it is drawn on an 8-pixel grid, so it is crisp only at whole multiples of 8 (at 1080 high).
        public const int Tiny = 8, Body = 16, Big = 24, Huge = 32;
        /// <summary>Room between the HUD's panels and the screen's edges, canvas pixels at 1080 high (his mockup's 24).</summary>
        public const float Edge = 24f;
        /// <summary>The hotbar's slots: their side and the gap between them (his mockup: 84 and 10).</summary>
        public const float Slot = 84f, SlotGap = 10f;
        /// <summary>The bottom-right status panel's width (health and shift).</summary>
        public const float StatusWidth = 380f;
        /// <summary>A key glyph's height (the Kenney glyphs are 64: 32 is exactly half).</summary>
        public const float Glyph = 32f;

        [Tooltip("OFF SWITCH: unticked, every HUD and in-game panel looks as it did before D-0243 (the rounded cream cards, the plain bars, the old fonts). Each HUD reads it when it is made, so change it before Play.")]
        public bool pixelOn = true;
        [Tooltip("The pixel font (Pixel Operator 8, CC0) as a bitmap TextMesh Pro font asset. Make HUD Fonts sets it. Empty = Resources/HUD/Pixel Operator 8.")]
        public TMP_FontAsset font;
        [Tooltip("Its bold face (Pixel Operator 8 Bold). Make HUD Fonts sets it. Empty = Resources/HUD/Pixel Operator 8 Bold.")]
        public TMP_FontAsset boldFont;
        [Tooltip("The objective card shows the big goal (ObjectiveSet > Goal) in a small line under its zone heading. Unticked: as his mockup (zone, star count, step, chart line); the goal then shows only outside every zone.")]
        public bool goalLine = true;
        [Range(2, 20), Tooltip("Cells in the health bar and the shift bar (his mockup: 10).")]
        public int barCells = 10;
        [Tooltip("The status panel's label right of the health bar (his mockup's).")]
        public string healthLabel = "HP";
        [Tooltip("The status panel's label right of the shift bar (his mockup's).")]
        public string shiftLabel = "SHIFT";
        [Tooltip("The title and pause menus' slab words and their button-prompt footer in the pixel font too (bold). Unticked: the menus keep TextMesh Pro's own font while the HUD is pixel. Only when the menu model's own Font is empty. Read when a slab's words are first made.")]
        public bool menuWords = true;

        // ---------- the palette (his menu's own colours, MenuModel and his mockup) ----------

        public static readonly Color Ink = Hex(0x0A0812), Fill = Hex(0x37125C), Deep = Hex(0x492393), Purple = Hex(0xB24DFF);
        public static readonly Color Pink = Hex(0xFF72CC), Cyan = Hex(0x73F1FF), Gold = Hex(0xF5C84C), Cream = Hex(0xFFF0FC), Lilac = Hex(0xCFC3F2);
        /// <summary>The fill's bands: under the top edge, along the bottom, down the sides.</summary>
        public static readonly Color BandTop = Hex(0xA8319D), BandBottom = Hex(0xBF329C), BandSide = Hex(0xC0389B);
        /// <summary>An unlit bar cell, a held slot's fill; an empty slot's fill; a quiet panel's fill and its words.</summary>
        public static readonly Color EmptyCell = Hex(0x3A1B5E), SlotFill = Hex(0x1A0C2E), QuietFill = Hex(0x24113D), QuietText = Hex(0x6D3BAB);
        /// <summary>The key prompts' see-through dark fill (his mockup: rgba(20, 9, 36, 0.92)).</summary>
        public static readonly Color ChipFill = new Color32(0x14, 0x09, 0x24, 235);

        /// <summary>The frames the kit draws (each a 9-sliced sprite of its own colours).</summary>
        public enum Frame
        {
            /// <summary>Pink frame, purple fill with bands: a panel.</summary>
            Normal,
            /// <summary>Cyan frame: the one picked, a tip.</summary>
            Selected,
            /// <summary>Gold frame: a reward.</summary>
            Reward,
            /// <summary>Purple frame: a core.</summary>
            Core,
            /// <summary>Deep purple frame on a darker fill, no bands: locked, empty or quiet.</summary>
            Quiet,
            /// <summary>Cyan frame on cream: the toy talking.</summary>
            Speech,
            /// <summary>Dim purple frame on cream: an older line of the toy's.</summary>
            SpeechQuiet,
            /// <summary>Deep purple frame on the see-through dark fill: the key prompt footer.</summary>
            Footer,
            /// <summary>Thin cyan frame on the dark fill: the held thing's name tag.</summary>
            NameTag,
            /// <summary>A held slot: cyan frame, purple fill, light top and dark bottom.</summary>
            SlotHeld,
            /// <summary>An empty slot: deep purple frame, darkest fill.</summary>
            SlotEmpty,
            /// <summary>A slot with a thing in it, not in hand: deep purple frame, the held slot's purple fill.</summary>
            SlotFilled,
            /// <summary>Thin cyan frame, faint cyan fill: the zone the player is in (the Sticker Chart).</summary>
            Highlight,
            /// <summary>White, no frame, small stepped corners: a key cap or a badge (tint it with the Image's colour).</summary>
            Solid,
            /// <summary>White 3-pixel square outline: a tick box (tint it).</summary>
            Box,
        }

        private static PixelKit _loaded;
        private static bool _looked;
        private static TMP_FontAsset _regular, _bold;
        private static readonly Dictionary<Spec, Sprite> _sprites = new Dictionary<Spec, Sprite>();
        private static Sprite _tick, _tail, _arrow;

        /// <summary>Where the bottom-left hotbar reaches while it shows (its right edge from the screen's left, its top over
        /// the bottom; both 0 while hidden), and the bottom-right status panel's (its width with the edge, its top), canvas
        /// pixels: the key prompt footer keeps clear of both. Set by those HUDs when they change.</summary>
        public static float HotbarRight, HotbarTop, StatusWide, StatusTop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = null; _looked = false; _regular = _bold = null;
            HotbarRight = HotbarTop = StatusWide = StatusTop = 0f;
        }

        /// <summary>Resources/PixelKit, or null (the pixel look is off).</summary>
        public static PixelKit Get()
        {
            if (!_looked) { _loaded = Resources.Load<PixelKit>(ResourcePath); _looked = true; }
            return _loaded;
        }

        /// <summary>The pixel look is on: the asset is there, Pixel On is ticked and both font assets are found.</summary>
        public static bool On
        {
            get
            {
                PixelKit kit = Get();
                return kit != null && kit.pixelOn && Regular != null && Bold != null;
            }
        }

        /// <summary>The menus' words are pixel too: the pixel look is on and Menu Words is ticked.</summary>
        public static bool MenuWords { get { PixelKit kit = Get(); return kit != null && kit.menuWords && On; } }

        /// <summary>The settings' cells per bar (10 without the asset).</summary>
        public static int BarCells { get { PixelKit kit = Get(); return kit != null ? Mathf.Clamp(kit.barCells, 2, 20) : 10; } }

        /// <summary>The objective card's goal line is wanted (see <see cref="goalLine"/>).</summary>
        public static bool GoalLine { get { PixelKit kit = Get(); return kit == null || kit.goalLine; } }

        /// <summary>The status panel's two labels (the asset's words).</summary>
        public static string HealthLabel { get { PixelKit kit = Get(); return kit != null ? kit.healthLabel ?? "" : "HP"; } }
        public static string ShiftLabel { get { PixelKit kit = Get(); return kit != null ? kit.shiftLabel ?? "" : "SHIFT"; } }

        /// <summary>The regular pixel font (its bold is set as its bold weight, so &lt;b&gt; uses the real bold face).</summary>
        public static TMP_FontAsset Regular
        {
            get
            {
                if (_regular != null) return _regular;
                PixelKit kit = Get();
                _regular = kit != null && kit.font != null ? kit.font : Resources.Load<TMP_FontAsset>("HUD/" + FontName);
                return _regular;
            }
        }

        /// <summary>The bold pixel font.</summary>
        public static TMP_FontAsset Bold
        {
            get
            {
                if (_bold != null) return _bold;
                PixelKit kit = Get();
                _bold = kit != null && kit.boldFont != null ? kit.boldFont : Resources.Load<TMP_FontAsset>("HUD/" + BoldFontName);
                return _bold;
            }
        }

        // ---------- the makers ----------

        /// <summary>Words in the pixel font: <paramref name="size"/> should be one of Tiny, Body, Big, Huge (whole multiples
        /// of 8). One line, no squeezing; <paramref name="caps"/> shows them in capitals (the words themselves stay as they are).</summary>
        public static TextMeshProUGUI Text(RectTransform rt, int size, Color colour, bool bold, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool caps = false)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? Bold : Regular;
            t.fontSize = size;
            t.fontStyle = caps ? FontStyles.UpperCase : FontStyles.Normal;
            t.color = colour;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.characterSpacing = 0f;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>The frame <paramref name="frame"/> on <paramref name="rt"/> (an Image, 9-sliced, not clickable).</summary>
        public static Image Panel(RectTransform rt, Frame frame)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            Restyle(img, frame);
            return img;
        }

        /// <summary>Puts another frame on an Image made by <see cref="Panel"/> (a slot picked, a card's kind).</summary>
        public static void Restyle(Image img, Frame frame)
        {
            if (img == null) return;
            img.sprite = FrameSprite(frame);
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = 1f;
            if (frame != Frame.Solid && frame != Frame.Box) img.color = Color.white;
        }

        /// <summary>A panel frame of any two colours (the big card's: an ink frame round the moment's own colour).</summary>
        public static Image Panel(RectTransform rt, Color frame, Color fill)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite = SpriteOf(new Spec { frame = frame, fill = fill, frameW = 4, notch = 6, innerNotch = 4 });
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        /// <summary>The sprite of a frame (drawn the first time it is asked for).</summary>
        public static Sprite FrameSprite(Frame frame) => SpriteOf(SpecOf(frame));

        /// <summary>A plain rectangle of <paramref name="colour"/> (a bar cell, a dash, a rule): no sprite, so it is exact.</summary>
        public static Image Block(RectTransform rt, Color colour)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>The HUD icon <paramref name="id"/> of the IconSet at <paramref name="scale"/> screen pixels per icon pixel,
        /// on <paramref name="rt"/> (sized to it). Hidden when the set has no such icon. Returns the Image.</summary>
        public static Image Icon(RectTransform rt, string id, int scale)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            SetIcon(img, id, scale);
            return img;
        }

        /// <summary>Puts the icon <paramref name="id"/> on an Image made by <see cref="Icon"/>, sized to it; hides it when the
        /// set has none.</summary>
        public static void SetIcon(Image img, string id, int scale)
        {
            if (img == null) return;
            IconSet set = IconSet.Get();
            Sprite s = set != null ? set.Hud(id) : null;
            img.sprite = s;
            img.enabled = s != null;
            if (s != null) img.rectTransform.sizeDelta = s.rect.size * Mathf.Max(1, scale);
        }

        /// <summary>The kit's tick mark (12 by 9, white: tint it), drawn once.</summary>
        public static Sprite Tick => _tick != null ? _tick : _tick = Bitmap("Tick", new[]
        {
            "..........##",
            ".........###",
            "........###.",
            "##.....###..",
            "###...###...",
            ".###.###....",
            "..#####.....",
            "...###......",
            "....#.......",
        });

        /// <summary>The speech card's tail: a stepped triangle 24 wide and 14 high pointing down (white: tint it).</summary>
        public static Sprite Tail
        {
            get
            {
                if (_tail != null) return _tail;
                const int w = 24, h = 14;
                var rows = new string[h];
                for (int y = 0; y < h; y++)
                {
                    int half = Mathf.CeilToInt(12f * (h - y) / h);
                    var row = new char[w];
                    for (int x = 0; x < w; x++) row[x] = x >= 12 - half && x < 12 + half ? '#' : '.';
                    rows[y] = new string(row);
                }
                return _tail = Bitmap("Tail", rows);
            }
        }

        /// <summary>A small stepped arrow pointing right, 8 by 14 (white: tint it): the current step on the Sticker Chart.</summary>
        public static Sprite Arrow
        {
            get
            {
                if (_arrow != null) return _arrow;
                const int w = 8, h = 14;
                var rows = new string[h];
                for (int y = 0; y < h; y++)
                {
                    var row = new char[w];
                    for (int x = 0; x < w; x++)
                        row[x] = Mathf.Abs(y + 0.5f - h * 0.5f) <= h * 0.5f * (1f - (float)x / w) ? '#' : '.';
                    rows[y] = new string(row);
                }
                return _arrow = Bitmap("Arrow", rows);
            }
        }

        /// <summary>An overlay canvas for a HUD on <paramref name="host"/>: 1920 by 1080 scaled by the screen's height (as
        /// every HUD, D-0133), drawn at <paramref name="sorting"/>, with a CanvasGroup that never takes clicks.</summary>
        public static CanvasGroup MakeCanvas(GameObject host, int sorting)
        {
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sorting;
            var scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = BuildUiKit.HudMatch;
            var group = host.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            return group;
        }

        /// <summary>A whole-pixel width for words: their preferred width rounded up.</summary>
        public static float Wide(TMP_Text t, string text) => string.IsNullOrEmpty(text) ? 0f : Mathf.Ceil(t.GetPreferredValues(text).x);

        // ---------- a bar of cells ----------

        /// <summary>A bar of cells along a fixed width (whole-pixel cells, the spare pixels shared out from the left):
        /// health, shift, the shield's strip. Its colours change only when what it shows changes.</summary>
        public sealed class Cells
        {
            private readonly RectTransform _root;
            private Image[] _cells = new Image[0];
            private readonly float _gap;
            private int _lit = -1;
            private Color _on, _off;

            public RectTransform Root => _root;
            public int Count => _cells.Length;

            /// <summary>A bar of <paramref name="count"/> cells filling <paramref name="root"/> (its width and height).</summary>
            public Cells(RectTransform root, int count, float gap)
            {
                _root = root;
                _gap = gap;
                Resize(count);
            }

            /// <summary>Makes it <paramref name="count"/> cells (only when that changes).</summary>
            public void Resize(int count)
            {
                count = Mathf.Max(1, count);
                if (count == _cells.Length) return;
                foreach (Image c in _cells) if (c != null) UnityEngine.Object.Destroy(c.gameObject);
                _cells = new Image[count];
                for (int i = 0; i < count; i++)
                {
                    var rt = BuildUiKit.Rect("Cell" + (i + 1), _root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                    _cells[i] = Block(rt, Color.white);
                }
                Lay();
                _lit = -1;
            }

            /// <summary>Lays the cells out again for the root's width (after it changes).</summary>
            public void Lay()
            {
                int n = _cells.Length;
                float width = _root.rect.width > 0f ? _root.rect.width : _root.sizeDelta.x;
                int total = Mathf.Max(n, Mathf.FloorToInt(width - _gap * (n - 1)));
                int each = total / n, spare = total - each * n;
                float x = 0f;
                for (int i = 0; i < n; i++)
                {
                    int w = each + (i < spare ? 1 : 0);
                    var rt = _cells[i].rectTransform;
                    rt.anchoredPosition = new Vector2(x, 0f);
                    rt.sizeDelta = new Vector2(w, 0f);
                    x += w + _gap;
                }
            }

            /// <summary><paramref name="lit"/> cells from the left in <paramref name="on"/>, the rest in <paramref name="off"/>.</summary>
            public void Show(int lit, Color on, Color off)
            {
                if (lit == _lit && on == _on && off == _off) return;
                _lit = lit; _on = on; _off = off;
                for (int i = 0; i < _cells.Length; i++) _cells[i].color = i < lit ? on : off;
            }

            /// <summary>How many of <paramref name="count"/> cells a share lights: any share above nothing lights at least one.</summary>
            public static int LitFor(float share, int count) => share <= 0f ? 0 : Mathf.Clamp(Mathf.CeilToInt(share * count - 1e-4f), 1, count);
        }

        // ---------- the status panel ----------

        /// <summary>The status panel's numbers (his mockup): the frame and padding round its rows, a row's height and the
        /// gap between rows, the icon's box, the gap after it, the label's room.</summary>
        public const float StatusPadX = 20f, StatusPadY = 16f, RowHigh = 32f, RowGap = 10f, IconBox = 32f, IconGap = 12f, LabelWide = 56f;

        /// <summary>The status panel's height for <paramref name="rows"/> rows.</summary>
        public static float StatusHigh(int rows) => StatusPadY * 2f + rows * RowHigh + Mathf.Max(0, rows - 1) * RowGap;

        /// <summary>The bottom-right status panel (his mockup): a Normal frame <see cref="StatusWidth"/> wide and
        /// <paramref name="rows"/> rows high, at the corner, under <paramref name="parent"/>.</summary>
        public static RectTransform StatusPanel(Transform parent, int rows)
        {
            var rt = BuildUiKit.Rect("Status", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Edge, Edge), new Vector2(StatusWidth, StatusHigh(rows)));
            Panel(rt, Frame.Normal);
            return rt;
        }

        /// <summary>One row of the status panel: an icon (2 screen pixels per icon pixel), a bar of cells, a short label at
        /// its right in the bar's colour.</summary>
        public sealed class StatusRow
        {
            public readonly RectTransform Root;
            public readonly Image Icon;
            public readonly TextMeshProUGUI Label;
            public readonly Cells Bar;
            /// <summary>A thin bar over the main one (the shield's over the health), or null.</summary>
            public Cells Strip { get; private set; }

            /// <param name="index">The row's place from the top (0 = the first).</param>
            public StatusRow(RectTransform panel, int index, string iconId, string label, Color colour, int cells)
            {
                float y = StatusPadY + index * (RowHigh + RowGap);
                Root = BuildUiKit.Rect("Row" + (index + 1), panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(-StatusPadX * 2f, RowHigh));
                var icon = BuildUiKit.Rect("Icon", Root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(IconBox * 0.5f, 0f), new Vector2(IconBox, IconBox));
                Icon = PixelKit.Icon(icon, iconId, 2);
                var bar = BuildUiKit.Rect("Bar", Root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                bar.offsetMin = new Vector2(IconBox + IconGap, -8f);
                bar.offsetMax = new Vector2(-(LabelWide + IconGap), 8f);
                Bar = new Cells(bar, cells, 3f);
                var words = BuildUiKit.Rect("Label", Root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(LabelWide, RowHigh));
                Label = Text(words, Body, colour, true, TextAlignmentOptions.MidlineLeft);
                Label.text = label;
            }

            /// <summary>Adds a thin bar (4 high) over the main one, which moves down to make room.</summary>
            public Cells AddStrip(int cells)
            {
                RectTransform bar = Bar.Root;
                bar.offsetMin = new Vector2(bar.offsetMin.x, -11f);
                bar.offsetMax = new Vector2(bar.offsetMax.x, 5f);
                var strip = BuildUiKit.Rect("Strip", Root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                strip.offsetMin = new Vector2(bar.offsetMin.x, 7f);
                strip.offsetMax = new Vector2(bar.offsetMax.x, 11f);
                Strip = new Cells(strip, cells, 3f);
                return Strip;
            }
        }

        // ---------- a row of key prompts ----------

        /// <summary>
        /// A row of key prompts ("[E] GRAB - [X] PUT DOWN"): for each, the key's Kenney glyph (IconSet > Keys) or, for a key
        /// with no glyph, a cream key cap with the key's name; then its words, bold and in capitals; a gold dash between
        /// two prompts. It reads the game's own prompt words in three forms: <see cref="Prompt"/> ("E  Grab     X  Put down":
        /// two spaces after a key, three or more between prompts), <see cref="Colon"/> ("Tab: Sticker Chart") and
        /// <see cref="Tagged"/> (the claw panel's "&lt;color=..&gt;Move&lt;/color&gt; steer  ..."). Its parts are kept and
        /// reused, and it is laid out only when its words change. Its root's pivot is its left middle: place the root.
        /// </summary>
        public sealed class KeyRow
        {
            private readonly RectTransform _root;
            private readonly int _wordSize;
            private readonly Color _wordColour;
            private readonly bool _boldWords;
            private readonly float _glyph, _keyGap, _itemGap, _dash;
            private readonly List<Image> _glyphs = new List<Image>();
            private readonly List<RectTransform> _caps = new List<RectTransform>();
            private readonly List<TextMeshProUGUI> _capWords = new List<TextMeshProUGUI>();
            private readonly List<TextMeshProUGUI> _words = new List<TextMeshProUGUI>();
            private readonly List<Image> _dashes = new List<Image>();
            private int _g, _c, _w, _d, _items;
            private float _x;
            private string _shown;

            public RectTransform Root => _root;
            /// <summary>The row's width and height now (canvas pixels).</summary>
            public float Width { get; private set; }
            public float Height => _glyph;
            /// <summary>Prompts on show now.</summary>
            public int Items => _items;

            /// <param name="parent">Where the row goes.</param>
            /// <param name="wordSize">The words' size (Body is the mockup's).</param>
            /// <param name="wordColour">The words' colour (cream in the mockup).</param>
            /// <param name="glyph">A key glyph's height (32 = half the Kenney glyphs' own).</param>
            /// <param name="keyGap">Between a key and its words.</param>
            /// <param name="itemGap">Either side of the gold dash between two prompts.</param>
            /// <param name="dash">The gold dash's width (it is 4 high).</param>
            /// <param name="boldWords">A key's words bold (his footer's) or regular (his objective card's chart line).</param>
            public KeyRow(RectTransform parent, string name, int wordSize, Color wordColour, float glyph = Glyph, float keyGap = 12f, float itemGap = 12f, float dash = 16f, bool boldWords = true)
            {
                _root = BuildUiKit.Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, glyph));
                _wordSize = wordSize; _wordColour = wordColour; _boldWords = boldWords;
                _glyph = glyph; _keyGap = keyGap; _itemGap = itemGap; _dash = dash;
            }

            /// <summary>The game's prompt words: "E  Grab", "LMB  Light     X  Put down", or words with no key.</summary>
            public void Prompt(string text)
            {
                if (!Begin(text)) return;
                string[] items = SplitItems(text);
                foreach (string item in items)
                {
                    int cut = item.IndexOf("  ", StringComparison.Ordinal);
                    if (cut > 0) Item(item.Substring(0, cut).Trim(), item.Substring(cut).Trim());
                    else Item("", item.Trim());
                }
                End();
            }

            /// <summary>"Key: words" (the objective card's chart line); no colon = words alone.</summary>
            public void Colon(string text)
            {
                if (!Begin(text)) return;
                int cut = text.IndexOf(": ", StringComparison.Ordinal);
                if (cut > 0) Item(text.Substring(0, cut).Trim(), text.Substring(cut + 2).Trim());
                else Item("", text.Trim());
                End();
            }

            /// <summary>The claw panel's form: each key in a &lt;color&gt; tag, its words after it. A tagged part with no words
            /// after it (the last, "(pad: stick, A/Y, B)") is not drawn in the row: it is <see cref="Note"/>, for a line of its own.</summary>
            public void Tagged(string text)
            {
                if (!Begin(text)) return;
                Note = "";
                const string open = "<color=", close = "</color>";
                int at = text.IndexOf(open, StringComparison.OrdinalIgnoreCase);
                if (at < 0) { Item("", text.Trim()); End(); return; }
                if (at > 0 && text.Substring(0, at).Trim().Length > 0) Item("", text.Substring(0, at).Trim());
                while (at >= 0 && at < text.Length)
                {
                    int shut = text.IndexOf('>', at);
                    int end = shut < 0 ? -1 : text.IndexOf(close, shut, StringComparison.OrdinalIgnoreCase);
                    if (end < 0) { Item("", Strip(text.Substring(at)).Trim()); break; }
                    string key = text.Substring(shut + 1, end - shut - 1).Trim();
                    int next = text.IndexOf(open, end, StringComparison.OrdinalIgnoreCase);
                    string words = (next < 0 ? text.Substring(end + close.Length) : text.Substring(end + close.Length, next - end - close.Length)).Trim();
                    if (words.Length > 0) Item(key, words);
                    else Note = Note.Length > 0 ? Note + "  " + key : key;
                    at = next;
                }
                End();
            }

            /// <summary>What <see cref="Tagged"/> found with no words after it (empty when none).</summary>
            public string Note { get; private set; } = "";

            /// <summary>Nothing on show.</summary>
            public void Clear()
            {
                if (!Begin("")) return;
                End();
            }

            private bool Begin(string text)
            {
                text ??= "";
                if (_shown != null && string.Equals(text, _shown, StringComparison.Ordinal)) return false;
                _shown = text;
                _g = _c = _w = _d = _items = 0;
                _x = 0f;
                return true;
            }

            private void End()
            {
                for (int i = _g; i < _glyphs.Count; i++) _glyphs[i].gameObject.SetActive(false);
                for (int i = _c; i < _caps.Count; i++) _caps[i].gameObject.SetActive(false);
                for (int i = _w; i < _words.Count; i++) _words[i].gameObject.SetActive(false);
                for (int i = _d; i < _dashes.Count; i++) _dashes[i].gameObject.SetActive(false);
                Width = _x;
                _root.sizeDelta = new Vector2(_x, _glyph);
            }

            /// <summary>One prompt: its key (glyphs or caps, one per "/"-piece) and its words; words with no key (a hint, "Dark.
            /// Shine your light on it.") are a plain line: regular, as written.</summary>
            private void Item(string key, string words)
            {
                if (key.Length == 0 && words.Length == 0) return;
                if (_items > 0) { _x += _itemGap; Dash(); _x += _itemGap; }
                _items++;
                if (key.Length > 0)
                {
                    string[] pieces = key.Split('/');
                    bool any = false;
                    foreach (string raw in pieces)
                    {
                        string piece = raw.Trim();
                        if (piece.Length == 0) continue;
                        if (any) _x += 4f;
                        any = true;
                        IconSet set = IconSet.Get();
                        Sprite glyph = set != null ? set.KeyGlyph(piece) : null;
                        if (glyph != null) GlyphPart(glyph);
                        else CapPart(piece);
                    }
                    if (words.Length > 0) _x += _keyGap;
                }
                if (words.Length > 0) WordsPart(words, key.Length == 0);
            }

            private void GlyphPart(Sprite glyph)
            {
                Image img;
                if (_g < _glyphs.Count) img = _glyphs[_g];
                else
                {
                    var rt = Part("Glyph" + _g);
                    img = rt.gameObject.AddComponent<Image>();
                    img.raycastTarget = false; img.preserveAspect = true;
                    _glyphs.Add(img);
                }
                _g++;
                img.sprite = glyph;
                img.gameObject.SetActive(true);
                float w = Mathf.Round(_glyph * glyph.rect.width / Mathf.Max(1f, glyph.rect.height));
                Place(img.rectTransform, w, _glyph);
            }

            private void CapPart(string key)
            {
                RectTransform cap;
                TextMeshProUGUI t;
                if (_c < _caps.Count) { cap = _caps[_c]; t = _capWords[_c]; }
                else
                {
                    cap = Part("Cap" + _c);
                    var back = Panel(cap, Frame.Solid);
                    back.color = Cream;
                    var words = BuildUiKit.Rect("Key", cap, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    t = Text(words, Body, Fill, true, TextAlignmentOptions.Center, true);
                    _caps.Add(cap); _capWords.Add(t);
                }
                _c++;
                t.text = key;
                cap.gameObject.SetActive(true);
                Place(cap, Wide(t, key) + 16f, 24f);
            }

            private void WordsPart(string words, bool plain)
            {
                TextMeshProUGUI t;
                if (_w < _words.Count) t = _words[_w];
                else
                {
                    var rt = Part("Words" + _w);
                    t = Text(rt, _wordSize, _wordColour, true, TextAlignmentOptions.MidlineLeft, true);
                    _words.Add(t);
                }
                _w++;
                t.font = !plain && _boldWords ? Bold : Regular;
                t.fontStyle = plain ? FontStyles.Normal : FontStyles.UpperCase;
                t.text = words;
                t.gameObject.SetActive(true);
                Place(t.rectTransform, Wide(t, words), _glyph);
            }

            private void Dash()
            {
                Image img;
                if (_d < _dashes.Count) img = _dashes[_d];
                else
                {
                    img = Block(Part("Dash" + _d), Gold);
                    _dashes.Add(img);
                }
                _d++;
                img.gameObject.SetActive(true);
                Place(img.rectTransform, _dash, 4f);
            }

            private RectTransform Part(string name) =>
                BuildUiKit.Rect(name, _root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            private void Place(RectTransform rt, float w, float h)
            {
                rt.anchoredPosition = new Vector2(_x, 0f);
                rt.sizeDelta = new Vector2(w, h);
                _x += w;
            }

            /// <summary>The prompts of "E  Grab     X  Put down": split where three or more spaces run.</summary>
            private static string[] SplitItems(string text)
            {
                var items = new List<string>();
                int start = 0, i = 0;
                while (i < text.Length)
                {
                    if (text[i] == ' ')
                    {
                        int run = i;
                        while (run < text.Length && text[run] == ' ') run++;
                        if (run - i >= 3)
                        {
                            if (i > start) items.Add(text.Substring(start, i - start));
                            start = run;
                        }
                        i = run;
                    }
                    else i++;
                }
                if (start < text.Length) items.Add(text.Substring(start));
                return items.ToArray();
            }

            private static string Strip(string text)
            {
                var sb = new System.Text.StringBuilder(text.Length);
                bool inTag = false;
                foreach (char ch in text)
                {
                    if (ch == '<') { inTag = true; continue; }
                    if (inTag) { if (ch == '>') inTag = false; continue; }
                    sb.Append(ch);
                }
                return sb.ToString();
            }
        }

        // ---------- the drawing ----------

        /// <summary>What a frame sprite looks like: the outer frame's colour and width, the notch cut from its corners, the
        /// fill and the notch of its own corners (the frame shows there: the stepped corner), and the fill's bands.</summary>
        private struct Spec : IEquatable<Spec>
        {
            public Color32 frame, fill, top, bottom, side;
            public int frameW, notch, innerNotch, topW, bottomW, sideW;

            public bool Equals(Spec o) =>
                Same(frame, o.frame) && Same(fill, o.fill) && Same(top, o.top) && Same(bottom, o.bottom) && Same(side, o.side)
                && frameW == o.frameW && notch == o.notch && innerNotch == o.innerNotch && topW == o.topW && bottomW == o.bottomW && sideW == o.sideW;

            public override bool Equals(object obj) => obj is Spec s && Equals(s);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Pack(frame);
                    h = h * 31 + Pack(fill); h = h * 31 + Pack(top); h = h * 31 + Pack(bottom); h = h * 31 + Pack(side);
                    h = h * 31 + frameW; h = h * 31 + notch; h = h * 31 + innerNotch; h = h * 31 + topW; h = h * 31 + bottomW; h = h * 31 + sideW;
                    return h;
                }
            }

            private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
            private static int Pack(Color32 c) => c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
        }

        /// <summary>The look of each frame, from his mockup's CSS (a 4-pixel frame cut 6 at the corners round a fill cut 4;
        /// the fill's bands 3 under the top, 6 along the bottom, 4 down the sides).</summary>
        private static Spec SpecOf(Frame frame)
        {
            Color banded = Fill;
            switch (frame)
            {
                case Frame.Selected: return Banded(Cyan);
                case Frame.Reward: return Banded(Gold);
                case Frame.Core: return Banded(Purple);
                case Frame.Quiet: return new Spec { frame = Deep, fill = QuietFill, frameW = 4, notch = 6, innerNotch = 4 };
                case Frame.Speech: return new Spec { frame = Cyan, fill = Cream, frameW = 4, notch = 6, innerNotch = 4 };
                case Frame.SpeechQuiet: return new Spec { frame = QuietText, fill = Cream, frameW = 4, notch = 6, innerNotch = 4 };
                case Frame.Footer: return new Spec { frame = Deep, fill = ChipFill, frameW = 4, notch = 6, innerNotch = 4 };
                case Frame.NameTag: return new Spec { frame = Cyan, fill = QuietFill, frameW = 3, notch = 4, innerNotch = 4 };
                case Frame.SlotHeld:
                    return new Spec
                    {
                        frame = Cyan, fill = EmptyCell, frameW = 4, notch = 6, innerNotch = 4,
                        top = Color.Lerp(EmptyCell, Color.white, 0.12f), topW = 4, bottom = Color.Lerp(EmptyCell, Color.black, 0.35f), bottomW = 4,
                    };
                case Frame.SlotEmpty: return new Spec { frame = Deep, fill = SlotFill, frameW = 4, notch = 6, innerNotch = 4 };
                case Frame.SlotFilled: return new Spec { frame = Deep, fill = EmptyCell, frameW = 4, notch = 6, innerNotch = 4 };
                case Frame.Highlight: return new Spec { frame = Cyan, fill = new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f), frameW = 2, notch = 4, innerNotch = 2 };
                case Frame.Solid: return new Spec { frame = Color.white, fill = Color.white, frameW = 0, notch = 4, innerNotch = 0 };
                case Frame.Box: return new Spec { frame = Color.white, fill = Color.clear, frameW = 3, notch = 0, innerNotch = 0 };
                default: return Banded(Pink);
            }

            Spec Banded(Color edge) => new Spec
            {
                frame = edge, fill = banded, frameW = 4, notch = 6, innerNotch = 4,
                top = BandTop, topW = 3, bottom = BandBottom, bottomW = 6, side = BandSide, sideW = 4,
            };
        }

        private static Sprite SpriteOf(Spec s)
        {
            if (_sprites.TryGetValue(s, out Sprite had) && had != null) return had;
            int side = Mathf.Max(s.notch, s.frameW + Mathf.Max(s.innerNotch, s.sideW));
            int top = Mathf.Max(s.notch, s.frameW + Mathf.Max(s.innerNotch, s.topW));
            int bottom = Mathf.Max(s.notch, s.frameW + Mathf.Max(s.innerNotch, s.bottomW));
            int w = side * 2 + 2, h = top + bottom + 2;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[(h - 1 - y) * w + x] = At(s, x, y, w, h);   // y counts from the top; the texture's rows from the bottom
            Sprite sprite = Make("PixelKit Frame " + _sprites.Count, w, h, px, new Vector4(side, bottom, side, top));
            _sprites[s] = sprite;
            return sprite;
        }

        /// <summary>One pixel of a frame, <paramref name="y"/> from the top.</summary>
        private static Color32 At(Spec s, int x, int y, int w, int h)
        {
            int n = s.notch;
            if ((x < n || x >= w - n) && (y < n || y >= h - n)) return new Color32(0, 0, 0, 0);   // the outer corner's notch
            int f = s.frameW;
            if (x < f || x >= w - f || y < f || y >= h - f) return s.frame;
            int ix = x - f, iy = y - f, iw = w - 2 * f, ih = h - 2 * f, m = s.innerNotch;
            if ((ix < m || ix >= iw - m) && (iy < m || iy >= ih - m)) return s.frame;   // the fill's own notch shows the frame
            if (iy < s.topW) return s.top;
            if (iy >= ih - s.bottomW) return s.bottom;
            if (ix < s.sideW || ix >= iw - s.sideW) return s.side;
            return s.fill;
        }

        /// <summary>A white sprite from rows of '#' (in) and '.' (out), the first row at the top.</summary>
        private static Sprite Bitmap(string name, string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[(h - 1 - y) * w + x] = x < rows[y].Length && rows[y][x] == '#' ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            return Make("PixelKit " + name, w, h, px, Vector4.zero);
        }

        private static Sprite Make(string name, int w, int h, Color32[] px, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
