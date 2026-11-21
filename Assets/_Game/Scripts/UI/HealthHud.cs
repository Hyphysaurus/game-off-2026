using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The player's shield and health on screen (D-0112): two bars in a corner (PlayerHealthSettings: bottom right by
    /// default; the ammo sits bottom left, the hotbar and charge bottom centre), the shield's above the health's. The
    /// shield bar brightens while it fills back up; a hit flashes the bar it came off; low health pulses. Hidden
    /// outside Play mode, under menus and while driving (the car has its own). Made by PlayerHurt for our own player.
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

        public static HealthHud Create(PlayerHurt hurt)
        {
            var hud = new GameObject("HealthHud").AddComponent<HealthHud>();
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
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;

            float w = _s.barWidth, h = _s.barHeight, gap = Mathf.Round(h * 0.4f);
            var box = BuildUiKit.Rect("Bars", _root.transform, _s.hudCorner, _s.hudCorner, _s.hudCorner, _s.hudOffset, new Vector2(w, h * 2f + gap));
            bool withShield = _s.shieldMax > 0f;
            _shieldFill = Bar("Shield", box, h + gap, w, h, _s.shieldColor, out _shieldImage);
            if (!withShield) _shieldFill.parent.gameObject.SetActive(false);
            _healthFill = Bar("Health", box, 0f, w, h, _s.healthColor, out _healthImage);
            _lastHealth = _hurt.Health.Health; _lastShield = _hurt.Shield.Current;
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
            if (!show) return;

            Damageable health = _hurt.Health; PlayerShield shield = _hurt.Shield;
            float hp = health.Health, sp = shield.Current;
            if (hp < _lastHealth - 0.01f) _healthFlash = 1f;
            if (sp < _lastShield - 0.01f) _shieldFlash = 1f;
            _lastHealth = hp; _lastShield = sp;
            float dt = Time.unscaledDeltaTime;
            _healthFlash = Mathf.Max(0f, _healthFlash - dt * 4f);
            _shieldFlash = Mathf.Max(0f, _shieldFlash - dt * 4f);

            float w = _s.barWidth - 4f;
            float hpShare = Mathf.Clamp01(hp / Mathf.Max(1f, health.MaxHealth));
            _healthFill.sizeDelta = new Vector2(w * hpShare, _healthFill.sizeDelta.y);
            _shieldFill.sizeDelta = new Vector2(w * shield.Share, _shieldFill.sizeDelta.y);

            Color hc = _s.healthColor;
            if (hpShare <= _s.lowHealthShare && hp > 0f) hc = Color.Lerp(hc, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)));
            _healthImage.color = Color.Lerp(hc, Color.white, _healthFlash);
            Color sc = shield.Refilling ? Color.Lerp(_s.shieldColor, Color.white, 0.25f) : _s.shieldColor;
            _shieldImage.color = Color.Lerp(sc, Color.white, _shieldFlash);
        }
    }
}
