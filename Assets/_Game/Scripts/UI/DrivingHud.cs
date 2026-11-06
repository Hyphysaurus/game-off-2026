using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The driving HUD (D-0037). Bottom right while driving in Play (hidden in the garage menu, Build Mode and first
    /// person): the speed in scale km/h (units per second x 4.608: 1 unit = 2 cm and the toy car is about 1:64) inside an
    /// arc that fills with speed and warms to red near top speed and while boosting, the nitro bar with the Shift key, a
    /// BOOST pop when a boost fires, and a small wind meter while the pull-back spring winds. Top right during a race and
    /// a while after it: the lap, lap time, best lap and gates, a toast for each lap, then FINISH and the total time.
    /// Reads the car only through IChaseTarget and IBoostable, and the race through RaceState. Made by code at start like
    /// KeyHelp and lives across scenes. Fonts come from Resources/HUD (Shift Jam > UI > Make HUD Fonts), TMP's default
    /// when missing. It sorts under the scene HUD (order 0), so the screen fade and the start and end cards cover it.
    /// </summary>
    public sealed class DrivingHud : MonoBehaviour
    {
        private const int SortingOrder = -10;
        private const float ArcDegrees = 270f, RedlineFrom = 0.85f;
        private const float DialSize = 250f, Width = 260f, NitroHeight = 64f, HealthHeight = 44f, Gap = 8f;
        private const float DialBase = NitroHeight + Gap + HealthHeight + Gap;   // the dial sits over the nitro and health panels
        private const int RoundRadius = 32;   // corner radius of the generated 9-slice sprite, pixels
        private const string NumberFont = "HUD/Teko SDF", LabelFont = "HUD/Fredoka SDF", ShiftIcon = "HUD/keyboard_shift";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        [Header("Speed")]
        [SerializeField, Tooltip("Scale km/h per unit per second (1 unit = 2 cm, the toy car is about 1:64).")] private float kmhPerUnit = 4.608f;
        [SerializeField, Tooltip("Top speed when the vehicle does not report one, units per second.")] private float fallbackTopSpeed = 26f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of top speed where the arc starts to warm.")] private float warmFrom = 0.6f;
        [SerializeField, Tooltip("Seconds the shown speed takes to settle (smooths bumps and landings).")] private float speedSmoothing = 0.08f;

        [Header("Colours")]
        [SerializeField] private Color accent = new Color(0.35f, 0.85f, 1f);       // BuildUiKit's cyan at full strength
        [SerializeField] private Color labelColour = new Color(0.56f, 0.89f, 1f);  // KeyHelp's key colour
        [SerializeField] private Color warm = new Color(1f, 0.74f, 0.22f);
        [SerializeField] private Color hot = new Color(1f, 0.26f, 0.18f);
        [SerializeField] private Color boostColour = new Color(1f, 0.45f, 0.12f);
        [SerializeField] private Color gold = new Color(1f, 0.84f, 0.3f);
        [SerializeField] private Color bestColour = new Color(0.45f, 1f, 0.62f);

        [Header("Timing (seconds, unscaled)")]
        [SerializeField] private float fadeSeconds = 0.25f;
        [SerializeField] private float boostPopSeconds = 1.1f;
        [SerializeField] private float lapToastSeconds = 3f;
        [SerializeField] private float finishBannerSeconds = 5f;
        [SerializeField, Tooltip("How long the race panel stays up after the finish.")] private float finishPanelSeconds = 8f;

        private readonly List<Object> _made = new List<Object>();   // generated textures and sprites, freed on destroy
        private TMP_FontAsset _numbers, _labels;
        private Sprite _ring, _disc, _ticks, _rounded, _shiftSprite;
        private string _speedFormat, _timeFormat, _fractionFormat, _percentFormat;

        private Canvas _canvas;
        private CanvasGroup _drive, _badge, _wind, _race, _toast, _banner;
        private RectTransform _badgeRt, _bannerRt, _nitroFillRt;
        private GameObject _nitroPanel, _healthPanel;
        private RectTransform _healthFillRt;
        private Image _healthFill, _shieldRim;
        private TextMeshProUGUI _healthRight;
        private Damageable _health;
        private CarShield _shield;
        private float _healthShown = 1f, _lastHealth = 1f, _hurtUntil, _nextShieldLook;
        private int _rightShown = -2;
        private Image _arc, _nitroFill, _windArc;
        private TextMeshProUGUI _speed, _ready, _windValue, _windLabel;
        private TextMeshProUGUI _lapLabel, _lapValue, _timeLabel, _timeValue, _bestValue, _gateLabel, _gateValue;
        private TextMeshProUGUI _toastLabel, _toastValue, _bannerTime, _bannerBest;

        private Transform _vehicle;
        private IChaseTarget _chase;
        private IBoostable _boost;
        private Vector3 _lastPos, _measured;
        private bool _hasLast, _wasBoosting, _windFull, _raceFinished;
        private float _shownSpeed, _speedVel, _nitroShown, _lastNitro;
        private float _popAt = -99f, _spendUntil, _toastUntil, _bannerAt = -99f, _bannerUntil, _panelUntil;
        private int _kmhShown = -1, _windShown = -1, _raceMode = -1;
        private int _lapA = -1, _lapB = -1, _timeCs = -1, _bestCs = -2, _gateA = -2, _gateB = -2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<DrivingHud>() != null) return;
            var go = new GameObject("DrivingHud");
            DontDestroyOnLoad(go);
            go.AddComponent<DrivingHud>();
        }

        private void Awake()
        {
            LoadAssets();
            _ring = Ring("Ring", 512, 0.84f);
            _disc = Ring("Disc", 256, 0f);
            _ticks = Ticks(512);
            _rounded = Rounded();
            BuildCanvas();
        }

        private void OnEnable()
        {
            RaceState.Started += OnRaceStarted;
            RaceState.LapDone += OnLapDone;
            RaceState.Finished += OnRaceFinished;
            RaceState.Stopped += OnRaceStopped;
        }

        private void OnDisable()
        {
            RaceState.Started -= OnRaceStarted;
            RaceState.LapDone -= OnLapDone;
            RaceState.Finished -= OnRaceFinished;
            RaceState.Stopped -= OnRaceStopped;
        }

        private void OnDestroy()
        {
            foreach (var o in _made) if (o != null) Destroy(o);
            _made.Clear();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime, now = Time.unscaledTime;
            bool play = GameMode.Current == GameMode.Mode.Play && !Garage.MenuOpen;

            Transform vehicle = Driving.Vehicle;
            if (vehicle != _vehicle) Bind(vehicle);
            bool driving = play && _vehicle != null;
            Fade(_drive, driving, dt);
            if (driving) UpdateDrive(dt, now);   // fading out keeps the last readout

            bool racing = play && (RaceState.Running || now < _panelUntil);
            Fade(_race, racing, dt);
            if (racing || _race.alpha > 0f) UpdateRace();
            Fade(_toast, play && now < _toastUntil, dt);
            Fade(_banner, play && now < _bannerUntil, dt);
            if (_banner.alpha > 0f) _bannerRt.localScale = Vector3.one * Punch(now - _bannerAt, 0.5f);

            bool any = _drive.alpha > 0f || _race.alpha > 0f || _toast.alpha > 0f || _banner.alpha > 0f;
            if (_canvas.enabled != any) _canvas.enabled = any;
        }

        // ---------- the vehicle ----------

        private void Bind(Transform vehicle)
        {
            _vehicle = vehicle;
            _chase = null; _boost = null; _hasLast = false;
            if (vehicle != null)
            {
                _chase = Find<IChaseTarget>(vehicle);
                _boost = Find<IBoostable>(vehicle);
                if (_chase == null || _boost == null)
                    foreach (var car in ToyCar.All)   // the wooden car's physics ball is a separate object
                        if (car != null && car.Ball == vehicle)
                        {
                            if (_chase == null) _chase = car;
                            if (_boost == null) _boost = Find<IBoostable>(car.transform);
                            break;
                        }
            }
            _health = vehicle != null ? Find<Damageable>(vehicle) : null;   // the wooden car has none: no health panel
            _shield = null; _nextShieldLook = 0f; _rightShown = -2; _hurtUntil = 0f;
            _healthShown = _lastHealth = _health != null ? Mathf.Clamp01(_health.Health / Mathf.Max(1f, _health.MaxHealth)) : 1f;
            if (_healthPanel.activeSelf != (_health != null)) _healthPanel.SetActive(_health != null);
            bool boost = _boost != null;
            _wasBoosting = boost && _boost.Boosting;   // getting into a car mid-boost does not pop
            _nitroShown = _lastNitro = boost ? Mathf.Clamp01(_boost.Nitro) : 0f;
            _windShown = -1; _windFull = false; _spendUntil = 0f;
            _wind.alpha = 0f; _badge.alpha = 0f;
            if (_nitroPanel.activeSelf != boost) _nitroPanel.SetActive(boost);
        }

        private static T Find<T>(Transform t) where T : class
        {
            T found = t.GetComponent<T>();
            if (!Alive(found)) found = t.GetComponentInParent<T>();
            return Alive(found) ? found : null;
        }

        /// <summary>Null-safe for destroyed components (Unity's own null check), plain null for anything else.</summary>
        private static bool Alive(object o) => (o is Object) ? (Object)o != null : o != null;

        private void UpdateDrive(float dt, float now)
        {
            if (_chase != null && !Alive(_chase)) _chase = null;
            if (_boost != null && !Alive(_boost)) { _boost = null; _nitroPanel.SetActive(false); }
            if (_health != null && !Alive(_health)) { _health = null; _healthPanel.SetActive(false); }

            Vector3 velocity = _chase != null ? _chase.ChaseVelocity : Measured();
            float top = _chase != null && _chase.ChaseTopSpeed > 0.01f ? _chase.ChaseTopSpeed : fallbackTopSpeed;
            if (dt > 0f) _shownSpeed = Mathf.SmoothDamp(_shownSpeed, velocity.magnitude, ref _speedVel, speedSmoothing, Mathf.Infinity, dt);
            int kmh = Mathf.RoundToInt(_shownSpeed * kmhPerUnit);
            if (kmh != _kmhShown) { _kmhShown = kmh; _speed.SetText(_speedFormat, kmh); }

            bool boosting = _boost != null && _boost.Boosting;
            float speed01 = _shownSpeed / top;
            _arc.fillAmount = ArcDegrees / 360f * Mathf.Clamp01(speed01);
            _arc.color = ArcColour(speed01, boosting, now);
            _speed.color = boosting ? Color.Lerp(Color.white, warm, 0.35f) : Color.white;

            if (boosting && !_wasBoosting) _popAt = now;
            _wasBoosting = boosting;
            float t = now - _popAt;
            if (t < boostPopSeconds)
            {
                float hold = boostPopSeconds * 0.6f;
                _badge.alpha = t < hold ? 1f : 1f - (t - hold) / (boostPopSeconds - hold);
                _badgeRt.localScale = Vector3.one * Punch(t, 0.3f);
            }
            else if (_badge.alpha > 0f) _badge.alpha = 0f;

            if (_boost != null) UpdateNitro(dt, now);
            if (_health != null) UpdateHealth(dt, now);
            UpdateWind(_boost != null ? Mathf.Clamp01(_boost.Wind) : 0f, dt, now);
        }

        /// <summary>For a vehicle that does not report its speed: measured from how it moves.</summary>
        private Vector3 Measured()
        {
            Vector3 p = _vehicle.position;
            if (_hasLast && Time.deltaTime > 0f) _measured = (p - _lastPos) / Time.deltaTime;
            _lastPos = p; _hasLast = true;
            return _measured;
        }

        private Color ArcColour(float speed01, bool boosting, float now)
        {
            if (boosting) return Color.Lerp(hot, Color.white, 0.2f * (0.5f + 0.5f * Mathf.Sin(now * 18f)));
            if (speed01 <= warmFrom) return accent;
            float k = Mathf.InverseLerp(warmFrom, 1f, speed01);
            return k < 0.6f ? Color.Lerp(accent, warm, k / 0.6f) : Color.Lerp(warm, hot, (k - 0.6f) / 0.4f);
        }

        private void UpdateNitro(float dt, float now)
        {
            float nitro = Mathf.Clamp01(_boost.Nitro);
            if (nitro < _lastNitro - 0.0001f) _spendUntil = now + 0.15f;   // burning: steps may come once per physics tick
            _lastNitro = nitro;
            _nitroShown = Mathf.Abs(_nitroShown - nitro) < 0.002f ? nitro : Mathf.Lerp(_nitroShown, nitro, 1f - Mathf.Exp(-14f * dt));
            Vector2 max = _nitroFillRt.anchorMax;
            if (max.x != _nitroShown) { max.x = _nitroShown; _nitroFillRt.anchorMax = max; }
            _nitroFill.enabled = _nitroShown > 0.02f;
            bool full = nitro >= 0.999f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 8f);
            _nitroFill.color = now < _spendUntil ? boostColour : full ? Color.Lerp(accent, Color.white, 0.45f * pulse) : accent;
            _ready.alpha = full ? 0.65f + 0.35f * pulse : 0f;
        }

        /// <summary>The health bar (green, amber, red and pulsing as it drops, a white flash on a hit) and, while a shield is up,
        /// a cyan rim round it and its seconds in place of the percent (D-0038).</summary>
        private void UpdateHealth(float dt, float now)
        {
            float share = Mathf.Clamp01(_health.Health / Mathf.Max(1f, _health.MaxHealth));
            if (share < _lastHealth - 0.0001f) _hurtUntil = now + 0.3f;
            _lastHealth = share;
            _healthShown = Mathf.Abs(_healthShown - share) < 0.002f ? share : Mathf.Lerp(_healthShown, share, 1f - Mathf.Exp(-10f * dt));
            Vector2 max = _healthFillRt.anchorMax;
            if (max.x != _healthShown) { max.x = _healthShown; _healthFillRt.anchorMax = max; }
            _healthFill.enabled = _healthShown > 0.01f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 9f);
            Color c = share > 0.6f ? bestColour : share > 0.3f ? warm : Color.Lerp(hot, Color.white, 0.25f * pulse);
            _healthFill.color = now < _hurtUntil ? Color.Lerp(c, Color.white, 0.6f) : c;

            if (_shield == null && now >= _nextShieldLook) { _shield = _health.GetComponent<CarShield>(); _nextShieldLook = now + 0.25f; }
            float left = _shield != null && Alive(_shield) ? _shield.Remaining : 0f;
            bool shielded = left > 0f;
            _shieldRim.enabled = shielded && (left > 2f || Mathf.Repeat(now * 6f, 1f) < 0.6f);
            if (shielded) _shieldRim.color = new Color(accent.r, accent.g, accent.b, 0.55f + 0.35f * pulse);
            int seconds = Mathf.CeilToInt(left), percent = Mathf.RoundToInt(share * 100f);
            int right = shielded ? 1000 + seconds : percent;
            if (right == _rightShown) return;
            _rightShown = right;
            if (shielded) { _healthRight.color = accent; _healthRight.SetText("SHIELD {0}", seconds); }
            else { _healthRight.color = labelColour; _healthRight.SetText(_percentFormat, percent); }
        }

        private void UpdateWind(float wind, float dt, float now)
        {
            bool on = wind > 0.001f;
            Fade(_wind, on, dt);
            if (!on) return;
            bool full = wind >= 0.98f;
            _windArc.fillAmount = wind;
            _windArc.color = full ? Color.Lerp(gold, Color.white, 0.5f + 0.5f * Mathf.Sin(now * 14f)) : Color.Lerp(warm, gold, wind);
            if (full != _windFull || _windShown < 0)
            {
                _windFull = full;
                _windLabel.SetText(full ? "LET GO" : "WIND");
                _windLabel.color = full ? gold : labelColour;
                _windValue.color = full ? gold : Color.white;
                _windShown = -1;
            }
            int pct = full ? 100 : Mathf.FloorToInt(wind * 100f);
            if (pct == _windShown) return;
            _windShown = pct;
            if (full) _windValue.SetText("MAX");
            else _windValue.SetText(_percentFormat, pct);
        }

        // ---------- the race ----------

        private void OnRaceStarted()
        {
            _raceFinished = false; _raceMode = -1;
            _panelUntil = _toastUntil = _bannerUntil = 0f;
        }

        private void OnRaceStopped()
        {
            _raceFinished = false;
            _panelUntil = _toastUntil = _bannerUntil = 0f;
        }

        private void OnLapDone()
        {
            if (RaceState.Lap >= RaceState.Laps) return;   // the last lap: the finish shows instead
            bool best = RaceState.Lap > 1 && RaceState.LastLap <= RaceState.BestLap;
            if (best) _toastLabel.SetText("BEST LAP!");
            else _toastLabel.SetText("LAP {0}", RaceState.Lap);
            _toastLabel.color = best ? bestColour : labelColour;
            int shown = -1;
            SetTime(_toastValue, RaceState.LastLap, ref shown);
            _toastUntil = Time.unscaledTime + lapToastSeconds;
        }

        private void OnRaceFinished()
        {
            float now = Time.unscaledTime;
            _raceFinished = true;
            _toastUntil = 0f;
            _panelUntil = now + finishPanelSeconds;
            _bannerAt = now; _bannerUntil = now + finishBannerSeconds;
            int shown = -1;
            SetTime(_bannerTime, RaceState.TotalTime, ref shown);
            shown = -2;
            SetBest(_bannerBest, RaceState.BestLap, ref shown);
        }

        private void UpdateRace()
        {
            bool running = RaceState.Running;
            if (!running && !_raceFinished) return;   // abandoned: fade out on the last readout
            int mode = running ? 0 : 1;
            if (mode != _raceMode)
            {
                _raceMode = mode;
                _lapLabel.SetText(running ? "LAP" : "FINISH");
                _lapLabel.color = running ? labelColour : gold;
                _timeLabel.SetText(running ? "TIME" : "TOTAL");
                _lapA = _lapB = _timeCs = -1; _bestCs = _gateA = _gateB = -2;
            }
            int lap = RaceState.Lap, laps = RaceState.Laps;   // after a finish Lap is the number of laps run
            if (lap != _lapA || laps != _lapB) { _lapA = lap; _lapB = laps; _lapValue.SetText(_fractionFormat, lap, laps); }
            SetTime(_timeValue, running ? RaceState.LapTime : RaceState.TotalTime, ref _timeCs);
            SetBest(_bestValue, RaceState.BestLap, ref _bestCs);

            int gates = running ? RaceState.Gates : 0, gate = gates > 0 ? Mathf.Min(RaceState.Gate, gates) : -1;
            if (gate == _gateA && gates == _gateB) return;
            _gateA = gate; _gateB = gates;
            _gateLabel.alpha = _gateValue.alpha = gate >= 0 ? 1f : 0f;
            if (gate >= 0) _gateValue.SetText(_fractionFormat, gate, gates);
        }

        /// <summary>m:ss.cc, only when the shown hundredth changes.</summary>
        private void SetTime(TextMeshProUGUI text, float seconds, ref int shown)
        {
            int cs = Mathf.Max(0, Mathf.FloorToInt(seconds * 100f));
            if (cs == shown) return;
            shown = cs;
            text.SetText(_timeFormat, cs / 6000, cs / 100 % 60, cs % 100);
        }

        private void SetBest(TextMeshProUGUI text, float seconds, ref int shown)
        {
            if (seconds > 0f) { SetTime(text, seconds, ref shown); return; }
            if (shown == -1) return;
            shown = -1;
            text.SetText("-:--.--");
        }

        // ---------- motion ----------

        private void Fade(CanvasGroup group, bool on, float dt)
        {
            float target = on ? 1f : 0f;
            if (group.alpha != target) group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSeconds > 0f ? dt / fadeSeconds : 1f);
        }

        /// <summary>Scale for a pop: starts at <paramref name="from"/>, overshoots, settles at 1.</summary>
        private static float Punch(float t, float from) => 1f + (from - 1f) * Mathf.Exp(-9f * t) * Mathf.Cos(22f * t);

        // ---------- building ----------

        private void LoadAssets()
        {
            _numbers = Resources.Load<TMP_FontAsset>(NumberFont);
            _labels = Resources.Load<TMP_FontAsset>(LabelFont);
            _shiftSprite = Resources.Load<Sprite>(ShiftIcon);
            if (_shiftSprite == null)
            {
                var tex = Resources.Load<Texture2D>(ShiftIcon);   // imported as a plain texture
                if (tex != null)
                {
                    _shiftSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), Centre, 100f, 0, SpriteMeshType.FullRect);
                    _made.Add(_shiftSprite);
                }
            }
            // digits in fixed cells so numbers do not shuffle sideways as they change (Teko's digits are proportional)
            string cell = _numbers != null ? "<mspace=0.52em>" : "<mspace=0.6em>";
            _speedFormat = cell + "{0}</mspace>";
            _timeFormat = cell + "{0}</mspace>:" + cell + "{1:00}</mspace>." + cell + "{2:00}</mspace>";
            _fractionFormat = cell + "{0}</mspace>/" + cell + "{1}</mspace>";
            _percentFormat = cell + "{0}</mspace>%";
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("DrivingHudCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;

            BuildDrive(canvasGo.transform);
            BuildRace(canvasGo.transform);
            BuildBanner(canvasGo.transform);
            _canvas.enabled = false;
        }

        private void BuildDrive(Transform canvas)
        {
            var drive = BuildUiKit.Rect("Drive", canvas, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 30f), new Vector2(Width, DialBase + DialSize));
            _drive = Group(drive);

            // the dial: speed arc over a dim track with a red zone, ticks, the number
            var dial = At("Dial", drive, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, DialBase), new Vector2(DialSize, DialSize));
            Img("Back", dial, _disc, BuildUiKit.PanelColor, 0f);
            float start = (360f - ArcDegrees) * 0.5f, share = ArcDegrees / 360f;
            Arc("Track", dial, start, share, new Color(1f, 1f, 1f, 0.1f));
            Arc("Redline", dial, start + ArcDegrees * RedlineFrom, share * (1f - RedlineFrom), new Color(hot.r, hot.g, hot.b, 0.4f));
            _arc = Arc("Speed", dial, start, 0f, accent);
            Img("Ticks", dial, _ticks, new Color(1f, 1f, 1f, 0.35f), 4f);
            _speed = Label(At("Speed", dial, Centre, Centre, new Vector2(0f, 8f), new Vector2(200f, 100f)), 96f, true, TextAlignmentOptions.Capline, Color.white);
            _speed.SetText(_speedFormat, 0f);
            Label(At("Unit", dial, Centre, Centre, new Vector2(0f, -52f), new Vector2(120f, 28f)), 18f, false, TextAlignmentOptions.Capline, labelColour).SetText("km/h");

            // nitro: the Shift key and the bar
            var nitro = At("Nitro", drive, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Width, NitroHeight));
            Round(nitro, BuildUiKit.PanelColor, 16f);
            _nitroPanel = nitro.gameObject;
            ShiftKey(At("ShiftKey", nitro, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(64f, 64f)));
            Label(At("Label", nitro, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(74f, 44f), new Vector2(110f, 20f)), 15f, false, TextAlignmentOptions.CaplineLeft, labelColour).SetText("NITRO");
            _ready = Label(At("Ready", nitro, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-14f, 44f), new Vector2(110f, 20f)), 15f, false, TextAlignmentOptions.CaplineRight, gold);
            _ready.SetText("READY");
            _ready.alpha = 0f;
            var bar = At("Bar", nitro, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(74f, 22f), new Vector2(Width - 88f, 16f));
            Round(bar, new Color(1f, 1f, 1f, 0.1f), 8f);
            var area = BuildUiKit.Fill("Area", bar, 3f, 3f, 3f, 3f);
            _nitroFillRt = BuildUiKit.Rect("Fill", area, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _nitroFill = Round(_nitroFillRt, accent, 5f);
            _nitroFill.enabled = false;

            // health over the nitro: the bar, the percent (or the shield's seconds), a cyan rim while shielded (D-0038)
            var health = At("Health", drive, Vector2.zero, Vector2.zero, new Vector2(0f, NitroHeight + Gap), new Vector2(Width, HealthHeight));
            Round(health, BuildUiKit.PanelColor, 14f);
            _healthPanel = health.gameObject;
            Label(At("Label", health, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(14f, 30f), new Vector2(120f, 18f)), 14f, false, TextAlignmentOptions.CaplineLeft, labelColour).SetText("HEALTH");
            _healthRight = Label(At("Right", health, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-14f, 30f), new Vector2(140f, 18f)), 14f, false, TextAlignmentOptions.CaplineRight, labelColour);
            _shieldRim = Round(At("ShieldRim", health, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(10f, 13f), new Vector2(Width - 20f, 20f)), accent, 10f);
            _shieldRim.enabled = false;
            var healthBar = At("Bar", health, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(14f, 13f), new Vector2(Width - 28f, 12f));
            Round(healthBar, new Color(0.08f, 0.1f, 0.14f, 1f), 6f);
            var healthArea = BuildUiKit.Fill("Area", healthBar, 2f, 2f, 2f, 2f);
            _healthFillRt = BuildUiKit.Rect("Fill", healthArea, Vector2.zero, new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _healthFill = Round(_healthFillRt, bestColour, 4f);
            _healthPanel.SetActive(false);

            // the BOOST pop above the dial
            _badgeRt = At("Boost", drive, new Vector2(0.5f, 0f), Centre, new Vector2(0f, DialBase + DialSize + 34f), new Vector2(190f, 56f));
            _badgeRt.localRotation = Quaternion.Euler(0f, 0f, 4f);
            Round(_badgeRt, boostColour, 28f);
            var boostText = Label(_badgeRt, 34f, false, TextAlignmentOptions.Capline, Color.white);
            boostText.fontStyle = FontStyles.Bold;
            boostText.SetText("BOOST!");
            _badge = Group(_badgeRt);

            // the wind meter, left of the dial, while the pull-back spring winds
            var wind = At("Wind", drive, Vector2.zero, Centre, new Vector2(-64f, DialBase + DialSize * 0.4f), new Vector2(100f, 100f));
            _wind = Group(wind);
            Img("Back", wind, _disc, BuildUiKit.PanelColor, 0f);
            Arc("Track", wind, 180f, 1f, new Color(1f, 1f, 1f, 0.1f));
            _windArc = Arc("Fill", wind, 180f, 0f, warm);
            _windValue = Label(At("Value", wind, Centre, Centre, new Vector2(0f, 7f), new Vector2(84f, 40f)), 34f, true, TextAlignmentOptions.Capline, Color.white);
            _windLabel = Label(At("Label", wind, Centre, Centre, new Vector2(0f, -20f), new Vector2(84f, 18f)), 13f, false, TextAlignmentOptions.Capline, labelColour);
            _windLabel.SetText("WIND");
        }

        private void BuildRace(Transform canvas)
        {
            var panel = At("Race", canvas, Vector2.one, Vector2.one, new Vector2(-36f, -30f), new Vector2(340f, 146f));
            Round(panel, BuildUiKit.PanelColor, 18f);
            _race = Group(panel);
            Vector2 left = new Vector2(0f, 1f), right = Vector2.one, leftPivot = new Vector2(0f, 0.5f), rightPivot = new Vector2(1f, 0.5f);
            _lapLabel = Label(At("LapLabel", panel, left, leftPivot, new Vector2(20f, -22f), new Vector2(150f, 20f)), 16f, false, TextAlignmentOptions.CaplineLeft, labelColour);
            _lapLabel.SetText("LAP");
            _timeLabel = Label(At("TimeLabel", panel, right, rightPivot, new Vector2(-20f, -22f), new Vector2(150f, 20f)), 16f, false, TextAlignmentOptions.CaplineRight, labelColour);
            _timeLabel.SetText("TIME");
            _lapValue = Label(At("Lap", panel, left, leftPivot, new Vector2(18f, -62f), new Vector2(130f, 56f)), 56f, true, TextAlignmentOptions.CaplineLeft, Color.white);
            _timeValue = Label(At("Time", panel, right, rightPivot, new Vector2(-18f, -62f), new Vector2(190f, 56f)), 56f, true, TextAlignmentOptions.CaplineRight, Color.white);
            var line = BuildUiKit.Rect("Line", panel, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -96f), new Vector2(-32f, 2f));
            BuildUiKit.Panel(line, new Color(1f, 1f, 1f, 0.08f)).raycastTarget = false;
            Label(At("BestLabel", panel, left, leftPivot, new Vector2(20f, -121f), new Vector2(54f, 20f)), 15f, false, TextAlignmentOptions.CaplineLeft, labelColour).SetText("BEST");
            _bestValue = Label(At("Best", panel, left, leftPivot, new Vector2(74f, -121f), new Vector2(120f, 30f)), 30f, true, TextAlignmentOptions.CaplineLeft, Color.white);
            _gateLabel = Label(At("GateLabel", panel, right, rightPivot, new Vector2(-76f, -121f), new Vector2(70f, 20f)), 15f, false, TextAlignmentOptions.CaplineRight, labelColour);
            _gateLabel.SetText("GATE");
            _gateValue = Label(At("Gate", panel, right, rightPivot, new Vector2(-20f, -121f), new Vector2(56f, 30f)), 30f, true, TextAlignmentOptions.CaplineRight, Color.white);

            // a toast under the panel for each finished lap
            var toast = At("LapToast", canvas, Vector2.one, Vector2.one, new Vector2(-36f, -184f), new Vector2(340f, 44f));
            Round(toast, BuildUiKit.PanelColor, 22f);
            _toast = Group(toast);
            _toastLabel = Label(At("Label", toast, new Vector2(0f, 0.5f), leftPivot, new Vector2(22f, 0f), new Vector2(170f, 20f)), 16f, false, TextAlignmentOptions.CaplineLeft, labelColour);
            _toastValue = Label(At("Time", toast, new Vector2(1f, 0.5f), rightPivot, new Vector2(-22f, 0f), new Vector2(150f, 30f)), 30f, true, TextAlignmentOptions.CaplineRight, Color.white);
        }

        private void BuildBanner(Transform canvas)
        {
            _bannerRt = At("Finish", canvas, Centre, Centre, new Vector2(0f, 262f), new Vector2(520f, 180f));   // clear of the scene's GoalText
            Round(_bannerRt, BuildUiKit.PanelColor, 26f);
            _banner = Group(_bannerRt);
            var title = Label(At("Title", _bannerRt, Centre, Centre, new Vector2(0f, 44f), new Vector2(480f, 70f)), 62f, false, TextAlignmentOptions.Capline, gold);
            title.fontStyle = FontStyles.Bold;
            title.SetText("FINISH!");
            _bannerTime = Label(At("Total", _bannerRt, Centre, Centre, new Vector2(0f, -14f), new Vector2(400f, 60f)), 60f, true, TextAlignmentOptions.Capline, Color.white);
            Label(At("BestLabel", _bannerRt, Centre, new Vector2(1f, 0.5f), new Vector2(-8f, -62f), new Vector2(160f, 22f)), 16f, false, TextAlignmentOptions.CaplineRight, labelColour).SetText("BEST LAP");
            _bannerBest = Label(At("Best", _bannerRt, Centre, new Vector2(0f, 0.5f), new Vector2(8f, -62f), new Vector2(160f, 30f)), 28f, true, TextAlignmentOptions.CaplineLeft, Color.white);
        }

        private void ShiftKey(RectTransform rt)
        {
            if (_shiftSprite != null)
            {
                var icon = rt.gameObject.AddComponent<Image>();
                icon.sprite = _shiftSprite; icon.preserveAspect = true; icon.raycastTarget = false;
                return;
            }
            var cap = At("Cap", rt, Centre, Centre, Vector2.zero, new Vector2(50f, 30f));   // no icon: a plain key
            Round(cap, Color.white, 7f);
            Label(cap, 13f, false, TextAlignmentOptions.Capline, new Color(0.06f, 0.07f, 0.1f)).SetText("SHIFT");
        }

        private static RectTransform At(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
            => BuildUiKit.Rect(name, parent, anchor, anchor, pivot, position, size);

        private static CanvasGroup Group(RectTransform rt)
        {
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            return group;
        }

        private TextMeshProUGUI Label(RectTransform rt, float size, bool number, TextAlignmentOptions align, Color colour)
        {
            var text = BuildUiKit.Label(rt, "", size, align);
            var font = number ? _numbers : _labels;
            if (font != null) text.font = font;
            text.color = colour;
            text.overflowMode = TextOverflowModes.Overflow;
            if (!number) text.characterSpacing = 4f;
            return text;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color colour, float inset)
        {
            var rt = BuildUiKit.Fill(name, parent, inset, inset, inset, inset);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = colour; img.raycastTarget = false;
            return img;
        }

        /// <summary>A ring filled clockwise from <paramref name="startDegrees"/> (clockwise from straight down).</summary>
        private Image Arc(string name, Transform parent, float startDegrees, float fill, Color colour)
        {
            var img = Img(name, parent, _ring, colour, 4f);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -startDegrees);
            img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Bottom; img.fillClockwise = true;
            img.fillAmount = fill;
            return img;
        }

        /// <summary>A rounded panel in the kit's way (BuildUiKit.Panel), corners <paramref name="radius"/> canvas units.</summary>
        private Image Round(RectTransform rt, Color colour, float radius)
        {
            var img = BuildUiKit.Panel(rt, colour);
            img.raycastTarget = false;
            img.sprite = _rounded; img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RoundRadius / Mathf.Max(1f, radius);
            return img;
        }

        // ---------- sprites drawn at start (white, anti-aliased; tinted by the images) ----------

        private Sprite Bake(string name, int size, Color32[] pixels, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "DrivingHud " + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), Centre, 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = tex.name;
            _made.Add(tex); _made.Add(sprite);
            return sprite;
        }

        private static Color32 White(float alpha) => new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f + 0.5f));

        /// <summary>A ring whose inner edge is <paramref name="inner"/> of the outer radius; 0 makes a disc.</summary>
        private Sprite Ring(string name, int size, float inner)
        {
            var px = new Color32[size * size];
            float c = size * 0.5f, outerR = c - 1f, innerR = outerR * inner;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(outerR - d + 0.5f);
                    if (inner > 0f) a *= Mathf.Clamp01(d - innerR + 0.5f);
                    px[y * size + x] = White(a);
                }
            return Bake(name, size, px, Vector4.zero);
        }

        /// <summary>Tick marks inside the ring along the speed arc: every 10%, longer at 0, 50 and 100%.</summary>
        private Sprite Ticks(int size)
        {
            const int count = 11;
            var px = new Color32[size * size];
            float c = size * 0.5f, outerR = c - 1f;
            float r0 = outerR * 0.71f, r0Major = outerR * 0.66f, r1 = outerR * 0.8f;
            float half = size / 256f, halfMajor = half * 1.6f;   // half widths in pixels
            var dirs = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float clockwiseFromDown = (360f - ArcDegrees) * 0.5f + ArcDegrees * i / (count - 1);
                float rad = (-90f - clockwiseFromDown) * Mathf.Deg2Rad;
                dirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            }
            float near = (r0Major - 2f) * (r0Major - 2f), far = (r1 + 2f) * (r1 + 2f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d2 = dx * dx + dy * dy, a = 0f;
                    if (d2 > near && d2 < far)
                        for (int i = 0; i < count; i++)
                        {
                            bool major = i % 5 == 0;
                            float along = dx * dirs[i].x + dy * dirs[i].y;
                            float across = Mathf.Abs(dx * dirs[i].y - dy * dirs[i].x);
                            float from = major ? r0Major : r0, w = major ? halfMajor : half;
                            float t = Mathf.Clamp01(w - across + 0.5f) * Mathf.Clamp01(along - from + 0.5f) * Mathf.Clamp01(r1 - along + 0.5f);
                            if (t > a) a = t;
                        }
                    px[y * size + x] = White(a);
                }
            return Bake("Ticks", size, px, Vector4.zero);
        }

        /// <summary>A white rounded square for 9-slicing: corners of RoundRadius pixels around 2 pixels of straight middle.</summary>
        private Sprite Rounded()
        {
            int r = RoundRadius, size = r * 2 + 2;
            var px = new Color32[size * size];
            float c = size * 0.5f, straight = c - r;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - c) - straight, qy = Mathf.Abs(y + 0.5f - c) - straight;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    px[y * size + x] = White(0.5f - outside);
                }
            return Bake("Rounded", size, px, new Vector4(r, r, r, r));
        }
    }
}
