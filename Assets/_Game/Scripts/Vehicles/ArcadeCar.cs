using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A drivable Rigidbody car, arcade style. Four ray springs hold the body up (so it rides ramps and lands jumps on its
    /// wheels), the engine pushes along the ground, grip kills sideways sliding (less while the handbrake is held, for
    /// drifts), and steering turns faster with speed. Ramming something with health hurts it; hard crashes hurt the car.
    /// At zero health it is a smoking wreck that throws its driver out and, after a while, is towed to a free Garage bay if
    /// the scene has one (else put back at its start).
    /// Flipped over and stuck, it rights itself. Everything is in units at car scale 1 and scales with the transform.
    /// The car knows nothing about players: a CarSeat feeds it a PlayerInput.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Damageable))]
    public sealed class ArcadeCar : MonoBehaviour, IResettable, IBuildMoved, IChaseTarget
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private ArcadeCarSettings settings;
        [SerializeField, Tooltip("Wheel objects (hub pivots). Found by name (Wheel_FL / FR / BL / BR / RL / RR) if empty.")] private Transform[] wheels;
        [SerializeField, Tooltip("Which of the wheels steer. Found by name (_FL, _FR, Front) if empty.")] private bool[] steers;
        [SerializeField] private float mass = 4f;
        [SerializeField, Tooltip("Centre of mass height above the wheel hubs, units at scale 1 (low = hard to roll).")] private float centreOfMassHeight = 0f;
        [Header("Feedback (all optional)")]
        [SerializeField, Tooltip("Looped engine sound; pitch follows speed.")] private AudioSource engine;
        [SerializeField] private AudioClip crashClip;
        [SerializeField, Tooltip("Played while wrecked.")] private ParticleSystem wreckSmoke;
        [SerializeField] private Color wreckTint = new Color(0.25f, 0.22f, 0.2f);

        private Rigidbody _rb;
        private Damageable _health;
        private PlayerInput _driver;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private Vector3[] _rest;            // hub rest positions, car space
        private Quaternion[] _baseRot;      // wheel rotation relative to the car
        private float[] _radius, _offset, _spin;
        private bool[] _contact;
        private Vector3 _spawn; private Quaternion _spawnRot;
        private float _sizedAt;             // the scale the wheel radii were measured at
        private float _steerVisual, _upsideDown, _nextCrash, _wreckedAt;
        private readonly Dictionary<Damageable, float> _nextRam = new Dictionary<Damageable, float>();
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private AudioSource _oneShots;

        public bool Occupied => _driver != null;
        public bool Wrecked => _health != null && _health.Dead;
        public Rigidbody Body => _rb;
        public IReadOnlyList<Transform> Wheels => wheels;
        public Damageable Health => _health;
        public ArcadeCarSettings Settings => settings;
        public float Scale => Mathf.Max(0.001f, transform.lossyScale.y);
        public Vector3 ChaseVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        public float ChaseTopSpeed => settings != null ? settings.maxSpeed * Scale : 0f;
        /// <summary>Any wheel touching the ground.</summary>
        public bool ChaseGrounded
        {
            get
            {
                if (_contact == null || _contact.Length == 0) return true;
                foreach (var c in _contact) if (c) return true;
                return false;
            }
        }
        /// <summary>Speed over the ground, world units per second.</summary>
        public float Speed => _rb != null ? Vector3.ProjectOnPlane(_rb.linearVelocity, Vector3.up).magnitude : 0f;
        /// <summary>Set by the seat while the driver waits to get out: full brakes, no throttle.</summary>
        public bool ForceBrake { get; set; }
        /// <summary>Raised once when the car is wrecked; the seat throws its driver out.</summary>
        public event Action WreckedNow;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<Damageable>();
            _rb.mass = Mathf.Max(0.01f, mass);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearDamping = 0.05f;
            _rb.angularDamping = 1.5f;
            _renderers = GetComponentsInChildren<Renderer>(true);   // hidden parts too: the garage can switch them on later
            _block = new MaterialPropertyBlock();
            _spawn = transform.position; _spawnRot = transform.rotation;
            FindWheels();
            _rest = new Vector3[wheels.Length]; _baseRot = new Quaternion[wheels.Length];
            _radius = new float[wheels.Length]; _offset = new float[wheels.Length]; _spin = new float[wheels.Length]; _contact = new bool[wheels.Length];
            float hubY = 0f;
            for (int i = 0; i < wheels.Length; i++)
            {
                _rest[i] = transform.InverseTransformPoint(wheels[i].position);
                _baseRot[i] = Quaternion.Inverse(transform.rotation) * wheels[i].rotation;
                var rs = wheels[i].GetComponentsInChildren<Renderer>();
                float r = 0f;
                foreach (var rr in rs) r = Mathf.Max(r, rr.bounds.extents.y);
                _radius[i] = r > 1e-4f ? r : (settings != null ? settings.wheelRadius : 0.45f) * Scale;
                hubY += _rest[i].y;
            }
            if (wheels.Length > 0) _rb.centerOfMass = new Vector3(0f, hubY / wheels.Length + centreOfMassHeight, AverageZ());
            _sizedAt = Scale;
            _oneShots = gameObject.AddComponent<AudioSource>(); _oneShots.playOnAwake = false; _oneShots.spatialBlend = 1f;
        }

        private float AverageZ()
        {
            float z = 0f;
            foreach (var p in _rest) z += p.z;
            return wheels.Length > 0 ? z / wheels.Length : 0f;
        }

        private void FindWheels()
        {
            if (wheels != null && wheels.Length > 0 && steers != null && steers.Length == wheels.Length) return;
            if (wheels == null || wheels.Length == 0)
            {
                var found = new List<Transform>();
                foreach (var t in GetComponentsInChildren<Transform>())
                {
                    if (t == transform || !IsWheelName(t.name)) continue;
                    bool nested = false;
                    for (var p = t.parent; p != null && p != transform; p = p.parent) if (IsWheelName(p.name)) { nested = true; break; }
                    if (!nested) found.Add(t);
                }
                wheels = found.ToArray();
            }
            steers = new bool[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                string n = wheels[i].name;
                string corner = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');   // Car 09's front pairs: _FL1, _FL2
                steers[i] = corner.EndsWith("_FL") || corner.EndsWith("_FR") || n.Contains("Front");
            }
        }

        private static bool IsWheelName(string n) => n.Contains("Wheel") && !n.Contains("Steering") && !n.Contains("steering");

        private void OnEnable() { _health.Died += OnDied; }
        private void OnDisable() { _health.Died -= OnDied; }

        /// <summary>Hands the car to a driver's input, or takes it back with null.</summary>
        public void Drive(PlayerInput input)
        {
            _driver = input;
            if (input == null) ForceBrake = false;
        }

        private void OnDied(DamageInfo _)
        {
            _wreckedAt = Time.time;
            if (wreckSmoke != null) wreckSmoke.Play();
            Play(crashClip, 1f, 0.8f);
            WreckedNow?.Invoke();
        }

        public void ResetState()
        {
            if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }   // parked: nothing to clear
            _rb.position = _spawn; _rb.rotation = _spawnRot;
            transform.SetPositionAndRotation(_spawn, _spawnRot);
            _health.ResetState();
            _nextRam.Clear();
            _upsideDown = 0f;
            if (wreckSmoke != null) wreckSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(null);
        }

        /// <summary>Full health and a clean body, where it stands (the garage's fix).</summary>
        public void Repair()
        {
            _health.ResetState();
            _nextRam.Clear();
            if (wreckSmoke != null) wreckSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(null);
        }

        /// <summary>Re-reads the renderers and wheel sizes after parts were swapped or the car was rescaled (the garage).</summary>
        public void RefreshParts()
        {
            var all = GetComponentsInChildren<Renderer>(true);
            var own = new List<Renderer>(all.Length);
            foreach (var r in all) if (r.GetComponentInParent<PlayerMotor>(true) == null) own.Add(r);   // not a seated driver
            _renderers = own.ToArray();
            if (wheels == null) return;
            for (int i = 0; i < wheels.Length; i++)
            {
                float r = RadiusOf(wheels[i]);
                if (r > 1e-4f) _radius[i] = r;
            }
        }

        /// <summary>
        /// A wheel's radius from the meshes drawn under its hub, measured in the hub's own axes. The hub turns with the
        /// wheel, so where the spin stopped does not matter (a turning wheel's world box reads up to 1.4x too tall).
        /// </summary>
        private static float RadiusOf(Transform hub)
        {
            float r = 0f;
            Matrix4x4 toHub = hub.worldToLocalMatrix;
            foreach (var mf in hub.GetComponentsInChildren<MeshFilter>())
            {
                var rend = mf.GetComponent<Renderer>();
                if (mf.sharedMesh == null || rend == null || !rend.enabled) continue;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = toHub * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    r = Mathf.Max(r, Mathf.Abs(m.MultiplyPoint3x4(c).y));
                }
            }
            return r * Mathf.Abs(hub.lossyScale.y);
        }

        /// <summary>Puts the car down at a pose, still, as if placed there (the garage's tow and body swap).</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }
            _rb.position = position; _rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _upsideDown = 0f;
        }

        /// <summary>True while a garage holds the car on its lift.</summary>
        public bool Parked => _rb != null && _rb.isKinematic;

        /// <summary>Holds the car still on a lift (kinematic: nothing pushes it and its physics step stops) or lets it go.</summary>
        public void Park(bool parked)
        {
            if (parked == _rb.isKinematic) return;
            if (parked)
            {
                _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode a kinematic body takes
                _rb.isKinematic = true;
            }
            else
            {
                _rb.isKinematic = false;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        /// <summary>Build Mode placed, moved, turned or scaled the car (or undid or loaded it): F9 and a wreck's recovery
        /// bring it back here now, and the springs use wheel sizes at its new scale.</summary>
        public void OnBuildMoved()
        {
            if (_rb == null || _radius == null) return;   // never woke up
            _spawn = transform.position; _spawnRot = transform.rotation;
            _upsideDown = 0f;
            // radii are world units: follow a scale first, so a hub with nothing to measure still fits; then re-measure
            float k = Scale / Mathf.Max(0.001f, _sizedAt);
            if (!Mathf.Approximately(k, 1f)) for (int i = 0; i < _radius.Length; i++) _radius[i] *= k;
            _sizedAt = Scale;
            RefreshParts();
        }

        /// <summary>Takes over another car's start: a garage body swap belongs where the old body started, not on the lift.</summary>
        public void InheritStart(ArcadeCar from)
        {
            if (from == null) return;
            _spawn = from._spawn; _spawnRot = from._spawnRot;
        }

        /// <summary>A wreck back in one piece: towed to the free garage bay nearest to where it wrecked, else to its start.</summary>
        private void Recover()
        {
            Vector3 at = transform.position;
            ResetState();
            if (Garage.TowSpot(this, at, out Vector3 p, out Quaternion q)) PlaceAt(p, q);
        }

        private void FixedUpdate()
        {
            if (settings == null || wheels.Length == 0 || _rb.isKinematic) return;   // parked on a lift: nothing to drive
            float dt = Time.fixedDeltaTime, s = Scale;
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            int n = wheels.Length, grounded = 0;
            Vector3 up = transform.up, normal = Vector3.zero;
            float travel = settings.suspensionTravel * s;

            // Springs: one ray per wheel. At rest each wheel carries its share of gravity exactly.
            for (int i = 0; i < n; i++)
            {
                Vector3 hub = transform.TransformPoint(_rest[i]);
                Vector3 origin = hub + up * travel;
                float length = 2f * travel + _radius[i];
                _contact[i] = Probe(origin, -up, length, out RaycastHit hit);
                if (!_contact[i]) { _offset[i] = -travel; continue; }
                grounded++;
                normal += hit.normal;
                float compression = Mathf.Clamp(length - hit.distance, 0f, 2f * travel);
                _offset[i] = compression - travel;
                float vUp = Vector3.Dot(_rb.GetPointVelocity(hub), up);
                float accel = g / n * (compression / Mathf.Max(1e-4f, travel)) - settings.damping * vUp / n;
                if (accel > 0f) _rb.AddForceAtPosition(up * (accel * _rb.mass), hub, ForceMode.Force);
            }

            float share = (float)grounded / n;
            Vector3 v = _rb.linearVelocity;
            bool driving = _driver != null && !Wrecked;
            Vector2 stick = driving ? _driver.Move : Vector2.zero;
            bool handbrake = driving && _driver.JumpHeld;
            float throttle = ForceBrake ? 0f : stick.y, steer = ForceBrake ? 0f : stick.x;

            if (grounded > 0)
            {
                Vector3 ground = (normal / grounded).normalized;
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, ground).normalized;
                Vector3 right = Vector3.Cross(ground, fwd);
                float speed = Vector3.Dot(v, fwd);

                // Engine and brakes: ease the forward speed toward the target.
                float target, rate;
                if (ForceBrake || (Mathf.Abs(throttle) > 0.05f && Mathf.Sign(throttle) != Mathf.Sign(speed) && Mathf.Abs(speed) > 0.5f * s))
                { target = 0f; rate = settings.braking * s; }
                else if (Mathf.Abs(throttle) > 0.05f)
                { target = throttle > 0f ? throttle * settings.maxSpeed * s : throttle * settings.reverseSpeed * s; rate = settings.acceleration * s; }
                else
                { target = 0f; rate = settings.coastDrag * s * (handbrake ? 3f : 1f); }
                if (Mathf.Abs(speed) > Mathf.Abs(target) && Mathf.Sign(speed) == Mathf.Sign(target) && Mathf.Abs(throttle) > 0.05f) rate = settings.coastDrag * s;
                float next = Mathf.MoveTowards(speed, target, rate * dt);
                _rb.AddForce(fwd * ((next - speed) / dt * share), ForceMode.Acceleration);

                // Grip: sideways sliding dies away (much less with the handbrake: drift).
                float lateral = Vector3.Dot(v, right);
                float grip = handbrake ? settings.handbrakeGrip : settings.grip;
                _rb.AddForce(-right * (lateral * Mathf.Min(grip * dt, 1f) / dt * share), ForceMode.Acceleration);

                // Steering: a yaw rate that grows with speed (and flips in reverse).
                float steerSpeed = Mathf.Clamp(speed / Mathf.Max(0.01f, settings.steerFullSpeed * s), -1f, 1f);
                float yawRate = steer * settings.steerRate * Mathf.Deg2Rad * steerSpeed * (handbrake ? 1.3f : 1f);
                Vector3 av = _rb.angularVelocity;
                float current = Vector3.Dot(av, ground);
                _rb.angularVelocity = av + ground * ((yawRate - current) * Mathf.Min(1f, 10f * dt) * share);

                // A little downforce at speed keeps it on the track over crests.
                _rb.AddForce(-ground * (g * settings.downforce * Mathf.Clamp01(Mathf.Abs(speed) / (settings.maxSpeed * s)) * share), ForceMode.Acceleration);
            }
            else
            {
                // In the air: turn gently upright so it lands on its wheels.
                Vector3 axis = Vector3.Cross(up, Vector3.up);
                _rb.AddTorque(axis * settings.airUpright - _rb.angularVelocity * 0.5f, ForceMode.Acceleration);
            }
            _steerVisual = Mathf.MoveTowards(_steerVisual, steer * settings.visualSteerAngle, 240f * dt);

            // Flipped and stuck: right it after a moment.
            _upsideDown = Vector3.Dot(up, Vector3.up) < 0.3f && v.magnitude < 2f * s ? _upsideDown + dt : 0f;
            if (_upsideDown > 1.5f)
            {
                _upsideDown = 0f;
                _rb.position += Vector3.up * (1.2f * s);
                _rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                _rb.angularVelocity = Vector3.zero;
            }
            if (transform.position.y < _spawn.y - 80f * s) { WreckedNow?.Invoke(); Recover(); }
        }

        private bool Probe(Vector3 origin, Vector3 dir, float length, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, dir, _hits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (_hits[i].distance < nearest) { nearest = _hits[i].distance; best = _hits[i]; found = true; }
            }
            return found;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (wheels == null) return;
            Vector3 v = _rb.linearVelocity;
            float along = Vector3.Dot(v, transform.forward);
            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                if (w == null) continue;
                _spin[i] = Mathf.Repeat(_spin[i] + along / Mathf.Max(1e-4f, _radius[i]) * Mathf.Rad2Deg * dt, 360f);
                Quaternion steer = Quaternion.Euler(0f, steers[i] ? _steerVisual : 0f, 0f);
                w.SetPositionAndRotation(transform.TransformPoint(_rest[i]) + transform.up * _offset[i],
                    transform.rotation * steer * Quaternion.Euler(_spin[i], 0f, 0f) * _baseRot[i]);
            }

            if (Wrecked)
            {
                // Dark while wrecked (re-applied each frame: a hit's flash clears property blocks).
                _block.Clear(); _block.SetColor(BaseColorId, wreckTint);
                foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(_block);
                if (settings != null && settings.wreckRespawn > 0f && Time.time - _wreckedAt >= settings.wreckRespawn && !Occupied) Recover();
            }

            if (engine != null)
            {
                bool running = Occupied && !Wrecked;
                if (running && !engine.isPlaying) engine.Play();
                else if (!running && engine.isPlaying) engine.Stop();
                if (running && settings != null)
                {
                    float t = Mathf.Clamp01(Speed / (settings.maxSpeed * Scale));
                    engine.pitch = Mathf.Lerp(0.7f, 1.6f, t);
                    engine.volume = Mathf.Lerp(0.25f, 0.6f, t);
                }
            }
        }

        private void OnCollisionEnter(Collision c)
        {
            if (settings == null) return;
            float s = Scale;
            float speed = c.relativeVelocity.magnitude / s;
            var target = Damageable.Find(c.collider);
            bool isDriver = _driver != null && target != null && target.transform == _driver.transform;
            if (target != null && target != _health && !isDriver && !Wrecked && speed > settings.ramMinSpeed)
            {
                if (!_nextRam.TryGetValue(target, out float ready) || Time.time >= ready)
                {
                    _nextRam[target] = Time.time + 0.35f;
                    float damage = Mathf.Min(settings.ramMaxDamage, (speed - settings.ramMinSpeed) * settings.ramDamagePerSpeed);
                    Vector3 away = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
                    Vector3 point = c.contactCount > 0 ? c.GetContact(0).point : target.transform.position;
                    target.Hit(new DamageInfo(damage, (away * settings.ramKnockback + Vector3.up * 3f) * s, point, transform));
                }
            }
            else if (target == null && !Wrecked && speed > settings.ramMinSpeed)
            {
                // A player with no health of their own still gets bowled over.
                var motor = c.collider.GetComponentInParent<PlayerMotor>();
                if (motor != null && (_driver == null || motor.transform != _driver.transform))
                {
                    Vector3 away = Vector3.ProjectOnPlane(motor.transform.position - transform.position, Vector3.up).normalized;
                    Knockback.Apply(motor.gameObject, (away * settings.ramKnockback + Vector3.up * 3f) * s, motor.transform.position);
                }
            }
            if (speed > settings.crashMinSpeed && Time.time >= _nextCrash)
            {
                _nextCrash = Time.time + 0.35f;
                _health.Hit(new DamageInfo((speed - settings.crashMinSpeed) * settings.crashDamagePerSpeed, Vector3.zero, transform.position, c.transform));
                Play(crashClip, Mathf.Clamp01(speed / (settings.crashMinSpeed * 2f)), 1.1f);
            }
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _oneShots == null) return;
            _oneShots.pitch = pitch;
            _oneShots.PlayOneShot(clip, volume);
        }
    }
}
