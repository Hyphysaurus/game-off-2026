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
    /// Later lines (a grenade count) stack above the count with AddLine.
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

        /// <summary>What the count shows now ("12 / 48", "RELOADING").</summary>
        public string CountText => _count.text;
        /// <summary>What the name line shows now ("Dart Pistol  |  Paint").</summary>
        public string NameText => _name.text;
        /// <summary>The count's colour now.</summary>
        public Color CountColour => _count.color;
        /// <summary>True while the read-out is on screen.</summary>
        public bool Showing => _group.alpha > 0f;
        /// <summary>True while the dot crosshair is on screen.</summary>
        public bool DotShowing => Showing && _dot.activeSelf;
        /// <summary>True while the hit marker's flash is on screen.</summary>
        public bool MarkerShowing => Showing && _markerGroup.alpha > 0f;
        /// <summary>The colour of the hit marker's last flash.</summary>
        public Color MarkerColour => _ticks[0].color;

        public static WeaponHud Create(WeaponHolder holder)
        {
            var go = new GameObject("WeaponHud");
            var hud = go.AddComponent<WeaponHud>();
            hud._holder = holder;
            hud._s = WeaponAmmoSettings.Load();
            holder.TryGetComponent(out hud._belt);
            if (hud._belt != null) hud._belt.Changed += hud.OnBeltChanged;
            hud.Build();
            hud.Refresh();
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

        private void OnEnable() { GameEvents.AmmoChanged += OnAmmoChanged; GameEvents.ShotLanded += OnShotLanded; }
        private void OnDisable() { GameEvents.AmmoChanged -= OnAmmoChanged; GameEvents.ShotLanded -= OnShotLanded; }
        private void OnDestroy() { if (_belt != null) _belt.Changed -= OnBeltChanged; }

        private void OnAmmoChanged(WeaponHolder who) { if (who == _holder) _stale = true; }
        private void OnBeltChanged(AmmoBelt belt) => _stale = true;
        private void OnShotLanded(WeaponHolder who, bool finished) { if (who == _holder) Flash(finished); }

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
            bool show = _holder.isActiveAndEnabled && _holder.Armed && _holder.Current is BlasterWeapon
                        && GameMode.Current != GameMode.Mode.Build && !PauseMenu.IsOpen && !TitleMenu.IsOpen && !KeyHelp.IsOpen && !DebugConsole.IsOpen;
            _group.alpha = show ? 1f : 0f;
            _dot.SetActive(show && !_holder.FirstPerson);
            if (show && _stale) Refresh();
            // The hit marker: big and solid as the shot lands, back to its size and gone over the flash.
            float flash = (Time.time - _flashAt) / _flashSeconds;
            if (flash >= 1f) { if (_markerGroup.alpha > 0f) _markerGroup.alpha = 0f; }
            else { _markerGroup.alpha = 1f - flash; _marker.localScale = Vector3.one * Mathf.Lerp(_flashScale, 1f, flash); }
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
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false; _group.alpha = 0f;

            // The read-out, bottom left: the name at the bottom, the count above it, later lines above that.
            _box = BuildUiKit.Rect("Ammo", canvasGo.transform, Vector2.zero, Vector2.zero, Vector2.zero, _s.corner, new Vector2(Width, 10f));
            BuildUiKit.Panel(_box, PanelColour).raycastTarget = false;
            _top = Pad * 0.5f;
            _name = AddLine("Name", _s.nameSize, false);
            _count = AddLine("Count", _s.countSize, true);

            // The crosshair: a white dot in a dark square, in the middle of the screen.
            var centre = new Vector2(0.5f, 0.5f);
            var dark = BuildUiKit.Rect("Crosshair", canvasGo.transform, centre, centre, centre, Vector2.zero, Vector2.one * _s.dotSizes.x);
            BuildUiKit.Panel(dark, DotDark).raycastTarget = false;
            var dot = BuildUiKit.Rect("Dot", dark, centre, centre, centre, Vector2.zero, Vector2.one * _s.dotSizes.y);
            BuildUiKit.Panel(dot, Color.white).raycastTarget = false;
            _dot = dark.gameObject;

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
