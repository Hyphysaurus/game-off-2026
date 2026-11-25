using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The objectives on screen (D-0220): a sticker card in the toasts' style (cream card, thick dark outline, a drop
    /// shadow) in the top left corner. In the house: line 1 the big goal (small capitals), line 2 the current step beside a
    /// tick box, and the gold-star counter (a star and "3 / 10"). A finished step ticks its box for a moment, then the next
    /// one slides in; a star earned makes the counter pop. Any other scene (the Raceway, the Colour Corner, the Workshop)
    /// shows the counter alone. Hidden on the title, in the menus, the key sheet and the console, during New Game's
    /// opening, in Build Mode, in a team match (its score sits in this corner), online, and in the house once every step is
    /// done (after the Goal). While the Flat Zone letters' strip (FlatHud) shows in the same corner, the card slides down
    /// under it. The stall nudge (ObjectiveHelpers, D-0221) makes the whole card pulse (<see cref="Pulse"/>).
    /// It only reads Objectives. One for the whole game, made at the first scene; built in code like the other
    /// HUDs; nothing is made or changed per frame unless something moves.
    /// Words and timings: Resources/ObjectiveSet. OFF SWITCH: untick On there.
    /// </summary>
    public sealed class ObjectiveHud : MonoBehaviour
    {
        private const string FontPath = "HUD/Fredoka SDF";
        // At 1920 by 1080, by the screen's height (D-0133): the corner (FlatHud's), the card's pad, its narrowest and widest,
        // the rows' letters, the tick box and the star, and the room the letters' strip takes above it.
        private const float Left = 28f, Top = 26f, Pad = 16f, MinWide = 200f, MaxWide = 560f, RowGap = 6f;
        private const float GoalLetters = 17f, StepLetters = 25f, CountLetters = 26f, Box = 28f, BoxGap = 10f, Icon = 30f, IconGap = 10f;
        private const float Outline = 4f, Shadow = 6f, Round = 20f, SlideFrom = 46f, PopFrom = 1.8f, FlatStrip = 84f + 14f + 6f;
        private static readonly Color Ink = new Color32(0x1f, 0x1b, 0x2e, 0xff);
        private static readonly Color Card = new Color32(0xff, 0xf7, 0xe0, 0xff);        // the "got it" toast's cream
        private static readonly Color Label = new Color32(0x92, 0x40, 0x0e, 0xff);       // the "got it" toast's label brown
        private static readonly Color Gold = new Color32(0xff, 0xd1, 0x66, 0xff);        // the "got it" badge's gold
        private static readonly Color BoxFill = new Color32(0xff, 0xfd, 0xf6, 0xff);

        private enum Phase { Still, Tick, Slide }

        private static ObjectiveHud _instance;
        private static Sprite _star;
        private CanvasGroup _group;
        private RectTransform _card, _goalRt, _stepRow, _stepRt, _starRow, _starIcon;
        private TextMeshProUGUI _goal, _step, _count;
        private Image _tick;
        private ObjectiveStep _shown, _pending;
        private Phase _phase;
        private float _phaseAt, _popAt = float.NegativeInfinity, _flatUntil = float.NegativeInfinity, _y = -1f;
        private float _pulseAt = float.NegativeInfinity;   // the stall nudge's pulse (D-0221)
        private bool _stepsHere;
        private int _shownCount = -1, _shownTotal = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make() => Get();

        /// <summary>The card, made on the first call (kept across scene loads, like the toasts). Batch runs make none.</summary>
        public static ObjectiveHud Get()
        {
            if (_instance != null) return _instance;
            if (Application.isBatchMode || !Application.isPlaying) return null;
            var go = new GameObject("ObjectiveHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ObjectiveHud>();
            return _instance;
        }

        /// <summary>A white five-pointed star, drawn once in code (no texture asset): the counter's and the end card's.
        /// Tint it with the Image's colour.</summary>
        public static Sprite StarSprite
        {
            get
            {
                if (_star != null) return _star;
                const int size = 64;
                var points = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 30f : 12.5f;
                    points[i] = new Vector2(32f + Mathf.Cos(a) * r, 31f + Mathf.Sin(a) * r);
                }
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = EdgeDistance(p, points);
                        float alpha = Mathf.Clamp01(Inside(p, points) ? 0.5f + d : 0.5f - d);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f + 0.5f));
                    }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ObjectiveHud Star", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _star = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _star.name = tex.name; _star.hideFlags = HideFlags.DontSave;
                return _star;
            }
        }

        private static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        private static float EdgeDistance(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], ab = poly[i] - poly[j];
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        private void Awake() => Build();

        private void OnEnable()
        {
            Objectives.Changed += OnChanged;
            Objectives.StarAwarded += OnStar;
            SceneManager.activeSceneChanged += OnActiveScene;
            _stepsHere = Objectives.StepsIn(SceneManager.GetActiveScene().name);
            Snap();
        }

        private void OnDisable()
        {
            Objectives.Changed -= OnChanged;
            Objectives.StarAwarded -= OnStar;
            SceneManager.activeSceneChanged -= OnActiveScene;
        }

        private void OnActiveScene(Scene from, Scene to)
        {
            _stepsHere = Objectives.StepsIn(to.name);
            _flatUntil = float.NegativeInfinity;
            Snap();
        }

        private void OnStar(string id, string title) => _popAt = Time.unscaledTime;

        /// <summary>The stall nudge (D-0221): the whole card swells a few beats (ObjectiveSet > Pulse Seconds, Swell, Beats)
        /// so the eye goes to the step. Nothing if the card is not made.</summary>
        public static void Pulse()
        {
            if (_instance != null) _instance._pulseAt = Time.unscaledTime;
        }

        /// <summary>A step finished or the save was read again. A step finished while the card shows: tick, then slide.
        /// Anything else (a load, New Game, another slot): the card shows the new state at once.</summary>
        private void OnChanged()
        {
            ObjectiveStep now = Objectives.Current;
            if (_phase == Phase.Tick) { _pending = now; Counter(); return; }   // the tick runs on; the slide brings the newest
            if (now == _shown) { Counter(); Layout(); return; }
            bool finished = _shown != null && Objectives.StepDone(_shown.id) && _group.alpha > 0.5f;
            if (!finished) { Snap(); return; }
            _phase = Phase.Tick; _phaseAt = Time.unscaledTime; _pending = now;
            _tick.enabled = true;
            _step.alpha = 0.55f;
            Counter();
        }

        /// <summary>Shows the state as it is, with no tick or slide.</summary>
        private void Snap()
        {
            _phase = Phase.Still; _pending = null;
            _shown = Objectives.Current;
            if (_tick != null) _tick.enabled = false;
            Counter();
            Layout();
        }

        private void Counter()
        {
            int count = Objectives.StarCount, total = Objectives.StarTotal;
            if (count == _shownCount && total == _shownTotal) return;
            _shownCount = count; _shownTotal = total;
            _count.text = Objectives.Format(Objectives.Set.counterFormat, count, total);
            Layout();
        }

        private void Update()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            ObjectiveSet set = Objectives.Set;
            if (_phase == Phase.Tick && now - _phaseAt >= set.tickSeconds)
            {
                _tick.enabled = false;
                _shown = _pending; _pending = null;
                Layout();
                if (_shown != null) { _phase = Phase.Slide; _phaseAt = now; _step.alpha = 0f; }
                else _phase = Phase.Still;
            }

            bool content = _stepsHere ? _shown != null || _phase == Phase.Tick : Objectives.StarTotal > 0;
            bool menus = PauseMenu.IsOpen || TitleMenu.IsOpen || KeyHelp.IsOpen || DebugConsole.IsOpen;
            bool want = content && !menus && Objectives.Live && !Opening.Playing && GameMode.Current != GameMode.Mode.Build && !TeamMatch.Running;
            _group.alpha = Mathf.MoveTowards(_group.alpha, want ? 1f : 0f, dt / Mathf.Max(0.01f, set.fadeSeconds));
            if (_group.alpha <= 0f) { _y = -1f; return; }   // placed afresh when it shows again

            // The letters' strip shares the corner: while it can show (in a zone with letters, and its seconds after),
            // the card sits under it.
            FlatZone zone = FlatZone.Current;
            if (zone != null)
            {
                FlatLetter.Mask(zone, out int present, out _);
                if (present != 0) _flatUntil = now + Mathf.Max(0f, zone.Settings.stripSeconds);
            }
            float top = now < _flatUntil ? Top + FlatStrip : Top;
            float y = _y < 0f ? top : Mathf.Lerp(_y, top, 1f - Mathf.Exp(-12f * dt));
            if (Mathf.Abs(y - top) < 0.05f) y = top;
            if (y != _y) { _y = y; _card.anchoredPosition = new Vector2(Left, -y); }

            if (_phase == Phase.Slide)
            {
                float t = Mathf.Clamp01((now - _phaseAt) / Mathf.Max(0.01f, set.slideSeconds));
                float ease = 1f - (1f - t) * (1f - t);
                _stepRt.anchoredPosition = new Vector2(Box + BoxGap + (1f - ease) * SlideFrom, 0f);
                _step.alpha = t;
                if (t >= 1f) _phase = Phase.Still;
            }

            // The nudge's pulse: beats of a swell, eased in and out (the card's corner stays put; it is its pivot).
            float u = (now - _pulseAt) / Mathf.Max(0.05f, set.pulseSeconds);
            if (u >= 0f && u < 1f)
            {
                float swell = 1f + set.pulseSwell * Mathf.Abs(Mathf.Sin(u * Mathf.PI * Mathf.Max(1, set.pulseBeats)));
                _card.localScale = new Vector3(swell, swell, 1f);
            }
            else if (_card.localScale.x != 1f) _card.localScale = Vector3.one;

            float p = Mathf.Clamp01((now - _popAt) / Mathf.Max(0.01f, set.popSeconds));
            if (p >= 1f && _starIcon.localScale.x == 1f) return;   // still: nothing is touched (no canvas rebuild)
            float scale = p < 1f ? Mathf.LerpUnclamped(PopFrom, 1f, EaseOutBack(p)) : 1f;
            _starIcon.localScale = new Vector3(scale, scale, 1f);
        }

        private static float EaseOutBack(float t) { const float k = 1.70158f; t -= 1f; return 1f + (k + 1f) * t * t * t + k * t * t; }

        /// <summary>Sizes the card round what it shows: the goal and the step in the house, the counter everywhere. Run on
        /// a change only.</summary>
        private void Layout()
        {
            if (_card == null) return;
            bool steps = _stepsHere && _shown != null;
            _goalRt.gameObject.SetActive(steps);
            _stepRow.gameObject.SetActive(steps);
            float maxText = MaxWide - Pad * 2f;
            float inner = 0f, y = Pad;
            if (steps)
            {
                _goal.text = Objectives.Goal;
                _step.text = _shown.text;
                Vector2 goalSize = _goal.GetPreferredValues(_goal.text, maxText, 0f);
                Vector2 stepSize = _step.GetPreferredValues(_step.text, maxText - Box - BoxGap, 0f);
                float stepW = Mathf.Min(stepSize.x + 2f, maxText - Box - BoxGap);
                inner = Mathf.Max(Mathf.Min(goalSize.x + 2f, maxText), Box + BoxGap + stepW);
                _goalRt.anchoredPosition = new Vector2(Pad, -y); _goalRt.sizeDelta = new Vector2(maxText, goalSize.y);
                y += goalSize.y + RowGap;
                float rowH = Mathf.Max(Box, stepSize.y);
                _stepRow.anchoredPosition = new Vector2(Pad, -y); _stepRow.sizeDelta = new Vector2(maxText, rowH);
                _stepRt.sizeDelta = new Vector2(stepW, rowH);
                if (_phase != Phase.Slide) { _stepRt.anchoredPosition = new Vector2(Box + BoxGap, 0f); _step.alpha = 1f; }
                y += rowH + RowGap;
            }
            Vector2 countSize = _count.GetPreferredValues(_count.text, maxText, 0f);
            inner = Mathf.Max(inner, Icon + IconGap + countSize.x + 2f);
            _starRow.anchoredPosition = new Vector2(Pad, -y); _starRow.sizeDelta = new Vector2(maxText, Icon);
            y += Icon + Pad;
            _card.sizeDelta = new Vector2(Mathf.Clamp(inner + Pad * 2f, MinWide, MaxWide), y);
        }

        // ---------- building ----------

        private void Build()
        {
            var font = Resources.Load<TMP_FontAsset>(FontPath);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 35;   // under the letters' strip (36) and the toasts (60)
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; _group.alpha = 0f;

            Vector2 topLeft = new Vector2(0f, 1f), middle = new Vector2(0.5f, 0.5f);
            _card = BuildUiKit.Rect("Objectives", canvasGo.transform, topLeft, topLeft, topLeft, new Vector2(Left, -Top), new Vector2(MinWide, 100f));
            HudShapes.RoundedImage(BuildUiKit.Rect("Shadow", _card, Vector2.zero, Vector2.one, middle, new Vector2(0f, -Shadow), Vector2.zero), Ink, Round);
            HudShapes.RoundedImage(BuildUiKit.Rect("Outline", _card, Vector2.zero, Vector2.one, middle, Vector2.zero, Vector2.zero), Ink, Round);
            HudShapes.RoundedImage(BuildUiKit.Rect("Fill", _card, Vector2.zero, Vector2.one, middle, Vector2.zero, new Vector2(-Outline * 2f, -Outline * 2f)), Card, Round - Outline);

            // Line 1: the big goal, small capitals.
            _goalRt = BuildUiKit.Rect("Goal", _card, topLeft, topLeft, topLeft, new Vector2(Pad, -Pad), new Vector2(MaxWide - Pad * 2f, 24f));
            _goal = Text(_goalRt, font, GoalLetters, Label, FontStyles.Bold | FontStyles.UpperCase, false);
            _goal.characterSpacing = 4f;

            // Line 2: the tick box and the step.
            _stepRow = BuildUiKit.Rect("Step", _card, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(MaxWide - Pad * 2f, Box));
            var box = BuildUiKit.Rect("Box", _stepRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(Box, Box));
            HudShapes.RoundedImage(box, Ink, 8f);
            HudShapes.RoundedImage(BuildUiKit.Rect("Fill", box, Vector2.zero, Vector2.one, middle, Vector2.zero, new Vector2(-6f, -6f)), BoxFill, 5f);
            _tick = BuildUiKit.Rect("Tick", box, middle, middle, middle, new Vector2(1f, 1f), new Vector2(Box + 6f, Box + 6f)).gameObject.AddComponent<Image>();
            _tick.sprite = HudShapes.Check; _tick.color = Ink; _tick.raycastTarget = false; _tick.enabled = false;
            _stepRt = BuildUiKit.Rect("Words", _stepRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Box + BoxGap, 0f), new Vector2(200f, Box));
            _step = Text(_stepRt, font, StepLetters, Ink, FontStyles.Normal, true);
            _step.alignment = TextAlignmentOptions.MidlineLeft;

            // The star counter.
            _starRow = BuildUiKit.Rect("Stars", _card, topLeft, topLeft, topLeft, Vector2.zero, new Vector2(MaxWide - Pad * 2f, Icon));
            _starIcon = BuildUiKit.Rect("Star", _starRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), middle, new Vector2(Icon * 0.5f, 0f), new Vector2(Icon, Icon));
            var back = BuildUiKit.Rect("Back", _starIcon, Vector2.zero, Vector2.one, middle, new Vector2(0f, -2f), new Vector2(8f, 8f)).gameObject.AddComponent<Image>();
            back.sprite = StarSprite; back.color = Ink; back.raycastTarget = false;
            var face = BuildUiKit.Rect("Face", _starIcon, Vector2.zero, Vector2.one, middle, new Vector2(0f, 1f), Vector2.zero).gameObject.AddComponent<Image>();
            face.sprite = StarSprite; face.color = Gold; face.raycastTarget = false;
            var countRt = BuildUiKit.Rect("Count", _starRow, Vector2.zero, Vector2.one, middle, new Vector2((Icon + IconGap) * 0.5f, 0f), new Vector2(-(Icon + IconGap), 0f));
            _count = Text(countRt, font, CountLetters, Ink, FontStyles.Bold, false);
            _count.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset font, float size, Color colour, FontStyles style, bool wrap)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.fontStyle = style; t.color = colour; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }
    }
}
