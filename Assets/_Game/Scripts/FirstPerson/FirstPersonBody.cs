using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The first-person body, moved the way Half-Life 2 moves (the Source engine's model): on the ground, friction slows
    /// you and acceleration pushes you toward the wished speed; in the air there is gravity and only a sliver of control
    /// (strafing still bends the flight). Crouch shrinks the capsule; crouching in the air pulls the feet up instead, so a
    /// crouch-jump clears higher ledges. You cannot stand up under a ceiling. Steps are walked up. Loose props are shoved.
    /// Everything is multiplied by the Shifter's scale, live, so a shift in first person grows or shrinks the body.
    /// Facing (yaw) is set by FirstPersonLook on this transform.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        private const float JumpBuffer = 0.1f;

        private FirstPersonSettings _s;
        private PlayerInput _input;
        private Shifter _shifter;
        private PhysicsGrabber _hands;
        private CharacterController _cc;
        private readonly Collider[] _overlap = new Collider[16];
        private Vector3 _velocity;
        private float _scale = -1f, _jumpPressed = -10f;
        private bool _crouched;

        /// <summary>The player this body stands in for: hits on the body are the player's.</summary>
        public Transform Owner { get; private set; }
        public float Scale => _shifter != null ? _shifter.Scale : 1f;
        public bool Grounded { get; private set; }
        public bool Crouched => _crouched;
        /// <summary>A share of the run, sprint and crouch speeds: a heavy gun in the hands is a little slower (the
        /// WeaponHolder sets it). 1 = all of it.</summary>
        public float SpeedShare { get; set; } = 1f;
        public Vector3 Velocity => _velocity;
        /// <summary>Eye height above the feet right now (eases through crouches).</summary>
        public float EyeHeight { get; private set; }
        /// <summary>Middle of the capsule, world space.</summary>
        public Vector3 Centre => transform.position + Vector3.up * (_cc != null ? _cc.height * 0.5f : 0.9f * Scale);
        public CharacterController Capsule => _cc;

        public void Setup(FirstPersonSettings settings, PlayerInput input, Shifter shifter, Transform owner, PhysicsGrabber hands)
        {
            _s = settings; _input = input; _shifter = shifter; Owner = owner; _hands = hands;
            _cc = GetComponent<CharacterController>();
            _cc.minMoveDistance = 0f;
            _cc.slopeLimit = settings.slopeLimit;
        }

        /// <summary>Stand here with no momentum, upright.</summary>
        public void Place(Vector3 feet)
        {
            _crouched = false;
            _scale = -1f;
            ApplySize();
            bool was = _cc.enabled; _cc.enabled = false;
            transform.position = feet;
            _cc.enabled = was;
            _velocity = Vector3.zero;
            EyeHeight = _s.standEye * Scale;
            _jumpPressed = -10f;
        }

        /// <summary>A shove from a hit or an explosion, world units per second.</summary>
        public void AddVelocity(Vector3 v)
        {
            _velocity += v;
            if (v.y > 0f) Grounded = false;
        }

        private void OnEnable() { if (_input != null) _input.Jump += OnJump; }
        private void OnDisable() { if (_input != null) _input.Jump -= OnJump; }
        private void OnJump() => _jumpPressed = Time.time;

        /// <summary>Capsule size for the current scale and crouch; feet stay put.</summary>
        private void ApplySize()
        {
            float scale = Scale;
            if (_scale > 0f && !Mathf.Approximately(scale, _scale)) { EyeHeight *= scale / _scale; _velocity *= scale / _scale; }
            _scale = scale;
            float h = (_crouched ? _s.crouchHeight : _s.standHeight) * scale;
            float r = Mathf.Min(_s.radius * scale, h * 0.5f);
            _cc.height = h;
            _cc.radius = r;
            _cc.center = Vector3.up * (h * 0.5f);
            _cc.skinWidth = Mathf.Max(0.0005f, r * 0.1f);
            _cc.stepOffset = Mathf.Min(_s.stepHeight * scale, h * 0.5f);
        }

        /// <summary>Would a capsule of this height, feet at <paramref name="feet"/>, touch anything (not us, not the held prop)?</summary>
        private bool Blocked(Vector3 feet, float height)
        {
            float r = _cc.radius * 0.95f;
            Vector3 a = feet + Vector3.up * (r + _cc.skinWidth), b = feet + Vector3.up * (height - r);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, r, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _overlap[i];
                if (c == _cc || c.transform.IsChildOf(transform) || (Owner != null && c.transform.IsChildOf(Owner))) continue;
                if (_hands != null && _hands.Held != null && c.attachedRigidbody == _hands.Held) continue;
                return true;
            }
            return false;
        }

        private void Crouch(float scale)
        {
            bool want = _input != null && _input.CrouchHeld;
            float drop = (_s.standHeight - _s.crouchHeight) * scale;
            if (want && !_crouched)
            {
                _crouched = true;
                if (!Grounded)
                {
                    // Tuck the legs: the head stays, the feet come up. This is the crouch-jump.
                    MoveTo(transform.position + Vector3.up * drop);
                    EyeHeight -= drop;
                }
                ApplySize();
            }
            else if (!want && _crouched)
            {
                float stand = _s.standHeight * scale;
                if (!Grounded && !Blocked(transform.position - Vector3.up * drop, stand))
                {
                    // Untuck in the air: feet drop back down, head stays.
                    _crouched = false;
                    MoveTo(transform.position - Vector3.up * drop);
                    EyeHeight += drop;
                    ApplySize();
                }
                else if (!Blocked(transform.position, stand))
                {
                    _crouched = false;
                    ApplySize();
                }
            }
        }

        private void MoveTo(Vector3 feet)
        {
            _cc.enabled = false;
            transform.position = feet;
            _cc.enabled = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _s == null) return;
            if (!Mathf.Approximately(Scale, _scale)) ApplySize();
            float scale = _scale;

            Crouch(scale);
            float eyeTarget = (_crouched ? _s.crouchEye : _s.standEye) * scale;
            float eyeRate = (_s.standEye - _s.crouchEye) * scale / Mathf.Max(0.01f, _s.duckTime);
            EyeHeight = Mathf.MoveTowards(EyeHeight, eyeTarget, eyeRate * dt);

            // Wished direction and speed, on the ground plane, from the body's facing.
            Vector2 move = _input != null ? _input.Move : Vector2.zero;
            Vector3 wish = transform.forward * move.y + transform.right * move.x;
            wish.y = 0f;
            float wishSpeed = Mathf.Clamp01(wish.magnitude);
            Vector3 wishDir = wishSpeed > 1e-4f ? wish.normalized : Vector3.zero;
            float top = (_input != null && _input.Sprint && !_crouched ? _s.sprintSpeed : _s.runSpeed) * scale;
            if (_crouched) top *= _s.crouchSpeedMultiplier;
            top *= SpeedShare;
            wishSpeed *= top;

            float gravity = _s.gravity * scale;
            bool jump = Grounded && Time.time - _jumpPressed <= JumpBuffer;
            if (jump)
            {
                _velocity.y = Mathf.Sqrt(2f * gravity * _s.jumpHeight * scale);
                _jumpPressed = -10f;
                Grounded = false;
            }

            if (Grounded)
            {
                Friction(dt, scale);
                Accelerate(wishDir, wishSpeed, _s.accelerate, dt);
                _velocity.y = -0.5f * gravity * dt - 0.05f * scale;   // hug the ground over steps and down slopes
            }
            else
            {
                AirAccelerate(wishDir, wishSpeed, dt, scale);
                _velocity.y = Mathf.Max(_velocity.y - gravity * dt, -_s.maxFallSpeed * scale);
            }

            _cc.stepOffset = Grounded ? Mathf.Min(_s.stepHeight * scale, _cc.height * 0.5f) : 0f;
            Vector3 before = transform.position;
            CollisionFlags flags = _cc.Move(_velocity * dt);
            // Walls take the speed you push into them (Source clips the velocity to what actually moved).
            Vector3 moved = (transform.position - before) / dt;
            _velocity.x = Mathf.Abs(moved.x) < Mathf.Abs(_velocity.x) ? moved.x : _velocity.x;
            _velocity.z = Mathf.Abs(moved.z) < Mathf.Abs(_velocity.z) ? moved.z : _velocity.z;
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;

            bool wasGrounded = Grounded;
            Grounded = _cc.isGrounded && _velocity.y <= 0f;
            if (Grounded && !wasGrounded) _velocity.y = 0f;
        }

        /// <summary>Source friction: speed lost is proportional to speed, but never less than the stop speed's share.</summary>
        private void Friction(float dt, float scale)
        {
            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            float speed = planar.magnitude;
            if (speed < 1e-4f) { _velocity.x = _velocity.z = 0f; return; }
            float control = Mathf.Max(speed, _s.stopSpeed * scale);
            float newSpeed = Mathf.Max(0f, speed - control * _s.friction * dt);
            planar *= newSpeed / speed;
            _velocity.x = planar.x; _velocity.z = planar.z;
        }

        /// <summary>Source acceleration: add speed along the wished direction until the projected speed reaches the wish.</summary>
        private void Accelerate(Vector3 dir, float wishSpeed, float accel, float dt)
        {
            if (wishSpeed <= 0f) return;
            float current = Vector3.Dot(new Vector3(_velocity.x, 0f, _velocity.z), dir);
            float add = wishSpeed - current;
            if (add <= 0f) return;
            float step = Mathf.Min(accel * wishSpeed * dt, add);
            _velocity.x += dir.x * step; _velocity.z += dir.z * step;
        }

        /// <summary>Air: the same push, but the target is capped very low, so you steer the flight without gaining speed.</summary>
        private void AirAccelerate(Vector3 dir, float wishSpeed, float dt, float scale)
        {
            if (wishSpeed <= 0f) return;
            float capped = Mathf.Min(wishSpeed, _s.airSpeedCap * scale);
            float current = Vector3.Dot(new Vector3(_velocity.x, 0f, _velocity.z), dir);
            float add = capped - current;
            if (add <= 0f) return;
            float step = Mathf.Min(_s.airAccelerate * wishSpeed * dt, add);
            _velocity.x += dir.x * step; _velocity.z += dir.z * step;
        }

        /// <summary>Walking into a loose prop shoves it, as the third-person motor does; heavy ones only at kid size.</summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body = hit.rigidbody;
            if (body == null || body.isKinematic || hit.moveDirection.y < -0.3f) return;
            if (_hands != null && body == _hands.Held) return;
            if (body.TryGetComponent(out Pushable pushable) && !pushable.PushableBy(_shifter != null ? _shifter.Size : ShiftSize.Kid)) return;
            if (pushable == null && body.mass > _s.maxLiftMass * 2f) return;
            Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            float want = Mathf.Min(new Vector3(_velocity.x, 0f, _velocity.z).magnitude, _s.runSpeed * 0.6f * _scale);
            float have = Vector3.Dot(body.linearVelocity, dir);
            if (have < want) body.AddForce(dir * (want - have), ForceMode.VelocityChange);
        }
    }
}
