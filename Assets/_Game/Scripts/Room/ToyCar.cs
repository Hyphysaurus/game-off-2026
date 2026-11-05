using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A toy car the toy-sized player climbs into and drives: E to get in and out, the move stick drives (forward /
    /// back, steer), Jump hops. Physics is a hidden sliding ball (one rigidbody, never tips over); the car body follows
    /// the ball, faces where it steers and leans to the ground under it. At kid size it is a toy to look at.
    /// </summary>
    public sealed class ToyCar : MonoBehaviour, IInteractable
    {
        public static readonly List<ToyCar> All = new List<ToyCar>();

        [SerializeField, Tooltip("Top speed, units per second (the toy sprints at 10).")] private float topSpeed = 26f;
        [SerializeField] private float acceleration = 34f, braking = 60f, coastDrag = 10f;
        [SerializeField, Tooltip("Degrees per second at full lock and speed.")] private float turnRate = 150f;
        [SerializeField, Tooltip("Hop height, units.")] private float hopHeight = 3f;
        [SerializeField, Tooltip("Where the driver sits, in the car's local space (found from the body if zero).")] private Vector3 seat;
        [SerializeField, Tooltip("Wheel objects spun with speed; found by name if empty.")] private Transform[] wheels;
        [SerializeField] private AudioClip rollLoop, crash;
        [SerializeField] private UnityEvent onEnter, onExit;

        private Rigidbody _ball; private SphereCollider _sphere; private float _radius, _yaw, _flip;
        private Vector3 _startPos; private Quaternion _startRot;
        private PlayerMotor _driverMotor; private PlayerInput _input; private Interactor _interactor; private CharacterController _cc; private Animator _anim;
        private Renderer[] _body;
        private Transform _driver; private float _enteredAt; private bool _grounded; private AudioSource _audio; private Vector3 _groundNormal = Vector3.up;

        public Transform Ball => _ball != null ? _ball.transform : null;
        public float BallRadius => _radius;
        public bool Driving => _driver != null;

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

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
            _ball = go.AddComponent<Rigidbody>(); _ball.mass = 2f; _ball.constraints = RigidbodyConstraints.FreezeRotation; _ball.interpolation = RigidbodyInterpolation.Interpolate; _ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(_sphere, c);
            var own = GetComponent<Rigidbody>(); if (own != null) Destroy(own);
            var push = GetComponent<Pushable>(); if (push != null) Destroy(push);
            _startPos = go.transform.position; _startRot = transform.rotation;
            _audio = gameObject.AddComponent<AudioSource>(); _audio.loop = true; _audio.clip = rollLoop; _audio.spatialBlend = 0.7f; _audio.volume = 0f; _audio.maxDistance = 120f; _audio.rolloffMode = AudioRolloffMode.Linear;
            if (rollLoop != null) _audio.Play();
        }

        private Bounds Bounds()
        {
            var rs = _body != null && _body.Length > 0 ? _body : GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (var r in rs) if (r != null) b.Encapsulate(r.bounds); return b;
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
            if (_anim != null) { _anim.SetFloat("Speed", 0f); _anim.SetBool("Grounded", true); _anim.SetFloat("VerticalVelocity", 0f); }
            _enteredAt = Time.time;
            _input.Interact += GetOut; _input.Jump += Hop;
            onEnter?.Invoke();
        }

        private void GetOut()
        {
            if (!Driving || Time.time - _enteredAt < 0.25f) return;
            _input.Interact -= GetOut; _input.Jump -= Hop;
            var b = Bounds();
            Vector3 side = transform.right * (b.extents.x + 1.5f);
            Vector3 at = new Vector3(b.center.x, b.min.y + 0.2f, b.center.z) + side;
            _driver.SetParent(null, true);
            _cc.enabled = true; _driverMotor.enabled = true; _interactor.enabled = true;
            _driverMotor.Warp(at, Quaternion.Euler(0f, transform.eulerAngles.y - _flip, 0f));
            _driver = null;
            onExit?.Invoke();
        }

        private void Hop()
        {
            if (!Driving || !_grounded) return;
            float g = Mathf.Abs(Physics.gravity.y);
            _ball.AddForce(Vector3.up * Mathf.Sqrt(2f * g * hopHeight), ForceMode.VelocityChange);
        }

        private void FixedUpdate()
        {
            if (_ball == null) return;
            float dt = Time.fixedDeltaTime;
            _grounded = Physics.SphereCast(_ball.position + Vector3.up * 0.1f, _radius * 0.9f, Vector3.down, out var hit, 0.4f, ~0, QueryTriggerInteraction.Ignore) && hit.collider != _sphere;
            _groundNormal = _grounded ? hit.normal : Vector3.up;
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
                _yaw += move.x * turnRate * dt * Mathf.Sign(next == 0 ? 1f : next) * Mathf.Clamp01(speed01 * 3f);
            }
            if (_ball.position.y < -20f) { if (Driving) GetOut(); _ball.position = _startPos; _ball.linearVelocity = Vector3.zero; _yaw = _startRot.eulerAngles.y + _flip; }
        }

        private void Update()
        {
            if (_ball == null) return;
            Vector3 bottom = _ball.position + Vector3.down * _radius;
            Quaternion facing = Quaternion.Euler(0f, _yaw - _flip, 0f);
            Quaternion lean = Quaternion.FromToRotation(Vector3.up, _grounded ? _groundNormal : Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lean * facing, 12f * Time.deltaTime);
            var b = Bounds();   // the body sits with its lowest point on the ball's lowest point, centred over it
            transform.position += new Vector3(_ball.position.x - b.center.x, bottom.y - b.min.y, _ball.position.z - b.center.z);
            float speed = new Vector3(_ball.linearVelocity.x, 0f, _ball.linearVelocity.z).magnitude;
            foreach (var w in wheels) if (w != null) w.Rotate(transform.right, speed * Time.deltaTime * 60f, Space.World);
            if (_audio != null) { _audio.volume = Mathf.MoveTowards(_audio.volume, _grounded ? Mathf.Clamp01(speed / topSpeed) * 0.6f : 0f, Time.deltaTime * 3f); _audio.pitch = 0.8f + speed / topSpeed * 0.6f; }
        }

    }
}
