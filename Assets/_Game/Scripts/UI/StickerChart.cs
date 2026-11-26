using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The Sticker Chart (D-0224, his "it should be linked to a journal or quest guide"): one big sticker card in the
    /// toasts' style (cream card, thick dark outline, a drop shadow) with a row for each zone of ObjectiveSet > Zones, in
    /// their Order, in two columns. Each row: the zone's name and its star count ("1 / 2"); its main steps (once the zone
    /// is visited), a tick on each one done and an arrow on the current one; and its gold stars: a filled star with its
    /// name when earned, an empty star with its name once the zone is visited, the Unknown Word ("???") until then. The
    /// zone the player is in now has a gold band behind its name. The heading carries all the stars held.
    /// Two ways in: HELD PEEK (Tab while held; the pad's D-pad up too when ObjectiveSet > Pad Peek is ticked; the game
    /// keeps running) and the pause menu's "Sticker Chart" slab, which opens it as a PAGE over the paused game (PauseMenu
    /// reads Esc, Start and the pad's East button to come back). Never on the title, in the opening, in Build
    /// Mode, in a team match or online (Tab stays the online scoreboard there). It fits a 16:9 and a 4:3 screen (the card
    /// is made smaller when it would not fit). The stars are the HUD's drawn star (the HUD font has no star letter).
    /// One for the whole game, made at the first scene and kept across loads; built in code like the other HUDs. Its
    /// parts are made once and only filled and placed when it opens or something changes while it is open: nothing is
    /// made or allocated per frame. Words: Resources/ObjectiveSet. OFF SWITCH: ObjectiveSet > Chart On.
    /// The heading carries the IconSet's sticker-chart icon on its left (D-0226; none without it).
    /// </summary>
    public sealed class StickerChart : MonoBehaviour
    {
        private const string FontPath = "HUD/Fredoka SDF";
        // At 1920 by 1080, by the screen's height (D-0133): the card's width, its pad, the gap between its two columns,
        // the room kept round it on the screen, its outline, shadow and corners.
        private const float Wide = 1300f, Pad = 30f, ColumnGap = 44f, ScreenMargin = 24f, Outline = 5f, Shadow = 8f, Round = 26f;
        // The heading, the zone rows' names and counts, the steps and the stars' names (letter sizes), and the room each takes.
        private const float TitleLetters = 34f, TotalLetters = 30f, ZoneLetters = 27f, ZoneCountLetters = 22f, StepLetters = 20f, StarLetters = 19f, HintLetters = 18f;
        private const float TitleHigh = 46f, TitleGap = 14f, ZoneHigh = 36f, ZoneGap = 16f, ItemGap = 4f, StepHigh = 26f, ChipHigh = 28f;
        private const float Arrow = 14f, ArrowGap = 6f, StepBox = 20f, StepBoxGap = 10f, Indent = 10f, ChipIcon = 22f, ChipGap = 6f, ChipSpacing = 18f;
        private const float TotalIcon = 32f, CountIcon = 20f, BandPad = 8f, HintGap = 12f;
        private const float PeekDim = 0.25f, PageDim = 0.6f, DoneFade = 0.5f, UnheldFade = 0.6f;
        private const int Sorting = 57;   // over the pause menu's (55), under the key sheet and the toasts (60)
        private static readonly Color Ink = new Color32(0x1f, 0x1b, 0x2e, 0xff);
        private static readonly Color Card = new Color32(0xff, 0xf7, 0xe0, 0xff);        // the "got it" toast's cream
        private static readonly Color Label = new Color32(0x92, 0x40, 0x0e, 0xff);       // the "got it" toast's label brown
        private static readonly Color Gold = new Color32(0xff, 0xd1, 0x66, 0xff);        // the "got it" badge's gold
        private static readonly Color BoxFill = new Color32(0xff, 0xfd, 0xf6, 0xff);
        private static readonly Color Band = new Color32(0xff, 0xd1, 0x66, 0x70);        // the current zone's band: the gold, see-through

        /// <summary>A step's line on the chart: the arrow, the tick box, the words.</summary>
        private sealed class StepView
        {
            public int step;
            public RectTransform rt, words;
            public Image arrow, tick;
            public TextMeshProUGUI text;
            public float high;
        }

        /// <summary>A star on the chart: the star (its dark back and its face) and its name.</summary>
        private sealed class StarView
        {
            public int star;
            public RectTransform rt, back, face, words;
            public Image backImage, faceImage;
            public TextMeshProUGUI text;
            public float wide;
        }

        /// <summary>A zone's row: its band, its name and count, its steps and stars.</summary>
        private sealed class ZoneView
        {
            public int zone;
            public RectTransform rt, band, countIcon;
            public TextMeshProUGUI name, count;
            public StepView[] steps;
            public StarView[] stars;
            public bool showSteps;
            public float high;
        }

        private static StickerChart _instance;
        private static Sprite _arrow;

        private Canvas _canvas;
        private RectTransform _canvasRt, _panel, _content, _hintRt, _totalIcon;
        private CanvasGroup _group;
        private Image _dim;
        private TextMeshProUGUI _title, _total, _hint;
        private ZoneView[] _zones = new ZoneView[0];
        private TMP_FontAsset _font;
        private bool _built, _peeking, _page;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make() => Get();

        /// <summary>The chart, made on the first call (kept across scene loads). Batch runs make none.</summary>
        public static StickerChart Get()
        {
            if (_instance != null) return _instance;
            if (Application.isBatchMode || !Application.isPlaying) return null;
            var go = new GameObject("StickerChart");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StickerChart>();
            return _instance;
        }

        /// <summary>The chart is on the screen (peeked or as the pause menu's page): the objective card hides.</summary>
        public static bool IsOpen => _instance != null && (_instance._peeking || _instance._page);

        /// <summary>The chart is the pause menu's page now.</summary>
        public static bool OnPage => _instance != null && _instance._page;

        /// <summary>The chart may be opened now: the set and the chart On, in play offline (not a batch run), not the title,
        /// not the opening, not Build Mode, not a team match, not online; for a peek also no menu, key sheet or console.
        /// The pause menu asks with <paramref name="page"/> true (it is the menu that is open then).</summary>
        public static bool CanOpen(bool page)
        {
            if (!Objectives.Live || !Objectives.Set.chartOn) return false;
            if (TitleMenu.IsOpen || Opening.Playing || GameMode.Current == GameMode.Mode.Build || TeamMatch.Running || NetSession.InParty) return false;
            if (page) return true;
            return !PauseMenu.IsOpen && !KeyHelp.IsOpen && !DebugConsole.IsOpen;
        }

        /// <summary>The pause menu's Sticker Chart slab: the chart as a page (its back line under it). False when it cannot
        /// open now (see <see cref="CanOpen"/>).</summary>
        public static bool OpenPage()
        {
            if (!CanOpen(true)) return false;
            StickerChart chart = Get();
            if (chart == null) return false;
            chart._peeking = false;
            chart._page = true;
            chart.Open();
            return true;
        }

        /// <summary>Back from the page (the pause menu shows again).</summary>
        public static void ClosePage()
        {
            if (_instance == null || !_instance._page) return;
            _instance._page = false;
            _instance._group.alpha = 0f;     // the pause menu comes back at once: no fade under it
            _instance._canvas.enabled = false;
        }

        private void Awake() => Build();

        private void OnEnable()
        {
            Objectives.Changed += OnChanged;
            Objectives.ZoneEntered += OnZone;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Objectives.Changed -= OnChanged;
            Objectives.ZoneEntered -= OnZone;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnChanged() { if (IsOpen) Refresh(); }
        private void OnZone(int zone) { if (IsOpen) Refresh(); }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _peeking = _page = false;
            _group.alpha = 0f;
            _canvas.enabled = false;
        }

        private void Update()
        {
            ObjectiveSet set = Objectives.Set;
            if (_page)
            {
                if (!CanOpen(true)) ClosePage();   // a party began, or the scene changed under it
            }
            else
            {
                bool want = Held(set) && CanOpen(false);
                if (want && !_peeking) { _peeking = true; Open(); }
                else if (!want) _peeking = false;
            }

            float target = _peeking || _page ? 1f : 0f;
            if (_group.alpha == target) return;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, set.fadeSeconds * 0.5f));
            if (_group.alpha <= 0f) _canvas.enabled = false;
        }

        /// <summary>Tab held (the keyboard), or the pad's D-pad up held while Pad Peek is ticked.</summary>
        private static bool Held(ObjectiveSet set)
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && keys.tabKey.isPressed) return true;
            Gamepad pad = Gamepad.current;
            return set.padPeek && pad != null && pad.dpad.up.isPressed;
        }

        /// <summary>On the screen: filled and placed as things are now.</summary>
        private void Open()
        {
            _canvas.enabled = true;
            Color dim = _dim.color; dim.a = _page ? PageDim : PeekDim; _dim.color = dim;
            Refresh();
        }

        // ---------- filling and placing ----------

        /// <summary>Every row's words, ticks, arrows and stars as they are now, then the rows placed in two columns and the
        /// card sized (and made smaller if the screen is too narrow or short for it).</summary>
        private void Refresh()
        {
            EnsureBuilt();
            ObjectiveSet set = Objectives.Set;
            _title.text = set.chartTitle;
            _total.text = Objectives.Format(set.counterFormat, Objectives.StarCount, Objectives.StarTotal);
            PlaceTotalIcon();
            string unknown = string.IsNullOrEmpty(set.unknownWord) ? "???" : set.unknownWord;
            int current = Objectives.CurrentZone, step = Objectives.CurrentStepIndex;
            float columnWide = (Wide - Pad * 2f - ColumnGap) * 0.5f;

            foreach (ZoneView zv in _zones)
            {
                ChartZone zone = Objectives.ZoneAt(zv.zone);
                bool visited = Objectives.ZoneVisited(zv.zone);
                string shown = zone != null ? (string.IsNullOrWhiteSpace(zone.title) ? zone.room : zone.title) : "";
                zv.name.text = visited || !set.hideUnvisitedNames ? shown : unknown;
                zv.name.color = visited ? Ink : new Color(Ink.r, Ink.g, Ink.b, UnheldFade);
                int held = Objectives.ZoneStarCount(zv.zone, out int total);
                zv.count.text = Objectives.Format(set.counterFormat, held, total);
                zv.countIcon.gameObject.SetActive(total > 0);
                zv.count.gameObject.SetActive(total > 0);
                float countWide = zv.count.GetPreferredValues(zv.count.text, columnWide * 0.25f, 0f).x;
                zv.countIcon.anchoredPosition = new Vector2(-countWide - 6f - CountIcon * 0.5f, -ZoneHigh * 0.5f);
                zv.band.gameObject.SetActive(zv.zone == current);

                zv.showSteps = visited && zv.steps.Length > 0;
                float y = ZoneHigh + ItemGap;
                foreach (StepView sv in zv.steps)
                {
                    sv.rt.gameObject.SetActive(zv.showSteps);
                    if (!zv.showSteps) continue;
                    ObjectiveStep s = Objectives.StepAt(sv.step);
                    bool done = Objectives.StepDoneAt(sv.step), now = sv.step == step;
                    sv.tick.enabled = done;
                    sv.arrow.enabled = now;
                    sv.text.text = s != null ? s.text : "";
                    sv.text.fontStyle = now ? FontStyles.Bold : FontStyles.Normal;
                    sv.text.color = now ? Label : done ? new Color(Ink.r, Ink.g, Ink.b, DoneFade) : Ink;
                    float wordsWide = columnWide - Indent - Arrow - ArrowGap - StepBox - StepBoxGap;
                    Vector2 size = sv.text.GetPreferredValues(sv.text.text, wordsWide, 0f);
                    sv.high = Mathf.Max(StepHigh, size.y);
                    sv.words.sizeDelta = new Vector2(wordsWide, sv.high);
                    sv.rt.anchoredPosition = new Vector2(Indent, -y);
                    sv.rt.sizeDelta = new Vector2(columnWide - Indent, sv.high);
                    y += sv.high + ItemGap;
                }

                // The stars, side by side, a new line when the next would run past the column.
                float x = Indent, lineTop = y;
                bool any = false;
                foreach (StarView st in zv.stars)
                {
                    ObjectiveStar star = Objectives.StarAt(st.star);
                    bool got = Objectives.StarHeldAt(st.star);
                    st.text.text = got || visited ? (star != null ? star.title : "") : unknown;
                    st.text.color = got ? Ink : new Color(Ink.r, Ink.g, Ink.b, UnheldFade);
                    st.text.fontStyle = got ? FontStyles.Bold : FontStyles.Normal;
                    Paint(st, got);
                    float wordsWide = Mathf.Min(st.text.GetPreferredValues(st.text.text, 2000f, 0f).x + 2f, columnWide - Indent - ChipIcon - ChipGap);
                    st.wide = ChipIcon + ChipGap + wordsWide;
                    st.words.sizeDelta = new Vector2(wordsWide, ChipHigh);
                    if (any && x + st.wide > columnWide) { x = Indent; lineTop += ChipHigh; }
                    st.rt.anchoredPosition = new Vector2(x, -lineTop);
                    st.rt.sizeDelta = new Vector2(st.wide, ChipHigh);
                    x += st.wide + ChipSpacing;
                    any = true;
                }
                if (any) y = lineTop + ChipHigh;
                else y -= ItemGap;
                zv.high = y;
                zv.rt.sizeDelta = new Vector2(columnWide, zv.high);
                zv.band.sizeDelta = new Vector2(columnWide + BandPad * 2f, ZoneHigh);
            }

            // Two columns, in order: the split that leaves the taller column shortest.
            int n = _zones.Length, split = n;
            float best = float.MaxValue, all = 0f;
            for (int i = 0; i < n; i++) all += _zones[i].high + (i > 0 ? ZoneGap : 0f);
            float left = 0f;
            for (int k = 0; k <= n; k++)
            {
                if (k > 0) left += _zones[k - 1].high + (k > 1 ? ZoneGap : 0f);
                float right = all - left - (k > 0 && k < n ? ZoneGap : 0f);
                float tallest = Mathf.Max(left, right);
                if (tallest < best - 0.5f) { best = tallest; split = k; }
            }
            float colY = 0f, height = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i == split) colY = 0f;
                float colX = i < split ? 0f : columnWide + ColumnGap;
                _zones[i].rt.anchoredPosition = new Vector2(colX, -colY);
                colY += _zones[i].high + ZoneGap;
                height = Mathf.Max(height, colY - ZoneGap);
            }

            // The back line on the page.
            bool hint = _page && !string.IsNullOrWhiteSpace(set.chartBackHint);
            _hintRt.gameObject.SetActive(hint);
            float hintHigh = 0f;
            if (hint)
            {
                _hint.text = set.chartBackHint;
                hintHigh = _hint.GetPreferredValues(_hint.text, Wide - Pad * 2f, 0f).y;
                _hintRt.sizeDelta = new Vector2(Wide - Pad * 2f, hintHigh);
            }

            float top = Pad + TitleHigh + TitleGap;
            _content.anchoredPosition = new Vector2(Pad, -top);
            _content.sizeDelta = new Vector2(Wide - Pad * 2f, height);
            float panelHigh = top + height + (hint ? HintGap + hintHigh : 0f) + Pad;
            if (hint) _hintRt.anchoredPosition = new Vector2(Pad, -(top + height + HintGap));
            _panel.sizeDelta = new Vector2(Wide, panelHigh);

            // Made smaller when the screen is too narrow (4:3 and narrower) or too short for it.
            Vector2 room = _canvasRt.rect.size;
            float fit = 1f;
            if (room.x > 0f && room.y > 0f)
                fit = Mathf.Min(1f, (room.x - ScreenMargin * 2f) / Wide, (room.y - ScreenMargin * 2f) / panelHigh);
            _panel.localScale = new Vector3(fit, fit, 1f);
        }

        /// <summary>A star earned: a gold face on a dark back a little below it (the HUD counter's star). Not yet: the dark
        /// star with a cream face inside it (an empty star's outline).</summary>
        private static void Paint(StarView st, bool got)
        {
            st.faceImage.color = got ? Gold : Card;
            st.back.anchoredPosition = got ? new Vector2(0f, -2f) : Vector2.zero;
            st.back.sizeDelta = got ? new Vector2(6f, 6f) : new Vector2(4f, 4f);
            st.face.anchoredPosition = got ? new Vector2(0f, 1f) : Vector2.zero;
            st.face.sizeDelta = got ? Vector2.zero : new Vector2(-6f, -6f);
        }

        // ---------- building ----------

        /// <summary>The canvas, the dim, the card, its heading and its back line: made once.</summary>
        private void Build()
        {
            _font = Resources.Load<TMP_FontAsset>(FontPath);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = Sorting;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
            _canvasRt = (RectTransform)canvasGo.transform;
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; _group.alpha = 0f;
            _canvas.enabled = false;

            Vector2 topLeft = new Vector2(0f, 1f), middle = new Vector2(0.5f, 0.5f), topRight = new Vector2(1f, 1f);
            _dim = BuildUiKit.Rect("Dim", canvasGo.transform, Vector2.zero, Vector2.one, middle, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            _dim.color = new Color(0.03f, 0.03f, 0.06f, PeekDim); _dim.raycastTarget = false;

            _panel = BuildUiKit.Rect("Chart", canvasGo.transform, middle, middle, middle, Vector2.zero, new Vector2(Wide, 600f));
            HudShapes.RoundedImage(BuildUiKit.Rect("Shadow", _panel, Vector2.zero, Vector2.one, middle, new Vector2(0f, -Shadow), Vector2.zero), Ink, Round);
            HudShapes.RoundedImage(BuildUiKit.Rect("Outline", _panel, Vector2.zero, Vector2.one, middle, Vector2.zero, Vector2.zero), Ink, Round);
            HudShapes.RoundedImage(BuildUiKit.Rect("Fill", _panel, Vector2.zero, Vector2.one, middle, Vector2.zero, new Vector2(-Outline * 2f, -Outline * 2f)), Card, Round - Outline);

            // The heading (left) and all the stars held (right).
            var titleRt = BuildUiKit.Rect("Title", _panel, topLeft, topLeft, topLeft, new Vector2(Pad, -Pad), new Vector2(Wide * 0.6f, TitleHigh));
            _title = Text(titleRt, TitleLetters, Label, FontStyles.Bold | FontStyles.UpperCase);
            _title.alignment = TextAlignmentOptions.MidlineLeft;
            _title.characterSpacing = 6f;
            MenuIcons.BeforeHeading(titleRt, IconSet.StickerChart);   // D-0226: the heading's icon; the words move right of it
            var totalRt = BuildUiKit.Rect("Total", _panel, topRight, topRight, topRight, new Vector2(-Pad, -Pad), new Vector2(Wide * 0.3f, TitleHigh));
            _total = Text(totalRt, TotalLetters, Ink, FontStyles.Bold);
            _total.alignment = TextAlignmentOptions.MidlineRight;
            _totalIcon = BuildUiKit.Rect("Star", _panel, topRight, topRight, middle, Vector2.zero, new Vector2(TotalIcon, TotalIcon));
            MakeStar(_totalIcon, true);

            _content = BuildUiKit.Rect("Rows", _panel, topLeft, topLeft, topLeft, new Vector2(Pad, -(Pad + TitleHigh + TitleGap)), new Vector2(Wide - Pad * 2f, 100f));

            _hintRt = BuildUiKit.Rect("Hint", _panel, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(Wide - Pad * 2f, 24f));
            _hint = Text(_hintRt, HintLetters, new Color(Label.r, Label.g, Label.b, 0.8f), FontStyles.Bold);
            _hint.alignment = TextAlignmentOptions.Center;
            _hintRt.gameObject.SetActive(false);
        }

        /// <summary>The rows: made the first time the chart opens (the set's zones, steps and stars do not change in play).</summary>
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            IReadOnlyList<int> order = Objectives.ZoneOrder;
            _zones = new ZoneView[order.Count];
            float columnWide = (Wide - Pad * 2f - ColumnGap) * 0.5f;
            Vector2 topLeft = new Vector2(0f, 1f), left = new Vector2(0f, 0.5f), middle = new Vector2(0.5f, 0.5f), topRight = new Vector2(1f, 1f);
            for (int o = 0; o < order.Count; o++)
            {
                int z = order[o];
                var zv = new ZoneView { zone = z };
                zv.rt = BuildUiKit.Rect("Zone " + (Objectives.ZoneAt(z) != null ? Objectives.ZoneAt(z).id : z.ToString()), _content, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(columnWide, ZoneHigh));
                zv.band = BuildUiKit.Rect("Here", zv.rt, topLeft, topLeft, topLeft, new Vector2(-BandPad, 0f), new Vector2(columnWide + BandPad * 2f, ZoneHigh));
                HudShapes.RoundedImage(zv.band, Band, 12f);
                var nameRt = BuildUiKit.Rect("Name", zv.rt, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(columnWide * 0.7f, ZoneHigh));
                zv.name = Text(nameRt, ZoneLetters, Ink, FontStyles.Bold);
                zv.name.alignment = TextAlignmentOptions.MidlineLeft;
                var countRt = BuildUiKit.Rect("Count", zv.rt, topRight, topRight, topRight, Vector2.zero, new Vector2(columnWide * 0.25f, ZoneHigh));
                zv.count = Text(countRt, ZoneCountLetters, Label, FontStyles.Bold);
                zv.count.alignment = TextAlignmentOptions.MidlineRight;
                zv.countIcon = BuildUiKit.Rect("Star", zv.rt, topRight, topRight, middle, Vector2.zero, new Vector2(CountIcon, CountIcon));
                MakeStar(zv.countIcon, true);

                IReadOnlyList<int> steps = Objectives.ZoneSteps(z);
                zv.steps = new StepView[steps.Count];
                for (int i = 0; i < steps.Count; i++)
                {
                    var sv = new StepView { step = steps[i] };
                    sv.rt = BuildUiKit.Rect("Step " + steps[i], zv.rt, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(columnWide - Indent, StepHigh));
                    var arrowRt = BuildUiKit.Rect("Arrow", sv.rt, new Vector2(0f, 1f), new Vector2(0f, 1f), left, new Vector2(0f, -StepHigh * 0.5f), new Vector2(Arrow, Arrow));
                    sv.arrow = arrowRt.gameObject.AddComponent<Image>(); sv.arrow.sprite = ArrowSprite; sv.arrow.color = Label; sv.arrow.raycastTarget = false;
                    var box = BuildUiKit.Rect("Box", sv.rt, new Vector2(0f, 1f), new Vector2(0f, 1f), left, new Vector2(Arrow + ArrowGap, -StepHigh * 0.5f), new Vector2(StepBox, StepBox));
                    HudShapes.RoundedImage(box, Ink, 6f);
                    HudShapes.RoundedImage(BuildUiKit.Rect("Fill", box, Vector2.zero, Vector2.one, middle, Vector2.zero, new Vector2(-5f, -5f)), BoxFill, 4f);
                    sv.tick = BuildUiKit.Rect("Tick", box, middle, middle, middle, new Vector2(1f, 1f), new Vector2(StepBox + 6f, StepBox + 6f)).gameObject.AddComponent<Image>();
                    sv.tick.sprite = HudShapes.Check; sv.tick.color = Ink; sv.tick.raycastTarget = false;
                    sv.words = BuildUiKit.Rect("Words", sv.rt, topLeft, topLeft, topLeft, new Vector2(Arrow + ArrowGap + StepBox + StepBoxGap, 0f), new Vector2(200f, StepHigh));
                    sv.text = Text(sv.words, StepLetters, Ink, FontStyles.Normal);
                    sv.text.alignment = TextAlignmentOptions.MidlineLeft;
                    sv.text.textWrappingMode = TextWrappingModes.Normal;
                    zv.steps[i] = sv;
                }

                IReadOnlyList<int> stars = Objectives.ZoneStars(z);
                zv.stars = new StarView[stars.Count];
                for (int i = 0; i < stars.Count; i++)
                {
                    var st = new StarView { star = stars[i] };
                    st.rt = BuildUiKit.Rect("Star " + stars[i], zv.rt, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(200f, ChipHigh));
                    var icon = BuildUiKit.Rect("Icon", st.rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), middle, new Vector2(ChipIcon * 0.5f, 0f), new Vector2(ChipIcon, ChipIcon));
                    st.back = BuildUiKit.Rect("Back", icon, Vector2.zero, Vector2.one, middle, Vector2.zero, Vector2.zero);
                    st.backImage = st.back.gameObject.AddComponent<Image>(); st.backImage.sprite = ObjectiveHud.StarSprite; st.backImage.color = Ink; st.backImage.raycastTarget = false;
                    st.face = BuildUiKit.Rect("Face", icon, Vector2.zero, Vector2.one, middle, Vector2.zero, Vector2.zero);
                    st.faceImage = st.face.gameObject.AddComponent<Image>(); st.faceImage.sprite = ObjectiveHud.StarSprite; st.faceImage.raycastTarget = false;
                    st.words = BuildUiKit.Rect("Words", st.rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), left, new Vector2(ChipIcon + ChipGap, 0f), new Vector2(160f, ChipHigh));
                    st.text = Text(st.words, StarLetters, Ink, FontStyles.Normal);
                    st.text.alignment = TextAlignmentOptions.MidlineLeft;
                    zv.stars[i] = st;
                }
                _zones[o] = zv;
            }
        }

        /// <summary>The heading's star sits just left of the total's words.</summary>
        private void PlaceTotalIcon()
        {
            float wide = _total.GetPreferredValues(_total.text, Wide * 0.3f, 0f).x;
            _totalIcon.anchoredPosition = new Vector2(-Pad - wide - 8f - TotalIcon * 0.5f, -Pad - TitleHigh * 0.5f);
        }

        /// <summary>A star of the HUD's drawing in <paramref name="rt"/>: a gold face on a dark back.</summary>
        private static void MakeStar(RectTransform rt, bool gold)
        {
            Vector2 middle = new Vector2(0.5f, 0.5f);
            var back = BuildUiKit.Rect("Back", rt, Vector2.zero, Vector2.one, middle, new Vector2(0f, -2f), new Vector2(6f, 6f)).gameObject.AddComponent<Image>();
            back.sprite = ObjectiveHud.StarSprite; back.color = Ink; back.raycastTarget = false;
            var face = BuildUiKit.Rect("Face", rt, Vector2.zero, Vector2.one, middle, new Vector2(0f, 1f), Vector2.zero).gameObject.AddComponent<Image>();
            face.sprite = ObjectiveHud.StarSprite; face.color = gold ? Gold : Card; face.raycastTarget = false;
        }

        private TextMeshProUGUI Text(RectTransform rt, float size, Color colour, FontStyles style)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size; t.fontStyle = style; t.color = colour; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A white triangle pointing right, drawn once in code (no texture asset): the current step's arrow.</summary>
        private static Sprite ArrowSprite
        {
            get
            {
                if (_arrow != null) return _arrow;
                const int size = 32;
                var px = new Color32[size * size];
                // Corners (5, 3), (5, 29), (29, 16): inside when on the inner side of all three edges, softened over a pixel.
                Vector2 a = new Vector2(5f, 3f), b = new Vector2(5f, 29f), c = new Vector2(29f, 16f);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Mathf.Min(Side(a, b, p), Mathf.Min(Side(b, c, p), Side(c, a, p)));
                        float alpha = Mathf.Clamp01(d + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f + 0.5f));
                    }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "StickerChart Arrow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _arrow = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _arrow.name = tex.name; _arrow.hideFlags = HideFlags.DontSave;
                return _arrow;
            }
        }

        /// <summary>How far <paramref name="p"/> is inside the edge from <paramref name="from"/> to <paramref name="to"/>
        /// (the triangle's corners go round clockwise, so inside is to the right), in pixels.</summary>
        private static float Side(Vector2 from, Vector2 to, Vector2 p)
        {
            Vector2 edge = to - from;
            return (edge.y * (p.x - from.x) - edge.x * (p.y - from.y)) / edge.magnitude;
        }
    }
}
