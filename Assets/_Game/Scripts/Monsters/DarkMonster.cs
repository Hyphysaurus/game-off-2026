using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A monster that comes alive in the dark (D-0222, his "monsters under the bed", made readable, alive and beatable in
    /// D-0228): slimes and a spiked shell that live in the cave under the bed, met before the toy has the flashlight.
    /// - The light rule: while the wall switch's lights are on (RoomLights.On) it is an ordinary toy: its Animator settles
    ///   into a rest pose and stops, its eyes and glow are out, it does not move or bump. A hit TIPS IT OVER like a toy
    ///   (no health lost; it lies a few seconds and rights itself). In the star lamp's starlight (StarLamp.Woken, the
    ///   switch off) it is SLEEPY: awake and visible, slower, noticing the toy only nearer. A lit Continue finds it frozen
    ///   on the first frame.
    /// - In the dark it wanders a little round its spot, eyes glinting, a soft glow behind it so its shape reads.
    /// - It spots a toy-sized local player it can see within Sight: it stops and stares (its sense state), a "!" pops over
    ///   its head, it squeaks, its eyes and glow turn red; then it chases (never past its Leash, never out of the cave, never
    ///   near the flashlight; it may cross the way from the cave's mouth to the flashlight, but never stands, waits,
    ///   wanders or hides in it). In reach it winds up (its attack clip held a moment: the tell, time to step away), then
    ///   bumps: the toy is pushed away and a little up if still in reach (Knockback: the motor's Knock, or the first-person
    ///   body), no damage, no charge lost; it backs off and waits its cooldown. All the monsters together bump no more
    ///   than Bumps At Most in any Bump Window seconds.
    /// - A flashlight's beam on its middle (LightQuery): it flinches (a squeak), runs toward the back of the cave and
    ///   cowers there while lit, home Return After seconds after the light has gone. A second of beam on it (added up)
    ///   makes it DIZZY for a few seconds (its dizzy clip, green eyes): any one hit knocks it down. A kid-sized player near
    ///   sends it running the same way (the kid cannot fit under the bed).
    /// - Knocked down through its Damageable (darts, punches, kicks: two or three hits, one while dizzy): its die clip, the
    ///   soldiers' burst and thud and a squeak, and it STAYS down for the rest of the visit (a reload, a New Game or the
    ///   console's "bedroom reset" brings it back). All of them down: GameEvents.MonstersAllDown (the gold star is an
    ///   ObjectiveHook on each one's On Died, wired by the maker).
    /// Stay Put (D-0223, the claw machine's prizes): only the light rule: idling with its eye glint in the dark and the
    /// starlight, frozen in the wall switch's light; it never looks, moves, bumps or takes a hit.
    /// Online: nothing is networked. Each PC's monsters react to that PC's own player (the one named Player, as the rest of
    /// the house finds it) and to any beam its own PC shows. MonsterSettings > Online off keeps them still toys online.
    /// Numbers, colours and each kind's Animator state names: Resources/MonsterSettings. OFF SWITCH: MonsterSettings > On.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(Damageable))]
    public sealed class DarkMonster : MonoBehaviour, IResettable
    {
        /// <summary>Roam: the cave's monsters. Stay Put: the prizes (only the light rule).</summary>
        public enum Mode { Roam, StayPut }

        private enum State { None, Frozen, Idle, Wander, Sense, Chase, Windup, Strike, Back, Flinch, Flee, Hide, Dizzy, Return, Hurt, Down }
        private enum Clip { None = -1, Idle, Alert, Walk, Run, Back, Attack, Hit, Hide, Die, Rest, Sense, Dizzy, Count }
        private enum Mood { None, Calm, Hunt, Dizzy }
        private enum Tip { None, Falling, Lying, Rising }

        [SerializeField, Tooltip("Its kind in MonsterSettings > Kinds (the Animator's state names and the eyes). Set by the maker on the prefab.")] private string kind = "Slime";
        [SerializeField, Tooltip("Roam: the cave's monsters (wander, spot, chase, bump, flee, go dizzy). Stay Put: the claw machine's prizes (idle in the dark, frozen in the wall switch's light, nothing else).")] private Mode mode = Mode.Roam;
        [SerializeField, Tooltip("The room's wall-switch lights: while they are on, it is an ordinary toy. Set by the maker. Empty: not this rule.")] private RoomLights roomLights;
        [SerializeField, Tooltip("The room's star lamp: while it is awake (and the switch's lights are off) it is sleepy. Set by the maker. Empty: never sleepy.")] private StarLamp starLamp;
        [SerializeField, Tooltip("The eye glints (small unlit spheres on its eyes): shown while it is awake. Set by the maker on the prefab.")] private Renderer[] glints;
        [SerializeField, Tooltip("The soft glow behind it (a square wearing the shift orb's glow shader; D-0228): shown while a cave monster is awake. Set by the maker on the prefab; switched off in the prefab and turned on here.")] private Renderer halo;
        [SerializeField, Tooltip("Its Animator. Found in its children if empty (the pack puts it on the prefab's root).")] private Animator animator;

        [Header("The cave (Roam; set by the maker)")]
        [SerializeField, Tooltip("The box (world) it never leaves: the floor under the bed, inset. Size 0 = no box.")] private Bounds cave = new Bounds(Vector3.zero, Vector3.zero);
        [SerializeField, Tooltip("The back of the cave (world): it runs toward here from a beam.")] private Vector3 caveBack;
        [SerializeField, Tooltip("The way kept open, from the cave's mouth (world)...")] private Vector3 laneFrom;
        [SerializeField, Tooltip("...to the flashlight (world). Its body never comes within MonsterSettings > Flashlight Clear of this point.")] private Vector3 laneTo;
        [SerializeField, Min(0f), Tooltip("Half the width of that way (toy units): it never stands, waits, wanders or hides in it (a chase may cross it). 0 = no way kept and no flashlight kept clear.")] private float laneHalfWidth;

        private static readonly RaycastHit[] Hits = new RaycastHit[12];
        private static readonly float[] Bumps = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
        private static readonly List<DarkMonster> Roamers = new List<DarkMonster>();
        private static int _bumpNext;
        private static bool _allDownTold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetStatics()   // the editor may enter Play without reloading scripts
        {
            for (int i = 0; i < Bumps.Length; i++) Bumps[i] = float.NegativeInfinity;
            _bumpNext = 0;
            Roamers.Clear();
            _allDownTold = false;
        }

        private MonsterSettings _s;
        private MonsterSettings.Kind _kind;
        private CharacterController _cc;
        private Damageable _health;
        private AudioSource _audio;
        private MonsterMark _mark;
        private readonly int[] _hash = new int[(int)Clip.Count];
        private bool _resolved;
        private Clip _want = Clip.Idle, _shown = Clip.None;
        private float _wantFade = 0.2f;
        private bool _restart;

        private State _state = State.None;
        private Mood _mood = Mood.None;
        private bool _sleepy, _bound;
        private Vector3 _home;
        private Quaternion _homeTurn;
        private float _stateAt, _downAt, _freezeAt, _vertical;
        private bool _bumped, _held;
        private float _bumpReady, _squeakReady;

        // What it knows of the player (looked at every Look Every seconds) and of the light.
        private PlayerMotor _player;
        private Shifter _shifter;
        private CharacterController _playerBody;
        private Transform _toy;            // the toy's body while it is tracked (the player, or the first-person body)
        private bool _seesToy, _kidNear, _beam;
        private Vector3 _kidAt, _beamFrom, _scareFrom, _fleeTo, _wanderTo;
        private float _lastSeen = -100f, _lastSpot = -100f, _lastScare = -100f, _nextLook, _nextLight, _lightCheckedAt, _findAgain;
        private float _beamFor, _wanderAt;

        // Getting home.
        private Vector3 _checkPos;
        private float _checkAt, _stuckFor;

        // The eyes.
        private Vector3[] _glintScale;
        private float _phase, _nextBlink, _blinkEnd;

        // Tipped over while frozen.
        private Tip _tip = Tip.None;
        private Vector3 _tipFromPos, _tipPivot, _tipAxis;
        private Quaternion _tipFromTurn;
        private float _tipAt, _tipUntil;

        /// <summary>Its kind's name (the maker reports it).</summary>
        public string Kind => kind;
        /// <summary>Roam or Stay Put (the maker reports it).</summary>
        public Mode Role => mode;
        /// <summary>True while it stands as an ordinary toy (the wall switch's lights are on).</summary>
        public bool Frozen => _state == State.Frozen;
        /// <summary>True while it lies knocked down.</summary>
        public bool Down => _state == State.Down;
        /// <summary>True while it is awake in the star lamp's starlight (slower, shorter sight).</summary>
        public bool Sleepy => _sleepy && _state != State.Frozen && _state != State.Down;
        /// <summary>True while it is dizzy (one hit knocks it down).</summary>
        public bool Dizzy => _state == State.Dizzy;
        /// <summary>Where it stands when it is at home.</summary>
        public Vector3 Spot => _home;
        /// <summary>The wall-switch lights it answers to (set by the maker; null when none).</summary>
        public RoomLights Lights => roomLights;
        /// <summary>The star lamp it answers to (set by the maker; null when none).</summary>
        public StarLamp Lamp => starLamp;
        /// <summary>The cave's monsters in play now (enabled, Roam).</summary>
        public static int RoamCount => Roamers.Count;
        /// <summary>The first cave monster in play now, or null (the console's "bedroom reset" finds the room's lights by it).</summary>
        public static DarkMonster FirstRoamer => Roamers.Count > 0 ? Roamers[0] : null;

        /// <summary>The cave's monsters knocked down now.</summary>
        public static int DownCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Roamers.Count; i++) if (Roamers[i] != null && Roamers[i].Down) n++;
                return n;
            }
        }

        /// <summary>Every cave monster whole and back at its spot, deciding afresh on the next frame (a New Game, the
        /// console's "bedroom reset"). Returns how many were put back.</summary>
        public static int ReviveAll()
        {
            int n = 0;
            for (int i = 0; i < Roamers.Count; i++) if (Roamers[i] != null) { Roamers[i].ResetState(); n++; }
            _allDownTold = false;
            return n;
        }

        private float BodyScale => Mathf.Max(0.001f, transform.lossyScale.y);
        private float BodyRadius => _cc != null ? _cc.radius * BodyScale : 0.5f * BodyScale;
        private Vector3 Middle => _cc != null ? transform.TransformPoint(_cc.center) : transform.position + Vector3.up * BodyScale;
        private float PlayerScale => _shifter != null ? Mathf.Max(0.001f, _shifter.Scale) : 1f;
        private float ToyRadius => _playerBody != null ? _playerBody.radius * Mathf.Max(0.001f, _playerBody.transform.lossyScale.x) : 0.5f * PlayerScale;
        private float Pace => _sleepy ? _s.sleepySpeed : 1f;
        private bool Hunting => _state == State.Sense || _state == State.Chase || _state == State.Windup || _state == State.Strike || _state == State.Back;

        private void Awake()
        {
            _s = MonsterSettings.Load();
            _kind = _s.Find(kind);
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Damageable>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
            _health.Configure(_s.health, _s.hitSafeSeconds);
            _home = transform.position; _homeTurn = transform.rotation;
            _checkPos = _home;
            if (mode == Mode.Roam)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false; _audio.spatialBlend = 1f; _audio.dopplerLevel = 0f;
                _audio.rolloffMode = AudioRolloffMode.Linear;
                _audio.minDistance = _s.soundNear; _audio.maxDistance = Mathf.Max(_s.soundNear + 1f, _s.soundFar);
            }
            _glintScale = new Vector3[glints != null ? glints.Length : 0];
            for (int i = 0; i < _glintScale.Length; i++) if (glints[i] != null) _glintScale[i] = glints[i].transform.localScale;
            ShowGlints(false);   // nothing shows until the first frame has read the room's lights
            if (halo != null) halo.enabled = false;
            _phase = Random.value * 10f;
            _nextBlink = Time.time + _s.blinkEvery * Random.Range(0.3f, 1.4f);
            _nextLook = Time.time + Random.value * _s.lookEvery;   // the monsters look in turn, not all on one frame
            _nextLight = Time.time + Random.value * _s.lightEvery;
            _lightCheckedAt = Time.time;
            _wanderAt = Time.time + Random.Range(_s.wanderPauseMin, Mathf.Max(_s.wanderPauseMin, _s.wanderPauseMax));
        }

        private void OnEnable()
        {
            if (_bound) return;
            _health.Damaged += OnDamaged; _health.Died += OnDied; _health.Blocked += OnBlocked;
            GameEvents.OpeningEnded += OnNewGame;
            if (mode == Mode.Roam && !Roamers.Contains(this)) Roamers.Add(this);
            _bound = true;
        }

        private void OnDisable()
        {
            if (_bound)
            {
                _health.Damaged -= OnDamaged; _health.Died -= OnDied; _health.Blocked -= OnBlocked;
                GameEvents.OpeningEnded -= OnNewGame;
                Roamers.Remove(this);
                _bound = false;
            }
            _health.DamageScale = 1f;
            if (animator != null) { if (!animator.enabled) animator.enabled = true; animator.speed = 1f; }
            _shown = Clip.None; _held = false;
            EndTip();
            ShowGlints(false);
            SetMood(Mood.None);
            if (_mark != null) _mark.Hide();
            if (_state == State.Frozen) _state = State.None;
        }

        /// <summary>F9 and the like, a New Game, "bedroom reset": whole, standing at its spot, deciding afresh on the next frame.</summary>
        public void ResetState()
        {
            if (animator != null) { if (!animator.enabled) animator.enabled = true; animator.speed = 1f; }
            _shown = Clip.None; _held = false;
            EndTip();
            _health.DamageScale = 1f;
            _beamFor = 0f; _lastSpot = -100f;
            if (_mark != null) _mark.Hide();
            StandAtHome();
            SetMood(Mood.None);
            _state = State.None;
        }

        /// <summary>The New Game's opening ended (TitleMenu.NewGame does not reload the scene): whole again.</summary>
        private void OnNewGame(bool skipped)
        {
            if (mode != Mode.Roam) return;
            ResetState();
            _allDownTold = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _s == null) return;
            if (_state == State.Down) { TickDown(); ApplyClip(); return; }

            // The wall switch (or switched off): an ordinary toy. Decided here, after every Start: the lights read the save there.
            bool still = !_s.on || (NetSession.InParty && !_s.online);
            if (still || (roomLights != null && roomLights.On))
            {
                if (_state != State.Frozen) Freeze(_state == State.None);
                else if (animator != null && animator.enabled && _tip == Tip.None && Time.time >= _freezeAt) animator.enabled = false;   // settled: the pose stays
                TickTip();
                ApplyClip();
                return;
            }
            if (_state == State.Frozen || _state == State.None) Wake();
            _sleepy = starLamp != null && starLamp.Woken;
            if (GameMode.Current == GameMode.Mode.Build) { ApplyClip(); return; }   // held still while the room is edited

            Glints();
            if (mode == Mode.StayPut) { SetClip(Clip.Idle); SetMood(Mood.Calm); ApplyClip(); return; }   // a prize idles where it sits

            Senses();
            Vector3 velocity = Think(out Vector3 face, out bool leashed, out bool lane, out float speed);
            Move(velocity, face, leashed, lane, speed, dt);
            SetMood(_state == State.Dizzy ? Mood.Dizzy : Hunting ? Mood.Hunt : Mood.Calm);
            ApplyClip();
        }

        /// <summary>The glow sits just behind the body as the camera sees it, square to the camera.</summary>
        private void LateUpdate()
        {
            if (halo == null || !halo.enabled) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform view = cam.transform;
            Vector3 middle = Middle;
            Vector3 away = middle - view.position;
            float far = away.magnitude;
            Vector3 at = far > 1e-4f ? middle + away / far * BodyRadius : middle;
            halo.transform.SetPositionAndRotation(at, Quaternion.LookRotation(view.forward, view.up));
        }

        // ------------------------------------------------------------------------------------------------ the light rule

        /// <summary>An ordinary toy: settles into its rest pose (at once on the first frame, a Continue on a lit slot), eyes
        /// and glow out; a hit tips it over (OnBlocked).</summary>
        private void Freeze(bool atOnce)
        {
            _state = State.Frozen;
            _health.DamageScale = 0f;   // hits are heard through Blocked: they tip it, they take no health
            ShowGlints(false);
            SetMood(Mood.None);
            if (_mark != null) _mark.Hide();
            _beam = _seesToy = _kidNear = false;
            _beamFor = 0f;
            _toy = null;
            if (animator == null) return;
            if (!animator.enabled) { animator.enabled = true; _shown = Clip.None; }
            animator.speed = 1f; _held = false;
            if (atOnce && Resolve())
            {
                if (_hash[(int)Clip.Rest] != 0) { animator.Play(_hash[(int)Clip.Rest], 0, _kind != null ? _kind.restAt : 0f); animator.Update(0f); }
                _want = _shown = Clip.Rest;
                animator.enabled = false;
                return;
            }
            SetClip(Clip.Rest, _s.freezeSeconds);
            _freezeAt = Time.time + _s.freezeSeconds + 0.05f;
        }

        /// <summary>The room went dark again (a New Game or "bedroom reset" in the loaded scene, or the first frame in the
        /// dark): alive.</summary>
        private void Wake()
        {
            EndTip();
            if (animator != null) { if (!animator.enabled) { animator.enabled = true; _shown = Clip.None; } animator.speed = 1f; }
            _held = false;
            _health.DamageScale = mode == Mode.StayPut ? 0f : 1f;   // a prize behind the glass takes no hits
            _state = State.Idle; _stateAt = Time.time;
            SetClip(Clip.Idle);
        }

        /// <summary>A hit turned away (Damage Scale 0): on a frozen cave monster it tips it over like a toy, or, lying
        /// tipped, starts its lie again.</summary>
        private void OnBlocked(DamageInfo hit)
        {
            if (_state != State.Frozen || mode != Mode.Roam || _s.tipAngle <= 0f) return;
            float now = Time.time;
            if (_tip == Tip.Lying) { _tipUntil = now + _s.tipLie; PlayCue(_s.bump, 1f); return; }
            if (_tip != Tip.None) return;
            Vector3 dir = Level(hit.Knock);
            if (dir.sqrMagnitude < 1e-6f) dir = Level(transform.position - hit.Point);
            if (dir.sqrMagnitude < 1e-6f) dir = Level(transform.forward);
            dir.Normalize();
            if (animator != null && animator.enabled) animator.enabled = false;   // the rest pose as it stands
            _tipFromPos = transform.position; _tipFromTurn = transform.rotation;
            _tipAxis = Vector3.Cross(Vector3.up, dir).normalized;
            _tipPivot = _tipFromPos + dir * BodyRadius;   // over its own edge, away from the hit (its capsule stays on: a lying toy still takes a dart)
            _tip = Tip.Falling; _tipAt = now;
            GameEvents.RaiseMonsterTipped(this);
        }

        private void TickTip()
        {
            if (_tip == Tip.None) return;
            float now = Time.time;
            switch (_tip)
            {
                case Tip.Falling:
                {
                    float k = Mathf.Clamp01((now - _tipAt) / _s.tipSeconds);
                    PoseTipped(_s.tipAngle * k * k);   // falls faster as it goes, like a toy
                    if (k >= 1f) { _tip = Tip.Lying; _tipUntil = now + _s.tipLie; PlayCue(_s.bump, 1f); }
                    break;
                }
                case Tip.Lying:
                    if (now >= _tipUntil) { _tip = Tip.Rising; _tipAt = now; }
                    break;
                case Tip.Rising:
                {
                    float k = Mathf.Clamp01((now - _tipAt) / _s.tipRightSeconds);
                    PoseTipped(_s.tipAngle * (1f - Mathf.SmoothStep(0f, 1f, k)));
                    if (k >= 1f) EndTip();
                    break;
                }
            }
        }

        private void PoseTipped(float degrees)
        {
            Quaternion turn = Quaternion.AngleAxis(degrees, _tipAxis);
            transform.SetPositionAndRotation(_tipPivot + turn * (_tipFromPos - _tipPivot), turn * _tipFromTurn);
        }

        /// <summary>Upright again where it stood before the tip (at once).</summary>
        private void EndTip()
        {
            if (_tip == Tip.None) return;
            _tip = Tip.None;
            bool on = _cc != null && _cc.enabled;
            if (on) _cc.enabled = false;   // off and on again: the controller takes the moved spot (it may walk this frame)
            transform.SetPositionAndRotation(_tipFromPos, _tipFromTurn);
            if (on) _cc.enabled = true;
        }

        // ------------------------------------------------------------------------------------------------ senses

        private void Senses()
        {
            float now = Time.time;
            if (now >= _nextLight)
            {
                _nextLight = now + _s.lightEvery;
                float since = now - _lightCheckedAt;
                _lightCheckedAt = now;
                _beam = Beamed(out _beamFrom);
                if (_beam) { _lastScare = now; _beamFor += since; }
                else if (now - _lastScare > _s.beamForget) _beamFor = 0f;
            }
            if (now >= _nextLook)
            {
                _nextLook = now + _s.lookEvery;
                Look(now);
            }
        }

        /// <summary>True when a switched-on flashlight's beam is on its middle; <paramref name="from"/> is that beam's lens.</summary>
        private bool Beamed(out Vector3 from)
        {
            from = Vector3.zero;
            Vector3 middle = Middle;
            for (int i = 0; i < Flashlight.Lit.Count; i++)
            {
                Flashlight torch = Flashlight.Lit[i];
                if (torch == null || !LightQuery.Shines(torch, middle, transform)) continue;
                from = torch.Beam.transform.position;
                return true;
            }
            return false;
        }

        /// <summary>Finds this PC's own player (the one named Player) and asks: a toy it can see within its sight (longer
        /// once it is after it, shorter when sleepy), or the kid near?</summary>
        private void Look(float now)
        {
            _seesToy = _kidNear = false;
            if (_player == null)
            {
                if (now < _findAgain) return;
                _findAgain = now + 1f;
                GameObject own = GameObject.Find("Player");   // online, this PC's own player (others' copies are "Player 2"...)
                if (own == null || !own.TryGetComponent(out _player)) return;
                _shifter = own.GetComponent<Shifter>();
                _playerBody = own.GetComponent<CharacterController>();
            }
            Transform body = PlayerBody();
            if (body == null) { _toy = null; return; }
            Vector3 at = body.position;
            bool toySized = _shifter == null || _shifter.Size == ShiftSize.Toy;
            if (!toySized)
            {
                Vector3 flat = at - transform.position; flat.y = 0f;
                if (flat.sqrMagnitude <= _s.kidReach * _s.kidReach) { _kidNear = true; _kidAt = at; _lastScare = now; }
                _toy = null;
                return;
            }
            float sight = _s.sight * (_sleepy ? _s.sleepySight : 1f) * (Hunting ? _s.keepTrack : 1f);
            if ((at - transform.position).sqrMagnitude > sight * sight) return;
            Vector3 chest = at + Vector3.up * (0.5f * 1.8f * PlayerScale);
            Vector3 eye = transform.TransformPoint(_cc.center + Vector3.up * (_cc.height * 0.25f));
            if (!Clear(eye, chest, body)) return;
            _seesToy = true; _toy = body; _lastSeen = now;
        }

        /// <summary>The body that stands for this PC's player on foot: the first-person body while it is in use, else the
        /// player. Null while it drives (the motor is parked) or is gone.</summary>
        private Transform PlayerBody()
        {
            if (_player == null || !_player.gameObject.activeInHierarchy) return null;
            FirstPersonMode fp = FirstPersonMode.Active;
            if (fp != null && fp.Body != null && fp.Body.isActiveAndEnabled) return fp.Body.transform;
            return _player.isActiveAndEnabled ? _player.transform : null;
        }

        /// <summary>Nothing solid between its eyes and the toy's chest (itself and the player aside).</summary>
        private bool Clear(Vector3 from, Vector3 to, Transform body)
        {
            Vector3 way = to - from;
            float distance = way.magnitude;
            if (distance < 1e-4f) return true;
            int count = Physics.RaycastNonAlloc(from, way / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = Hits[i].transform;
                if (t.IsChildOf(transform) || t.IsChildOf(body) || (_player != null && t.IsChildOf(_player.transform))) continue;
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------ what it does

        /// <summary>This frame's level velocity (world units a second), where it faces, whether its leash holds it, whether
        /// the way to the flashlight is kept (everything but a chase and its bump), and the most speed it may have.</summary>
        private Vector3 Think(out Vector3 face, out bool leashed, out bool lane, out float speed)
        {
            float now = Time.time;
            face = Vector3.zero; leashed = false; lane = true; speed = 0f;
            Vector3 here = transform.position;

            // A beam or the kid comes first (not while dizzy or flinching from a hit).
            if (_state != State.Dizzy && _state != State.Hurt)
            {
                bool scared = _state == State.Flinch || _state == State.Flee || _state == State.Hide;
                if (_beam && _beamFor >= _s.dizzyAfter) { StartDizzy(now); return Vector3.zero; }
                if (!scared)
                {
                    if (_beam) StartFlinch();
                    else if (_kidNear) StartFlee(_kidAt);
                }
                else if (_state == State.Hide && (_beam || _kidNear)) _scareFrom = _beam ? _beamFrom : _kidAt;
            }

            switch (_state)
            {
                case State.Idle:
                {
                    if (_seesToy) { StartSense(now); return Vector3.zero; }
                    if (Level(here - _home).sqrMagnitude > _s.wanderRadius * _s.wanderRadius + 0.25f) { StartReturn(); return Vector3.zero; }
                    SetClip(Clip.Idle);
                    if (now >= _wanderAt && _s.wanderRadius > 0f) StartWander(now, here);
                    return Vector3.zero;
                }
                case State.Wander:
                {
                    if (_seesToy) { StartSense(now); return Vector3.zero; }
                    Vector3 step = Level(_wanderTo - here);
                    if (step.magnitude <= 0.3f || now >= _stateAt + _s.wanderSeconds) { ToIdle(now); return Vector3.zero; }
                    face = step;
                    SetClip(Clip.Walk);
                    speed = _s.creepSpeed * Pace;
                    return step.normalized * speed;
                }
                case State.Sense:
                {
                    if (_toy != null) face = Level(_toy.position - here);
                    SetClip(Clip.Sense);
                    if (now >= _stateAt + _s.senseSeconds) { _state = State.Chase; _stateAt = now; }
                    return Vector3.zero;
                }
                case State.Chase:
                {
                    if (_toy == null || (!_seesToy && now - _lastSeen > _s.loseAfter)) { StartReturn(); return Vector3.zero; }
                    Vector3 to = Level(_toy.position - here);
                    float distance = to.magnitude;
                    float reach = BodyRadius + ToyRadius + _s.bumpGap;
                    face = to;
                    if (distance <= reach && now >= _bumpReady && BumpAllowed(now)) { StartWindup(now); return Vector3.zero; }
                    Vector3 want = _toy.position - (distance > 1e-4f ? to / distance : transform.forward) * (reach * 0.9f);
                    Vector3 goal = Keep(want, true, false);
                    if (Level(goal - want).sqrMagnitude > 0.01f) goal = Keep(goal, true, true);   // held back: it waits outside the way
                    Vector3 step = Level(goal - here);
                    leashed = true; lane = false;
                    if (step.magnitude > 0.15f)
                    {
                        speed = _s.chaseSpeed * Pace;
                        SetClip(Clip.Run);
                        return step.normalized * speed;
                    }
                    SetClip(Clip.Alert);   // at the end of its leash (or the flashlight's edge): it watches
                    return Vector3.zero;
                }
                case State.Windup:
                {
                    if (_toy != null) face = Level(_toy.position - here);
                    lane = false;
                    SetClip(Clip.Attack);
                    if (!_held && now >= _stateAt + _s.windupPoseAt && animator != null && _shown == Clip.Attack)
                    {
                        _held = true; animator.speed = 0f;   // the tell: it rears back and holds
                    }
                    if (now >= _stateAt + _s.windupSeconds)
                    {
                        if (animator != null) animator.speed = 1f;
                        _held = false;
                        _state = State.Strike; _stateAt = now; _bumped = false;
                    }
                    return Vector3.zero;
                }
                case State.Strike:
                {
                    if (_toy != null) face = Level(_toy.position - here);
                    lane = false;
                    SetClip(Clip.Attack);
                    if (!_bumped && now >= _stateAt + _s.strikeSeconds) { _bumped = true; Push(); }
                    if (now >= _stateAt + _s.strikeSeconds + _s.bumpFollow)
                    {
                        _state = State.Back; _stateAt = now;
                        _bumpReady = now + _s.bumpCooldown;
                    }
                    return Vector3.zero;
                }
                case State.Back:
                {
                    Vector3 away = _toy != null ? Level(here - _toy.position) : -Level(transform.forward);
                    if (away.sqrMagnitude < 1e-6f) away = -Level(transform.forward);
                    face = -away;
                    SetClip(Clip.Back);
                    if (now >= _stateAt + _s.backSeconds)
                    {
                        if (_seesToy) { _state = State.Chase; _stateAt = now; }
                        else StartReturn();
                        return Vector3.zero;
                    }
                    leashed = true;
                    speed = _s.backSpeed * Pace;
                    return away.normalized * speed;
                }
                case State.Flinch:
                {
                    face = Level(_scareFrom - here);
                    SetClip(Clip.Hit);
                    if (now >= _stateAt + _s.flinchSeconds) StartFlee(_scareFrom);
                    return Vector3.zero;
                }
                case State.Flee:
                {
                    Vector3 step = Level(_fleeTo - here);
                    if (step.magnitude <= Mathf.Max(0.3f, BodyRadius) || now >= _stateAt + _s.fleeSeconds)
                    {
                        _state = State.Hide; _stateAt = now;
                        return Vector3.zero;
                    }
                    face = step;
                    SetClip(Clip.Run);
                    speed = _s.runSpeed * Pace;
                    return step.normalized * speed;
                }
                case State.Hide:
                {
                    face = Level(_scareFrom - here);
                    SetClip(Clip.Hide);
                    if (!_beam && !_kidNear && now - _lastScare >= _s.returnAfter) StartReturn();
                    return Vector3.zero;
                }
                case State.Dizzy:
                {
                    SetClip(Clip.Dizzy);
                    if (now >= _stateAt + _s.dizzySeconds)
                    {
                        _health.DamageScale = 1f;
                        _beamFor = 0f;
                        if (_beam) StartFlinch();
                        else if (_seesToy) { _state = State.Chase; _stateAt = now; }
                        else StartReturn();
                    }
                    return Vector3.zero;
                }
                case State.Return:
                {
                    if (_seesToy) { StartSense(now); return Vector3.zero; }
                    Vector3 step = Level(_home - here);
                    if (step.magnitude <= 0.2f) { ToIdle(now); return Vector3.zero; }
                    if (Stuck(now, here)) { StandAtHome(); ToIdle(now); return Vector3.zero; }
                    face = step;
                    lane = false;   // it may cross the way on its walk home; it never stops in it
                    SetClip(Clip.Walk);
                    speed = _s.creepSpeed * Pace;
                    return step.normalized * speed;
                }
                case State.Hurt:
                {
                    SetClip(Clip.Hit);
                    if (now >= _stateAt + _s.hurtSeconds)
                    {
                        if (_seesToy || (_toy != null && now - _lastSeen <= _s.loseAfter)) { _state = State.Chase; _stateAt = now; _lastSpot = now; }
                        else StartReturn();
                    }
                    return Vector3.zero;
                }
            }
            return Vector3.zero;
        }

        private void ToIdle(float now)
        {
            _state = State.Idle; _stateAt = now;
            _wanderAt = now + Random.Range(_s.wanderPauseMin, Mathf.Max(_s.wanderPauseMin, _s.wanderPauseMax));
        }

        /// <summary>A little walk to a point near its spot (never into the way, near the flashlight or out of the cave).</summary>
        private void StartWander(float now, Vector3 here)
        {
            Vector2 r = Random.insideUnitCircle * _s.wanderRadius;
            Vector3 to = Keep(_home + new Vector3(r.x, 0f, r.y), true, true);
            if (Level(to - here).sqrMagnitude < 0.5f * 0.5f) { ToIdle(now); return; }
            _wanderTo = to;
            _state = State.Wander; _stateAt = now;
        }

        /// <summary>It has seen the toy: it stops and stares, the "!" pops, a squeak, then the chase. Seen again soon after
        /// the last time: straight back to the chase.</summary>
        private void StartSense(float now)
        {
            if (now - _lastSpot < _s.spotAgainAfter) { _state = State.Chase; _stateAt = now; _lastSpot = now; return; }
            _lastSpot = now;
            _state = State.Sense; _stateAt = now;
            SetClip(Clip.Sense, 0.1f, true);
            if (_mark == null) _mark = MonsterMark.Make(transform, _cc);
            _mark.Pop();
            PlayCue(_s.spotSqueak, _s.spotPitch);
            GameEvents.RaiseMonsterSpotted(this);
        }

        private void StartReturn()
        {
            _state = State.Return; _stateAt = Time.time;
            _checkPos = transform.position; _checkAt = Time.time + 1f; _stuckFor = 0f;
        }

        /// <summary>On its way home and not getting anywhere for Stuck Seconds.</summary>
        private bool Stuck(float now, Vector3 here)
        {
            if (now < _checkAt) return false;
            float moved = Level(here - _checkPos).magnitude;
            _stuckFor = moved < 0.3f ? _stuckFor + 1f : 0f;
            _checkPos = here; _checkAt = now + 1f;
            return _stuckFor >= _s.stuckSeconds;
        }

        /// <summary>The wind-up: its attack clip, held still a moment early on (the tell). Counted toward Bumps At Most from
        /// here, so no other monster piles in meanwhile.</summary>
        private void StartWindup(float now)
        {
            _state = State.Windup; _stateAt = now; _bumped = false; _held = false;
            Bumps[_bumpNext] = now; _bumpNext = (_bumpNext + 1) % Bumps.Length;
            SetClip(Clip.Attack, 0.1f, true);
        }

        /// <summary>Fewer than Bumps At Most bumps (all monsters) in the last Bump Window seconds.</summary>
        private bool BumpAllowed(float now)
        {
            int recent = 0;
            for (int i = 0; i < Bumps.Length; i++) if (now - Bumps[i] < _s.bumpWindow) recent++;
            return recent < Mathf.Clamp(_s.bumpsAtMost, 1, Bumps.Length);
        }

        /// <summary>The swing lands: the toy, if still in reach, is pushed away and a little up. A push, never a hurt.</summary>
        private void Push()
        {
            if (_toy == null) return;
            Vector3 here = transform.position;
            Vector3 away = Level(_toy.position - here);
            float reach = (BodyRadius + ToyRadius + _s.bumpGap) * _s.bumpMissShare;
            if (away.sqrMagnitude > reach * reach) return;   // it stepped away in time: the dodge
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Level(transform.forward).normalized;
            float size = PlayerScale;
            if (!Knockback.Apply(_toy.gameObject, away * (_s.bumpPush * size) + Vector3.up * (_s.bumpLift * size), Middle)) return;
            PlayCue(_s.bump, 1f);
            GameEvents.RaiseMonsterBumped(this);
        }

        private void StartFlinch()
        {
            _state = State.Flinch; _stateAt = Time.time;
            _scareFrom = _beamFrom;
            SetClip(Clip.Hit, 0.08f, true);
            if (Time.time >= _squeakReady) { _squeakReady = Time.time + _s.squeakEvery; PlayCue(_s.squeak, 1f); }
        }

        /// <summary>A second of beam on it: dizzy where it stands; any one hit knocks it down.</summary>
        private void StartDizzy(float now)
        {
            _state = State.Dizzy; _stateAt = now;
            _beamFor = 0f;
            _health.DamageScale = _s.dizzyHitScale;
            SetClip(Clip.Dizzy, 0.1f, true);
            if (now >= _squeakReady) { _squeakReady = now + _s.squeakEvery; PlayCue(_s.squeak, 0.8f); }
            GameEvents.RaiseMonsterDizzy(this);
        }

        /// <summary>Runs away from <paramref name="from"/>, bent toward the cave's back, never out of the cave nor into the way.</summary>
        private void StartFlee(Vector3 from)
        {
            float now = Time.time;
            _state = State.Flee; _stateAt = now; _lastScare = now;
            _scareFrom = from;
            Vector3 here = transform.position;
            Vector3 away = Level(here - from);
            if (away.sqrMagnitude < 1e-6f) away = -Level(transform.forward);
            away.Normalize();
            if (cave.size.x > 0f)
            {
                Vector3 back = Level(caveBack - here);
                if (back.sqrMagnitude > 1e-4f) away = (away + back.normalized * _s.fleeToBack).normalized;
            }
            _fleeTo = Keep(here + away * _s.fleeDistance, false, true);
        }

        // ------------------------------------------------------------------------------------------------ hits

        private void OnDamaged(DamageInfo hit)
        {
            if (_state == State.Down || _state == State.Frozen || _health.Dead || mode == Mode.StayPut) return;
            if (_state == State.Dizzy) return;   // a hit while dizzy knocks it down (Died follows)
            if (animator != null) animator.speed = 1f;
            _held = false;
            _state = State.Hurt; _stateAt = Time.time;
            SetClip(Clip.Hit, 0.05f, true);
        }

        private void OnDied(DamageInfo hit)
        {
            if (mode == Mode.StayPut) { _health.ResetState(); return; }   // a prize is never knocked down
            _state = State.Down; _downAt = Time.time;
            _health.DamageScale = 1f;
            if (_cc != null) _cc.enabled = false;   // lying down it stops nothing
            ShowGlints(false);
            SetMood(Mood.None);
            if (_mark != null) _mark.Hide();
            if (animator != null) { if (!animator.enabled) { animator.enabled = true; _shown = Clip.None; } animator.speed = 1f; }
            _held = false;
            SetClip(Clip.Die, 0.08f, true);
            KnockDown.Show(transform, hit, null, true);   // the soldiers' burst of bits and thud
            PlayCue(_s.downSqueak, _s.downPitch);
            GameEvents.RaiseMonsterDown(this);
            int roam = Roamers.Count;
            if (!_allDownTold && roam > 0 && DownCount >= roam)
            {
                _allDownTold = true;
                GameEvents.RaiseMonstersAllDown(roam);
            }
        }

        /// <summary>Lies there (Stay Down: for the rest of the visit; else Down Seconds, then gets up at its spot).</summary>
        private void TickDown()
        {
            if (_s.stayDown || Time.time - _downAt < _s.downSeconds) return;
            StandAtHome();
            ToIdle(Time.time);
        }

        private void StandAtHome()
        {
            bool wasOn = _cc != null && _cc.enabled;
            if (_cc != null) _cc.enabled = false;
            transform.SetPositionAndRotation(_home, _homeTurn);
            if (_cc != null) _cc.enabled = true;
            if (!wasOn || _health.Dead) _health.ResetState();
            _vertical = 0f; _stuckFor = 0f;
            _toy = null; _seesToy = false;
            SetClip(Clip.Idle, 0.2f, true);
        }

        // ------------------------------------------------------------------------------------------------ moving

        /// <summary>Moves by <paramref name="velocity"/> with gravity, held inside its leash (when <paramref name="leashed"/>),
        /// out of the way (when <paramref name="lane"/>), clear of the flashlight and inside the cave, never faster than
        /// <paramref name="speed"/>; turns toward <paramref name="face"/>.</summary>
        private void Move(Vector3 velocity, Vector3 face, bool leashed, bool lane, float speed, float dt)
        {
            if (_cc == null || !_cc.enabled) return;
            Vector3 here = transform.position;
            if (velocity.sqrMagnitude > 0f)
            {
                Vector3 next = Keep(here + velocity * dt, leashed, lane);
                velocity = Vector3.ClampMagnitude(Level(next - here) / dt, speed);
            }
            if (_cc.isGrounded && _vertical < 0f) _vertical = -1f;
            _vertical -= _s.gravity * dt;
            _cc.Move((velocity + Vector3.up * _vertical) * dt);
            face.y = 0f;
            if (face.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face, Vector3.up), _s.turnSpeed * dt);
        }

        /// <summary>The nearest point to <paramref name="point"/> it may stand on: inside its leash (if asked), out of the way
        /// from the cave's mouth to the flashlight (if asked), its body clear of the flashlight's spot (always), inside the cave.</summary>
        private Vector3 Keep(Vector3 point, bool leashed, bool lane)
        {
            Vector3 p = point;
            if (leashed)
            {
                Vector3 off = Level(p - _home);
                float most = _s.leash;
                if (off.sqrMagnitude > most * most) p = _home + off.normalized * most;
            }
            if (laneHalfWidth > 0f)
            {
                if (lane)
                {
                    Vector3 near = OnLane(p);
                    Vector3 off = Level(p - near);
                    float keep = laneHalfWidth + BodyRadius;
                    if (off.sqrMagnitude < keep * keep)
                    {
                        Vector3 side = off.sqrMagnitude > 1e-6f ? off.normalized : Level(_home - OnLane(_home)).normalized;
                        if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(Vector3.up, Level(laneTo - laneFrom)).normalized;
                        p = near + side * keep;
                    }
                }
                Vector3 fromTorch = Level(p - laneTo);
                float clear = _s.flashlightClear + BodyRadius;
                if (fromTorch.sqrMagnitude < clear * clear)
                {
                    Vector3 side = fromTorch.sqrMagnitude > 1e-6f ? fromTorch.normalized : Level(_home - laneTo).normalized;
                    if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(Vector3.up, Level(laneTo - laneFrom)).normalized;
                    p = new Vector3(laneTo.x, p.y, laneTo.z) + side * clear;
                }
            }
            if (cave.size.x > 0f)
            {
                float r = BodyRadius;
                p.x = Mathf.Clamp(p.x, cave.min.x + r, Mathf.Max(cave.min.x + r, cave.max.x - r));
                p.z = Mathf.Clamp(p.z, cave.min.z + r, Mathf.Max(cave.min.z + r, cave.max.z - r));
            }
            p.y = point.y;
            return p;
        }

        /// <summary>The nearest point on the kept way (level) to <paramref name="p"/>.</summary>
        private Vector3 OnLane(Vector3 p)
        {
            Vector3 a = Level(laneFrom), ab = Level(laneTo - laneFrom), q = Level(p);
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 on = a + ab * t;
            on.y = p.y;
            return on;
        }

        private static Vector3 Level(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------------------------------------------ the Animator

        /// <summary>Asks for a clip; it is played (cross-faded over <paramref name="fade"/> seconds) at the end of the frame.
        /// <paramref name="restart"/>: play it again from its start even if it is already the one shown (a second bump).</summary>
        private void SetClip(Clip clip, float fade = 0.2f, bool restart = false)
        {
            if (clip == _want && !restart) return;
            _want = clip; _wantFade = fade;
            _restart |= restart;
        }

        private void ApplyClip()
        {
            if (animator == null || !animator.enabled || (_want == _shown && !_restart) || !Resolve()) return;
            int hash = _hash[(int)_want];
            if (_held || animator.speed != 1f) { _held = false; animator.speed = 1f; }   // a new clip ends a wind-up's hold
            if (hash != 0) animator.CrossFadeInFixedTime(hash, _wantFade, 0, 0f);
            _shown = _want; _restart = false;
        }

        /// <summary>Each clip's state, worked out once the Animator runs: a name the controller does not have falls back
        /// (sense to the alert, dizzy to the hide, the rest to the kind's idle; no warning every frame); no idle either: 0
        /// (nothing played).</summary>
        private bool Resolve()
        {
            if (_resolved) return true;
            if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized || _kind == null) return false;
            int idle = Has(_kind.idle);
            _hash[(int)Clip.Idle] = idle;
            _hash[(int)Clip.Alert] = Or(Has(_kind.alert), idle);
            _hash[(int)Clip.Walk] = Or(Has(_kind.walk), idle);
            _hash[(int)Clip.Run] = Or(Has(_kind.run), _hash[(int)Clip.Walk]);
            _hash[(int)Clip.Back] = Or(Has(_kind.back), _hash[(int)Clip.Walk]);
            _hash[(int)Clip.Attack] = Or(Has(_kind.attack), idle);
            _hash[(int)Clip.Hit] = Or(Has(_kind.hit), idle);
            _hash[(int)Clip.Hide] = Or(Has(_kind.hide), _hash[(int)Clip.Alert]);
            _hash[(int)Clip.Die] = Or(Has(_kind.die), _hash[(int)Clip.Hit]);
            _hash[(int)Clip.Rest] = Or(Has(_kind.rest), idle);
            _hash[(int)Clip.Sense] = Or(Has(_kind.sense), _hash[(int)Clip.Alert]);
            _hash[(int)Clip.Dizzy] = Or(Has(_kind.dizzy), _hash[(int)Clip.Hide]);
            if (idle == 0 && !string.IsNullOrEmpty(_kind.idle))
            {
                // Not even the idle was found by name: the names are taken as written (the maker's report checked them
                // against the controller), so a mismatch costs one warning per change of clip, not a frozen monster.
                string[] names = { _kind.idle, _kind.alert, _kind.walk, _kind.run, _kind.back, _kind.attack, _kind.hit, _kind.hide, _kind.die, _kind.rest, _kind.sense, _kind.dizzy };
                for (int i = 0; i < names.Length; i++) _hash[i] = string.IsNullOrEmpty(names[i]) ? Animator.StringToHash(_kind.idle) : Animator.StringToHash(names[i]);
            }
            _resolved = true;
            return true;
        }

        /// <summary>The state's hash when layer 0 has it (by its own name, or under the layer's name), else 0.</summary>
        private int Has(string state)
        {
            if (string.IsNullOrEmpty(state)) return 0;
            int hash = Animator.StringToHash(state);
            if (animator.HasState(0, hash)) return hash;
            int full = Animator.StringToHash(animator.GetLayerName(0) + "." + state);
            return animator.HasState(0, full) ? full : 0;
        }

        private static int Or(int hash, int fallback) => hash != 0 ? hash : fallback;

        // ------------------------------------------------------------------------------------------------ eyes, glow and sound

        /// <summary>Calm, hunting or dizzy: the eye glints' and the glow's materials (only on a change); None: the glow out
        /// (the eyes are switched by ShowGlints).</summary>
        private void SetMood(Mood mood)
        {
            if (mood == _mood || _s == null) return;
            _mood = mood;
            if (mood == Mood.None)
            {
                if (halo != null && halo.enabled) halo.enabled = false;
                return;
            }
            Material eye = mood == Mood.Hunt ? _s.eyeHunt : mood == Mood.Dizzy ? _s.eyeDizzy : null;
            if (eye == null) eye = _s.eyeCalm;
            if (eye != null && glints != null)
                for (int i = 0; i < glints.Length; i++) if (glints[i] != null && glints[i].sharedMaterial != eye) glints[i].sharedMaterial = eye;
            if (halo == null) return;
            bool show = mode == Mode.Roam && _s.haloOn;
            Material glow = mood == Mood.Hunt ? _s.haloHunt : mood == Mood.Dizzy ? _s.haloDizzy : null;
            if (glow == null) glow = _s.haloCalm;
            if (glow == null) show = false;
            else if (halo.sharedMaterial != glow) halo.sharedMaterial = glow;
            if (halo.enabled != show) halo.enabled = show;
        }

        /// <summary>The glint while awake: breathing a little, blinking now and then, smaller when sleepy.</summary>
        private void Glints()
        {
            if (_glintScale.Length == 0) return;
            float now = Time.time;
            if (now >= _nextBlink)
            {
                _blinkEnd = now + _s.blinkSeconds;
                _nextBlink = now + _s.blinkEvery * Random.Range(0.6f, 1.4f);
            }
            bool open = (mode == Mode.Roam || _s.prizeGlint) && now >= _blinkEnd;
            float breath = 1f - _s.glintDepth * 0.5f * (1f + Mathf.Sin((now + _phase) * _s.glintBreath * 2f * Mathf.PI));
            if (_sleepy) breath *= _s.sleepyEyes;
            for (int i = 0; i < _glintScale.Length; i++)
            {
                Renderer r = glints[i];
                if (r == null) continue;
                if (r.enabled != open) r.enabled = open;
                if (open) r.transform.localScale = _glintScale[i] * breath;
            }
        }

        private void ShowGlints(bool show)
        {
            if (glints == null) return;
            for (int i = 0; i < glints.Length; i++) if (glints[i] != null && glints[i].enabled != show) glints[i].enabled = show;
        }

        private void PlayCue(CourseSounds.Cue cue, float pitch)
        {
            if (_audio == null || cue.clip == null || cue.volume <= 0f) return;
            _audio.pitch = pitch * (1f + Random.Range(-cue.pitchSpread, cue.pitchSpread));
            _audio.PlayOneShot(cue.clip, cue.volume);
        }

        private void OnDrawGizmosSelected()
        {
            if (mode != Mode.Roam) return;
            Vector3 home = Application.isPlaying ? _home : transform.position;
            MonsterSettings s = Application.isPlaying ? _s : Resources.Load<MonsterSettings>(MonsterSettings.ResourceName);
            if (s == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            DrawCircle(home, s.leash);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
            DrawCircle(home, s.sight);
            Gizmos.color = new Color(0.6f, 1f, 1f, 0.6f);
            DrawCircle(home, s.wanderRadius);
            if (laneHalfWidth > 0f)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
                Gizmos.DrawLine(laneFrom, laneTo);
                DrawCircle(laneTo, s.flashlightClear);
            }
            if (cave.size.x > 0f)
            {
                Gizmos.color = new Color(0.4f, 0.4f, 1f, 0.5f);
                Gizmos.DrawWireCube(cave.center, cave.size);
                Gizmos.DrawSphere(caveBack, 0.5f);
            }
        }

        private static void DrawCircle(Vector3 centre, float radius)
        {
            const int sides = 32;
            Vector3 prev = centre + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides;
                Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
