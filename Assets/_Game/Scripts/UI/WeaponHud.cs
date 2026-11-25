using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The gun in hand on screen, after the test room's: the rounds in the gun and in reserve ("12 / 48", or RELOADING),
    /// the gun's name and the loaded ammo's name under it, and a dot crosshair in the middle (third person; first
    /// person draws its own), with a hit marker round it: four short ticks that flash when one of the holder's shots
    /// lands on something with health, bigger, longer and in another colour when that hit finishes it off (both
    /// views). The count turns to a warning colour when the magazine is nearly empty. Shown only while a
    /// blaster is out: not holstered or carrying, not in Build Mode, a menu, the key sheet or the console, not while
    /// driving. It only listens (GameEvents.AmmoChanged and ShotLanded, the AmmoBelt) and reads the holder; nothing knows it exists.
    /// Made by the WeaponHolder at start; built in code like the other HUDs. Numbers in Resources/WeaponAmmo.
    /// A player who throws grenades gets one more line above the count: how many are carried ("Grenades  3", dimmed at
    /// none; it listens to GameEvents.GrenadesChanged), and a grenade's blast flashes the same hit marker
    /// (GrenadeWentOff). Like the rest it is on screen only while a blaster is out. Numbers in Resources/Grenades.
    /// Later lines stack above with AddLine.
    /// In a Flat Zone's side view (D-0216) the screen-centre dot means nothing (the aim is the cursor or the pad's stick,
    /// not the middle of the view): a small reticle, four short strokes round the aim point, sits there instead while a
    /// blaster is out or a tool is in hand, and the hit marker flashes round it. Its numbers are in Resources/FlatView.
    /// </summary>
    public sealed class WeaponHud : MonoBehaviour
    {
        private const string NumberFont = "HUD/Teko SDF", LabelFont = "HUD/Fredoka SDF";
        private static readonly Color PanelColour = new Color(0f, 0f, 0f, 0.35f), DotDark = new Color(0f, 0f, 0f, 0.75f), TickDark = new Color(0f, 0f, 0f, 0.6f);
        private const float Width = 420f, Pad = 14f;

        private WeaponHolder _holder;
        private AmmoBelt _belt;
        private WeaponAmmoSettings _s;
        private CanvasGroup _group;
        private Carrier _carrier;
        private RectTransform _box;
        private TextMeshProUGUI _count, _name;
        private GameObject _dot;
        private TMP_FontAsset _numbers, _labels;
        private float _top;          // where the next line goes, above the box's bottom
        private bool _stale = true;  // the read-out no longer matches the gun
        private RectTransform _marker;          // the hit marker: four ticks round the middle
        private CanvasGroup _markerGroup;
        private Image[] _ticks;
        private float _flashAt = -100f, _flashSeconds = 0.2f, _flashScale = 1.25f;
        private GrenadeThrower _thrower;        // the holder's grenades, when it throws any
        private TextMeshProUGUI _grenades;      // their count, the line above the rounds
        private RectTransform _reticle;         // the side view's reticle (D-0216): four strokes round the aim point
        private RectTransform[] _strokes;       // its strokes, dark behind and white in front, by pairs
        private Vector3 _reticleFit;            // the size, thickness and gap it was last laid out for
        private bool _markerMoved;              // the hit marker is at the reticle, not the middle of the screen

        /// <summary>What the count shows now ("12 / 48", "RELOADING").</summary>
        public string CountText => _count.text;
        /// <summary>What the name line shows now ("Dart Pistol  |  Paint").</summary>
        public string NameText => _name.text;
        /// <summary>The count's colour now.</summary>
        public Color CountColour => _count.color;
        /// <summary>True while the read-out is on screen.</summary>
        public bool Showing => _group.alpha > 0f;
        /// <summary>Whose gun it reads.</summary>
        public WeaponHolder Holder => _holder;
        /// <summary>True while the ammo box itself (the count and the name) is drawn.</summary>
        public bool BoxShowing => Showing && _box.gameObject.activeInHierarchy;
        /// <summary>Where the ammo box is on the screen, in pixels from the bottom left ("x 40-460, y 72-190 of 1920 x 1080").</summary>
        public string BoxOnScreen
        {
            get
            {
                var corners = new Vector3[4];
                _box.GetWorldCorners(corners);
                return "x " + Mathf.RoundToInt(corners[0].x) + "-" + Mathf.RoundToInt(corners[2].x) + ", y " + Mathf.RoundToInt(corners[0].y) + "-" + Mathf.RoundToInt(corners[2].y) + " of " + Screen.width + " x " + Screen.height;
            }
        }
        /// <summary>Why the ammo box is not drawn now, in a word or two (empty while it is drawn). The same tests as Update.</summary>
        public string WhyHidden
        {
            get
            {
                if (_holder == null) return "no holder";
                if (!_holder.isActiveAndEnabled) return "the holder is switched off";
                if (!_holder.Armed) return "no gun out";
                if (!(_holder.Current is BlasterWeapon)) return "not a dart gun";
                if (GameMode.Current == GameMode.Mode.Build) return "build mode";
                if (PauseMenu.IsOpen) return "the pause menu is open";
                if (TitleMenu.IsOpen) return "the title menu counts as open";
                if (KeyHelp.IsOpen) return "the key sheet is open";
                if (DebugConsole.IsOpen) return "the console is open";
                return BoxShowing ? "" : "not drawn yet";
            }
        }
        /// <summary>True while the dot crosshair is on screen.</summary>
        public bool DotShowing => Showing && _dot.activeSelf;
        /// <summary>True while the side view's reticle is on screen (D-0216).</summary>
        public bool ReticleShowing => Showing && _reticle.gameObject.activeSelf;
        /// <summary>Where the side view's reticle is, pixels from the bottom left of the screen.</summary>
        public Vector2 ReticleAt => _reticle.position;
        /// <summary>True while the hit marker's flash is on screen.</summary>
        public bool MarkerShowing => Showing && _markerGroup.alpha > 0f;
        /// <summary>The colour of the hit marker's last flash.</summary>
        public Color MarkerColour => _ticks[0].color;
        /// <summary>What the grenade line shows now ("Grenades  3"); empty when the holder throws none.</summary>
        public string GrenadeText => _grenades != null ? _grenades.text : "";
        /// <summary>The grenade line's colour now.</summary>
        public Color GrenadeColour => _grenades != null ? _grenades.color : Color.clear;

        public static WeaponHud Create(WeaponHolder holder)
        {
            var go = new GameObject("WeaponHud");
            if (holder.GetComponent<NetPlayer>() != null) DontDestroyOnLoad(go);   // an online player outlives scene loads (it was made on the title, before its world): so does its HUD (D-0126)
            var hud = go.AddComponent<WeaponHud>();
            hud._holder = holder;
            hud._s = WeaponAmmoSettings.Load();
            holder.TryGetComponent(out hud._belt);
            holder.TryGetComponent(out hud._thrower);
            if (hud._belt != null) hud._belt.Changed += hud.OnBeltChanged;
            hud.Build();
            hud.Refresh();
            hud.WriteGrenades();
            return hud;
        }

        /// <summary>One more line above the ones already there (a grenade count); the caller writes its text.</summary>
        public TextMeshProUGUI AddLine(string name, float size, bool numbers)
        {
            var rt = BuildUiKit.Rect(name, _box, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(Pad, _top), new Vector2(Width - Pad * 2f, size * 1.15f));
            var text = BuildUiKit.Label(rt, "", size, TextAlignmentOptions.BottomLeft);
            TMP_FontAsset font = numbers ? _numbers : _labels;
            if (font != null) text.font = font;
            _top += size * 1.15f;
            _box.sizeDelta = new Vector2(Width, _top + Pad * 0.5f);
            return text;
        }

        private void OnEnable() { GameEvents.AmmoChanged += OnAmmoChanged; GameEvents.ShotLanded += OnShotLanded; GameEvents.GrenadesChanged += OnGrenadesChanged; GameEvents.GrenadeWentOff += OnGrenadeWentOff; }
        private void OnDisable() { GameEvents.AmmoChanged -= OnAmmoChanged; GameEvents.ShotLanded -= OnShotLanded; GameEvents.GrenadesChanged -= OnGrenadesChanged; GameEvents.GrenadeWentOff -= OnGrenadeWentOff; }
        private void OnDestroy() { if (_belt != null) _belt.Changed -= OnBeltChanged; }

        private void OnAmmoChanged(WeaponHolder who) { if (who == _holder) _stale = true; }
        private void OnBeltChanged(AmmoBelt belt) => _stale = true;
        private void OnShotLanded(WeaponHolder who, bool finished) { if (who == _holder) Flash(finished); }
        private void OnGrenadesChanged(GrenadeThrower who) { if (who == _thrower) WriteGrenades(); }
        private void OnGrenadeWentOff(GrenadeThrower who, int hits, bool finished) { if (who == _thrower && hits > 0) Flash(finished); }

        /// <summary>Writes the grenade line: how many are carried, dimmed when there are none.</summary>
        private void WriteGrenades()
        {
            if (_grenades == null || _thrower == null) return;
            GrenadeSettings g = _thrower.Settings;
            _grenades.text = string.Format(g.countText, _thrower.Carried);
            _grenades.color = _thrower.Carried > 0 ? g.countColour : g.emptyColour;
        }

        /// <summary>Flashes the hit marker: bigger, longer and in the other colour for the hit that finished something.</summary>
        public void Flash(bool finished)
        {
            Color colour = finished ? _s.downColour : _s.hitColour;
            foreach (Image tick in _ticks) tick.color = colour;
            _flashAt = Time.time;
            _flashSeconds = Mathf.Max(0.01f, _s.markerSeconds * (finished ? 2f : 1f));
            _flashScale = finished ? _s.markerScales.y : _s.markerScales.x;
            _markerGroup.alpha = 1f;
            _marker.localScale = Vector3.one * _flashScale;
        }

        private void Update()
        {
            if (_holder == null) { Destroy(gameObject); return; }   // the player is gone with its scene
            bool free = GameMode.Current != GameMode.Mode.Build && !PauseMenu.IsOpen && !TitleMenu.IsOpen && !KeyHelp.IsOpen && !DebugConsole.IsOpen;
            bool show = _holder.isActiveAndEnabled && _holder.Armed && _holder.Current is BlasterWeapon && free;
            if (_carrier == null) _carrier = _holder.GetComponent<Carrier>();
            bool lightAim = free && _carrier != null && _carrier.AimHeld;   // aiming the light shows the dot too (D-0147)
            // The side view (D-0216): a gun out or a tool in hand is always aimed at the cursor or the stick's way, so the
            // reticle shows while either is; the centre dot never does there.
            bool side = _holder.isActiveAndEnabled && _holder.AimsInSideView;
            Vector2 aimAt = default;
            bool reticle = side && free && (show || (_carrier != null && _carrier.ToolUp)) && _holder.SideAim.ScreenPoint(out aimAt);
            _group.alpha = show || lightAim || reticle ? 1f : 0f;
            if (_box.gameObject.activeSelf != show) _box.gameObject.SetActive(show);   // the ammo read-out is the gun's
            // The dot only while it means something (D-0154, Alan Wake 2): a gun raised (aim held, or a shot on its way) or
            // the light aimed. A gun hanging at the side does not point at it.
            _dot.SetActive(!side && ((show && _holder.Aiming) || lightAim) && !_holder.FirstPerson);
            ShowReticle(reticle, aimAt);
            if (show && _stale) Refresh();
            // The hit marker: big and solid as the shot lands, back to its size and gone over the flash.
            float flash = (Time.time - _flashAt) / _flashSeconds;
            if (flash >= 1f) { if (_markerGroup.alpha > 0f) _markerGroup.alpha = 0f; }
            else { _markerGroup.alpha = 1f - flash; _marker.localScale = Vector3.one * Mathf.Lerp(_flashScale, 1f, flash); }
        }

        /// <summary>The side view's reticle at <paramref name="at"/> (screen pixels) while <paramref name="on"/>, its numbers
        /// read from FlatView as they stand (tunable in Play); the hit marker goes with it, and back to the middle after.</summary>
        private void ShowReticle(bool on, Vector2 at)
        {
            if (_reticle.gameObject.activeSelf != on) _reticle.gameObject.SetActive(on);
            if (on)
            {
                FitReticle(_holder.SideAim.Settings);
                _reticle.position = at;   // an overlay canvas: its positions are screen pixels
                _marker.position = at;
                _markerMoved = true;
            }
            else if (_markerMoved)
            {
                _marker.anchoredPosition = Vector2.zero;
                _markerMoved = false;
            }
        }

        /// <summary>Lays the reticle's four strokes out for FlatView's Reticle Size, Thickness and Gap (pixels on a
        /// 1080-high screen): each runs outward from the gap to half the size, dark and a little wider behind, white in
        /// front. Only when the numbers changed.</summary>
        private void FitReticle(FlatViewSettings v)
        {
            var want = new Vector3(v.reticleSize, v.reticleThickness, v.reticleGap);
            if (want == _reticleFit) return;
            _reticleFit = want;
            float half = v.reticleSize * 0.5f, gap = Mathf.Min(v.reticleGap, half - 1f), length = Mathf.Max(1f, half - gap);
            _reticle.sizeDelta = Vector2.one * v.reticleSize;
            for (int i = 0; i < 4; i++)
            {
                var way = new Vector2(Mathf.Cos(i * 90f * Mathf.Deg2Rad), Mathf.Sin(i * 90f * Mathf.Deg2Rad));
                RectTransform behind = _strokes[i * 2], front = _strokes[i * 2 + 1];
                behind.anchoredPosition = front.anchoredPosition = way * gap;
                behind.sizeDelta = new Vector2(length + 2f, v.reticleThickness + 3f);
                front.sizeDelta = new Vector2(length, v.reticleThickness);
            }
        }

        /// <summary>Writes the read-out from the gun in hand.</summary>
        public void Refresh()
        {
            if (_holder == null || !(_holder.Current is BlasterWeapon gun)) return;
            _stale = false;
            Magazine m = gun.Magazine;
            bool reloading = _holder.Reloading;
            _count.text = reloading ? _s.reloadingText : m.Endless ? "--" : m.Rounds + " / " + m.Reserve;
            _count.color = !reloading && m.Low(_s.lowShare) ? _s.lowColour : _s.countColour;
            string ammo = _belt != null ? _belt.CurrentName : "";
            _name.text = string.IsNullOrEmpty(ammo) ? gun.DisplayName : gun.DisplayName + "  |  " + ammo;
        }

        private void Build()
        {
            _numbers = Resources.Load<TMP_FontAsset>(NumberFont);
            _labels = Resources.Load<TMP_FontAsset>(LabelFont);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 35;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; _group.alpha = 0f;

            // The read-out, bottom left: the name at the bottom, the count above it, later lines above that.
            _box = BuildUiKit.Rect("Ammo", canvasGo.transform, Vector2.zero, Vector2.zero, Vector2.zero, _s.corner, new Vector2(Width, 10f));
            BuildUiKit.Panel(_box, PanelColour).raycastTarget = false;
            _top = Pad * 0.5f;
            _name = AddLine("Name", _s.nameSize, false);
            _count = AddLine("Count", _s.countSize, true);
            if (_thrower != null) _grenades = AddLine("Grenades", _thrower.Settings.countSize, false);

            // The crosshair: a white dot in a dark square, in the middle of the screen.
            var centre = new Vector2(0.5f, 0.5f);
            var dark = BuildUiKit.Rect("Crosshair", canvasGo.transform, centre, centre, centre, Vector2.zero, Vector2.one * _s.dotSizes.x);
            BuildUiKit.Panel(dark, DotDark).raycastTarget = false;
            var dot = BuildUiKit.Rect("Dot", dark, centre, centre, centre, Vector2.zero, Vector2.one * _s.dotSizes.y);
            BuildUiKit.Panel(dot, Color.white).raycastTarget = false;
            _dot = dark.gameObject;

            // The side view's reticle (D-0216): four strokes round the aim point, dark behind and white in front, laid out
            // by FitReticle from FlatView's numbers; moved to the aim point every frame it shows.
            _reticle = BuildUiKit.Rect("Reticle2D", canvasGo.transform, Vector2.zero, Vector2.zero, centre, Vector2.zero, Vector2.zero);
            _strokes = new RectTransform[8];
            var startEnd = new Vector2(0f, 0.5f);   // each stroke runs outward from its inner end
            for (int i = 0; i < 4; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, 90f * i);
                _strokes[i * 2] = BuildUiKit.Rect("StrokeDark", _reticle, centre, centre, startEnd, Vector2.zero, Vector2.zero);
                _strokes[i * 2].localRotation = turn;
                BuildUiKit.Panel(_strokes[i * 2], TickDark).raycastTarget = false;
                _strokes[i * 2 + 1] = BuildUiKit.Rect("Stroke", _reticle, centre, centre, startEnd, Vector2.zero, Vector2.zero);
                _strokes[i * 2 + 1].localRotation = turn;
                BuildUiKit.Panel(_strokes[i * 2 + 1], Color.white).raycastTarget = false;
            }
            _reticleFit = new Vector3(-1f, -1f, -1f);   // laid out the first time it shows
            _reticle.gameObject.SetActive(false);

            // The hit marker: four short ticks on the diagonals round the middle, dark behind, the flash's colour in front.
            _marker = BuildUiKit.Rect("HitMarker", canvasGo.transform, centre, centre, centre, Vector2.zero, Vector2.zero);
            _markerGroup = _marker.gameObject.AddComponent<CanvasGroup>();
            _markerGroup.alpha = 0f; _markerGroup.blocksRaycasts = false; _markerGroup.interactable = false;
            float gap = _s.markerTicks.x, length = _s.markerTicks.y, thick = _s.markerTicks.z;
            var nearEnd = new Vector2(0f, 0.5f);   // each tick runs outward from its near end
            _ticks = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + 90f * i;
                var way = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var behind = BuildUiKit.Rect("TickDark", _marker, centre, centre, nearEnd, way * gap, new Vector2(length, thick + 3f));
                behind.localRotation = Quaternion.Euler(0f, 0f, angle);
                BuildUiKit.Panel(behind, TickDark).raycastTarget = false;
                var tick = BuildUiKit.Rect("Tick", _marker, centre, centre, nearEnd, way * gap, new Vector2(length, thick));
                tick.localRotation = Quaternion.Euler(0f, 0f, angle);
                _ticks[i] = BuildUiKit.Panel(tick, _s.hitColour);
                _ticks[i].raycastTarget = false;
            }
        }
    }
}
