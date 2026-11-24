using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's side of a rope (D-0170, ported from the Goblin game's rope swinger): the grab (E beside a rope, or
    /// flying into one), the swing the stick pushes by the camera, the climb (Sprint) and the slide (Crouch), and the
    /// let-go on Jump. Toy only, third person only, this PC's player only (on another player's copy NetPlayer switches
    /// this script off).
    /// WHY it works the swing out itself and only hands the motor a speed: the body is moved through PlayerMotor's
    /// line mode (SetPull), so the capsule still stops on walls and floors, nothing on a rope counts as a landing (no
    /// fall rule, no landing roll), and every other script sees an ordinary player being pulled. What the capsule
    /// meets, the swing kicks off. The let-go goes through PlayerMotor.Fling, so the flight after it is the motor's
    /// own free fall and its landing is judged like any other (the fall rule, the roll out of a landing).
    /// The body hangs upright under its hands and faces where the stick pushes; both hands go on the rope through
    /// CarryPose, a tool in hand is put away, and the gun hides (WeaponHolder asks Holds).
    /// What a ride takes it gives back however it ends: the motor's pull, the standing capsule, the Animator's state,
    /// the hands, the rope.
    /// </summary>
    [DefaultExecutionOrder(-5)]   // after the moves (-10) have said whether the body is free, before the motor (0) moves it
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerInput))]
    public sealed class RopeRider : MonoBehaviour, ICarryHands
    {
        private const float MaxSwingStep = 1f / 60f;   // the swing is worked out in slices no longer than this, so a stalled frame cannot swing it higher than it earned
        private const float FacingStick = 0.2f;        // the stick must be pushed this far before the body turns to it
        private const float Never = float.NegativeInfinity;

        /// <summary>How a ride ends: what the motor is handed.</summary>
        private enum Leave
        {
            Fling,   // Jump: the swing's speed and a lift
            Keep,    // let go by something else (a shift, a hit, full hands): the swing's speed, no lift
            Drop,    // taken off it (a respawn, parked, the rope gone): no speed
            Taken,   // someone else already ended the motor's pull: it is left as they set it
        }

        [SerializeField, Tooltip("Resources/Rope is loaded if empty.")] private RopeSettings settings;

        private static readonly HashSet<GameObject> Riders = new HashSet<GameObject>();

        private PlayerMotor _motor;
        private PlayerInput _input;
        private CharacterController _cc;
        private Shifter _shifter;
        private PlayerMoves _moves;
        private PlayerStance _stance;
        private PlayerAnimation _animation;
        private PlayerBody _body;
        private Carrier _carrier;
        private Glider _glider;
        private CarryPose _pose;

        private Vector3 _dir = Vector3.down;   // from where the rope is tied to the hands, a unit vector
        private float _radius;                 // rope between the two, world units
        private Vector3 _velocity;             // the swing's speed, world units a second, always across the rope
        private float _yaw;                    // the heading the body is held at
        private Vector3 _grabFrom;             // where the feet were at the grab (the body is eased from there)
        private Vector3 _expected;             // where this frame's pull sends the feet; ending elsewhere means the body met something
        private bool _jumpPressed;             // Jump came in while riding; acted on in Update
        private bool _posed;                   // this ride started the hang pose and must end it
        private float _airFor;                 // seconds off the ground and free to catch a rope
        private Rope _lastRope;                // the rope last let go of
        private float _releasedAt = Never;     // when it was let go of, or when the feet last touched down
        private bool _mustLeave;               // the rope just let go of has not been left behind yet
        private float _handsOffAt = Never;     // when the hands came off (their fade runs from here)
        private Vector3 _handsUp = Vector3.up; // the rope's line through the hands, kept for that fade
        private float _handsSide;              // half the rope's width, world units, kept for that fade

        /// <summary>True while <paramref name="who"/> swings on a rope (both hands are on it: the gun is hidden).</summary>
        public static bool Holds(GameObject who) => who != null && Riders.Contains(who);

        /// <summary>True while on a rope.</summary>
        public bool Riding { get; private set; }
        /// <summary>The rope being ridden; null when on none.</summary>
        public Rope Current { get; private set; }
        /// <summary>Where the hands hold a rope, world: the rope is drawn through here.</summary>
        public Vector3 HandPoint => transform.TransformPoint(settings.handOffset);
        /// <summary>Rope between where it is tied and the hands, world units.</summary>
        public float HeldAlong => _radius;
        /// <summary>The swing's speed, world units a second (after a let-go: the speed it was let go at).</summary>
        public Vector3 Velocity => _velocity;
        /// <summary>When the rope was last taken hold of (Time.time).</summary>
        public float GrabbedTime { get; private set; }
        /// <summary>Why the last ride ended: "jump", "shift", "hit", "hands", "mode", "parked", "warp", "line",
        /// "rope gone", "let go", "off".</summary>
        public string LastLetGo { get; private set; } = "";

        private float Scale => Mathf.Max(1e-4f, _motor.Scale);
        /// <summary>The body cannot be moved by the motor right now (first person, a car seat, Build Mode, the console's
        /// fly, the wake-up): a ride is dropped. Input switched off is NOT this: a menu, the console or a cutscene camera
        /// leaves the toy hanging until the input is back.</summary>
        private bool Parked => !_motor.isActiveAndEnabled || !_cc.enabled;
        private bool IsToy => _shifter == null || (_shifter.Size == ShiftSize.Toy && !_shifter.Shifting);

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _input = GetComponent<PlayerInput>();
            _cc = GetComponent<CharacterController>();
            TryGetComponent(out _shifter); TryGetComponent(out _moves); TryGetComponent(out _stance);
            TryGetComponent(out _animation); TryGetComponent(out _body); TryGetComponent(out _carrier);
            if (settings == null) settings = RopeSettings.Load();
        }

        private void Start() => TryGetComponent(out _glider);   // the Shifter adds it in its Awake

        private void OnEnable()
        {
            _input.Jump += OnJump;
            _motor.Landed += OnLanded;
            _motor.Warped += OnWarped;
            if (_shifter != null) _shifter.ShiftStarted += OnShiftStarted;
            GameMode.Changed += OnModeChanged;
        }

        private void OnDisable()
        {
            End(Leave.Drop, "off");
            _input.Jump -= OnJump;
            _motor.Landed -= OnLanded;
            _motor.Warped -= OnWarped;
            if (_shifter != null) _shifter.ShiftStarted -= OnShiftStarted;
            GameMode.Changed -= OnModeChanged;
        }

        private void OnDestroy() { if (_pose != null) _pose.Remove(this); }

        // The let-go itself waits for Update: the motor hears the same press, before or after us, and must still find
        // the pull set to know the press is the rope's. A press that arrives while the game is stopped is not kept.
        private void OnJump() { if (Riding && Time.timeScale > 0f) _jumpPressed = true; }

        /// <summary>The feet are down: the rope just let go of may catch again, but not at once (it swings back past
        /// the landing spot).</summary>
        private void OnLanded(float speed)
        {
            _mustLeave = false;
            _releasedAt = Time.time;
        }

        private void OnWarped(Vector3 position, Quaternion rotation) => End(Leave.Drop, "warp");
        private void OnShiftStarted(ShiftSize size) => End(Leave.Keep, "shift");
        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to) { if (to != GameMode.Mode.Play) End(Leave.Keep, "mode"); }

        // ---------- taking hold ----------

        /// <summary>Everything a grab needs that is not about one rope: on foot as a toy, in normal control, hands free,
        /// no other line pulling, and room to stand up if crouched (a ride is ridden standing).</summary>
        private bool Ready()
        {
            if (Riding || Parked || !_input.isActiveAndEnabled) return false;   // input off: a cutscene or a menu has it, no new grab
            if (!IsToy) return false;   // a rope is a toy's
            if (GameMode.Current != GameMode.Mode.Play || Time.timeScale <= 0f) return false;
            if (_moves != null && (!_moves.Free || _moves.HandsFull)) return false;
            if (Grabbable.Holds(gameObject)) return false;
            if (_motor.Pull.HasValue) return false;   // another line has the body (the Dart Line)
            if (_stance != null && _stance.Crouched && !_stance.StandFits()) return false;
            return true;
        }

        /// <summary>True if this player could take hold of <paramref name="rope"/> right now (its E prompt asks).</summary>
        public bool CanGrab(Rope rope) => rope != null && rope.isActiveAndEnabled && !rope.Held && Ready();

        /// <summary>E on a rope: takes it where it hangs at rest, at the hands' own height. False if it may not be taken.</summary>
        public bool TryGrab(Rope rope)
        {
            if (!CanGrab(rope)) return false;
            Grab(rope, inAir: false, 0f);
            return true;
        }

        /// <summary>Not riding: in the air for long enough, the nearest free rope within reach of the hands is caught.</summary>
        private void Watch(float dt)
        {
            float s = Scale;
            Vector3 hands = HandPoint;
            float minHold = settings.minHold * s;
            // The rope just let go of. Letting go while hanging still falls ALONG it, so time alone is no test: the hands
            // must have been clear of it once (or the toy has landed, which clears the mark). Looked at every frame, not
            // only when a catch is possible, so a hit taken on the way down does not hide the moment the hands got clear.
            if (_mustLeave)
            {
                if (_lastRope == null) _mustLeave = false;
                else if (Vector3.Distance(hands, _lastRope.ClosestPoint(hands, minHold, out _)) > settings.leaveReach * s) _mustLeave = false;
            }

            _airFor = Ready() && !_motor.Grounded ? _airFor + dt : 0f;
            if (_airFor < settings.catchAirSeconds) return;

            Rope best = null;
            float bestDistance = settings.catchReach * s, bestAlong = 0f;
            IReadOnlyList<Rope> all = Rope.All;
            for (int i = 0; i < all.Count; i++)
            {
                Rope rope = all[i];
                if (rope == null || rope.Held) continue;
                if (ReferenceEquals(rope, _lastRope) && (_mustLeave || Time.time - _releasedAt < settings.regrabSeconds)) continue;
                float distance = Vector3.Distance(hands, rope.ClosestPoint(hands, minHold, out float along));
                if (distance > bestDistance) continue;
                best = rope; bestDistance = distance; bestAlong = along;
            }
            if (best != null) Grab(best, inAir: true, bestAlong);
        }

        private void Grab(Rope rope, bool inAir, float along)
        {
            float s = Scale;
            // Standing, and kept standing from this moment: E arrives before any Update, and the stance (which runs
            // before us) would crouch the capsule again with Crouch held, for the whole ride.
            if (_stance != null)
            {
                if (_stance.Crouched) _stance.Set(false);   // Ready has checked there is room
                _stance.Frozen = true;
            }
            if (_glider != null && _glider.Gliding) _glider.FoldNow("rope");
            if (_carrier != null) _carrier.Stow();   // a tool in hand goes to its slot, as on the turntable

            Vector3 anchor = rope.Anchor, hands = HandPoint;
            float length = rope.Length, least = Mathf.Min(settings.minHold * s, length);
            if (inAir)
            {
                // Caught in flight: where the hands are, keeping the speed across the rope (the speed along it is dropped).
                Vector3 to = hands - anchor;
                _dir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector3.down;
                _radius = Mathf.Clamp(along, least, length);
                _velocity = Vector3.ProjectOnPlane(_motor.Velocity, _dir);
            }
            else
            {
                // By E: the rope where it hangs at rest, at the hands' own height, from a standstill. A rope still
                // swaying is snatched to the hands and whips.
                _dir = Vector3.down;
                _radius = Mathf.Clamp(anchor.y - hands.y, least, length);
                _velocity = Vector3.zero;
            }
            _yaw = transform.eulerAngles.y;
            _grabFrom = _expected = transform.position;
            _jumpPressed = false;
            GrabbedTime = Time.time;
            Riding = true;
            Current = rope;
            Riders.Add(gameObject);
            rope.SetHolder(this);
            // The motor is ours from this moment, not from the next Update: a Jump pressed in between must find the pull.
            _motor.SetPull(Vector3.zero);
            _motor.PullYaw = _yaw;
            _motor.PullOwnsJump = true;

            // The hands (asked again at every grab: the body can be swapped).
            Animator body = _body != null && _body.Animator != null ? _body.Animator : GetComponentInChildren<Animator>();
            CarryPose pose = CarryPose.On(body);
            if (pose != _pose && _pose != null) _pose.Remove(this);
            _pose = pose;
            if (_pose != null) _pose.Add(this);
            // The hang. A controller built before the state was added does not have it: the body shows the fall then.
            _posed = _animation != null && _animation.Has(PlayerAnimation.RopeHang);
            if (_posed) _animation.Play(PlayerAnimation.RopeHang, 1f);

            Toasts.OnceFor(this, "rope-keys", settings.keysHint, ToastKind.Tip, settings.keysTitle);
            GameEvents.RaiseRopeChanged(this, true);
        }

        // ---------- riding ----------

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // paused: it hangs, and goes on after
            if (!Riding)
            {
                Watch(dt);
                if (!Riding) return;   // a catch in the air rides from this same frame (the stance must not get a frame in between)
            }
            Ride(dt);
        }

        private void Ride(float dt)
        {
            Rope rope = Current;
            // 1. What ends a ride, first.
            if (Parked) { End(Leave.Drop, "parked"); return; }
            if (rope == null || !rope.isActiveAndEnabled) { End(Leave.Drop, "rope gone"); return; }
            if (!_motor.Pull.HasValue) { End(Leave.Taken, "line"); return; }   // someone else ended the pull
            if (GameMode.Current != GameMode.Mode.Play) { End(Leave.Keep, "mode"); return; }
            if (!IsToy) { End(Leave.Keep, "shift"); return; }
            if (_moves != null && !_moves.Free) { End(Leave.Keep, "hit"); return; }   // a hit reaction, a death: its clip plays
            if ((_moves != null && _moves.HandsFull) || Grabbable.Holds(gameObject)) { End(Leave.Keep, "hands"); return; }
            if (_carrier != null && _carrier.Held != null) _carrier.Stow();   // a slot key brought a tool to hand

            // 2. Jump lets go, with the fling.
            if (_jumpPressed) { _jumpPressed = false; End(Leave.Fling, "jump"); return; }

            float s = Scale;
            Vector3 anchor = rope.Anchor;
            float length = rope.Length, least = Mathf.Min(settings.minHold * s, length);
            Vector3 offset = settings.handOffset * s;
            float settle = Mathf.Max(0f, settings.settleSeconds);
            float since = Time.time - GrabbedTime;

            // 3. What the body met last frame. Not read while it is still getting onto the rope: from a ledge the capsule
            // slides along the top until it is past the edge, then drops, and none of that is a bump.
            if (since >= settle)
            {
                Vector3 miss = _expected - transform.position;
                float slack = settings.bumpSlack * s;
                if (miss.sqrMagnitude > slack * slack)
                {
                    // The hands are where the body really is: they slip on the rope rather than press into a floor or a ceiling.
                    Vector3 to = transform.position + Quaternion.Euler(0f, _yaw, 0f) * offset - anchor;
                    float far = to.magnitude;
                    if (far > 1e-4f * s) _dir = to / far;
                    _radius = Mathf.Clamp(far, least, length);
                    // The speed into what it met is lost, and a fast one comes back out: the toy kicks off it.
                    Vector3 n = miss.normalized;
                    float into = Vector3.Dot(_velocity, n);
                    if (into > 0f)
                    {
                        _velocity -= n * into;
                        if (into > settings.bounceFrom * s) _velocity -= n * (into * settings.bumpBounce);
                    }
                    _velocity = Vector3.ProjectOnPlane(_velocity, _dir);
                }
            }

            // 4. Up and down the rope: higher = a shorter, quicker swing; lower = a longer, wider one.
            if (_input.Sprint) _radius -= settings.climbSpeed * s * dt;
            else if (_input.CrouchHeld) _radius += settings.slideSpeed * s * dt;
            _radius = Mathf.Clamp(_radius, least, length);

            // 5. Facing: where the stick pushes, held when it is let go. Not the travel (the body would spin at each end
            // of the swing) and not the camera (looking round would swing the body round the rope).
            Vector3 stick = StickWorld();
            if (stick.magnitude > FacingStick)
                _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(stick.x, stick.z) * Mathf.Rad2Deg, settings.turnSpeed * dt);

            // 6. The swing, in short slices: a share of the player's own gravity and the stick's push, across the rope.
            Vector3 force = Vector3.down * (_motor.Gravity * settings.gravityShare) + stick * (settings.push * s);
            for (float left = dt; left > 0f; left -= MaxSwingStep) Swing(force, Mathf.Min(left, MaxSwingStep));

            // 7. Where the feet go: under the hands. Getting on, the body is eased there from where it was.
            Vector3 feet = anchor + _dir * _radius - Quaternion.Euler(0f, _yaw, 0f) * offset;
            if (since < settle) feet = Vector3.Lerp(_grabFrom, feet, Mathf.SmoothStep(0f, 1f, since / settle));

            // 8. The motor takes it there this frame (its capsule stops on what is in the way).
            Vector3 pull = Vector3.ClampMagnitude((feet - transform.position) / dt, settings.maxPullSpeed * s);
            _expected = transform.position + pull * dt;
            _motor.SetPull(pull);
            _motor.PullYaw = _yaw;
            _motor.PullOwnsJump = true;
            if (_stance != null)
            {
                // We run after PlayerMoves (which frees the stance every frame), so Crouch slides and never shrinks the
                // capsule; PlayerMoves takes the stance back the frame after the ride ends.
                _stance.Frozen = true;
                if (_stance.Crouched && _stance.StandFits()) _stance.Set(false);
            }
        }

        /// <summary>One slice of the pendulum, <paramref name="h"/> seconds long: the push across the rope, the damping,
        /// the stop at the widest angle (the speed that would carry it further out is dropped).</summary>
        private void Swing(Vector3 force, float h)
        {
            _velocity += Vector3.ProjectOnPlane(force, _dir) * h;
            _velocity /= 1f + settings.damping * h;
            Vector3 next = (_dir * _radius + _velocity * h).normalized;
            if (Vector3.Angle(Vector3.down, next) > settings.maxAngle)
            {
                next = Vector3.RotateTowards(Vector3.down, next, settings.maxAngle * Mathf.Deg2Rad, 0f);
                Vector3 outward = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(next, Vector3.up), next).normalized;
                _velocity -= Mathf.Max(0f, Vector3.Dot(_velocity, outward)) * outward;
            }
            _velocity = Vector3.ProjectOnPlane(_velocity, next);
            _dir = next;
        }

        /// <summary>Where the stick points on the ground, by the camera the motor steers by (as the walk reads it).</summary>
        private Vector3 StickWorld()
        {
            Vector2 move = _input.Move;
            Transform view = _motor.View;
            Vector3 forward = view != null ? Vector3.ProjectOnPlane(view.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return forward * move.y + right * move.x;
        }

        // ---------- letting go ----------

        /// <summary>Lets go keeping the swing's speed, with no lift (Jump is the let-go with a lift).</summary>
        public void LetGo() => End(Leave.Keep, "let go");

        /// <summary>The rope calls this when it is switched off or destroyed while held: the toy falls from where it is.</summary>
        public void RopeGone() => End(Leave.Drop, "rope gone");

        /// <summary>The one way a ride ends. Gives back everything the ride took: the motor's pull, the rope, the mark
        /// that fills the hands, the hands themselves (they fade off), and the Animator's state. The standing capsule
        /// goes back to PlayerMoves and the stance by itself the frame after. A tool put away and a glider folded at the
        /// grab stay that way, as after the turntable and the Dart Line.</summary>
        private void End(Leave how, string reason)
        {
            if (!Riding) return;
            Rope rope = Current;
            Riding = false;
            Current = null;
            _jumpPressed = false;
            float s = Scale;
            switch (how)
            {
                case Leave.Fling:
                    _motor.Fling(_velocity + Vector3.up * (settings.releaseLift * s), settings.flingSteer, settings.flingDrag);
                    break;
                case Leave.Keep:
                    // A fling without the lift: plain EndPull would hand the speed to the walk's air rule, which brakes
                    // it to walking speed in a fifth of a second.
                    _motor.Fling(_velocity, settings.flingSteer, settings.flingDrag);
                    break;
                case Leave.Drop:
                    if (_motor.Pull.HasValue) _motor.EndPull(Vector3.zero);
                    break;
            }
            if (rope != null) rope.SetHolder(null);   // at a scene's end the rope can be gone already
            _lastRope = rope;
            _releasedAt = Time.time;
            _mustLeave = true;
            _airFor = 0f;
            // The hands fade off along the line the rope had; the rope itself has left them.
            _handsOffAt = Time.time;
            _handsUp = -_dir;
            _handsSide = rope != null ? rope.Thickness : 0f;
            Riders.Remove(gameObject);
            LastLetGo = reason;
            GameEvents.RaiseRopeChanged(this, false);
            // Out of the hang into the air pose, which finds the fall or the landing by itself. A hit or a death has
            // the Animator already: left alone.
            if (_posed && _animation != null && _animation.Playing && (_moves == null || _moves.Free))
                _animation.ResumeInto(PlayerAnimation.Air);
            _posed = false;
        }

        // ---------- the hands (CarryPose asks) ----------

        public float CarryWeight
        {
            get
            {
                if (!isActiveAndEnabled) return 0f;
                float fade = Mathf.Max(0.01f, settings.handsFade);
                float k = Mathf.Clamp01(Riding ? (Time.time - GrabbedTime) / fade : 1f - (Time.time - _handsOffAt) / fade);
                return k * k * (3f - 2f * k);
            }
        }

        /// <summary>One hand above the other on the rope in front of the face: the right palm a little above the hand
        /// point and to the right of the rope, the left below and to its left, each palm facing the rope and the fingers
        /// pointing the way the body faces. False when not on a rope and the fade off it is over.</summary>
        public bool CarryGoals(out HandGoal right, out HandGoal left)
        {
            right = left = default;
            if (!Riding && Time.time - _handsOffAt >= settings.handsFade) return false;
            Vector3 up = Riding ? -_dir : _handsUp;
            float side = Riding && Current != null ? Current.Thickness : _handsSide;
            Vector3 at = HandPoint, across = transform.right, ahead = transform.forward;
            float half = settings.handsApart * 0.5f * Scale;
            right = new HandGoal { palm = at + up * half + across * side, fingers = ahead, normal = -across };
            left = new HandGoal { palm = at - up * half - across * side, fingers = ahead, normal = across };
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Riders.Clear();
    }
}
