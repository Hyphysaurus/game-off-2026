using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A room that starts dark until its light switch is pressed (D-0214, the bedroom). While dark, its Dark Zone holds the
    /// room's lamps down and a faint soft light rides the player so the toy stays readable (made here at run time; nothing on
    /// the player changes), and the Dark Only lights (the switch's own small glow) show the way. The switch (a kid's
    /// height: out of a toy's reach) turns the lights on for good: the lamps and the ambient rise to their own over Rise
    /// seconds (as the playroom reveal raises its lights), the zone and the player's glow go. The sound is the switch's own
    /// press cue (CourseSounds Switch Press, played for every switch).
    /// Remembered per save slot (SaveGame's seen list, Key): on Continue and after a respawn the room is lit; a New Game
    /// (the slot cleared) makes it dark again, also in a scene that is already loaded (SaveGame.Changed).
    /// The lights are a reward, not a lock: nothing waits on them.
    /// Online: switches are not networked, so each player's lights are their own.
    /// </summary>
    public sealed class RoomLights : MonoBehaviour
    {
        [SerializeField, Tooltip("Remembered per save slot under this name (SaveGame seen list).")] private string key = "bedroom:lights";
        [SerializeField, Tooltip("The room's Dark Zone: released when the lights come on.")] private DarkZone zone;
        [SerializeField, Tooltip("The switch that turns the lights on (its own prompt, its own press sound).")] private Switch lightSwitch;
        [SerializeField, Min(0f), Tooltip("Seconds the room's lamps and ambient take to rise to their own brightness (eased). 0 = at once.")] private float riseSeconds = 2.5f;
        [SerializeField, Tooltip("Lights that show only while the room is dark (the switch's own glow): at their own brightness times how dark the room is, off once the lights are on. Put a KeepLit on them so no zone dims them.")] private Light[] darkOnly;

        [Header("The player's glow while dark")]
        [SerializeField, Tooltip("A faint soft light rides the player while the room is dark (the toy stays readable). Off = none.")] private bool playerGlow = true;
        [SerializeField, Min(0f), Tooltip("Its brightness at toy size; it grows with the square of the player's size, so it lights the same patch round them at either size (as a flashlight's beam does).")] private float glowIntensity = 1.2f;
        [SerializeField, Min(0.1f), Tooltip("Its reach at toy size (toy units); it grows with the player's size.")] private float glowRange = 6f;
        [SerializeField, Range(0f, 1f), Tooltip("Its brightness at kid size, as a share of the toy's (grown with the size): grown in full, a kid's patch of light is half the room and the dark is gone; the toy is the one that must stay readable.")] private float kidGlow = 0.35f;
        [SerializeField, Tooltip("Where it rides, in the player's own space (it scales with them): over and behind a 1.8-tall toy, between it and the camera, so the side the camera sees is lit.")] private Vector3 glowAt = new Vector3(0f, 2.2f, -1.5f);
        [SerializeField, Tooltip("Its colour: a soft warm white.")] private Color glowColour = new Color(1f, 0.88f, 0.74f);
        [SerializeField, Min(0.1f), Tooltip("Seconds between looks for the player until one is found.")] private float lookEvery = 1f;

        [Header("Events")]
        [SerializeField, Tooltip("When the lights come on by the switch (not on a lit Continue): a Feel sting, a lamp's own sound.")] private UnityEvent onLightsOn;

        private float[] _darkOnlyOwn;
        private Shifter _player;
        private Light _glow;
        private float _lookAgain, _glowScale = -1f;
        private bool _bound;

        /// <summary>True once the lights are on (by the switch, or from the save).</summary>
        public bool On { get; private set; }
        /// <summary>The save key (the maker reports it).</summary>
        public string Key => key;

        private void Awake()
        {
            _darkOnlyOwn = new float[darkOnly != null ? darkOnly.Length : 0];
            for (int i = 0; i < _darkOnlyOwn.Length; i++) if (darkOnly[i] != null) _darkOnlyOwn[i] = darkOnly[i].intensity;
        }

        private void OnEnable()
        {
            if (_bound) return;
            GameEvents.SwitchPressed += OnSwitchPressed;
            SaveGame.Changed += Read;
            _bound = true;
        }

        private void OnDisable()
        {
            if (!_bound) return;
            GameEvents.SwitchPressed -= OnSwitchPressed;
            SaveGame.Changed -= Read;
            _bound = false;
        }

        private void Start() => Read();

        /// <summary>What the active slot says: lit if it has seen the lights on, dark if not. At once either way.</summary>
        private void Read()
        {
            bool seen = !string.IsNullOrEmpty(key) && SaveGame.Seen(SaveGame.Slot, key);
            if (seen && !On) SetOn(0f);
            else if (!seen && On) SetDark();
        }

        private void OnSwitchPressed(Switch pressed)
        {
            if (pressed == null || pressed != lightSwitch || On) return;
            SetOn(riseSeconds);   // first: MarkSeen raises SaveGame.Changed, and Read must find the lights already on
            if (!string.IsNullOrEmpty(key)) SaveGame.MarkSeen(SaveGame.Slot, key);
            onLightsOn?.Invoke();
        }

        private void SetOn(float seconds)
        {
            On = true;
            if (zone != null) zone.Release(seconds);
            if (lightSwitch != null) lightSwitch.SetUsed(true);   // reads Done (pressed already when it was the hand)
        }

        private void SetDark()
        {
            On = false;
            if (zone != null) zone.Restore();
            if (lightSwitch != null) lightSwitch.SetUsed(false);
        }

        private void Update()
        {
            // How dark the room is where the player looks: 0 outside it, rising to 1 inside, and falling with the lamps as
            // they rise (the zone is off once they are up).
            float dark = zone != null && zone.isActiveAndEnabled ? zone.Darkness : 0f;
            for (int i = 0; i < _darkOnlyOwn.Length; i++)
            {
                Light l = darkOnly[i];
                if (l == null) continue;
                bool show = dark > 0f;
                if (l.enabled != show) l.enabled = show;
                if (show) l.intensity = _darkOnlyOwn[i] * dark;
            }
            Glow(dark);
        }

        /// <summary>The faint light that rides the player while the room is dark (made the first time it is needed, on this
        /// PC's own player: the one named Player, as the prompt HUD finds it).</summary>
        private void Glow(float dark)
        {
            if (!playerGlow) return;
            if (dark <= 0f)
            {
                if (_glow != null && _glow.enabled) _glow.enabled = false;
                return;
            }
            if (_player == null)
            {
                if (Time.unscaledTime < _lookAgain) return;
                _lookAgain = Time.unscaledTime + lookEvery;
                GameObject own = GameObject.Find("Player");
                if (own == null || !own.TryGetComponent(out _player)) return;
                _glow = null;
            }
            if (_glow == null)
            {
                var go = new GameObject("DarkRoomGlow");
                go.transform.SetParent(_player.transform, false);
                go.transform.localPosition = glowAt;
                go.AddComponent<KeepLit>();   // no Dark Zone ever dims it
                _glow = go.AddComponent<Light>();
                _glow.type = LightType.Point;
                _glow.color = glowColour;
                _glow.shadows = LightShadows.None;
                _glowScale = -1f;
            }
            float toy = Mathf.Max(0.01f, _player.ScaleOf(ShiftSize.Toy));
            float scale = Mathf.Max(0.01f, _player.Scale / toy);
            if (!Mathf.Approximately(scale, _glowScale)) { _glowScale = scale; _glow.range = glowRange * scale; }
            float towardKid = Mathf.InverseLerp(1f, Mathf.Max(1.01f, _player.ScaleOf(ShiftSize.Kid) / toy), scale);
            _glow.intensity = glowIntensity * scale * scale * Mathf.Lerp(1f, kidGlow, towardKid) * dark;
            if (!_glow.enabled) _glow.enabled = true;
        }

        private void OnDestroy()
        {
            if (_glow != null) Destroy(_glow.gameObject);
        }
    }
}
