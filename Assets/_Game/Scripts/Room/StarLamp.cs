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
    /// THE STUN SWEEP (D-0241, his pick of the lamp's new job): each time a light fills its LightTarget (the wake too), its
    /// side star spots tilt down and sweep across the floor for Sweep Seconds, turning fast, and every cave monster a spot
    /// crosses (in its cone, within its reach, nothing solid between the dome and the monster) is stunned DIZZY
    /// (DarkMonster.Stun), as a beam does. Then the lamp rests Rest Seconds (its dome steady) before a light can start
    /// another (its dome breathes again: ready). The spots cast shadows only during a sweep, so the furniture keeps the
    /// light off what the stun cannot reach either (the bed's frame over the cave). The wake still earns the bedroom's
    /// light-bedroom star and saves the lamp awake; a sweep is never saved. Sweep On unticked: the D-0218 lamp.
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

        [Header("The stun sweep (D-0241)")]
        [SerializeField, Tooltip("D-0241: each time a light fills its LightTarget (the wake too), the side star spots tilt down and sweep the floor for Sweep Seconds, and any cave monster a spot crosses is stunned DIZZY. Then it rests Rest Seconds before a light can start another. Off: the D-0218 lamp (woken once, then it stays awake and wants no light).")]
        private bool sweepOn = true;
        [SerializeField, Min(0.1f), Tooltip("Seconds one sweep lasts (from the tilt down starting).")] private float sweepSeconds = 5f;
        [SerializeField, Range(0f, 80f), Tooltip("Degrees under the level each side spot aims during a sweep (as placed they look 48 over it, onto the walls and ceiling). The straight-up spot keeps its place.")]
        private float sweepPitch = 40f;
        [SerializeField, Min(0.05f), Tooltip("Seconds the spots take to tilt down at a sweep's start, and back up at its end.")] private float sweepTilt = 0.6f;
        [SerializeField, Tooltip("Degrees a second the field of stars turns at the height of a sweep (eased from Spin Speed and back).")] private float sweepSpin = 70f;
        [SerializeField, Min(0f), Tooltip("Seconds it rests after a sweep before a light can start another: its dome glows steady meanwhile, and breathes again once it is ready.")]
        private float restSeconds = 8f;
        [SerializeField, Tooltip("The spots cast soft shadows during a sweep only, so the furniture keeps their light off the cave under the bed (the bed's frame over it; with Sweep Through Furniture the stun reaches it all the same). Off: the light goes through the furniture (only the look: the stun asks the real line either way).")]
        private bool sweepShadows = true;
        [SerializeField, Min(0.02f), Tooltip("Seconds between looks for monsters in the spots during a sweep (rays: not every frame).")] private float sweepCheckEvery = 0.1f;
        [SerializeField, Tooltip("D-0245 (his call): the sweep stuns a cave monster in a spot's cone and reach even with the bed or a table between them. With nothing allowed between, it reached none of them: the bedside table and the bed's frame were always in the way. Off = only with nothing solid between.")] private bool sweepThroughFurniture = true;
        [SerializeField, Min(0f), Tooltip("The dome's glow during a sweep, times its awake glow.")] private float sweepGlow = 1.6f;
        [SerializeField, Tooltip("Played as each sweep after the first starts (the wake's own sound plays for the first). No clip: silent.")]
        private CourseSounds.Cue sweepSound = new CourseSounds.Cue(null, 0.5f);

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private float[] _starsOwn;
        private float _glowOwn, _share = -1f, _shown = -1f;
        private bool _bound;
        // The sweep: each star's turn as placed and whether it tilts (a side star) toward which heading; the tilt now.
        private Quaternion[] _restTurn;
        private float[] _heading;
        private bool[] _tilts;
        private LightShadows[] _restShadows;
        private float _tilt, _sweepUntil, _readyAt, _nextCheck;
        private bool _sweeping, _resting, _shadowsOn;

        /// <summary>True once it is awake (by a light, or from the save).</summary>
        public bool Woken { get; private set; }
        /// <summary>The save key (the maker reports it).</summary>
        public string Key => key;
        /// <summary>D-0241: a stun sweep is under way.</summary>
        public bool Sweeping => _sweeping;
        /// <summary>D-0241: the sweep's numbers (the bedroom gate's maker reports what the spots reach).</summary>
        public bool SweepOn => sweepOn;
        public float SweepPitch => sweepPitch;
        /// <summary>D-0241: the star spots (the maker measures their reach).</summary>
        public System.Collections.Generic.IReadOnlyList<Light> Stars => stars ?? System.Array.Empty<Light>();

        private void Awake()
        {
            _starsOwn = new float[stars != null ? stars.Length : 0];
            for (int i = 0; i < _starsOwn.Length; i++) if (stars[i] != null) _starsOwn[i] = stars[i].intensity;
            if (glow != null) _glowOwn = glow.intensity;

            // D-0241: each star's turn as placed (in the spinner's space, whose up is the world's); a side star sweeps to
            // look Sweep Pitch under the level on its own heading, the straight-up one stays.
            int n = _starsOwn.Length;
            _restTurn = new Quaternion[n]; _heading = new float[n]; _tilts = new bool[n]; _restShadows = new LightShadows[n];
            for (int i = 0; i < n; i++)
            {
                if (stars[i] == null) continue;
                _restTurn[i] = stars[i].transform.localRotation;
                _restShadows[i] = stars[i].shadows;
                Vector3 f = _restTurn[i] * Vector3.forward;
                _tilts[i] = Mathf.Abs(f.y) < 0.9f && f.x * f.x + f.z * f.z > 1e-4f;
                _heading[i] = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            }
        }

        private void OnEnable()
        {
            if (_bound) return;
            SaveGame.Changed += Read;
            if (target != null) target.OnFilled.AddListener(OnLit);
            _bound = true;
        }

        private void OnDisable()
        {
            if (!_bound) return;
            SaveGame.Changed -= Read;
            if (target != null) target.OnFilled.RemoveListener(OnLit);
            _bound = false;
            StopSweep();   // D-0241: the spots back as placed, no shadows
            if (zone != null) zone.Soften(lampsTo, ambientTo, 0f);   // switched off: the room is as dark as without it
            _shown = -1f;
        }

        /// <summary>A light filled its LightTarget: the wake the first time, a stun sweep after that (D-0241).</summary>
        private void OnLit()
        {
            if (!Woken) Wake();
            else StartSweep(true);
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
            StartSweep(false);   // D-0241: the first sweep comes with the wake (its sound is the wake's)
        }

        /// <summary>D-0241: starts a stun sweep now (a light filled its LightTarget). False while asleep, sweeping or resting,
        /// or with Sweep On unticked.</summary>
        public bool StartSweep(bool withSound)
        {
            if (!sweepOn || !Woken || _sweeping || _resting) return false;
            _sweeping = true;
            _sweepUntil = Time.time + sweepSeconds;
            _nextCheck = Time.time;
            if (target != null) target.enabled = false;   // no new fill while it sweeps and rests
            Shadows(true);
            if (withSound) UiAudio.Play(sweepSound);
            Playtest.PlayLog.Tell("star lamp: stun sweep");
            return true;
        }

        /// <summary>Awake or asleep at once (a Continue, a New Game): no rise, no sound, no sweep under way.</summary>
        private void SetAwake(bool awake)
        {
            Woken = awake;
            _share = awake ? 1f : 0f;
            StopSweep();
            if (target != null)
            {
                target.enabled = !awake || sweepOn;   // D-0241: awake with the sweep on, a light starts a sweep
                if (target.enabled) target.Clear();   // the light must fill it again
            }
        }

        /// <summary>D-0241: no sweep, no rest, the spots as placed and without shadows.</summary>
        private void StopSweep()
        {
            _sweeping = _resting = false;
            if (_tilt != 0f) { _tilt = 0f; Tilt(0f); }
            Shadows(false);
        }

        /// <summary>D-0241, once a frame: the sweep's clock, the spots' tilt, the looks for monsters, the rest's end.</summary>
        private void TickSweep(float dt)
        {
            if (!sweepOn || _restTurn == null) return;
            float now = Time.time;
            if (_sweeping && now >= _sweepUntil) { _sweeping = false; _resting = true; _readyAt = now + restSeconds; }
            float want = _sweeping ? 1f : 0f;
            if (_tilt != want)
            {
                _tilt = Mathf.MoveTowards(_tilt, want, dt / sweepTilt);
                Tilt(Mathf.SmoothStep(0f, 1f, _tilt));
                if (_tilt <= 0f) Shadows(false);
            }
            if (_sweeping && _tilt >= 0.5f && now >= _nextCheck) { _nextCheck = now + sweepCheckEvery; StunCrossed(); }
            if (_resting && _tilt <= 0f && now >= _readyAt)
            {
                _resting = false;
                if (target != null && Woken) { target.Clear(); target.enabled = true; }   // ready: a light starts the next
            }
        }

        private void Tilt(float k)
        {
            for (int i = 0; i < _restTurn.Length; i++)
            {
                if (!_tilts[i] || stars[i] == null) continue;
                stars[i].transform.localRotation = Quaternion.Slerp(_restTurn[i], Quaternion.Euler(sweepPitch, _heading[i], 0f), k);
            }
        }

        private void Shadows(bool on)
        {
            on &= sweepShadows;
            if (on == _shadowsOn || _restShadows == null) return;
            _shadowsOn = on;
            for (int i = 0; i < _restShadows.Length; i++)
                if (stars[i] != null) stars[i].shadows = on ? LightShadows.Soft : _restShadows[i];
        }

        /// <summary>Every cave monster a spot crosses now (its middle or the top of its body in the spot's cone, within its
        /// reach, nothing solid between unless Sweep Through Furniture) is stunned. Rays only after the cone's own test; no list is made.</summary>
        private void StunCrossed()
        {
            for (int m = 0; m < DarkMonster.RoamCount; m++)
            {
                DarkMonster monster = DarkMonster.RoamAt(m);
                if (monster == null || monster.Down || monster.Dizzy || monster.Borrowed) continue;
                Vector3 middle = monster.BodyMiddle;
                Vector3 top = middle + Vector3.up * (monster.BodyTall * 0.35f);
                for (int i = 0; i < _restTurn.Length; i++)
                {
                    Light spot = stars[i];
                    if (spot == null || !spot.enabled || spot.type != LightType.Spot) continue;
                    if (Crosses(spot, middle, monster.transform) || Crosses(spot, top, monster.transform)) { monster.Stun(); break; }
                }
            }
        }

        private bool Crosses(Light spot, Vector3 point, Transform monster)
        {
            Vector3 origin = spot.transform.position;
            Vector3 to = point - origin;
            float distance = to.magnitude;
            if (distance < 1e-4f || distance > spot.range) return false;
            if (Vector3.Angle(spot.transform.forward, to) > spot.spotAngle * 0.5f) return false;
            if (sweepThroughFurniture) return true;   // D-0245: the bed and the table do not shield them
            int count = Physics.RaycastNonAlloc(origin, to / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = Hits[i].transform;
                if (t.IsChildOf(monster) || t.IsChildOf(transform)) continue;   // the monster itself, the lamp itself
                return false;
            }
            return true;
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
            TickSweep(Time.deltaTime);   // D-0241
            float spin = Mathf.Lerp(spinSpeed, sweepSpin, Mathf.SmoothStep(0f, 1f, _tilt));   // D-0241: fast while it sweeps
            if (on && spinner != null) spinner.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);

            // Its own glow: breathing while asleep, steady and brighter awake. D-0241 with the sweep on: brighter while it
            // sweeps, steady while it rests, breathing again at its awake brightness once a light can start another.
            if (glow != null)
            {
                float breath = 1f - breathDepth * 0.5f * (1f + Mathf.Sin(Time.time * breathSpeed * 2f * Mathf.PI));
                float awake = glowAwake;
                if (sweepOn && Woken) awake = _sweeping ? glowAwake * sweepGlow : _resting ? glowAwake : glowAwake * breath;
                glow.intensity = _glowOwn * Mathf.Lerp(breath, awake, shown);
                if (!glow.enabled) glow.enabled = true;
            }
        }
    }
}
