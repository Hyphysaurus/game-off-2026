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
    /// How fast it goes is the third-person toy's own numbers while FirstPersonSettings > Match Third Person is on
    /// (D-0175): its walk, sprint, crouched speed, the slower walk with aim held, its jump, gravity and fastest fall,
    /// read from the third-person motor and moves, so the same toy moves the same in either view. How it gets up to
    /// speed and slows down stays HL2's.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        private const float JumpBuffer = 0.1f;

        private FirstPersonSettings _s;
        private PlayerInput _input;
        private Shifter _shifter;
        private PhysicsGrabber _hands;
        private PlayerMotor _third;          // the third-person motor this body stands in for: its numbers are this body's while matched (D-0175)
        private PlayerMoveSettings _moves;   // third person's crouched speed
        private CharacterController _cc;
        private readonly Collider[] _overlap = new Collider[16];
        private Vector3 _velocity;
        private float _scale = -1f, _jumpPressed = -10f;
        private bool _crouched;
        // D-0194, first person on the player itself: the player's own motor moves, and this body only stands where the
        // player is and tells the view and the hands the player's state. Its capsule is off, so hits, pads, winds and
        // triggers find the player and not a stand-in.
        private bool _riding;
        private PlayerStance _stance;
        private CharacterController _ownerCapsule;

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
        /// <summary>Middle of the capsule, world space (the player's own while it rides the player, D-0194).</summary>
        public Vector3 Centre => _riding && _ownerCapsule != null ? _ownerCapsule.bounds.center : transform.position + Vector3.up * (_cc != null ? _cc.height * 0.5f : 0.9f * Scale);
        public CharacterController Capsule => _riding && _ownerCapsule != null ? _ownerCapsule : _cc;
        /// <summary>True while the player's own motor moves and this body only rides along (D-0194).</summary>
        public bool Riding => _riding;

        /// <summary>First person on the player itself (D-0194), or back to moving by itself. Riding needs the player's
        /// motor; without one it stays as it was. Call before Place.</summary>
        public void Ride(bool on)
        {
            _riding = on && _third != null && Owner != null;
            if (_riding) { Owner.TryGetComponent(out _stance); Owner.TryGetComponent(out _ownerCapsule); }
            if (_cc != null) _cc.enabled = !_riding;
        }

        /// <summary>Riding: what the view and the hands ask of this body is the player's own state, and the eye eases
        /// through the player's crouch (never above its crouched capsule).</summary>
        private void RideAlong(float scale, float dt)
        {
            _crouched = _stance != null && _stance.Crouched;
            Grounded = _third.Grounded;
            _velocity = _third.Velocity;
            float crouchEye = _stance != null ? Mathf.Min(_s.crouchEye, _s.standEye * _stance.HeightShare) : _s.crouchEye;
            float eyeTarget = (_crouched ? crouchEye : _s.standEye) * scale;
            float eyeRate = (_s.standEye - _s.crouchEye) * scale / Mathf.Max(0.01f, _s.duckTime);
            EyeHeight = Mathf.MoveTowards(EyeHeight, eyeTarget, eyeRate * dt);
        }

        // While matched (D-0175) the third-person toy's numbers stand in for this body's own.
        private bool Matched => _s.matchThirdPerson && _third != null;
        /// <summary>The walk's top speed, own units a second (the view's bob is paced against it).</summary>
        public float WalkSpeed => Matched ? _third.WalkSpeed : _s.runSpeed;
        private float OwnGravity => Matched ? _third.OwnGravity : _s.gravity;
        private float JumpHeight => Matched ? _third.JumpHeight : _s.jumpHeight;
        private float MaxFallSpeed => Matched ? _third.MaxFallSpeed : _s.maxFallSpeed;
        private float PushSpeed => Matched ? _third.PushSpeed : _s.runSpeed * 0.6f;

        /// <summary>The fastest the body may go right now, own units a second, before a heavy gun's share. Matched, it is
        /// the third-person motor's own rule: the sprint, or the walk when Sprint is not held, when crouched, or while
        /// aim is held; never above the crouched speed while crouched; the aim's share of that while aim is held (the
        /// player's moves go on writing those two asks to the parked motor in first person). With a prop in the hands
        /// the aim button turns the prop: that is no aim, and nothing slows. Not matched: this body's own run, sprint
        /// and crouch share.</summary>
        private float TopSpeed()
        {
            bool sprint = _input != null && _input.Sprint && !_crouched;
            if (!Matched)
            {
                float own = sprint ? _s.sprintSpeed : _s.runSpeed;
                return _crouched ? own * _s.crouchSpeedMultiplier : own;
            }
            bool aimed = _hands == null || _hands.Held == null;   // the same rule that narrows the view (FirstPersonMode)
            float top = sprint && !(aimed && _third.SprintLocked) ? _third.SprintSpeed : _third.WalkSpeed;
            if (_crouched) top = _moves != null ? Mathf.Min(top, _moves.crouchSpeed) : top * _s.crouchSpeedMultiplier;
            return aimed ? top * _third.AimShare : top;
        }

        /// <summary><paramref name="third"/> and <paramref name="moves"/>: the third-person motor this body stands in for
        /// and the moves' numbers, for Match Third Person; without the motor the body keeps its own numbers.</summary>
        public void Setup(FirstPersonSettings settings, PlayerInput input, Shifter shifter, Transform owner, PhysicsGrabber hands,
            PlayerMotor third = null, PlayerMoveSettings moves = null)
        {
            _s = settings; _input = input; _shifter = shifter; Owner = owner; _hands = hands; _third = third; _moves = moves;
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
            if (_riding) return;   // nothing shoves a body that rides the player: its capsule is off, the player is shoved
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
                if (HitShapes.IsShape(c)) continue;   // a figure's hit shape stops nothing
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
            if (_riding) { RideAlong(scale, dt); return; }   // the player's own motor moves (D-0194)

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
            float top = TopSpeed() * SpeedShare * scale;
            wishSpeed *= top;

            float gravity = OwnGravity * scale;
            bool jump = Grounded && Time.time - _jumpPressed <= JumpBuffer;
            if (jump)
            {
                _velocity.y = Mathf.Sqrt(2f * gravity * JumpHeight * scale);
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
                _velocity.y = Mathf.Max(_velocity.y - gravity * dt, -MaxFallSpeed * scale);
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
            float want = Mathf.Min(new Vector3(_velocity.x, 0f, _velocity.z).magnitude, PushSpeed * _scale);
            float have = Vector3.Dot(body.linearVelocity, dir);
            if (have < want) body.AddForce(dir * (want - have), ForceMode.VelocityChange);
        }
    }
}
