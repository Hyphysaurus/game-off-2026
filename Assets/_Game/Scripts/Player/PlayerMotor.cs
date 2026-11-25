using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Turns intents into movement of the capsule: camera-relative walking and sprinting with acceleration,
    /// a jump with coyote time and a buffer, gravity, and the numbers the Animator reads.
    /// Every speed, height and gravity is multiplied by the Shifter's scale, so a toy-sized player
    /// moves at toy speed and still jumps with the same timing as a kid-sized one.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int VerticalHash = Animator.StringToHash("VerticalVelocity");
        private static readonly int AirPhaseHash = Animator.StringToHash("AirPhase");
        // The air pose follows the jump (D-0169): the Air state shows its clip at the time AirPhase says, not at the clock's.
        private const float AirPhaseRate = 4f;       // most the phase moves a second once the air pose shows: a cut jump, a glider or a line opened in mid-jump catches up in a few frames without a pop
        private const float AirPhaseFloor = 0.18f;   // least phase when no jump is under way (a pad, a knock, a fling off a line): just airborne, not the planted push-off
        private const float AirPhaseHeld = 0.5f;     // gliding or on a line: the top of the arc, neither going up nor coming down
        private const float LeastTakeOff = 0.01f;    // own units a second: a jump height of 0 in the settings must not divide by 0

        [SerializeField] private MovementSettings settings;
        [SerializeField] private PlayerInput input;
        [SerializeField, Tooltip("Movement is relative to this camera's facing. Main Camera if empty.")]
        private Transform cameraTransform;
        [SerializeField, Tooltip("Found in children if empty. Reads Speed (0..1), Grounded, VerticalVelocity and AirPhase (0 take-off, 0.5 the top, 1 landing).")]
        private Animator animator;
        [SerializeField, Tooltip("Optional. Without one the player is always kid size.")]
        private Shifter shifter;

        private CharacterController _controller;
        private Vector3 _planar;            // horizontal velocity, world space
        private float _vertical;            // vertical speed, positive up
        private float _lastScale;           // the size the speeds above were last moving at
        private float _lastGroundedTime = -10f;
        private float _jumpPressedTime = -10f;
        private bool _jumping;              // a jump started and has not landed yet
        private bool _jumpCut;              // this jump was already shortened by an early release
        private Vector3 _windTarget; private float _windAccel; private int _windFrame = -10;   // this frame's wind, from an Updraft
        private float _airPhase;            // where the air pose is in the jump's arc: 0 take-off, 0.5 the top, 1 landing
        private bool _toldGrounded = true;  // what the Animator was last told of the ground (the controller's Grounded starts true)
        private Animator _airPhaseFor;      // the Animator the answer below was worked out for (the body can be swapped)
        private bool _hasAirPhase;          // that Animator lists AirPhase (a controller built before D-0169 does not)
        private bool _flung;                // flung off a line (Fling) and not landed yet: the sideways speed is its own, not the walk's
        private float _flingSteer;          // own units a second per second the stick may bend that flight by
        private float _flingDrag;           // per second: how fast that flight's sideways speed wears off

        public bool Grounded { get; private set; }
        public Vector3 Velocity => _planar + Vector3.up * _vertical;
        /// <summary>Gravity at the current size, world units per second squared (pads work out their throws from it).</summary>
        public float Gravity => settings.gravity * Scale;
        /// <summary>The current shift scale; 1 at kid size.</summary>
        public float Scale => shifter != null ? shifter.Scale : 1f;
        /// <summary>Raised when a jump starts.</summary>
        public event Action Jumped;
        /// <summary>Raised at the end of every Warp (respawn, portal, console tp): whoever holds the player elsewhere (first
        /// person) follows it instead of undoing it.</summary>
        public event Action<Vector3, Quaternion> Warped;
        /// <summary>Raised on landing with the speed it came down at, in the player's own units a second (the world speed over
        /// its size at the moment it touched down). A free fall lands at its fastest; a glide lands at its sink (D-0096).</summary>
        public event Action<float> Landed;
        /// <summary>The speed of the last landing, own units a second (what Landed said).</summary>
        public float LandingSpeed { get; private set; }
        /// <summary>Gravity in the player's own units (MovementSettings.gravity): the fall rule turns a height into a landing speed with it.</summary>
        public float OwnGravity => settings.gravity;
        /// <summary>The paper plane's numbers while it is open under the toy (D-0095; the Glider sets it, null = not gliding):
        /// a fixed sink and a forward glide the stick steers, in place of gravity and the walk.</summary>
        public GlideSettings Glide { get; set; }
        /// <summary>Asked on a Jump press in the air (past the coyote time) and on every press while gliding. True: the press
        /// opened or folded the glider and is no jump. The Glider sets it.</summary>
        public Func<bool> AirJump { get; set; }

        // What the moves ask of the motor (PlayerStance and PlayerMoves write these; left alone they change nothing).
        /// <summary>Top speed in the player's own units a second (a crouch); infinity = the motor's own walk and sprint.</summary>
        public float SpeedCap { get; set; } = float.PositiveInfinity;
        /// <summary>A share of the walk, sprint and crouch speeds: a heavy gun in the hands is a little slower (the
        /// WeaponHolder sets it). 1 = all of it.</summary>
        public float SpeedShare { get; set; } = 1f;
        /// <summary>No jump: no headroom over a crouch, or a move is under way. A press meanwhile is dropped.</summary>
        public bool JumpLocked { get; set; }
        /// <summary>True while a Jump press is still remembered and waiting for the ground (the jump buffer). A roll that
        /// starts by itself out of a landing gives way to it, so that press hops as it always did (D-0171).</summary>
        public bool JumpWaiting => Time.time - _jumpPressedTime <= settings.jumpBuffer;
        /// <summary>The stick is ignored: the body stands its ground (a block, a strike, a hit, dead).</summary>
        public bool SteerLocked { get; set; }
        /// <summary>Sprint is ignored (aim is held).</summary>
        public bool SprintLocked { get; set; }
        /// <summary>The sideways velocity is this, world units a second, whatever the stick says (a dodge roll).</summary>
        public Vector3? Drive { get; set; }
        /// <summary>The body turns to this heading instead of the way it travels (aiming: it steps sideways and backwards).</summary>
        public float? FaceYaw { get; set; }
        /// <summary>The body IS at this heading, at once, every step: first person on the player itself (D-0194), where the
        /// body faces wherever the eyes look and steps sideways and backwards under them. Before Face Yaw; after a grip's
        /// Hold Yaw. A line that has the body (a rope) still turns it its own way.</summary>
        public float? ViewYaw { get; set; }
        /// <summary>Share of the top speed kept while aiming (D-0141; PlayerMoves sets it). 1 = full speed.</summary>
        public float AimShare { get; set; } = 1f;

        /// <summary>Held at a grip (D-0156, a Grabbable): the body is moved to this point each step and faces Hold Yaw; the
        /// stick goes to the held thing, not the walk. Before every other rule (a roll's Drive, an aim's facing).</summary>
        public Vector3? HoldAt { get; set; }
        public float HoldYaw { get; set; }
        /// <summary>The body's sideways velocity, world units a second.</summary>
        public Vector3 PlanarVelocity => _planar;
        /// <summary>The fastest the body runs, own units a second (the legs' playback is worked out against it).</summary>
        public float SprintSpeed => settings.sprintSpeed;
        /// <summary>The walk's top speed, own units a second.</summary>
        public float WalkSpeed => settings.walkSpeed;
        /// <summary>How high a full jump goes, own units.</summary>
        public float JumpHeight => settings.jumpHeight;
        /// <summary>The fastest the body falls, own units a second.</summary>
        public float MaxFallSpeed => settings.maxFallSpeed;
        /// <summary>How fast a shoved loose thing is set moving, own units a second.</summary>
        public float PushSpeed => settings.pushSpeed;
        /// <summary>The camera the stick is read against.</summary>
        public Transform View => cameraTransform;
        /// <summary>A Flat Zone holds the body to a plane (D-0198): the stick's depth part, the sideways speed along the
        /// plane's normal and any drift off it are taken away. Null = free, as always. The zone sets it every frame it
        /// holds the body. The moment it comes on or goes off, a stick that is held is kept (see <see cref="Stick"/>).</summary>
        public FlatLock? Flat
        {
            get => _flat;
            set
            {
                bool was = _flat.HasValue;
                _flat = value;
                if (value.HasValue) _keepStickCos = value.Value.keepStick > 0f ? Mathf.Cos(value.Value.keepStick * Mathf.Deg2Rad) : NeverKept;
                if (was != value.HasValue) KeepStick();
            }
        }
        /// <summary>A vector on the ground with a Flat Zone's depth part taken away; unchanged with no zone.</summary>
        public Vector3 FlatWish(Vector3 wish) => _flat.HasValue ? _flat.Value.Flatten(wish) : wish;

        private FlatLock? _flat;
        // The kept stick (D-0198). The stick is read against the camera, and a Flat Zone's camera turns a quarter round
        // on the way in and on the way out: W held on the way in would fade to a stop, D held at the finish would turn
        // into "south" as the camera goes back behind the toy. So a stick that is held at that moment keeps meaning
        // the way the player was going, until it is let go or moved by more than the zone's Keep Stick Angle.
        private const float NeverKept = 2f;            // a cosine no stick can reach: nothing is kept (and with no zone ever met)
        private const float KeepStickAtLeast = 0.3f;   // the stick must be pushed this far to be kept, and to stay kept
        private float _keepStickCos = NeverKept;       // the cosine of the last zone's Keep Stick Angle
        private bool _stickKept;                       // a stick is being kept now
        private Vector2 _keptStick;                    // where the stick itself pointed then (unit)
        private Vector3 _keptWay;                      // and what it meant in the world (unit, on the ground)

        /// <summary>Where the stick points on the ground, by the camera: forward is the way the camera looks.</summary>
        private Vector3 CameraWish(Vector2 move)
        {
            Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return forward * move.y + right * move.x;
        }

        /// <summary>Where the stick (<paramref name="move"/>) points, in the world on the ground, as the motor steers by
        /// it: relative to its camera; in a Flat Zone without the depth part; and a stick held through a zone's start
        /// or end keeps the way it had. The walk and the rope both read the stick here. With no zone it is the
        /// camera's answer, as it always was.</summary>
        public Vector3 Stick(Vector2 move)
        {
            if (_stickKept)
            {
                float push = move.magnitude;
                // First person steers by the eyes at once; a stick let go or moved is read afresh.
                if (ViewYaw.HasValue || push < KeepStickAtLeast || Vector2.Dot(move / push, _keptStick) < _keepStickCos) _stickKept = false;
                else return _keptWay * push;
            }
            return FlatWish(CameraWish(move));
        }

        /// <summary>A Flat Zone's hold came on or went off: the camera has not moved yet, so what the stick means right now
        /// is what the player means. Kept if it is pushed, and (coming on) if it has a real part along the lane. A way
        /// that is already being kept stays the one kept (W held all the way through a zone still means "onward" at
        /// its end, where the side-on camera would read it as "into the wall").</summary>
        private void KeepStick()
        {
            if (_keepStickCos > 1f || input == null) { _stickKept = false; return; }
            Vector2 move = input.Move;
            float pushSquared = move.sqrMagnitude;
            if (pushSquared < KeepStickAtLeast * KeepStickAtLeast) { _stickKept = false; return; }
            bool was = _stickKept;
            Vector3 wish = Stick(move);   // a way still kept, else the camera's (its length is the stick's push, less what a zone took out)
            bool same = was && _stickKept;
            if (_flat.HasValue) wish = _flat.Value.Flatten(wish);
            if (wish.sqrMagnitude < 0.25f * pushSquared) { _stickKept = false; return; }   // pushed mostly into the depth: nothing along the lane to keep
            _keptWay = wish.normalized;
            if (!same) _keptStick = move.normalized;   // a way already kept keeps the stick it was kept for
            _stickKept = true;
        }

        /// <summary>A Flat Zone: a driven move (a dodge roll) goes along the lane with all of its speed. One aimed mostly
        /// into the depth (the roll takes its way from the camera, and the camera is side-on) goes the way the kept
        /// stick points, else the way the body was already going, else the way it faces.</summary>
        private Vector3 DriveAlongLane(FlatLock hold, Vector3 drive, Vector3 goingBefore)
        {
            drive.y = 0f;
            float speed = drive.magnitude;
            Vector3 along = hold.Flatten(drive);
            if (along.sqrMagnitude >= 0.25f * speed * speed) return along.normalized * speed;
            Vector3 hint = _stickKept ? _keptWay : hold.Flatten(goingBefore).sqrMagnitude > 1e-4f * speed * speed ? goingBefore : transform.forward;
            return hold.Along(hint) * speed;
        }

        private const float SteerLeastSpeed = 0.5f;   // own units a second: slower than this the body is starting, not turning

        /// <summary>The sideways speed after this step, on the ground with the stick pushed (D-0179): the way the body
        /// travels turns toward the stick at Steer Turn Speed and how fast it goes moves toward the stick's speed, each
        /// on its own, so a corner is an arc that keeps its speed. Before, the speed cut straight across to the new
        /// direction: a square corner at a sprint took 0.27 s, sagged to 71 % of the speed on the way and showed the
        /// legs running sideways; now it takes 0.13 s at full speed. Unchanged: a start from rest (straight off, at
        /// once), a turn-about sharper than Steer Reverse Angle (it brakes through a stop and comes back), letting go
        /// of the stick, and everything in the air. Steer Turn Speed 0 = the old rule everywhere.</summary>
        private Vector3 Steered(Vector3 wish, float topSpeed, float rate, float scale, float dt)
        {
            Vector3 straight = Vector3.MoveTowards(_planar, wish * topSpeed, rate * dt);
            float speed = _planar.magnitude, push = wish.magnitude;
            if (settings.steerTurnSpeed <= 0f || speed < SteerLeastSpeed * scale || push < 1e-4f) return straight;
            Vector3 heading = _planar / speed, wanted = wish / push;
            if (Vector3.Angle(heading, wanted) > settings.steerReverseAngle) return straight;
            Vector3 turned = Vector3.RotateTowards(heading, wanted, settings.steerTurnSpeed * Mathf.Deg2Rad * dt, 0f);
            turned.y = 0f;
            if (turned.sqrMagnitude < 1e-8f) return straight;
            return turned.normalized * Mathf.MoveTowards(speed, push * topSpeed, rate * dt);
        }

        /// <summary>Steer by this camera from now on (D-0111: the online player meets its camera after Awake).</summary>
        public void UseCamera(Transform view) => cameraTransform = view;
        /// <summary>A line pulls the body (D-0109, the Dart Line: reeled up to a target, or riding a zip line): its whole
        /// velocity is this, world units a second, in place of the stick, gravity and jumping. Null = not pulled. Its
        /// landings are not falls. End it with EndPull, which sets the velocity it lets go with, or with Fling (a rope).</summary>
        public Vector3? Pull { get; private set; }
        /// <summary>While pulled the body faces this heading instead of the way it travels (D-0170, a rope: a swing goes
        /// back and forth, and a body that turned to its travel would spin round at each end). Null = face the travel
        /// (the Dart Line). Cleared when the pull ends.</summary>
        public float? PullYaw { get; set; }
        /// <summary>While pulled, a Jump press belongs to whoever pulls (a rope lets go on it): it is no jump, it is not
        /// remembered, and the glider is not asked, so the press that lets go does not also open the plane. Cleared
        /// when the pull ends.</summary>
        public bool PullOwnsJump { get; set; }
        public void SetPull(Vector3 velocity) { Pull = velocity; _flung = false; }
        /// <summary>The line lets go: the body moves on at <paramref name="velocity"/> (world units a second).</summary>
        public void EndPull(Vector3 velocity)
        {
            Pull = null;
            PullYaw = null; PullOwnsJump = false; _flung = false;
            _planar = new Vector3(velocity.x, 0f, velocity.z);
            _vertical = velocity.y;
            _jumping = false; _jumpCut = true; _lastGroundedTime = -10f; _jumpPressedTime = -10f;
        }
        /// <summary>The line lets go as a fling (D-0170, a rope let go on a swing): EndPull, and until the next landing the
        /// sideways speed it left with is its own. WHY: the walk's air rule moves the sideways speed toward stick x walk
        /// speed at 25 to 30 a second per second, so a fling of 12 would be at walking speed in a fifth of a second.
        /// <paramref name="steer"/>: own units a second per second the stick may bend the flight by (never faster than
        /// it left, or than a sprint); <paramref name="drag"/>: per second, how fast the speed wears off. The fall is
        /// the motor's own, so the landing is judged like any other (the fall rule, the roll out of a landing). It ends
        /// on landing, and when something else takes the speed over: the glider, a grip, a pad, a warp, another line.</summary>
        public void Fling(Vector3 velocity, float steer, float drag)
        {
            EndPull(velocity);
            _flung = true;
            _flingSteer = Mathf.Max(0f, steer);
            _flingDrag = Mathf.Max(0f, drag);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (shifter == null) shifter = GetComponent<Shifter>();
            if (cameraTransform == null) { Camera view = PlayerSlot.ViewFor(this); if (view != null) cameraTransform = view.transform; }   // a party player's own camera (D-0110)
            if (animator != null) animator.applyRootMotion = false;
            if (settings != null && settings.propGravity > 0f) Physics.gravity = Vector3.down * settings.propGravity;
            _lastScale = Scale;
        }

        /// <summary>Walking into a loose prop shoves it: a velocity change along the way we were moving, capped at the push
        /// speed for our size, only if the prop's weight class allows this size. Standing on it is not a push.</summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.rigidbody;
            if (body == null || body.isKinematic || hit.moveDirection.y < -0.3f) return;
            if (!body.TryGetComponent(out Pushable pushable)) return;
            if (!pushable.PushableBy(shifter != null ? shifter.Size : ShiftSize.Kid)) return;
            Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (dir.sqrMagnitude < 0.001f) return;
            dir.Normalize();
            float want = Mathf.Min(_planar.magnitude, settings.pushSpeed * Scale);
            float have = Vector3.Dot(body.linearVelocity, dir);
            if (have < want) body.AddForce(dir * (want - have), ForceMode.VelocityChange);
        }

        private void OnEnable() { input.Jump += OnJumpPressed; }
        private void OnDisable() { input.Jump -= OnJumpPressed; _flung = false; }   // parked (a car seat, first person): whatever comes after is not that fling

        private void OnJumpPressed()
        {
            // On a line whose owner takes Jump (a rope lets go on it, D-0170): no jump is remembered and the glider is not asked.
            if (Pull.HasValue && PullOwnsJump) { _jumpPressedTime = -10f; return; }
            // In the air past the coyote time, or while gliding, the press may be the glider's (it opens or folds it): no jump then.
            bool inAir = !Grounded && Time.time - _lastGroundedTime > settings.coyoteTime;
            if ((inAir || Glide != null) && AirJump != null && AirJump()) { _jumpPressedTime = -10f; return; }
            _jumpPressedTime = Time.time;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float scale = Scale;
            // A shift changes the body's size mid-motion, and its speeds change with it: a kid's speeds are not a toy's. Kept
            // at the old size, a shrink that lost the ground for a frame fell at a kid's speed, which the fall rule read as a
            // toy's fatal fall, and the player went back to the checkpoint (D-0041). The direction is kept: a kid's leap
            // becomes a toy-sized leap and a toy's hop a kid-sized one.
            float pivot = 0f;
            if (_lastScale > 0f && !Mathf.Approximately(scale, _lastScale))
            {
                float k = scale / _lastScale;
                _planar *= k;
                _vertical *= k;
                // A shift in the air keeps the body's middle where it is (D-0094): the toy appears where the kid's chest was
                // and the kid grows round the toy. The feet move by what the middle would have; near a floor the capsule's
                // own move stops them on it, and the kid stands there instead.
                if (shifter != null && shifter.AroundMiddle) pivot = -_controller.center.y * (scale - _lastScale);
            }
            _lastScale = scale;

            if (Pull.HasValue)
            {
                // On the line: it moves the body, nothing else does. Facing the heading its owner asks for (a rope), or
                // the way it travels, across the ground.
                Vector3 v = Pull.Value;
                Vector3 pullFix = Vector3.zero;
                if (_flat.HasValue) { v = _flat.Value.Flatten(v); pullFix = _flat.Value.Correction(transform.position, dt); }   // a Flat Zone: the line pulls along the lane only
                _planar = new Vector3(v.x, 0f, v.z); _vertical = v.y;
                if (PullYaw.HasValue) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, PullYaw.Value, 0f), settings.turnSpeed * dt);
                else if (_planar.sqrMagnitude > 1e-4f * scale * scale) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_planar, Vector3.up), settings.turnSpeed * dt);
                _controller.Move(v * dt + Vector3.up * pivot + pullFix);
                Grounded = false;
                _jumping = false; _jumpCut = true; _lastGroundedTime = -10f; _jumpPressedTime = -10f;
                if (animator != null)
                {
                    animator.SetFloat(SpeedHash, 0f, 0.08f, dt); animator.SetBool(GroundedHash, false); animator.SetFloat(VerticalHash, 0f);
                    WriteAirPhase(toldGrounded: false, held: true, scale, dt);
                }
                return;
            }

            // Where the stick points, on the ground, relative to the camera (Stick: in a Flat Zone, along its lane only).
            // Read even while the stick is locked out, so a stick kept through a zone's start is not dropped by a hit.
            Vector3 wish = Stick(input.Move);
            if (SteerLocked || Drive.HasValue || HoldAt.HasValue) wish = Vector3.zero;
            bool steering = wish.sqrMagnitude > 0.0001f;
            // A Flat Zone holds the body unless a grip has it (the grip's rule comes before every other).
            bool flat = _flat.HasValue && !HoldAt.HasValue;
            Vector3 goingBefore = _planar;

            // A fling is over once the plane or a grip has the sideways speed: after them the walk's rule is back.
            if (_flung && (Glide != null || HoldAt.HasValue)) _flung = false;
            if (HoldAt.HasValue)
            {
                Vector3 gap = HoldAt.Value - transform.position; gap.y = 0f;
                _planar = Vector3.ClampMagnitude(gap / Mathf.Max(dt, 1e-4f), 30f * scale);   // at the grip, walking round with the thing
            }
            else if (Glide != null && !Drive.HasValue) _planar = GlidePlanar(wish, steering, scale, dt);
            else if (_flung && !Grounded && !Drive.HasValue)
            {
                // Flung off a line (D-0170): the speed it left with is kept. The stick bends the path but never makes it
                // faster than it already is (or than a sprint), and the speed wears off slowly. With the stick locked
                // (a hit) wish is zero, and the body flies on its arc.
                float most = Mathf.Max(_planar.magnitude, settings.sprintSpeed * scale);
                _planar = Vector3.ClampMagnitude(_planar + wish * (_flingSteer * scale * dt), most) / (1f + _flingDrag * dt);
            }
            else
            {
                // Horizontal speed eases toward the target instead of snapping: that is the "weight".
                float topSpeed = Mathf.Min(input.Sprint && !SprintLocked ? settings.sprintSpeed : settings.walkSpeed, SpeedCap) * SpeedShare * AimShare * scale;
                float rate = (steering ? settings.acceleration : settings.deceleration) * scale;
                if (!Grounded) rate *= settings.airControl;
                if (Drive.HasValue) _planar = flat ? DriveAlongLane(_flat.Value, Drive.Value, goingBefore) : Drive.Value;
                else if (Grounded && steering) _planar = Steered(wish, topSpeed, rate, scale, dt);
                else _planar = Vector3.MoveTowards(_planar, wish * topSpeed, rate * dt);
            }

            if (HoldAt.HasValue)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, HoldYaw, 0f), settings.turnSpeed * dt);
            else if (ViewYaw.HasValue)
                transform.rotation = Quaternion.Euler(0f, ViewYaw.Value, 0f);
            else if (FaceYaw.HasValue)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, FaceYaw.Value, 0f), settings.turnSpeed * dt);
            else if (Glide != null && _planar.sqrMagnitude > 1e-6f * scale * scale)   // on the plane: facing the way it flies
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_planar, Vector3.up), settings.turnSpeed * dt);
            else if (flat && Drive.HasValue && _planar.sqrMagnitude > 1e-6f * scale * scale)   // a Flat Zone: a roll faces the way it goes along the lane
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_planar, Vector3.up), settings.turnSpeed * dt);
            else if (steering)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wish, Vector3.up), settings.turnSpeed * dt);

            // Vertical: a small downward push while grounded keeps isGrounded steady on slopes and steps.
            float gravity = settings.gravity * scale;
            if (Grounded && _vertical < 0f) _vertical = -2f * scale;

            bool mayJump = Time.time - _lastGroundedTime <= settings.coyoteTime;
            bool wantsJump = Time.time - _jumpPressedTime <= settings.jumpBuffer;
            if (JumpLocked) _jumpPressedTime = -10f;
            else if (mayJump && wantsJump)
            {
                _vertical = Mathf.Sqrt(2f * gravity * settings.jumpHeight * scale);
                _jumpPressedTime = -10f;
                _lastGroundedTime = -10f;
                _jumping = true;
                _jumpCut = false;
                Jumped?.Invoke();
            }

            // Let go of Jump early and the rise is cut short: a tap hops, a hold clears the full height.
            if (_jumping && !_jumpCut && _vertical > 0f && !input.JumpHeld && Glide == null)
            {
                _vertical *= settings.jumpCutMultiplier;
                _jumpCut = true;
            }

            // On the plane the fall is brought to its steady sink (and a rise braked to it), in place of gravity.
            if (Glide != null) _vertical = Mathf.MoveTowards(_vertical, -Glide.sinkSpeed * GlideSinkShare * scale, Glide.brake * scale * dt);
            else _vertical = Mathf.Max(_vertical - gravity * dt, -settings.maxFallSpeed * scale);
            // Wind, after gravity, so a column only lifts when it blows harder than gravity pulls.
            if (_windFrame >= Time.frameCount - 1)
            {
                Vector3 v = Vector3.MoveTowards(_planar + Vector3.up * _vertical, _windTarget, _windAccel * dt);
                _planar = new Vector3(v.x, 0f, v.z); _vertical = v.y;
                if (_vertical > 0f) { _jumping = false; _jumpCut = true; _lastGroundedTime = -10f; }
            }

            // A Flat Zone: nothing moves the body along the plane's normal (a knock, a fling, wind, the glider's heading),
            // and any drift off the plane is eased away (never faster than the zone's snap speed).
            Vector3 flatFix = Vector3.zero;
            if (flat) { _planar = _flat.Value.Flatten(_planar); flatFix = _flat.Value.Correction(transform.position, dt); }
            CollisionFlags flags = _controller.Move((_planar + Vector3.up * _vertical) * dt + Vector3.up * pivot + flatFix);
            if ((flags & CollisionFlags.Above) != 0 && _vertical > 0f) _vertical = 0f;
            // A fling that meets a wall keeps only what the wall let through (the walk re-aims its speed every frame; a
            // fling does not, and would push into the wall all the way down).
            if (_flung && (flags & CollisionFlags.Sides) != 0)
            {
                Vector3 went = _controller.velocity;
                _planar = Vector3.ClampMagnitude(new Vector3(went.x, 0f, went.z), _planar.magnitude);
            }

            bool wasGrounded = Grounded;
            Grounded = _controller.isGrounded;
            if (Grounded)
            {
                _lastGroundedTime = Time.time;
                _jumping = false;
                _flung = false;   // a fling ends where it lands (before Landed is raised: a roll out of the landing takes the speed from here)
                if (!wasGrounded)
                {
                    // The speed it came down at, in its own units (D-0096). A free fall gathers speed all the way down, so this
                    // is its fastest, as the fall rule always read; a glide comes down at its sink.
                    LandingSpeed = Mathf.Max(0f, -_vertical / scale);
                    Landed?.Invoke(LandingSpeed);
                }
            }

            if (animator != null)
            {
                // 0 = still, 0.5 = full walk, 1 = full sprint: the blend tree's thresholds. On the plane: standing still on it.
                float planarSpeed = Glide != null ? 0f : new Vector3(_planar.x, 0f, _planar.z).magnitude / scale;
                float walk = Mathf.Max(0.01f, settings.walkSpeed);
                float sprint = Mathf.Max(walk + 0.01f, settings.sprintSpeed);
                float speed01 = planarSpeed <= walk
                    ? 0.5f * planarSpeed / walk
                    : 0.5f + 0.5f * Mathf.InverseLerp(walk, sprint, planarSpeed);
                animator.SetFloat(SpeedHash, Mathf.Clamp01(speed01), 0.08f, dt);
                // The capsule's isGrounded blinks off for a frame on steps and slopes; the Animator gets a steadier answer.
                // A jump clears _lastGroundedTime, so the grace never delays a jump's take-off.
                // The rider stands on the plane: grounded as far as the Animator knows, so the crouch it rides in holds.
                bool steadyGrounded = Glide != null || Grounded || Time.time - _lastGroundedTime <= settings.groundedGrace;
                animator.SetBool(GroundedHash, steadyGrounded);
                animator.SetFloat(VerticalHash, Glide != null ? 0f : _vertical / scale);
                WriteAirPhase(steadyGrounded, held: Glide != null, scale, dt);
            }
        }

        /// <summary>
        /// Moves the air pose's phase and hands it to the Animator (D-0169), so the body pushes off, hangs at the top and
        /// reaches for the floor in step with the jump itself instead of holding one pose. Called wherever the Animator
        /// is told of the ground, with the same answer it was given (<paramref name="toldGrounded"/>): on the frame that
        /// answer turns to "in the air" the phase starts at where the jump is; after that it follows at no more than
        /// AirPhaseRate, so a cut jump or a bumped head catches up without a pop. On the ground it is left alone: the
        /// air pose is still blending out there and must not run backwards. <paramref name="held"/>: on the glider or on
        /// a line, where the body neither rises nor comes down: the phase goes to AirPhaseHeld, and at the same rate,
        /// because both can start in the middle of a jump with the air pose on screen (the line leaves the body in Air;
        /// the glider's crouch fades in over it).
        /// </summary>
        private void WriteAirPhase(bool toldGrounded, bool held, float scale, float dt)
        {
            bool lostGround = _toldGrounded && !toldGrounded;
            if (held || !toldGrounded)
            {
                float target = held ? AirPhaseHeld : AirPhaseTarget(scale);
                // Only the frame the ground is lost may jump: the air pose is not on screen yet. Anything later is walked to,
                // so opening the glider or firing the line in mid-jump does not pop the pose.
                _airPhase = lostGround ? target : Mathf.MoveTowards(_airPhase, target, AirPhaseRate * dt);
            }
            _toldGrounded = toldGrounded;
            if (ListsAirPhase()) animator.SetFloat(AirPhaseHash, _airPhase);
        }

        /// <summary>
        /// Where the body is in a jump's arc, from its vertical speed alone (own units, so a toy and a kid agree): 0 while it
        /// rises at a jump's take-off speed, 0.5 at the top, 1 when it comes down as fast as it took off. Anything that is
        /// not a jump (a pad, a knock, a fling off a line) can rise much faster and for longer: it never shows less than
        /// AirPhaseFloor, so a long rise holds a just-airborne pose and not the push-off with a foot planted.
        /// </summary>
        private float AirPhaseTarget(float scale)
        {
            float takeOff = Mathf.Max(LeastTakeOff, Mathf.Sqrt(Mathf.Max(0f, 2f * settings.gravity * settings.jumpHeight)));
            float target = Mathf.Clamp01((takeOff - _vertical / scale) / (2f * takeOff));
            return _jumping ? target : Mathf.Max(target, AirPhaseFloor);
        }

        /// <summary>
        /// True if the Animator in use has the AirPhase parameter. A controller built before D-0169 does not, and Unity
        /// logs a warning every frame for a parameter it does not know. Worked out once for each Animator (the list is
        /// only there once the Animator is running, so it is asked again until then), never by name each frame.
        /// </summary>
        private bool ListsAirPhase()
        {
            if (ReferenceEquals(animator, _airPhaseFor)) return _hasAirPhase;
            if (!animator.isInitialized) return false;
            _airPhaseFor = animator;
            _hasAirPhase = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.nameHash == AirPhaseHash && p.type == AnimatorControllerParameterType.Float) _hasAirPhase = true;
            return _hasAirPhase;
        }

        /// <summary>The glider's forward flight (D-0095): the heading turns toward where the stick points at the glide's turn
        /// rate, and the speed eases toward the glide speed (less with the stick at rest: a paper plane never stops in the air).</summary>
        private Vector3 GlidePlanar(Vector3 wish, bool steering, float scale, float dt)
        {
            GlideSettings g = Glide;
            Vector3 flat = new Vector3(_planar.x, 0f, _planar.z);
            float speed = flat.magnitude;
            Vector3 heading = speed > 0.05f * scale ? flat / speed : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (_flat.HasValue)
            {
                // A Flat Zone: the plane flies along the lane. A heading into the depth (opened facing the wall) would
                // be flattened to no speed at all, every frame: it takes the lane's way instead (the stick's, if pushed).
                Vector3 along = _flat.Value.Flatten(heading);
                heading = along.sqrMagnitude > 0.01f ? along.normalized : _flat.Value.Along(steering ? wish : heading);
            }
            if (steering)
            {
                float from = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg, to = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
                heading = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(from, to, g.turnRate * dt), 0f) * Vector3.forward;
            }
            float want = g.glideSpeed * (steering ? Mathf.Lerp(g.idleShare, 1f, Mathf.Clamp01(wish.magnitude)) : g.idleShare) * scale;
            if (GlideDiving) want = g.glideSpeed * g.diveSpeed * scale;
            else if (GlideFloating) want *= g.floatSpeed;
            // speeding up comes at the glide's acceleration; extra speed (a dive, a ring) bleeds off slowly: it carries
            float rate = (want >= speed ? g.acceleration : Mathf.Max(0.1f, g.slowDown)) * scale;
            return heading * Mathf.MoveTowards(speed, want, rate * dt);
        }

        /// <summary>Gliding and Sprint held: the nose down, faster and sinking faster (D-0104).</summary>
        public bool GlideDiving => Glide != null && input != null && input.Sprint;
        /// <summary>Gliding and Crouch held (not diving): slower, and a gentle sink.</summary>
        public bool GlideFloating => Glide != null && input != null && input.CrouchHeld && !input.Sprint;
        private float GlideSinkShare => GlideDiving ? Glide.diveSink : GlideFloating ? Glide.floatSink : 1f;

        /// <summary>Throws the player straight up as if from a spring: a pad calls this. Height is in the player's own
        /// units, so a toy and a kid get the same look. Not a jump: no jump cut, no buffer.</summary>
        public void Launch(float height)
        {
            float gravity = settings.gravity * Scale;
            _vertical = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, height) * Scale);
            _jumping = false;
            _jumpCut = true;
            _flung = false;             // a pad's throw is steered as it always was, not as a fling
            Grounded = false;
            _lastGroundedTime = -10f;   // no coyote jump on top of a launch
        }

        /// <summary>Wind for this frame: the velocity eases toward <paramref name="velocity"/> (world units, already scaled)
        /// at <paramref name="acceleration"/> per second. An Updraft calls this every frame the player is inside it.</summary>
        public void Blow(Vector3 velocity, float acceleration)
        {
            _windTarget = velocity; _windAccel = acceleration; _windFrame = Time.frameCount;
        }

        /// <summary>A hit's shove: adds a velocity (world units per second, already scaled by whoever hit). Any upward part
        /// lifts the player off the ground like a small launch; the sideways part fades with the usual deceleration.</summary>
        public void Knock(Vector3 velocity)
        {
            _planar += new Vector3(velocity.x, 0f, velocity.z);
            if (velocity.y > 0f)
            {
                _vertical = Mathf.Max(_vertical, velocity.y);
                _jumping = false;
                _jumpCut = true;
                Grounded = false;
                _lastGroundedTime = -10f;
            }
        }

        /// <summary>Moves the player somewhere else with no travel and no leftover momentum. Used by spawns and portals.</summary>
        public void Warp(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
            _planar = Vector3.zero;
            _vertical = 0f;
            _jumpPressedTime = -10f;
            _jumping = false;
            _flung = false;
            Warped?.Invoke(position, rotation);
        }
    }
}
