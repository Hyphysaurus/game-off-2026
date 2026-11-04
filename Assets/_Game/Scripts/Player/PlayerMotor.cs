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
        private float _peakFall;
        private float _lastGroundedTime = -10f;
        private float _jumpPressedTime = -10f;
        private bool _jumping;              // a jump started and has not landed yet
        private bool _jumpCut;              // this jump was already shortened by an early release

        public bool Grounded { get; private set; }
        public Vector3 Velocity => _planar + Vector3.up * _vertical;
        /// <summary>The current shift scale; 1 at kid size.</summary>
        public float Scale => shifter != null ? shifter.Scale : 1f;
        /// <summary>Raised when a jump starts.</summary>
        public event Action Jumped;
        /// <summary>Raised on landing with the fall speed at impact, in kid-size metres per second.</summary>
        public event Action<float> Landed;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (shifter == null) shifter = GetComponent<Shifter>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable() { input.Jump += OnJumpPressed; }
        private void OnDisable() { input.Jump -= OnJumpPressed; }
        private void OnJumpPressed() { _jumpPressedTime = Time.time; }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float scale = Scale;

            // Where the stick points, on the ground, relative to the camera.
            Vector3 forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 move = input.Move;
            Vector3 wish = forward * move.y + right * move.x;
            bool steering = wish.sqrMagnitude > 0.0001f;

            // Horizontal speed eases toward the target instead of snapping: that is the "weight".
            float topSpeed = (input.Sprint ? settings.sprintSpeed : settings.walkSpeed) * scale;
            float rate = (steering ? settings.acceleration : settings.deceleration) * scale;
            if (!Grounded) rate *= settings.airControl;
            _planar = Vector3.MoveTowards(_planar, wish * topSpeed, rate * dt);

            if (steering)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wish, Vector3.up), settings.turnSpeed * dt);

            // Vertical: a small downward push while grounded keeps isGrounded steady on slopes and steps.
            float gravity = settings.gravity * scale;
            if (Grounded && _vertical < 0f) _vertical = -2f * scale;

            bool mayJump = Time.time - _lastGroundedTime <= settings.coyoteTime;
            bool wantsJump = Time.time - _jumpPressedTime <= settings.jumpBuffer;
            if (mayJump && wantsJump)
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
            if (_vertical < 0f) _peakFall = Mathf.Max(_peakFall, -_vertical);

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
                    Landed?.Invoke(_peakFall / scale);
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
        }
    }
}
