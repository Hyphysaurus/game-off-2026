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

        [SerializeField] private MovementSettings settings;
        [SerializeField] private PlayerInput input;
        [SerializeField, Tooltip("Movement is relative to this camera's facing. Main Camera if empty.")]
        private Transform cameraTransform;
        [SerializeField, Tooltip("Found in children if empty. Reads Speed (0..1), Grounded, VerticalVelocity.")]
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
        /// <summary>The stick is ignored: the body stands its ground (a block, a strike, a hit, dead).</summary>
        public bool SteerLocked { get; set; }
        /// <summary>Sprint is ignored (aim is held).</summary>
        public bool SprintLocked { get; set; }
        /// <summary>The sideways velocity is this, world units a second, whatever the stick says (a dodge roll).</summary>
        public Vector3? Drive { get; set; }
        /// <summary>The body turns to this heading instead of the way it travels (aiming: it steps sideways and backwards).</summary>
        public float? FaceYaw { get; set; }
        /// <summary>The body's sideways velocity, world units a second.</summary>
        public Vector3 PlanarVelocity => _planar;
        /// <summary>The fastest the body runs, own units a second (the legs' playback is worked out against it).</summary>
        public float SprintSpeed => settings.sprintSpeed;
        /// <summary>The camera the stick is read against.</summary>
        public Transform View => cameraTransform;
        /// <summary>A line pulls the body (D-0109, the Dart Line: reeled up to a target, or riding a zip line): its whole
        /// velocity is this, world units a second, in place of the stick, gravity and jumping. Null = not pulled. Its
        /// landings are not falls. End it with EndPull, which sets the velocity it lets go with.</summary>
        public Vector3? Pull { get; private set; }
        public void SetPull(Vector3 velocity) { Pull = velocity; }
        /// <summary>The line lets go: the body moves on at <paramref name="velocity"/> (world units a second).</summary>
        public void EndPull(Vector3 velocity)
        {
            Pull = null;
            _planar = new Vector3(velocity.x, 0f, velocity.z);
            _vertical = velocity.y;
            _jumping = false; _jumpCut = true; _lastGroundedTime = -10f; _jumpPressedTime = -10f;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (shifter == null) shifter = GetComponent<Shifter>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
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
        private void OnDisable() { input.Jump -= OnJumpPressed; }

        private void OnJumpPressed()
        {
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
                // On the line: it moves the body, nothing else does. Facing the way it travels, across the ground.
                Vector3 v = Pull.Value;
                _planar = new Vector3(v.x, 0f, v.z); _vertical = v.y;
                if (_planar.sqrMagnitude > 1e-4f * scale * scale) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(_planar, Vector3.up), settings.turnSpeed * dt);
                _controller.Move(v * dt + Vector3.up * pivot);
                Grounded = false;
                _jumping = false; _jumpCut = true; _lastGroundedTime = -10f; _jumpPressedTime = -10f;
                if (animator != null) { animator.SetFloat(SpeedHash, 0f, 0.08f, dt); animator.SetBool(GroundedHash, false); animator.SetFloat(VerticalHash, 0f); }
                return;
            }

            // Where the stick points, on the ground, relative to the camera.
            Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 move = SteerLocked || Drive.HasValue ? Vector2.zero : input.Move;
            Vector3 wish = forward * move.y + right * move.x;
            bool steering = wish.sqrMagnitude > 0.0001f;

            if (Glide != null && !Drive.HasValue) _planar = GlidePlanar(wish, steering, scale, dt);
            else
            {
                // Horizontal speed eases toward the target instead of snapping: that is the "weight".
                float topSpeed = Mathf.Min(input.Sprint && !SprintLocked ? settings.sprintSpeed : settings.walkSpeed, SpeedCap) * SpeedShare * scale;
                float rate = (steering ? settings.acceleration : settings.deceleration) * scale;
                if (!Grounded) rate *= settings.airControl;
                _planar = Drive ?? Vector3.MoveTowards(_planar, wish * topSpeed, rate * dt);
            }

            if (FaceYaw.HasValue)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, FaceYaw.Value, 0f), settings.turnSpeed * dt);
            else if (Glide != null && _planar.sqrMagnitude > 1e-6f * scale * scale)   // on the plane: facing the way it flies
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

            CollisionFlags flags = _controller.Move((_planar + Vector3.up * _vertical) * dt + Vector3.up * pivot);
            if ((flags & CollisionFlags.Above) != 0 && _vertical > 0f) _vertical = 0f;

            bool wasGrounded = Grounded;
            Grounded = _controller.isGrounded;
            if (Grounded)
            {
                _lastGroundedTime = Time.time;
                _jumping = false;
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
            }
        }

        /// <summary>The glider's forward flight (D-0095): the heading turns toward where the stick points at the glide's turn
        /// rate, and the speed eases toward the glide speed (less with the stick at rest: a paper plane never stops in the air).</summary>
        private Vector3 GlidePlanar(Vector3 wish, bool steering, float scale, float dt)
        {
            GlideSettings g = Glide;
            Vector3 flat = new Vector3(_planar.x, 0f, _planar.z);
            float speed = flat.magnitude;
            Vector3 heading = speed > 0.05f * scale ? flat / speed : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
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
            Warped?.Invoke(position, rotation);
        }
    }
}
