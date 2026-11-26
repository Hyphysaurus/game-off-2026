using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The player's shield and health on screen (D-0112): two bars in a corner (PlayerHealthSettings: bottom right by
    /// default; the ammo sits bottom left, the hotbar and charge bottom centre), the shield's above the health's. The
    /// shield bar brightens while it fills back up; a hit flashes the bar it came off; low health pulses. Hidden
    /// outside Play mode, under menus and while driving (the car has its own). Made by PlayerHurt for our own player.
    /// THE PIXEL LOOK (D-0243, PixelKit > Pixel On): one status panel in the bottom right corner, his mockup's: a heart, the
    /// health as a bar of cells and "HP"; under it the shift charge (the player's ShiftCharge: a shift icon, its cells,
    /// "SHIFT"; ShiftChargeHud then draws none of its own). The shield is a thin lilac strip of cells over the health's.
    /// The same flashes and pulse; the cells' colours change only when what they show changes.
    /// </summary>
    public sealed class HealthHud : MonoBehaviour
    {
        private PlayerHurt _hurt;
        private PlayerMotor _motor;
        private PlayerHealthSettings _s;
        private GameObject _root;
        private RectTransform _healthFill, _shieldFill;
        private Image _healthImage, _shieldImage;
        private float _lastHealth, _lastShield, _healthFlash, _shieldFlash;
        // The pixel look (D-0243).
        private bool _pixel;
        private ShiftCharge _charge;
        private PixelKit.StatusRow _healthRow, _shiftRow;
        private RectTransform _status;
        private int _shownMax = -1;
        private static ShiftCharge _drawsShift;   // the charge whose row a pixel status panel draws

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _drawsShift = null;

        /// <summary>True while a pixel status panel draws <paramref name="charge"/>'s shift row: ShiftChargeHud then shows none.</summary>
        public static bool DrawsShift(ShiftCharge charge) => charge != null && _drawsShift == charge;

        public static HealthHud Create(PlayerHurt hurt)
        {
            var hud = new GameObject("HealthHud").AddComponent<HealthHud>();
            if (hurt.GetComponent<NetPlayer>() != null) DontDestroyOnLoad(hud.gameObject);   // an online player outlives scene loads (it was made on the title, before its world): so does its HUD (D-0126)
            hud._hurt = hurt;
            hud._motor = hurt.GetComponent<PlayerMotor>();
            hud._s = PlayerHealthSettings.Load();
            hud.Build();
            return hud;
        }

        private void Build()
        {
            _root = new GameObject("HealthCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
            _lastHealth = _hurt.Health.Health; _lastShield = _hurt.Shield.Current;
            if (PixelKit.On) { BuildPixel(); return; }

            float w = _s.barWidth, h = _s.barHeight, gap = Mathf.Round(h * 0.4f);
            var box = BuildUiKit.Rect("Bars", _root.transform, _s.hudCorner, _s.hudCorner, _s.hudCorner, _s.hudOffset, new Vector2(w, h * 2f + gap));
            bool withShield = _s.shieldMax > 0f;
            _shieldFill = Bar("Shield", box, h + gap, w, h, _s.shieldColor, out _shieldImage);
            if (!withShield) _shieldFill.parent.gameObject.SetActive(false);
            _healthFill = Bar("Health", box, 0f, w, h, _s.healthColor, out _healthImage);
        }

        /// <summary>The pixel status panel (D-0243): the health row (with the shield's strip when there is a shield) and the
        /// shift row when the player carries a shift charge.</summary>
        private void BuildPixel()
        {
            _pixel = true;
            _hurt.TryGetComponent(out _charge);
            int cells = PixelKit.BarCells;
            _status = PixelKit.StatusPanel(_root.transform, _charge != null ? 2 : 1);
            _healthRow = new PixelKit.StatusRow(_status, 0, IconSet.Heart, PixelKit.HealthLabel, PixelKit.Pink, cells);
            if (_s.shieldMax > 0f) _healthRow.AddStrip(cells);
            if (_charge != null)
            {
                _shiftRow = new PixelKit.StatusRow(_status, 1, IconSet.Shift, PixelKit.ShiftLabel, PixelKit.Cyan, cells);
                _drawsShift = _charge;
            }
        }

        private void OnDestroy()
        {
            if (_drawsShift != null && _drawsShift == _charge) _drawsShift = null;
            if (_pixel) PixelKit.StatusWide = PixelKit.StatusTop = 0f;
        }

        /// <summary>One bar: a dark back with the fill on top, its pivot at the left so its width is the share.</summary>
        private static RectTransform Bar(string name, RectTransform box, float y, float w, float h, Color colour, out Image fill)
        {
            var back = BuildUiKit.Rect(name, box, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, y), new Vector2(w, h));
            back.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var rt = BuildUiKit.Rect("Fill", back, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(2f, 2f), new Vector2(w - 4f, h - 4f));
            fill = rt.gameObject.AddComponent<Image>();
            fill.color = colour; fill.raycastTarget = false;
            return rt;
        }

        private void LateUpdate()
        {
            if (_hurt == null) { Destroy(gameObject); return; }   // the player is gone with its scene
            bool show = GameMode.Current == GameMode.Mode.Play && !PauseMenu.IsOpen && !TitleMenu.IsOpen
                        && (_motor == null || _motor.enabled || _hurt.KnockedOut);
            if (_root.activeSelf != show) _root.SetActive(show);
            if (_pixel) Corner(show);
            if (!show) return;

            Damageable health = _hurt.Health; PlayerShield shield = _hurt.Shield;
            float hp = health.Health, sp = shield.Current;
            if (hp < _lastHealth - 0.01f) _healthFlash = 1f;
            if (sp < _lastShield - 0.01f) _shieldFlash = 1f;
            _lastHealth = hp; _lastShield = sp;
            float dt = Time.unscaledDeltaTime;
            _healthFlash = Mathf.Max(0f, _healthFlash - dt * 4f);
            _shieldFlash = Mathf.Max(0f, _shieldFlash - dt * 4f);

            float hpShare = Mathf.Clamp01(hp / Mathf.Max(1f, health.MaxHealth));
            if (_pixel) { ShowPixel(hpShare, hp, shield); return; }

            float w = _s.barWidth - 4f;
            _healthFill.sizeDelta = new Vector2(w * hpShare, _healthFill.sizeDelta.y);
            _shieldFill.sizeDelta = new Vector2(w * shield.Share, _shieldFill.sizeDelta.y);

            Color hc = _s.healthColor;
            if (hpShare <= _s.lowHealthShare && hp > 0f) hc = Color.Lerp(hc, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)));
            _healthImage.color = Color.Lerp(hc, Color.white, _healthFlash);
            Color sc = shield.Refilling ? Color.Lerp(_s.shieldColor, Color.white, 0.25f) : _s.shieldColor;
            _shieldImage.color = Color.Lerp(sc, Color.white, _shieldFlash);
        }

        /// <summary>The pixel panel's cells: health (pulsing when low, flashing on a hit), the shield's strip (brighter while
        /// it fills back up), the shift charge (each charge an equal share of the cells; its icon and label dim at none).</summary>
        private void ShowPixel(float hpShare, float hp, PlayerShield shield)
        {
            Color hc = PixelKit.Pink;
            if (hpShare <= _s.lowHealthShare && hp > 0f) hc = Color.Lerp(hc, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)));
            hc = Color.Lerp(hc, Color.white, _healthFlash);
            PixelKit.Cells bar = _healthRow.Bar;
            bar.Show(PixelKit.Cells.LitFor(hpShare, bar.Count), hc, PixelKit.EmptyCell);
            PixelKit.Cells strip = _healthRow.Strip;
            if (strip != null)
            {
                Color sc = shield.Refilling ? Color.Lerp(PixelKit.Lilac, Color.white, 0.25f) : PixelKit.Lilac;
                strip.Show(PixelKit.Cells.LitFor(shield.Share, strip.Count), Color.Lerp(sc, Color.white, _shieldFlash), PixelKit.EmptyCell);
            }
            if (_shiftRow == null) return;
            if (_charge == null) { _shiftRow.Root.gameObject.SetActive(false); return; }
            int max = _charge.Max, charges = Mathf.Clamp(_charge.Charges, 0, Mathf.Max(0, max));
            int each = max > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)PixelKit.BarCells / max)) : 1;
            if (max != _shownMax) { _shownMax = max; _shiftRow.Bar.Resize(Mathf.Max(1, max * each)); }
            _shiftRow.Bar.Show(charges * each, PixelKit.Cyan, PixelKit.EmptyCell);
            bool any = charges > 0;
            Color label = any ? PixelKit.Cyan : PixelKit.QuietText;
            if (_shiftRow.Label.color != label) _shiftRow.Label.color = label;
            Color icon = new Color(1f, 1f, 1f, any ? 1f : 0.55f);
            if (_shiftRow.Icon.color != icon) _shiftRow.Icon.color = icon;
        }

        /// <summary>Tells the kit where the panel is while it shows (the key prompt footer keeps clear of it).</summary>
        private void Corner(bool show)
        {
            float wide = show ? PixelKit.StatusWidth + PixelKit.Edge : 0f;
            float top = show ? PixelKit.Edge + _status.sizeDelta.y : 0f;
            if (PixelKit.StatusWide != wide) PixelKit.StatusWide = wide;
            if (PixelKit.StatusTop != top) PixelKit.StatusTop = top;
        }
    }
}
