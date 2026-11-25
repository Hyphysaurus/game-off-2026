using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The bedroom's star lamp (D-0218, his idea: "the interactive star lamp", woken by the flashlight). Asleep, its dome
    /// breathes a faint glow so it can be found in the dark room. A flashlight's beam on it (its LightTarget: any light,
    /// about half a second) wakes it: a slowly turning field of stars comes up over the walls and ceiling (spot lights
    /// wearing the pack's star picture as a cookie, turned by the Spinner), the dome glows steady, and the room's Dark
    /// Zone is lifted part-way to a soft starlight (DarkZone.Soften: between the dark and the lamps). The kid's wall switch
    /// still brings the full lamps (RoomLights); once they are on, the projected stars wash out to In Lit Room.
    /// Remembered per save slot (SaveGame's seen list, Key): Continue and a respawn find it awake; a New Game (the slot
    /// cleared) puts it to sleep again, also in a scene that is already loaded (SaveGame.Changed).
    /// Its lights must sit under a KeepLit (the maker puts one on the projector) so no Dark Zone dims them.
    /// Online: the lamp is not networked, so each player's lamp is their own.
    /// OFF SWITCH: untick this script and the lamp stays as placed (its lights as the maker left them: off).
    /// </summary>
    public sealed class StarLamp : MonoBehaviour
    {
        [SerializeField, Tooltip("Remembered per save slot under this name (SaveGame seen list).")] private string key = "bedroom:stars";
        [SerializeField, Tooltip("What wakes it: the LightTarget on the lamp (its On Filled is listened to here; nothing to wire). Switched off while the lamp is awake, back on and emptied when it sleeps again (New Game).")] private LightTarget target;
        [SerializeField, Tooltip("The room's Dark Zone: awake, its dark is lifted to Lamps To and Ambient To below. Empty: the stars only.")] private DarkZone zone;

        [Header("The stars")]
        [SerializeField, Tooltip("The projector's spot lights, each wearing the star cookie. The brightness each has as placed is its awake brightness; they are off while the lamp sleeps.")] private Light[] stars;
        [SerializeField, Tooltip("What turns (the stars' holder), about the world's up.")] private Transform spinner;
        [SerializeField, Tooltip("Degrees a second the field of stars turns (a real projector turns about once a minute: 6).")] private float spinSpeed = 4f;
        [SerializeField, Min(0f), Tooltip("Seconds the stars and the starlight take to come up when it wakes. 0 = at once.")] private float wakeSeconds = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of their brightness the stars keep once the room is lit (the wall switch, or the view outside the room): a projector washes out in a lit room.")] private float inLitRoom = 0.35f;

        [Header("The starlight (the Dark Zone, lifted part-way)")]
        [SerializeField, Range(0f, 1f), Tooltip("Awake, the room's lamps dim no lower than this share of their own (the dark room's is 0.08, the switch's lights-on 1).")] private float lampsTo = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("Awake, the ambient dims no lower than this share (the dark room's is 0.14, lights-on 1).")] private float ambientTo = 0.42f;

        [Header("Its own glow")]
        [SerializeField, Tooltip("A small light at the dome. Its brightness as placed is the sleeping glow (breathing, so it is found in the dark); awake it glows steady at Glow Awake times that. Empty: none.")] private Light glow;
        [SerializeField, Min(0f), Tooltip("The awake glow, times the sleeping one.")] private float glowAwake = 3f;
        [SerializeField, Min(0f), Tooltip("Breaths a second while asleep.")] private float breathSpeed = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("How far each breath dims the sleeping glow (0 = steady).")] private float breathDepth = 0.5f;

        [Header("Moments")]
        [SerializeField, Tooltip("Played once when the flashlight wakes it (not on a Continue). No clip: silent.")] private CourseSounds.Cue wakeSound = new CourseSounds.Cue(null, 0.6f);
        [SerializeField, Tooltip("Raised when the flashlight wakes it (not on a Continue).")] private UnityEvent onWoken;

        private float[] _starsOwn;
        private float _glowOwn, _share = -1f, _shown = -1f;
        private bool _bound;

        /// <summary>True once it is awake (by a light, or from the save).</summary>
        public bool Woken { get; private set; }
        /// <summary>The save key (the maker reports it).</summary>
        public string Key => key;

        private void Awake()
        {
            _starsOwn = new float[stars != null ? stars.Length : 0];
            for (int i = 0; i < _starsOwn.Length; i++) if (stars[i] != null) _starsOwn[i] = stars[i].intensity;
            if (glow != null) _glowOwn = glow.intensity;
        }

        private void OnEnable()
        {
            if (_bound) return;
            SaveGame.Changed += Read;
            if (target != null) target.OnFilled.AddListener(Wake);
            _bound = true;
        }

        private void OnDisable()
        {
            if (!_bound) return;
            SaveGame.Changed -= Read;
            if (target != null) target.OnFilled.RemoveListener(Wake);
            _bound = false;
            if (zone != null) zone.Soften(lampsTo, ambientTo, 0f);   // switched off: the room is as dark as without it
            _shown = -1f;
        }

        private void Start() => Read();

        /// <summary>What the active slot says: awake if it has been woken, asleep if not. At once either way.</summary>
        private void Read()
        {
            bool seen = !string.IsNullOrEmpty(key) && SaveGame.Seen(SaveGame.Slot, key);
            if (seen != Woken || _share < 0f) SetAwake(seen);
        }

        /// <summary>The flashlight filled its LightTarget: the stars come up over Wake Seconds and the slot remembers it.</summary>
        public void Wake()
        {
            if (Woken) return;
            Woken = true;
            if (target != null) target.enabled = false;
            if (!string.IsNullOrEmpty(key)) SaveGame.MarkSeen(SaveGame.Slot, key);   // after Woken: MarkSeen raises Changed, and Read must find it awake
            UiAudio.Play(wakeSound);
            onWoken?.Invoke();
        }

        /// <summary>Awake or asleep at once (a Continue, a New Game): no rise, no sound.</summary>
        private void SetAwake(bool awake)
        {
            Woken = awake;
            _share = awake ? 1f : 0f;
            if (target != null)
            {
                target.enabled = !awake;
                if (!awake) target.Clear();   // a New Game: the light must fill it again
            }
        }

        private void Update()
        {
            if (_share < 0f) return;   // not read yet (Start)
            float want = Woken ? 1f : 0f;
            if (_share != want) _share = wakeSeconds > 0f ? Mathf.MoveTowards(_share, want, Time.deltaTime / wakeSeconds) : want;
            float shown = Mathf.SmoothStep(0f, 1f, _share);
            if (shown != _shown)
            {
                _shown = shown;
                if (zone != null) zone.Soften(lampsTo, ambientTo, shown);
            }

            // The stars: as bright as they were placed, times the rise, washed out once the room is lit.
            float dark = zone != null && zone.isActiveAndEnabled ? zone.Darkness : 0f;
            float starShare = shown * Mathf.Lerp(inLitRoom, 1f, dark);
            bool on = starShare > 0f;
            for (int i = 0; i < _starsOwn.Length; i++)
            {
                Light l = stars[i];
                if (l == null) continue;
                if (l.enabled != on) l.enabled = on;
                if (on) l.intensity = _starsOwn[i] * starShare;
            }
            if (on && spinner != null) spinner.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            // Its own glow: breathing while asleep, steady and brighter awake.
            if (glow != null)
            {
                float breath = 1f - breathDepth * 0.5f * (1f + Mathf.Sin(Time.time * breathSpeed * 2f * Mathf.PI));
                glow.intensity = _glowOwn * Mathf.Lerp(breath, glowAwake, shown);
                if (!glow.enabled) glow.enabled = true;
            }
        }
    }
}
