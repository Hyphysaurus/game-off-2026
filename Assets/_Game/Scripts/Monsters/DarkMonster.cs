using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A monster that comes alive in the dark (D-0222, his "monsters under the bed"): slimes and a spiked shell that live in
    /// the cave under the bed, met before the toy has the flashlight.
    /// - While its room is lit (the wall switch's RoomLights.On or the StarLamp.Woken: references the maker sets) it is an
    ///   ordinary toy: its Animator settles into a rest pose and stops, its eyes are shut, it does not move or bump, and
    ///   hits do nothing to it. A Continue on a lit slot finds it frozen on the first frame.
    /// - In the dark it idles at its spot with a faint glint in its eyes (small unlit spheres that breathe and blink).
    /// - It notices a toy-sized local player it can see within Sight, creeps toward it (never past its Leash, never out of
    ///   the cave, never into the way from the cave's mouth to the flashlight), and in reach bumps it: its attack clip and
    ///   a gentle push away and a little up (Knockback: the motor's Knock, or the first-person body), no damage, no charge
    ///   lost; then it backs off and waits its cooldown. All the monsters together bump no more than Bumps At Most in any
    ///   Bump Window seconds.
    /// - A flashlight's beam on its middle (LightQuery): it flinches (its hit clip and a squeak), runs away from the light
    ///   toward the back of the cave, hides there (its dizzy clip) while lit, and walks back to its spot Return After
    ///   seconds after the light has gone. A kid-sized player near sends it the same way (the kid cannot fit under the bed).
    /// - Knocked down through its Damageable (darts, punches, kicks): its die clip, the soldiers' burst and thud, it lies
    ///   there Down Seconds and gets up again at its spot.
    /// Stay Put (D-0223, the claw machine's prizes): only the light rule: idling (with or without eyes) in the dark, frozen
    /// in the light; it never looks, moves or bumps.
    /// Online: nothing is networked. Each PC's monsters react to that PC's own player (the one named Player, as the rest of
    /// the house finds it) and to any beam its own PC shows. MonsterSettings > Online off keeps them still toys online.
    /// Numbers and each kind's Animator state names: Resources/MonsterSettings. OFF SWITCH: MonsterSettings > On.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(Damageable))]
    public sealed class DarkMonster : MonoBehaviour, IResettable
    {
        /// <summary>Roam: the cave's monsters. Stay Put: the prizes (only the light rule).</summary>
        public enum Mode { Roam, StayPut }

        private enum State { None, Frozen, Idle, Creep, Bump, Back, Flinch, Flee, Hide, Return, Hurt, Down }
        private enum Clip { None = -1, Idle, Alert, Walk, Run, Back, Attack, Hit, Hide, Die, Rest, Count }

        [SerializeField, Tooltip("Its kind in MonsterSettings > Kinds (the Animator's state names and the eyes). Set by the maker on the prefab.")] private string kind = "Slime";
        [SerializeField, Tooltip("Roam: the cave's monsters (look, creep, bump, flee, hide). Stay Put: the claw machine's prizes (idle in the dark, frozen in the light, nothing else).")] private Mode mode = Mode.Roam;
        [SerializeField, Tooltip("The room's wall-switch lights: while they are on, it is an ordinary toy. Set by the maker. Empty: not this rule.")] private RoomLights roomLights;
        [SerializeField, Tooltip("The room's star lamp: while it is awake, it is an ordinary toy. Set by the maker. Empty: not this rule.")] private StarLamp starLamp;
        [SerializeField, Tooltip("The eye glints (small unlit spheres on its eyes): shown in the dark only. Set by the maker on the prefab.")] private Renderer[] glints;
        [SerializeField, Tooltip("Its Animator. Found in its children if empty (the pack puts it on the prefab's root).")] private Animator animator;

        [Header("The cave (Roam; set by the maker)")]
        [SerializeField, Tooltip("The box (world) it never leaves: the floor under the bed, inset. Size 0 = no box.")] private Bounds cave = new Bounds(Vector3.zero, Vector3.zero);
        [SerializeField, Tooltip("The back of the cave (world): it runs toward here from a beam.")] private Vector3 caveBack;
        [SerializeField, Tooltip("The way kept open, from the cave's mouth (world)...")] private Vector3 laneFrom;
        [SerializeField, Tooltip("...to the flashlight (world).")] private Vector3 laneTo;
        [SerializeField, Min(0f), Tooltip("Half the width of that way (toy units): it never stands in it. 0 = no way kept.")] private float laneHalfWidth;

        private static readonly RaycastHit[] Hits = new RaycastHit[12];
        private static readonly float[] Bumps = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
        private static int _bumpNext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetBumps()   // the editor may enter Play without reloading scripts
        {
            for (int i = 0; i < Bumps.Length; i++) Bumps[i] = float.NegativeInfinity;
            _bumpNext = 0;
        }

        private MonsterSettings _s;
        private MonsterSettings.Kind _kind;
        private CharacterController _cc;
        private Damageable _health;
        private AudioSource _audio;
        private readonly int[] _hash = new int[(int)Clip.Count];
        private bool _resolved;
        private Clip _want = Clip.Idle, _shown = Clip.None;
        private float _wantFade = 0.2f;
        private bool _restart;

        private State _state = State.None;
        private Vector3 _home;
        private Quaternion _homeTurn;
        private float _stateAt, _downAt, _freezeAt, _vertical;
        private bool _bumped;
        private float _bumpReady, _squeakReady;

        // What it knows of the player (looked at every Look Every seconds) and of the light.
        private PlayerMotor _player;
        private Shifter _shifter;
        private CharacterController _playerBody;
        private Transform _toy;            // the toy's body while it is seen (the player, or the first-person body)
        private bool _seesToy, _kidNear, _beam;
        private Vector3 _kidAt, _beamFrom, _scareFrom, _fleeTo;
        private float _lastSeen = -100f, _lastScare = -100f, _nextLook, _nextLight, _findAgain;

        // Getting home.
        private Vector3 _checkPos;
        private float _checkAt, _stuckFor;

        // The eyes.
        private Vector3[] _glintScale;
        private float _phase, _nextBlink, _blinkEnd;

        /// <summary>Its kind's name (the maker reports it).</summary>
        public string Kind => kind;
        /// <summary>Roam or Stay Put (the maker reports it).</summary>
        public Mode Role => mode;
        /// <summary>True while it stands as an ordinary toy (its room is lit).</summary>
        public bool Frozen => _state == State.Frozen;
        /// <summary>True while it lies knocked down.</summary>
        public bool Down => _state == State.Down;
        /// <summary>Where it stands when it is at home.</summary>
        public Vector3 Spot => _home;

        private float Scale => Mathf.Max(0.001f, transform.lossyScale.y);
        private float BodyRadius => _cc != null ? _cc.radius * Scale : 0.5f * Scale;
        private Vector3 Middle => _cc != null ? transform.TransformPoint(_cc.center) : transform.position + Vector3.up * Scale;
        private float PlayerScale => _shifter != null ? Mathf.Max(0.001f, _shifter.Scale) : 1f;
        private float ToyRadius => _playerBody != null ? _playerBody.radius * Mathf.Max(0.001f, _playerBody.transform.lossyScale.x) : 0.5f * PlayerScale;

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
                _audio.minDistance = _s.soundNear * Scale; _audio.maxDistance = Mathf.Max(_s.soundNear + 1f, _s.soundFar) * Scale;
            }
            _glintScale = new Vector3[glints != null ? glints.Length : 0];
            for (int i = 0; i < _glintScale.Length; i++) if (glints[i] != null) _glintScale[i] = glints[i].transform.localScale;
            ShowGlints(false);   // nothing shows until the first frame has read the room's lights
            _phase = Random.value * 10f;
            _nextBlink = Time.time + _s.blinkEvery * Random.Range(0.3f, 1.4f);
            _nextLook = Time.time + Random.value * _s.lookEvery;   // the monsters look in turn, not all on one frame
            _nextLight = Time.time + Random.value * _s.lightEvery;
        }

        private void OnEnable() { _health.Damaged += OnDamaged; _health.Died += OnDied; }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged; _health.Died -= OnDied;
            _health.DamageScale = 1f;
            if (animator != null && !animator.enabled) { animator.enabled = true; _shown = Clip.None; }
            ShowGlints(false);
            if (_state == State.Frozen) _state = State.None;
        }

        /// <summary>F9 and the like: whole, standing at its spot, deciding afresh on the next frame.</summary>
        public void ResetState()
        {
            if (animator != null && !animator.enabled) animator.enabled = true;
            _shown = Clip.None;
            _health.DamageScale = 1f;
            StandAtHome();
            _state = State.None;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _s == null) return;
            if (_state == State.Down) { TickDown(); ApplyClip(); return; }

            // Lit (or switched off): an ordinary toy. Decided here, after every Start: the lights read the save there.
            bool still = !_s.on || (NetSession.InParty && !_s.online);
            if (still || RoomLit())
            {
                if (_state != State.Frozen) Freeze(_state == State.None);
                else if (animator != null && animator.enabled && Time.time >= _freezeAt) animator.enabled = false;   // settled: the pose stays
                ApplyClip();
                return;
            }
            if (_state == State.Frozen || _state == State.None) Wake();
            if (GameMode.Current == GameMode.Mode.Build) { ApplyClip(); return; }   // held still while the room is edited

            Glints();
            if (mode == Mode.StayPut) { SetClip(Clip.Idle); ApplyClip(); return; }   // a prize idles where it sits

            Senses();
            Vector3 face = Vector3.zero;
            Vector3 velocity = Think(out face, out bool leashed, out float speed);
            Move(velocity, face, leashed, speed, dt);
            ApplyClip();
        }

        // ------------------------------------------------------------------------------------------------ the light rule

        private bool RoomLit() => (roomLights != null && roomLights.On) || (starLamp != null && starLamp.Woken);

        /// <summary>An ordinary toy: settles into its rest pose (at once on the first frame, a Continue on a lit slot), eyes
        /// shut, hits do nothing.</summary>
        private void Freeze(bool atOnce)
        {
            _state = State.Frozen;
            _health.DamageScale = 0f;
            ShowGlints(false);
            _beam = _seesToy = _kidNear = false;
            _toy = null;
            if (animator == null) return;
            if (!animator.enabled) { animator.enabled = true; _shown = Clip.None; }
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

        /// <summary>The room went dark again (a New Game in the loaded scene, or the first frame in the dark): alive.</summary>
        private void Wake()
        {
            if (animator != null && !animator.enabled) { animator.enabled = true; _shown = Clip.None; }
            _health.DamageScale = 1f;
            _state = State.Idle; _stateAt = Time.time;
            SetClip(Clip.Idle);
        }

        // ------------------------------------------------------------------------------------------------ senses

        private void Senses()
        {
            float now = Time.time;
            if (now >= _nextLight)
            {
                _nextLight = now + _s.lightEvery;
                _beam = Beamed(out _beamFrom);
                if (_beam) _lastScare = now;
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

        /// <summary>Finds this PC's own player (the one named Player) and asks: a toy it can see within Sight, or the kid near?</summary>
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
            float scale = Scale;
            bool toySized = _shifter == null || _shifter.Size == ShiftSize.Toy;
            if (!toySized)
            {
                Vector3 flat = at - transform.position; flat.y = 0f;
                float reach = _s.kidReach * scale;
                if (flat.sqrMagnitude <= reach * reach) { _kidNear = true; _kidAt = at; _lastScare = now; }
                _toy = null;
                return;
            }
            float sight = _s.sight * scale;
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

        /// <summary>This frame's level velocity (world units a second), where it faces, whether its leash holds it, and the
        /// most speed it may have.</summary>
        private Vector3 Think(out Vector3 face, out bool leashed, out float speed)
        {
            float now = Time.time, scale = Scale;
            face = Vector3.zero; leashed = false; speed = 0f;
            Vector3 here = transform.position;

            // A beam or the kid comes first.
            bool hiding = _state == State.Flinch || _state == State.Flee || _state == State.Hide;
            if (!hiding && _state != State.Hurt)
            {
                if (_beam) StartFlinch();
                else if (_kidNear) StartFlee(_kidAt);
            }
            else if (hiding && _state == State.Hide && (_beam || _kidNear)) _scareFrom = _beam ? _beamFrom : _kidAt;

            switch (_state)
            {
                case State.Idle:
                {
                    if (_seesToy) { _state = State.Creep; _stateAt = now; return Vector3.zero; }
                    if (Level(here - _home).sqrMagnitude > (0.3f * scale) * (0.3f * scale)) { StartReturn(); return Vector3.zero; }
                    SetClip(Clip.Idle);
                    face = _homeTurn * Vector3.forward;
                    return Vector3.zero;
                }
                case State.Creep:
                {
                    if (_toy == null || (!_seesToy && now - _lastSeen > _s.loseAfter)) { StartReturn(); return Vector3.zero; }
                    Vector3 to = Level(_toy.position - here);
                    float distance = to.magnitude;
                    float reach = BodyRadius + ToyRadius + _s.bumpGap * scale;
                    face = to;
                    if (distance <= reach && now >= _bumpReady && BumpAllowed(now)) { StartBump(now); return Vector3.zero; }
                    Vector3 goal = Keep(_toy.position - (distance > 1e-4f ? to / distance : transform.forward) * (reach * 0.9f), true);
                    Vector3 step = Level(goal - here);
                    leashed = true;
                    if (step.magnitude > 0.15f * scale)
                    {
                        speed = _s.creepSpeed * scale;
                        SetClip(Clip.Walk);
                        return step.normalized * speed;
                    }
                    SetClip(Clip.Alert);   // at the end of its leash (or the way's edge): it watches
                    return Vector3.zero;
                }
                case State.Bump:
                {
                    if (_toy != null) face = Level(_toy.position - here);
                    SetClip(Clip.Attack);
                    if (!_bumped && now >= _stateAt + _s.bumpWindup) { _bumped = true; Push(); }
                    if (now >= _stateAt + _s.bumpWindup + _s.bumpFollow)
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
                        if (_seesToy) { _state = State.Creep; _stateAt = now; }
                        else StartReturn();
                        return Vector3.zero;
                    }
                    leashed = true;
                    speed = _s.backSpeed * scale;
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
                    if (step.magnitude <= Mathf.Max(0.3f * scale, BodyRadius) || now >= _stateAt + _s.fleeSeconds)
                    {
                        _state = State.Hide; _stateAt = now;
                        return Vector3.zero;
                    }
                    face = step;
                    SetClip(Clip.Run);
                    speed = _s.runSpeed * scale;
                    return step.normalized * speed;
                }
                case State.Hide:
                {
                    face = Level(_scareFrom - here);
                    SetClip(Clip.Hide);
                    if (!_beam && !_kidNear && now - _lastScare >= _s.returnAfter) StartReturn();
                    return Vector3.zero;
                }
                case State.Return:
                {
                    if (_seesToy) { _state = State.Creep; _stateAt = now; return Vector3.zero; }
                    Vector3 step = Level(_home - here);
                    if (step.magnitude <= 0.2f * scale) { _state = State.Idle; _stateAt = now; return Vector3.zero; }
                    if (Stuck(now, here)) { StandAtHome(); return Vector3.zero; }
                    face = step;
                    SetClip(Clip.Walk);
                    speed = _s.creepSpeed * scale;
                    return step.normalized * speed;
                }
                case State.Hurt:
                {
                    SetClip(Clip.Hit);
                    if (now >= _stateAt + _s.hurtSeconds)
                    {
                        if (_seesToy) { _state = State.Creep; _stateAt = now; }
                        else StartReturn();
                    }
                    return Vector3.zero;
                }
            }
            return Vector3.zero;
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
            _stuckFor = moved < 0.3f * Scale ? _stuckFor + 1f : 0f;
            _checkPos = here; _checkAt = now + 1f;
            return _stuckFor >= _s.stuckSeconds;
        }

        private void StartBump(float now)
        {
            _state = State.Bump; _stateAt = now; _bumped = false;
            Bumps[_bumpNext] = now; _bumpNext = (_bumpNext + 1) % Bumps.Length;   // counted from the swing: no other piles in meanwhile
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
            float reach = (BodyRadius + ToyRadius + _s.bumpGap * Scale) * _s.bumpMissShare;
            if (away.sqrMagnitude > reach * reach) return;   // it stepped back in time
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Level(transform.forward).normalized;
            float size = PlayerScale;
            Knockback.Apply(_toy.gameObject, away * (_s.bumpPush * size) + Vector3.up * (_s.bumpLift * size), Middle);
            PlayCue(_s.bump);
        }

        private void StartFlinch()
        {
            _state = State.Flinch; _stateAt = Time.time;
            _scareFrom = _beamFrom;
            SetClip(Clip.Hit, 0.08f, true);
            if (Time.time >= _squeakReady) { _squeakReady = Time.time + _s.squeakEvery; PlayCue(_s.squeak); }
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
            _fleeTo = Keep(here + away * (_s.fleeDistance * Scale), false);
        }

        // ------------------------------------------------------------------------------------------------ hits

        private void OnDamaged(DamageInfo hit)
        {
            if (_state == State.Down || _state == State.Frozen || _health.Dead || mode == Mode.StayPut) return;
            _state = State.Hurt; _stateAt = Time.time;
            SetClip(Clip.Hit, 0.05f, true);
        }

        private void OnDied(DamageInfo hit)
        {
            _state = State.Down; _downAt = Time.time;
            if (_cc != null) _cc.enabled = false;   // lying down it stops nothing
            ShowGlints(false);
            if (animator != null && !animator.enabled) { animator.enabled = true; _shown = Clip.None; }
            SetClip(Clip.Die, 0.08f, true);
            KnockDown.Show(transform, hit, null, true);   // the soldiers' burst of bits and thud
        }

        /// <summary>Lies there Down Seconds, then gets up at its spot (next frame decides: the light freezes it, the dark wakes it).</summary>
        private void TickDown()
        {
            if (Time.time - _downAt < _s.downSeconds) return;
            StandAtHome();
            _state = State.Idle; _stateAt = Time.time;
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
        /// the cave and out of the way, never faster than <paramref name="speed"/>; turns toward <paramref name="face"/>.</summary>
        private void Move(Vector3 velocity, Vector3 face, bool leashed, float speed, float dt)
        {
            if (_cc == null || !_cc.enabled) return;
            Vector3 here = transform.position;
            if (velocity.sqrMagnitude > 0f)
            {
                Vector3 next = Keep(here + velocity * dt, leashed);
                velocity = Vector3.ClampMagnitude(Level(next - here) / dt, speed);
            }
            float scale = Scale;
            if (_cc.isGrounded && _vertical < 0f) _vertical = -1f * scale;
            _vertical -= _s.gravity * scale * dt;
            _cc.Move((velocity + Vector3.up * _vertical) * dt);
            face.y = 0f;
            if (face.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face, Vector3.up), _s.turnSpeed * dt);
        }

        /// <summary>The nearest point to <paramref name="point"/> it may stand on: inside its leash (if asked), out of the way
        /// from the cave's mouth to the flashlight, inside the cave.</summary>
        private Vector3 Keep(Vector3 point, bool leashed)
        {
            float scale = Scale;
            Vector3 p = point;
            if (leashed)
            {
                Vector3 off = Level(p - _home);
                float most = _s.leash * scale;
                if (off.sqrMagnitude > most * most) p = _home + off.normalized * most;
            }
            if (laneHalfWidth > 0f)
            {
                Vector3 near = OnLane(p);
                Vector3 off = Level(p - near);
                float keep = laneHalfWidth * scale + BodyRadius;
                if (off.sqrMagnitude < keep * keep)
                {
                    Vector3 side = off.sqrMagnitude > 1e-6f ? off.normalized : Level(_home - OnLane(_home)).normalized;
                    if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(Vector3.up, Level(laneTo - laneFrom)).normalized;
                    p = near + side * keep;
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
            if (hash != 0) animator.CrossFadeInFixedTime(hash, _wantFade, 0, 0f);
            _shown = _want; _restart = false;
        }

        /// <summary>Each clip's state, worked out once the Animator runs: a name the controller does not have falls back to
        /// the kind's idle (no warning every frame); no idle either: 0 (nothing played).</summary>
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
            if (idle == 0 && !string.IsNullOrEmpty(_kind.idle))
            {
                // Not even the idle was found by name: the names are taken as written (the maker's report checked them
                // against the controller), so a mismatch costs one warning per change of clip, not a frozen monster.
                string[] names = { _kind.idle, _kind.alert, _kind.walk, _kind.run, _kind.back, _kind.attack, _kind.hit, _kind.hide, _kind.die, _kind.rest };
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

        // ------------------------------------------------------------------------------------------------ eyes and sound

        /// <summary>The glint in the dark: breathing a little, blinking now and then.</summary>
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

        private void PlayCue(CourseSounds.Cue cue)
        {
            if (_audio == null || cue.clip == null || cue.volume <= 0f) return;
            _audio.pitch = 1f + Random.Range(-cue.pitchSpread, cue.pitchSpread);
            _audio.PlayOneShot(cue.clip, cue.volume);
        }

        private void OnDrawGizmosSelected()
        {
            if (mode != Mode.Roam) return;
            Vector3 home = Application.isPlaying ? _home : transform.position;
            float scale = Mathf.Max(0.001f, transform.lossyScale.y);
            MonsterSettings s = Application.isPlaying ? _s : Resources.Load<MonsterSettings>(MonsterSettings.ResourceName);
            if (s == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            DrawCircle(home, s.leash * scale);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.4f);
            DrawCircle(home, s.sight * scale);
            if (laneHalfWidth > 0f)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
                Gizmos.DrawLine(laneFrom, laneTo);
            }
            if (cave.size.x > 0f)
            {
                Gizmos.color = new Color(0.4f, 0.4f, 1f, 0.5f);
                Gizmos.DrawWireCube(cave.center, cave.size);
                Gizmos.DrawSphere(caveBack, 0.5f * scale);
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
