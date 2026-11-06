using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A toy car the toy-sized player climbs into and drives: E to get in and out, the move stick drives (forward /
    /// back, steer), Jump hops. Physics is a hidden sliding ball (one rigidbody, never tips over); the car body follows
    /// the ball, faces where it steers and leans to the ground under it. At kid size it is a toy to look at.
    /// In Build Mode it is the other way round: the body is moved by hand and the ball waits, switched off; when Build
    /// Mode lets go, the ball goes back under the body (sized to it) and takes over again.
    /// </summary>
    public sealed class ToyCar : MonoBehaviour, IInteractable, IBuildMoved, IChaseTarget
    {
        public static readonly List<ToyCar> All = new List<ToyCar>();

        /// <summary>True when <paramref name="rb"/> is a toy car's hidden physics ball (a separate scene object, so it is not
        /// found under the car): first-person hands must not lift it.</summary>
        public static bool OwnsBall(Rigidbody rb)
        {
            if (rb == null) return false;
            foreach (var car in All) if (car != null && car._ball == rb) return true;
            return false;
        }

        [SerializeField, Tooltip("Top speed, units per second (the toy sprints at 10).")] private float topSpeed = 26f;
        [SerializeField] private float acceleration = 34f, braking = 60f, coastDrag = 10f;
        [SerializeField, Tooltip("Degrees per second at full lock and speed.")] private float turnRate = 150f;
        [SerializeField, Tooltip("Hop height, units.")] private float hopHeight = 3f;
        [SerializeField, Tooltip("Gravity on the car, units/s². The toy's own is 25; Unity's default 9.8 made jumps float.")] private float gravity = 25f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of the turn rate available standing still (a toy car can turn on the spot).")] private float pivotTurn = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of the turn rate in the air, to line up a landing.")] private float airTurn = 0.35f;
        [SerializeField, Tooltip("The camera pulls back this much while driving.")] private float chaseDistance = 1.6f;
        [SerializeField, Tooltip("Where the driver sits, in the car's local space (found from the body if zero).")] private Vector3 seat;
        [SerializeField, Tooltip("Wheel objects spun with speed; found by name if empty.")] private Transform[] wheels;
        [SerializeField] private AudioClip rollLoop, crash;
        [SerializeField] private UnityEvent onEnter, onExit;

        private Rigidbody _ball; private SphereCollider _sphere; private float _radius, _yaw, _flip, _scaleAt;
        private bool _held, _ignoreDirty;   // held: Build Mode is on, the body leads; dirty: the ball must skip the body's colliders again
        private Vector3 _startPos; private Quaternion _startRot;
        private PlayerMotor _driverMotor; private PlayerInput _input; private Interactor _interactor; private CharacterController _cc; private Animator _anim;
        private Renderer[] _body; private Vector3 _bodyOffset; private ShoulderCamera _camera;
        private Transform _driver; private float _enteredAt; private bool _grounded; private AudioSource _audio; private Vector3 _groundNormal = Vector3.up;

        /// <summary>The physics ball, or null while Build Mode holds it.</summary>
        public Transform Ball => _ball != null && !_held ? _ball.transform : null;
        public float BallRadius => _radius;
        public bool Driving => _driver != null;
        public Vector3 ChaseVelocity => _ball != null && !_held ? _ball.linearVelocity : Vector3.zero;
        public float ChaseTopSpeed => topSpeed;
        public bool ChaseGrounded => _grounded;
        private readonly List<Renderer> _hiddenDriver = new List<Renderer>();   // only the car shows while driving

        private void OnEnable()
        {
            All.Add(this);
            if (_ball == null) return;   // Start makes it
            _ball.gameObject.SetActive(true);
            PutBallUnder();
            _ignoreDirty = true;   // switching colliders off and on forgets IgnoreCollision
        }

        private void OnDisable()
        {
            All.Remove(this);
            if (_ball != null) _ball.gameObject.SetActive(false);   // deleted or hidden in Build Mode: the ball goes too
        }

        private void OnDestroy()
        {
            if (_ball != null) Destroy(_ball.gameObject);
        }

        private void Start()
        {
            _body = GetComponentsInChildren<Renderer>();
            var b = Bounds();
            _radius = Mathf.Clamp(b.size.y * 0.35f, 0.5f, Mathf.Min(b.size.x, b.size.z) * 0.4f);
            if (seat == Vector3.zero) seat = transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.center.z));
            Transform front = null, rear = null;
            foreach (Transform t in GetComponentsInChildren<Transform>()) { if (t.name.Contains("Front") || t.name.EndsWith("_FL") || t.name.EndsWith("_FR")) front = t; if (t.name.Contains("Rear") || t.name.EndsWith("_RL") || t.name.EndsWith("_RR")) rear = t; }
            if (wheels == null || wheels.Length == 0) { var w = new List<Transform>(); foreach (Transform t in GetComponentsInChildren<Transform>()) if (t != transform && (t.name.Contains("Wheel"))) w.Add(t); wheels = w.ToArray(); }
            // which way the body's nose points: from the rear wheels to the front ones
            _flip = front != null && rear != null && Vector3.Dot(front.position - rear.position, transform.forward) < 0f ? 180f : 0f;
            _yaw = transform.eulerAngles.y + _flip;

            var go = new GameObject(name + "_Ball"); go.transform.position = new Vector3(b.center.x, b.min.y + _radius, b.center.z);
            _sphere = go.AddComponent<SphereCollider>(); _sphere.radius = _radius;
            _sphere.material = new PhysicsMaterial("CarBall") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0f };
            _ball = go.AddComponent<Rigidbody>(); _ball.useGravity = false; _ball.mass = 2f; _ball.constraints = RigidbodyConstraints.FreezeRotation; _ball.interpolation = RigidbodyInterpolation.Interpolate; _ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            IgnoreOwnColliders();
            var own = GetComponent<Rigidbody>(); if (own != null) Destroy(own);
            var push = GetComponent<Pushable>(); if (push != null) Destroy(push);
            _startPos = go.transform.position; _startRot = transform.rotation;
            // the body's place relative to the ball's lowest point, measured once: following the renderer bounds every
            // frame made the body bob as the wheels spun and the car leaned
            _bodyOffset = Quaternion.Inverse(transform.rotation) * (transform.position - (go.transform.position + Vector3.down * _radius));
            _scaleAt = Mathf.Abs(transform.lossyScale.y);
            // a Build Mode copy arrives with the original's roll source: use it rather than adding a second
            foreach (var a in GetComponents<AudioSource>()) if (a.loop && a.clip == rollLoop) { _audio = a; break; }
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.loop = true; _audio.clip = rollLoop; _audio.spatialBlend = 0.7f; _audio.volume = 0f; _audio.maxDistance = 120f; _audio.rolloffMode = AudioRolloffMode.Linear;
            if (rollLoop != null && !_audio.isPlaying) _audio.Play();
            if (GameMode.Current == GameMode.Mode.Build) Hold();
        }

        private Bounds Bounds()
        {
            var rs = _body != null && _body.Length > 0 ? _body : GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (var r in rs) if (r != null) b.Encapsulate(r.bounds); return b;
        }

        /// <summary>The ball never hits the car's own colliders (switched-off ones are skipped: Unity refuses them, and a
        /// seated driver is not part of the car).</summary>
        private void IgnoreOwnColliders()
        {
            foreach (var c in GetComponentsInChildren<Collider>())
                if (c != null && c.enabled && (_driver == null || !c.transform.IsChildOf(_driver))) Physics.IgnoreCollision(_sphere, c);
        }

        public string PromptFor(Interactor who)
        {
            if (Driving) return null;
            if (who.Shifter != null && who.Shifter.Size == ShiftSize.Kid) return "Too big to drive";
            return "Drive";
        }

        public void Interact(Interactor who)
        {
            if (Driving || (who.Shifter != null && who.Shifter.Size == ShiftSize.Kid)) return;
            _driver = who.transform; _driverMotor = who.GetComponent<PlayerMotor>(); _input = who.GetComponent<PlayerInput>(); _interactor = who; _cc = who.GetComponent<CharacterController>(); _anim = who.GetComponentInChildren<Animator>();
            _driverMotor.enabled = false; _cc.enabled = false; _interactor.enabled = false;
            _driver.SetParent(transform, true); _driver.localPosition = seat; _driver.localRotation = Quaternion.Euler(0f, _flip, 0f);
            _hiddenDriver.Clear();
            foreach (var r in _driver.GetComponentsInChildren<Renderer>()) if (!r.forceRenderingOff) { r.forceRenderingOff = true; _hiddenDriver.Add(r); }
            if (_anim != null) { _anim.SetFloat("Speed", 0f); _anim.SetBool("Grounded", true); _anim.SetFloat("VerticalVelocity", 0f); }
            _enteredAt = Time.time;
            _input.Interact += GetOut; _input.Jump += Hop;
            _camera = FindFirstObjectByType<ShoulderCamera>(); if (_camera != null) _camera.SetChase(transform, _flip, chaseDistance);
            onEnter?.Invoke();
        }

        /// <summary>Puts the driver out now, whatever the timing (Build Mode, before it hides, moves or copies the car).</summary>
        public void Eject()
        {
            if (!Driving) return;
            _enteredAt = -10f;
            GetOut();
        }

        private void GetOut()
        {
            if (!Driving || Time.time - _enteredAt < 0.25f) return;
            _input.Interact -= GetOut; _input.Jump -= Hop;
            var b = Bounds();
            Vector3 side = transform.right * (b.extents.x + 1.5f);
            Vector3 at = new Vector3(b.center.x, b.min.y + 0.2f, b.center.z) + side;
            _driver.SetParent(null, true);
            foreach (var r in _hiddenDriver) if (r != null) r.forceRenderingOff = false;
            _hiddenDriver.Clear();
            _cc.enabled = true; _driverMotor.enabled = true; _interactor.enabled = true;
            _driverMotor.Warp(at, Quaternion.Euler(0f, transform.eulerAngles.y - _flip, 0f));
            _driver = null;
            if (_camera != null) _camera.SetChase(null);
            onExit?.Invoke();
        }

        private void Hop()
        {
            if (!Driving || !_grounded || _held) return;
            _ball.AddForce(Vector3.up * Mathf.Sqrt(2f * gravity * hopHeight), ForceMode.VelocityChange);
        }

        // ------------------------------------------------------------------ Build Mode

        /// <summary>Build Mode placed, moved, turned or scaled the car (or undid or loaded it): it starts from here now.</summary>
        public void OnBuildMoved()
        {
            if (_ball == null) return;   // not started yet: Start measures the pose it is given
            _startPos = PutBallUnder();
            _startRot = transform.rotation;
        }

        /// <summary>Build Mode is on: the ball stops and switches its collider off, so Build Mode's rays and drags never
        /// land on it, and the body stays where Build Mode puts it.</summary>
        private void Hold()
        {
            _held = true;
            if (!_ball.isKinematic) _ball.linearVelocity = Vector3.zero;
            _ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode a kinematic body takes
            _ball.isKinematic = true;
            _sphere.enabled = false;
        }

        /// <summary>Build Mode let go: the ball goes under the body where it was left and drives it again.</summary>
        private void Release()
        {
            _held = false;
            PutBallUnder();
            _sphere.enabled = true;
            _ball.isKinematic = false;
            _ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _ball.linearVelocity = Vector3.zero;
            _ignoreDirty = true;
        }

        /// <summary>Puts the ball back under the body as it stands now, grown or shrunk with it (the body is the truth
        /// after a Build edit, an undo or a load). Returns where the ball went.</summary>
        private Vector3 PutBallUnder()
        {
            float s = Mathf.Abs(transform.lossyScale.y);
            if (_scaleAt > 1e-4f && s > 1e-4f && !Mathf.Approximately(s, _scaleAt))
            {
                float k = s / _scaleAt;
                _radius *= k; _bodyOffset *= k; _sphere.radius = _radius;
            }
            if (s > 1e-4f) _scaleAt = s;
            _yaw = transform.eulerAngles.y + _flip;
            // the inverse of Update's "body = ball bottom + rotation * offset", so nothing jumps
            Vector3 at = transform.position - transform.rotation * _bodyOffset + Vector3.up * _radius;
            _ball.transform.position = at;
            if (_ball.gameObject.activeInHierarchy)
            {
                _ball.position = at;
                if (!_ball.isKinematic) _ball.linearVelocity = Vector3.zero;
            }
            return at;
        }

        // ------------------------------------------------------------------ per frame

        private void FixedUpdate()
        {
            if (_ball == null || _held) return;
            if (_ignoreDirty) { _ignoreDirty = false; IgnoreOwnColliders(); }
            float dt = Time.fixedDeltaTime;
            _grounded = Physics.SphereCast(_ball.position + Vector3.up * 0.1f, _radius * 0.9f, Vector3.down, out var hit, 0.4f, ~0, QueryTriggerInteraction.Ignore) && hit.collider != _sphere;
            _groundNormal = _grounded ? hit.normal : Vector3.up;
            _ball.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
            Vector3 v = _ball.linearVelocity; Vector3 planar = new Vector3(v.x, 0f, v.z);
            Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            float along = Vector3.Dot(planar, fwd);
            Vector2 move = Driving ? _input.Move : Vector2.zero;
            if (_grounded)
            {
                float want = move.y * topSpeed;
                float rate = Mathf.Approximately(move.y, 0f) ? coastDrag : (Mathf.Sign(want) != Mathf.Sign(along) && Mathf.Abs(along) > 0.5f ? braking : acceleration);
                float next = Mathf.MoveTowards(along, want, rate * dt);
                // grip: the sideways slide dies quickly, so the car goes where it points
                Vector3 side = planar - fwd * along;
                Vector3 newPlanar = fwd * next + Vector3.MoveTowards(side, Vector3.zero, 80f * dt);
                _ball.linearVelocity = new Vector3(newPlanar.x, v.y, newPlanar.z);
                float speed01 = Mathf.Clamp01(Mathf.Abs(next) / topSpeed);
                // steering flips in reverse like a real car; standing still it turns the way the stick says
                float dir = Mathf.Abs(next) > 0.5f ? Mathf.Sign(next) : (move.y < -0.1f ? -1f : 1f);
                _yaw += move.x * turnRate * dt * dir * Mathf.Lerp(pivotTurn, 1f, Mathf.Clamp01(speed01 * 3f));
            }
            else _yaw += move.x * turnRate * airTurn * dt;
            if (_ball.position.y < -20f) { if (Driving) GetOut(); _ball.position = _startPos; _ball.linearVelocity = Vector3.zero; _yaw = _startRot.eulerAngles.y + _flip; }
        }

        private void Update()
        {
            if (_ball == null) return;
            bool build = GameMode.Current == GameMode.Mode.Build;
            if (build && !_held) Hold();
            else if (!build && _held) Release();
            if (_held)
            {
                if (_audio != null) _audio.volume = Mathf.MoveTowards(_audio.volume, 0f, Time.deltaTime * 3f);
                return;
            }
            Vector3 bottom = _ball.position + Vector3.down * _radius;
            Quaternion facing = Quaternion.Euler(0f, _yaw - _flip, 0f);
            Quaternion lean = Quaternion.FromToRotation(Vector3.up, _grounded ? _groundNormal : Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lean * facing, 12f * Time.deltaTime);
            transform.position = bottom + transform.rotation * _bodyOffset;
            float speed = new Vector3(_ball.linearVelocity.x, 0f, _ball.linearVelocity.z).magnitude;
            foreach (var w in wheels) if (w != null) w.Rotate(transform.right, speed * Time.deltaTime * 60f, Space.World);
            if (_audio != null) { _audio.volume = Mathf.MoveTowards(_audio.volume, _grounded ? Mathf.Clamp01(speed / topSpeed) * 0.6f : 0f, Time.deltaTime * 3f); _audio.pitch = 0.8f + speed / topSpeed * 0.6f; }
        }

    }
}
