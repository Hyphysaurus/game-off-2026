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
        private float _peakFall;            // the fastest fall since leaving the ground, in the player's own units a second
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
        /// <summary>Raised on landing with the fall speed at impact in the player's own units a second (the world speed over its size, taken as it fell).</summary>
        public event Action<float> Landed;

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
        private void OnJumpPressed() { _jumpPressedTime = Time.time; }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float scale = Scale;
            // A shift changes the body's size mid-motion, and its speeds change with it: a kid's speeds are not a toy's. Kept
            // at the old size, a shrink that lost the ground for a frame fell at a kid's speed, which the fall rule read as a
            // toy's fatal fall, and the player went back to the checkpoint (D-0041).
            if (_lastScale > 0f && !Mathf.Approximately(scale, _lastScale))
            {
                float k = scale / _lastScale;
                _planar *= k;
                _vertical *= k;
            }
            _lastScale = scale;

            // Where the stick points, on the ground, relative to the camera.
            Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 move = SteerLocked || Drive.HasValue ? Vector2.zero : input.Move;
            Vector3 wish = forward * move.y + right * move.x;
            bool steering = wish.sqrMagnitude > 0.0001f;

            // Horizontal speed eases toward the target instead of snapping: that is the "weight".
            float topSpeed = Mathf.Min(input.Sprint && !SprintLocked ? settings.sprintSpeed : settings.walkSpeed, SpeedCap) * SpeedShare * scale;
            float rate = (steering ? settings.acceleration : settings.deceleration) * scale;
            if (!Grounded) rate *= settings.airControl;
            _planar = Drive ?? Vector3.MoveTowards(_planar, wish * topSpeed, rate * dt);

            if (FaceYaw.HasValue)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, FaceYaw.Value, 0f), settings.turnSpeed * dt);
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
            if (_jumping && !_jumpCut && _vertical > 0f && !input.JumpHeld)
            {
                _vertical *= settings.jumpCutMultiplier;
                _jumpCut = true;
            }

            _vertical = Mathf.Max(_vertical - gravity * dt, -settings.maxFallSpeed * scale);
            // Wind, after gravity, so a column only lifts when it blows harder than gravity pulls.
            if (_windFrame >= Time.frameCount - 1)
            {
                Vector3 v = Vector3.MoveTowards(_planar + Vector3.up * _vertical, _windTarget, _windAccel * dt);
                _planar = new Vector3(v.x, 0f, v.z); _vertical = v.y;
                if (_vertical > 0f) { _jumping = false; _jumpCut = true; _lastGroundedTime = -10f; _peakFall = 0f; }
            }
            // A fall counts off the ground only (the grounded push down is not a fall), in the player's own units as it happens.
            if (!Grounded && _vertical < 0f) _peakFall = Mathf.Max(_peakFall, -_vertical / scale);

            CollisionFlags flags = _controller.Move((_planar + Vector3.up * _vertical) * dt);
            if ((flags & CollisionFlags.Above) != 0 && _vertical > 0f) _vertical = 0f;

            bool wasGrounded = Grounded;
            Grounded = _controller.isGrounded;
            if (Grounded)
            {
                _lastGroundedTime = Time.time;
                _jumping = false;
                if (!wasGrounded)
                {
                    Landed?.Invoke(_peakFall);
                    _peakFall = 0f;
                }
            }

            if (animator != null)
            {
                // 0 = still, 0.5 = full walk, 1 = full sprint: the blend tree's thresholds.
                float planarSpeed = new Vector3(_planar.x, 0f, _planar.z).magnitude / scale;
                float walk = Mathf.Max(0.01f, settings.walkSpeed);
                float sprint = Mathf.Max(walk + 0.01f, settings.sprintSpeed);
                float speed01 = planarSpeed <= walk
                    ? 0.5f * planarSpeed / walk
                    : 0.5f + 0.5f * Mathf.InverseLerp(walk, sprint, planarSpeed);
                animator.SetFloat(SpeedHash, Mathf.Clamp01(speed01), 0.08f, dt);
                // The capsule's isGrounded blinks off for a frame on steps and slopes; the Animator gets a steadier answer.
                // A jump clears _lastGroundedTime, so the grace never delays a jump's take-off.
                bool steadyGrounded = Grounded || Time.time - _lastGroundedTime <= settings.groundedGrace;
                animator.SetBool(GroundedHash, steadyGrounded);
                animator.SetFloat(VerticalHash, _vertical / scale);
            }
        }

        /// <summary>Moves the player somewhere else with no travel and no leftover momentum. Used by spawns and portals.</summary>
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
            _peakFall = 0f;
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
                _peakFall = 0f;
            }
        }

        public void Warp(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
            _planar = Vector3.zero;
            _vertical = 0f;
            _peakFall = 0f;
            _jumpPressedTime = -10f;
            _jumping = false;
            Warped?.Invoke(position, rotation);
        }
    }
}
