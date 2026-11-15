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
    /// Boosts (D-0037, IBoostable): bursts from pads, gates and the launch raise the top speed and push along the nose;
    /// Sprint (Shift) burns the nitro meter, which slides, air time and pickups fill; stopped, the handbrake (Jump) with
    /// forward held winds a pull-back spring (the car squats back) and letting go of Jump launches. Nothing boosts while
    /// the car is parked on a lift, held (the cannon) or wrecked. Flames, tyre smoke and the key: CarFx.
    /// Feel (the juice pass): top speed is settings.TopSpeed and the engine's pull fades toward it (AccelerationAt); in the
    /// air the stick tips and turns the car a little; a held handbrake slide charges a drift boost (DriftCharge) that
    /// fires when the handbrake lets go; a clean landing gives a tiny burst. The car reports what happened
    /// (<see cref="Landed"/>, <see cref="Crashed"/>, <see cref="BoostStarted"/>, <see cref="DriftLevelChanged"/>) and where
    /// its wheels are; DrivingFeel turns that into camera, sound, rumble and trails. The car itself plays none of it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Damageable))]
    public sealed class ArcadeCar : MonoBehaviour, IResettable, IBuildMoved, IChaseTarget, IChaseHold, IBoostable, IDriftCharge
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly BoostTuning DefaultBoost = new BoostTuning();

        [SerializeField] private ArcadeCarSettings settings;
        [SerializeField, Tooltip("Wheel objects (hub pivots). Found by name (any 'wheel' that is not a steering wheel) if empty.")] private Transform[] wheels;
        [SerializeField, Tooltip("Which of the wheels steer. Found by name (_FL, _FR, Front) if empty, else the wheels ahead of the middle.")] private bool[] steers;
        [SerializeField] private float mass = 4f;
        [SerializeField, Tooltip("Centre of mass height above the wheel hubs, units at scale 1 (low = hard to roll).")] private float centreOfMassHeight = 0f;
        [Header("Feedback (all optional)")]
        [SerializeField, Tooltip("Looped engine sound; pitch follows speed.")] private AudioSource engine;
        [SerializeField, Tooltip("Played while wrecked.")] private ParticleSystem wreckSmoke;
        [SerializeField] private Color wreckTint = new Color(0.25f, 0.22f, 0.2f);
        [Header("Boosts (all optional)")]
        [SerializeField, Tooltip("Exhaust flames while boosting (the Cyber pack's BoostFX), made at the rear at run time.")] private GameObject boostFxPrefab;
        [SerializeField, Tooltip("Tyre smoke while sliding (the Cyber pack's DriftFX), made across the rear axle at run time.")] private GameObject driftFxPrefab;
        [SerializeField, Tooltip("A wind-up key that turns while the pull-back spring winds.")] private Transform windKey;
        [SerializeField, Tooltip("The axis the key turns about, in the key's own space.")] private Vector3 windKeyAxis = Vector3.up;
        [SerializeField, Tooltip("Played when a burst starts and when nitro lights.")] private AudioClip boostClip;
        [SerializeField, Tooltip("Looped while the pull-back spring winds.")] private AudioClip windClip;
        [SerializeField, Tooltip("Played on the pull-back launch.")] private AudioClip launchClip;

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
        private float _lastHurt = -99f;
        private readonly Dictionary<Damageable, float> _nextRam = new Dictionary<Damageable, float>();
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Collider _underKnown;       // the last thing a wheel stood on, and whether it is track the car holds on to
        private bool _underSticky;
        private bool _holding;              // the last physics step found the car on, or just off, track it holds on to
        private CarBoost _boost;
        private CarFx _fx;
        private CarDamageFx _damageFx;
        private Vector3 _rearAxle;          // car space, at hub height: where the wind presses down
        private float _slip;                // sideways speed as a share of top speed (0 in the air)
        private bool _heldOutside, _wasNitro;   // held: Hold(true) has it (the cannon)
        private float _lastWhoosh = -10f;
        private readonly DriftCharge _drift = new DriftCharge();
        private int _driftShown;            // the drift level last announced
        private float _airTime;             // seconds since a wheel last touched (0 on the ground)
        private float _midZ;                // car space: wheels behind this are rear wheels
        private float _nextImpactFeel;      // Crashed is raised at most ten times a second
        private Vector3 _stepVelocity;      // the velocity at the start of the last physics step (what a hit came in at)
        private Vector3 _groundUp = Vector3.up;   // the road's up under the wheels in the last step (world up in the air)
        private float _wallUntil = -1f;     // physics time until which a wall's knock is settled (D-0058)
        private Vector3 _wallAlong;         // the way along the wall the last glance sent the car (zero: no glance to align to)
        private Vector3 _wallSide;          // the last wall's face, pointing out toward the car
        private bool _driftChance, _driftHeld;

        public bool Occupied => _driver != null;
        public bool Wrecked => _health != null && _health.Dead;
        public Rigidbody Body => _rb;
        public IReadOnlyList<Transform> Wheels => wheels;
        public Damageable Health => _health;
        public ArcadeCarSettings Settings => settings;
        public float Scale => Mathf.Max(0.001f, transform.lossyScale.y);
        public Vector3 ChaseVelocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        /// <summary>Top speed before boosts, world units per second at the car's size (settings.TopSpeed x Scale).</summary>
        public float TopSpeed => settings != null ? settings.TopSpeed * Scale : 0f;
        public float ChaseTopSpeed => TopSpeed;
        /// <summary>Any wheel touching the ground.</summary>
        /// <summary>The car stands on, or hangs just off, track it holds on to (a loop, a spiral, a banked bend).</summary>
        public bool ChaseHolds => _holding;
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

        // ---------- feel: what the car reports (DrivingFeel listens while the car is driven) ----------
        /// <summary>Raised when a wheel touches down after settings.landMinAir or longer in the air, with the speed the car
        /// came down INTO the ground (along the ground's normal), units per second at scale 1. From the physics step.</summary>
        public event Action<float> Landed;
        /// <summary>Raised on a hit, with how hard the car went INTO the surface (the measure crash damage uses, D-0038),
        /// units per second at scale 1. Softer hits than settings.feelMinImpact are not reported; damage starts at
        /// settings.crashMinSpeed. At most ten a second.</summary>
        public event Action<float> Crashed;
        /// <summary>Raised on every hit felt (as Crashed) and every hit that hurts, BEFORE the damage is taken, with where it
        /// landed: CarDamageFx shows and plays it (D-0059).</summary>
        public event Action<CarImpact> Struck;
        /// <summary>Raised when a burst takes (a pad, the launch, a drift or landing boost) and when nitro lights, with its
        /// top speed multiplier.</summary>
        public event Action<float> BoostStarted;
        /// <summary>Raised when the drift charge reaches level 1 or 2, and with 0 when it is spent or lost.</summary>
        public event Action<int> DriftLevelChanged;
        /// <summary>Sideways speed over the ground as a share of top speed (0 in the air).</summary>
        public float Slip => _slip;
        /// <summary>Sliding sideways on the ground hard enough for tyre smoke (BoostTuning.driftMinSlip).</summary>
        public bool Sliding => _slip > Tuning.driftMinSlip && ChaseGrounded;
        /// <summary>Seconds since a wheel last touched the ground (0 on the ground).</summary>
        public float AirTime => _airTime;
        public int DriftLevel => _drift.Level;
        public float DriftMeter => _drift.Charge;
        public bool DriftChance => _driftChance;
        public bool DriftHeld => _driftHeld;
        /// <summary>How many wheels the car has (the index range of the Wheel* calls).</summary>
        public int WheelCount => _rest != null ? _rest.Length : 0;
        /// <summary>Wheel <paramref name="i"/> touched the ground in the last physics step.</summary>
        public bool WheelGrounded(int i) => _contact != null && i >= 0 && i < _contact.Length && _contact[i];
        /// <summary>Wheel <paramref name="i"/> sits behind the middle of the wheels.</summary>
        public bool WheelIsRear(int i) => _rest != null && i >= 0 && i < _rest.Length && _rest[i].z < _midZ - 1e-4f;
        /// <summary>Wheel <paramref name="i"/>'s radius, world units.</summary>
        public float WheelRadius(int i) => _radius != null && i >= 0 && i < _radius.Length ? _radius[i] : 0f;
        /// <summary>Where wheel <paramref name="i"/>'s hub is now, world space, riding its spring.</summary>
        public Vector3 WheelHub(int i) =>
            _rest != null && i >= 0 && i < _rest.Length ? transform.TransformPoint(_rest[i]) + transform.up * _offset[i] : transform.position;

        // ---------- IBoostable ----------
        public float Nitro => _boost != null ? _boost.Nitro : 0f;
        public bool Boosting => _boost != null && _boost.Boosting && !Parked && !Wrecked;
        public float Wind => _boost != null ? _boost.Wind : 0f;
        public bool Driven => Occupied;
        private BoostTuning Tuning => settings != null && settings.boost != null ? settings.boost : DefaultBoost;
        /// <summary>The car can drive (and so boost) right now: tuned, on wheels, free and in one piece.</summary>
        private bool Drivable => settings != null && wheels != null && wheels.Length > 0 && _rb != null && !_rb.isKinematic && !Wrecked;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<Damageable>();
            _rb.mass = Mathf.Max(0.01f, mass);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearDamping = 0.05f;
            _rb.angularDamping = 1.5f;
            _renderers = OwnRenderers();   // hidden parts too: the garage can switch them on later
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
                foreach (var rr in rs) if (!(rr is ParticleSystemRenderer)) r = Mathf.Max(r, rr.bounds.extents.y);
                _radius[i] = r > 1e-4f ? r : (settings != null ? settings.wheelRadius : 0.45f) * Scale;
                hubY += _rest[i].y;
            }
            if (wheels.Length > 0) _rb.centerOfMass = new Vector3(0f, hubY / wheels.Length + centreOfMassHeight, AverageZ());
            _sizedAt = Scale;
            // the engine source on the piece carries no sound of its own: the settings name a recording, or the hum is made
            if (engine != null) engine.clip = settings != null && settings.engineClip != null ? settings.engineClip : ProceduralClips.Engine();
            _boost = new CarBoost(Tuning);
            _rearAxle = RearAxle(out _);
            _midZ = AverageZ();
        }

        private void Start()
        {
            // After every Awake (the Damageable has listed its renderers, so a hit's flash never tints the flames).
            _fx = new CarFx(transform, boostFxPrefab, driftFxPrefab, windKey, windKeyAxis, boostClip != null || windClip != null || launchClip != null);
            PlaceFx();
            // smoke, sparks, flecks, knock sounds and loose parts for the hurt car (D-0059); a Build Mode copy has one already
            if (!TryGetComponent(out _damageFx)) _damageFx = gameObject.AddComponent<CarDamageFx>();
            _damageFx.Bind(this);
        }

        private float AverageZ()
        {
            float z = 0f;
            foreach (var p in _rest) z += p.z;
            return wheels.Length > 0 ? z / wheels.Length : 0f;
        }

        /// <summary>The middle of the wheels behind the car's middle, car space at hub height, and their radius (car space).
        /// With no wheel behind the middle, a guess: the car's middle at the hubs' height.</summary>
        private Vector3 RearAxle(out float radius)
        {
            float mid = AverageZ(), s = Scale;
            Vector3 sum = Vector3.zero; float r = 0f; int n = 0;
            for (int i = 0; i < _rest.Length; i++)
            {
                if (_rest[i].z >= mid - 1e-4f) continue;
                sum += _rest[i]; r += _radius[i] / s; n++;
            }
            if (n > 0) { radius = r / n; return sum / n; }
            radius = (settings != null ? settings.wheelRadius : 0.45f);
            float y = 0f;
            foreach (var p in _rest) y += p.y;
            return new Vector3(0f, _rest.Length > 0 ? y / _rest.Length : radius, mid);
        }

        /// <summary>Flames at the rear end at hub height, smoke across the rear axle, sized by the car's drawn length.</summary>
        private void PlaceFx()
        {
            if (_fx == null) return;
            if (!BuildBounds.Local(transform, out Bounds b)) b = new Bounds(Vector3.zero, Vector3.one);
            Vector3 axle = RearAxle(out float r);
            _fx.Place(new Vector3(b.center.x, axle.y, b.min.z), new Vector3(b.center.x, axle.y - 0.5f * r, axle.z), b.size.z, Tuning.fxScale);
        }

        /// <summary>The car's own renderers, hidden ones too: not effects (a Build Mode copy carries the original's) and not
        /// a seated driver.</summary>
        private Renderer[] OwnRenderers()
        {
            var all = GetComponentsInChildren<Renderer>(true);
            var own = new List<Renderer>(all.Length);
            foreach (var r in all)
                if (!(r is ParticleSystemRenderer) && r.GetComponentInParent<PlayerMotor>(true) == null) own.Add(r);
            return own.ToArray();
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
            bool named = false;
            for (int i = 0; i < wheels.Length; i++)
            {
                string n = wheels[i].name;
                string corner = n.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');   // Car 09's front pairs: _FL1, _FL2
                steers[i] = corner.EndsWith("_FL", StringComparison.OrdinalIgnoreCase) || corner.EndsWith("_FR", StringComparison.OrdinalIgnoreCase)
                    || n.IndexOf("front", StringComparison.OrdinalIgnoreCase) >= 0;
                named |= steers[i];
            }
            if (named || wheels.Length == 0) return;
            // No wheel says front (Battle Stadium's car_4_wheel_001): the wheels ahead of the middle steer.
            float mid = 0f, min = float.MaxValue, max = float.MinValue;
            var z = new float[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                z[i] = transform.InverseTransformPoint(wheels[i].position).z;
                mid += z[i]; min = Mathf.Min(min, z[i]); max = Mathf.Max(max, z[i]);
            }
            mid /= wheels.Length;
            float slack = 0.01f * (max - min);
            for (int i = 0; i < wheels.Length; i++) steers[i] = max - min < 1e-4f || z[i] > mid + slack;   // all in a line: all steer
        }

        /// <summary>A wheel by name, any case (the Cyber pack's Car_01_Wheel_FL, Battle Stadium's car_4_wheel_001), never a
        /// steering wheel.</summary>
        private static bool IsWheelName(string n) =>
            n.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("steering", StringComparison.OrdinalIgnoreCase) < 0;

        private void OnEnable() { _health.Died += OnDied; _health.Damaged += OnHurt; }
        private void OnDisable() { _health.Died -= OnDied; _health.Damaged -= OnHurt; }
        private void OnHurt(DamageInfo _) => _lastHurt = Time.time;

        /// <summary>Hands the car to a driver's input, or takes it back with null.</summary>
        public void Drive(PlayerInput input)
        {
            _driver = input;
            if (input == null) { ForceBrake = false; DropDrift(); }
        }

        private void OnDied(DamageInfo _)
        {
            _wreckedAt = Time.time;
            if (wreckSmoke != null) wreckSmoke.Play();
            if (_boost != null) _boost.Halt();
            DropDrift();
            WreckedNow?.Invoke();
        }

        public void ResetState()
        {
            if (_heldOutside) { _heldOutside = false; Park(false); }   // a reset frees a car the cannon was holding
            if (!_rb.isKinematic) { _rb.linearVelocity = Vector3.zero; _rb.angularVelocity = Vector3.zero; }   // parked: nothing to clear
            _rb.position = _spawn; _rb.rotation = _spawnRot;
            transform.SetPositionAndRotation(_spawn, _spawnRot);
            _health.ResetState();
            _nextRam.Clear();
            _upsideDown = 0f;
            if (_boost != null) _boost.Reset();
            _wasNitro = false;
            _airTime = 0f;
            DropDrift();
            if (_fx != null) { _fx.StopAll(); _fx.ResetKey(); }
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
            _renderers = OwnRenderers();
            if (wheels == null) return;
            for (int i = 0; i < wheels.Length; i++)
            {
                float r = RadiusOf(wheels[i]);
                if (r > 1e-4f) _radius[i] = r;
            }
            PlaceFx();
            if (_damageFx != null) _damageFx.Place();
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
            _airTime = 0f;   // put down, not landed
        }

        /// <summary>True while a garage holds the car on its lift (or the cannon holds it).</summary>
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
                if (_boost != null) _boost.Halt();
                DropDrift();
            }
            else
            {
                _rb.isKinematic = false;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        // ---------- boosts (IBoostable) ----------

        /// <summary>A burst: up to <paramref name="topSpeedMultiplier"/> times top speed for <paramref name="seconds"/>, pushed
        /// along the nose, fading at the end. Ignored while parked, held or wrecked.</summary>
        public void Boost(float topSpeedMultiplier, float seconds)
        {
            if (_boost == null || !Drivable) return;
            if (!_boost.Burst(topSpeedMultiplier, seconds)) return;   // a stronger burst is running: nothing starts
            Whoosh();
            BoostStarted?.Invoke(topSpeedMultiplier);
        }

        /// <summary>The drift charge gone with nothing paid, and anyone showing it told.</summary>
        private void DropDrift()
        {
            _drift.Cancel();
            AnnounceDrift();
        }

        private void AnnounceDrift()
        {
            if (_drift.Level == _driftShown) return;
            _driftShown = _drift.Level;
            DriftLevelChanged?.Invoke(_driftShown);
        }

        public void AddNitro(float amount) { if (_boost != null) _boost.AddNitro(amount); }

        /// <summary>Sets the car flying (jump pads, the cannon). A car the cannon holds is let go first; one parked on a
        /// garage lift stays put.</summary>
        public void Launch(Vector3 velocity)
        {
            if (_rb == null) return;
            if (_heldOutside) Hold(false);
            if (_rb.isKinematic) return;
            _rb.linearVelocity = velocity;
            _upsideDown = 0f;
        }

        /// <summary>A portal: the same as PlaceAt, then on its way at <paramref name="velocity"/>. A held, parked or wrecked
        /// car stays put.</summary>
        public bool Teleport(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (_rb == null || _rb.isKinematic || _heldOutside || Wrecked) return false;
            PlaceAt(position, rotation);
            _rb.linearVelocity = velocity;
            return true;
        }

        /// <summary>Holds the car still where it is, driver and all (the cannon loads and aims it), or lets it go. A car a
        /// garage has parked stays the garage's.</summary>
        public void Hold(bool held)
        {
            if (_rb == null) return;
            if (held)
            {
                if (_heldOutside || _rb.isKinematic) return;   // already held, or on a garage lift
                _heldOutside = true;
                Park(true);
            }
            else
            {
                if (!_heldOutside) return;
                _heldOutside = false;
                Park(false);
            }
            if (_boost != null) _boost.Halt();
        }

        /// <summary>The boost sound, at most four times a second (chained pads).</summary>
        private void Whoosh()
        {
            if (_fx == null || Time.time - _lastWhoosh < 0.25f) return;
            _lastWhoosh = Time.time;
            _fx.Play(boostClip, 0.8f);
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
            if (settings == null || wheels.Length == 0) return;
            if (_rb.isKinematic) { _boost.Halt(); _slip = 0f; _airTime = 0f; _holding = false; _driftChance = _driftHeld = false; return; }   // parked on a lift or held: nothing to drive
            float dt = Time.fixedDeltaTime, s = Scale;
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            int n = wheels.Length, grounded = 0;
            bool holds = false;   // a wheel stands on, or the car hangs just off, track it holds on to (StickyTrack)
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
                if (!holds && Sticky(hit.collider)) holds = true;
                float compression = Mathf.Clamp(length - hit.distance, 0f, 2f * travel);
                _offset[i] = compression - travel;
                float vUp = Vector3.Dot(_rb.GetPointVelocity(hub), up);
                float accel = g / n * (compression / Mathf.Max(1e-4f, travel)) - settings.damping * vUp / n;
                // Bump stop (D-0038): the end of the travel, and past it when the ground comes up faster than the spring,
                // pushes much harder and soaks the hit, so a ramp's foot or a hard landing lifts the body instead of
                // slamming its box into the ground.
                float raw = length - hit.distance, stopFrom = (2f - settings.bumpZone) * travel;
                if (raw > stopFrom)
                {
                    float into = Mathf.Min(3f, (raw - stopFrom) / Mathf.Max(1e-4f, settings.bumpZone * travel));
                    accel += g / n * settings.bumpStiffness * into;
                    if (vUp < 0f) accel -= settings.bumpDamping * vUp / n;
                }
                if (accel > 0f) _rb.AddForceAtPosition(up * (accel * _rb.mass), hub, ForceMode.Force);
            }

            float share = (float)grounded / n;
            Vector3 v = _rb.linearVelocity;
            bool driving = _driver != null && !Wrecked;
            Vector2 stick = driving ? _driver.Move : Vector2.zero;
            bool handbrake = driving && _driver.JumpHeld;
            float throttle = ForceBrake ? 0f : stick.y, steer = ForceBrake ? 0f : stick.x;
            float top = settings.TopSpeed * s;
            var tuning = Tuning;

            Vector3 ground = grounded > 0 ? (normal / grounded).normalized : Vector3.up;
            _stepVelocity = v; _groundUp = ground;
            // Sticky track (D-0050): on a loop, a spiral or a banked bend the car's weight presses onto the road, not toward
            // the floor. The world's pull is cancelled and the same pull is put along the road's own down, so the springs
            // carry what they carry on level ground and climbing costs no speed: a loop is driven at any pace. (The
            // world's pull here is the props' gravity, 60: no speed the car has would carry it round a loop against that.)
            if (holds) _rb.AddForce((Vector3.up - ground) * g, ForceMode.Acceleration);
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, ground).normalized;
            Vector3 right = Vector3.Cross(ground, fwd);
            float speed = Vector3.Dot(v, fwd);
            float lateral = Vector3.Dot(v, right);
            _slip = grounded > 0 ? Mathf.Abs(lateral) / Mathf.Max(0.01f, top) : 0f;

            // Landing: the first wheel down after real air time. v is still the speed the car came in at (this step's
            // spring forces have not been applied yet).
            bool landedClean = false;
            if (grounded == 0) _airTime += dt;
            else
            {
                float air = _airTime;
                _airTime = 0f;
                if (air >= settings.landMinAir)
                {
                    Landed?.Invoke(Mathf.Max(0f, -Vector3.Dot(v, ground)) / s);
                    landedClean = driving && !ForceBrake && throttle > 0.05f && air >= settings.cleanLandingMinAir
                        && Vector3.Dot(up, ground) >= Mathf.Cos(settings.cleanLandingAngle * Mathf.Deg2Rad);
                }
            }

            // Boosts: bursts run down, nitro fills and burns, the pull-back spring winds or launches.
            _boost.Tuning = tuning;
            if (Wrecked) _boost.Halt();
            else
            {
                var feed = new CarBoost.Feed
                {
                    Driven = driving && !ForceBrake && _driver.isActiveAndEnabled,
                    Grounded = grounded > 0, Slip = _slip,
                    Sprint = driving && _driver.Sprint, Brake = handbrake,
                    Throttle = throttle, Speed = speed / Mathf.Max(0.01f, top),
                };
                if (_boost.CanWind(feed)) _boost.StartWind();
                float launched = _boost.Step(dt, feed);
                if (launched > 0f)
                {
                    if (_fx != null) _fx.Play(launchClip, 0.6f + 0.4f * launched);
                    BoostStarted?.Invoke(1f + tuning.launchBoost * launched);
                }
                if (_boost.NitroOn && !_wasNitro) { Whoosh(); BoostStarted?.Invoke(tuning.nitroMultiplier); }
            }
            _wasNitro = _boost.NitroOn;
            bool winding = _boost.Winding;

            // Drift boost: a held handbrake slide banks a level; letting go of the handbrake fires it as a burst (the
            // usual Boost path: a weaker burst never cuts a stronger one, one whoosh). Anything else drops the charge.
            var drift = settings.drift;
            _driftChance = _driftHeld = false;
            if (!driving || ForceBrake || winding || drift == null) _drift.Cancel();
            else
            {
                bool charging = grounded > 0 && _slip > drift.minSlip && speed > drift.minSpeed * top;
                // the cue (D-0058): fast enough that a slide would charge, the wheel turned hard, the handbrake not yet down
                _driftChance = !handbrake && grounded > 0 && speed > drift.minSpeed * top * 1.5f && Mathf.Abs(steer) > 0.6f;
                _driftHeld = handbrake && _drift.Charge > 0f;
                int paid = _drift.Step(dt, drift, handbrake, charging);
                if (paid == 2) Boost(drift.levelTwoBoost, drift.levelTwoSeconds);
                else if (paid == 1) Boost(drift.levelOneBoost, drift.levelOneSeconds);
            }
            AnnounceDrift();
            if (landedClean) Boost(settings.cleanLandingBoost, settings.cleanLandingSeconds);
            float mult = _boost.Multiplier, push = _boost.Push;

            if (grounded > 0)
            {
                // Engine and brakes: ease the forward speed toward the target (a boost raises the top speed).
                float target, rate;
                if (winding)
                { target = 0f; rate = settings.braking * s; }   // winding: held still while the spring tightens
                else if (ForceBrake || (Mathf.Abs(throttle) > 0.05f && Mathf.Sign(throttle) != Mathf.Sign(speed) && Mathf.Abs(speed) > 0.5f * s))
                { target = 0f; rate = settings.braking * s; }
                else if (Mathf.Abs(throttle) > 0.05f)
                {
                    // forwards the pull follows the curve (strong to the knee, fading to the top); reverse is flat
                    target = throttle > 0f ? throttle * top * mult : throttle * settings.reverseSpeed * s;
                    rate = (throttle > 0f ? settings.AccelerationAt(speed / Mathf.Max(0.01f, top)) : settings.acceleration) * s;
                }
                else
                { target = 0f; rate = settings.coastDrag * s * (handbrake ? 3f : 1f); }
                if (!winding && Mathf.Abs(speed) > Mathf.Abs(target) && Mathf.Sign(speed) == Mathf.Sign(target) && Mathf.Abs(throttle) > 0.05f) rate = settings.coastDrag * s;
                float next = Mathf.MoveTowards(speed, target, rate * dt);
                _rb.AddForce(fwd * ((next - speed) / dt * share), ForceMode.Acceleration);

                // Boost: a push along the nose up to the raised top speed (never against the brakes).
                if (push > 0f && !winding && !ForceBrake && throttle > -0.05f)
                {
                    float cap = top * mult;
                    float room = Mathf.Clamp01((cap - speed) / Mathf.Max(0.01f, 0.1f * cap));
                    if (room > 0f) _rb.AddForce(fwd * (tuning.push * settings.acceleration * s * push * room * share), ForceMode.Acceleration);
                }

                // Grip: sideways sliding dies away (much less with the handbrake: drift; full grip while winding).
                float grip = handbrake && !winding ? settings.handbrakeGrip : settings.grip;
                _rb.AddForce(-right * (lateral * Mathf.Min(grip * dt, 1f) / dt * share), ForceMode.Acceleration);

                // Steering: a yaw rate that grows with speed (and flips in reverse).
                float steerSpeed = Mathf.Clamp(speed / Mathf.Max(0.01f, settings.steerFullSpeed * s), -1f, 1f);
                float yawRate = steer * settings.steerRate * Mathf.Deg2Rad * steerSpeed * (handbrake ? 1.3f : 1f);
                Vector3 av = _rb.angularVelocity;
                float current = Vector3.Dot(av, ground);
                float follow = Mathf.Min(1f, 10f * dt);
                // Just off a wall (D-0058): the spin the hit gave the body is taken away at once (that sudden twist was
                // the jerk), and after a glance a nose still pointing into the wall eases round along it.
                if (Time.fixedTime < _wallUntil)
                {
                    follow = 1f;
                    if (_wallAlong != Vector3.zero && speed > 0f && Vector3.Dot(fwd, _wallAlong) > 0.3f && Vector3.Dot(fwd, _wallSide) < 0f)   // only a nose still turned INTO the wall
                    {
                        float off = Vector3.SignedAngle(fwd, Vector3.ProjectOnPlane(_wallAlong, ground), ground);
                        if (Mathf.Abs(off) < 70f) yawRate += off * Mathf.Deg2Rad / Mathf.Max(0.02f, settings.wallAlignSeconds);
                    }
                }
                _rb.angularVelocity = av + ground * ((yawRate - current) * follow * share);

                // A little downforce at speed keeps it on the track over crests. Measured against maxSpeed (the base, not
                // the scaled top), so the pull at any given speed is what it was before the top speed went up.
                _rb.AddForce(-ground * (g * settings.downforce * Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, settings.maxSpeed * s)) * share), ForceMode.Acceleration);

                // Winding: the rear is pressed down onto its springs, so the car squats back.
                if (winding && _boost.Wind > 0f)
                    _rb.AddForceAtPosition(-up * (settings.windSquat * g * _boost.Wind * _rb.mass), transform.TransformPoint(_rearAxle), ForceMode.Force);
            }
            else
            {
                // Sticky track (D-0050): on a loop, a spiral or a banked bend the wheels can lose the road for a moment. While
                // such a piece is within its reach under the car, the car keeps the way up it has and is drawn back onto
                // the road: it is not turned upright (that would tip it off a loop) and the stick does not tumble it.
                StickyTrack held = StickyTrack.Under(_rb.worldCenterOfMass, -up, s, _hits, transform);
                if (held != null)
                {
                    holds = true;
                    _airTime = 0f;   // a hop on road it holds on to is not a flight: no landing, no landing burst
                    // its weight still points at the road it hopped off, plus the piece's own pull to bring it back
                    _rb.AddForce(Vector3.up * g - up * (g + held.Pull * s), ForceMode.Acceleration);
                    _rb.AddTorque(-_rb.angularVelocity * 0.5f, ForceMode.Acceleration);
                }
                else
                {
                    // In the air: turn gently upright so it lands on its wheels.
                    Vector3 axis = Vector3.Cross(up, Vector3.up);
                    _rb.AddTorque(axis * settings.airUpright - _rb.angularVelocity * 0.5f, ForceMode.Acceleration);
                    // Air control: the stick tips the nose (forward = down) and turns the car, or rolls it with the handbrake
                    // held. Gentle: the self-righting above pulls against it, enough to line up a landing, not to fly.
                    if (driving && !ForceBrake && stick.sqrMagnitude > 0.0025f)
                    {
                        Vector3 control = transform.right * (stick.y * settings.airPitch)
                            + (handbrake ? -transform.forward * (stick.x * settings.airRoll) : up * (stick.x * settings.airYaw));
                        _rb.AddTorque(control, ForceMode.Acceleration);
                    }
                }
            }
            _steerVisual = Mathf.MoveTowards(_steerVisual, steer * settings.visualSteerAngle, 240f * dt);

            // Flipped and stuck: right it after a moment.
            // (not on track it holds on to: a car stopped at the top of a loop is where it should be)
            _upsideDown = !holds && Vector3.Dot(up, Vector3.up) < 0.3f && v.magnitude < 2f * s ? _upsideDown + dt : 0f;
            if (_upsideDown > 1.5f)
            {
                _upsideDown = 0f;
                _rb.position += Vector3.up * (1.2f * s);
                _rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                _rb.angularVelocity = Vector3.zero;
            }
            _holding = holds;
            if (transform.position.y < _spawn.y - 80f * s) { WreckedNow?.Invoke(); Recover(); }
        }

        /// <summary>Whether what a wheel stands on is track the car holds on to. Looked up once per thing stood on.</summary>
        private bool Sticky(Collider under)
        {
            if (under == _underKnown) return _underSticky;
            _underKnown = under;
            _underSticky = under != null && under.GetComponentInParent<StickyTrack>() != null;
            return _underSticky;
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
                    // past the top (a boost) the pitch keeps climbing, so the burst is heard
                    float raw = Speed / Mathf.Max(0.01f, TopSpeed), t = Mathf.Clamp01(raw);
                    engine.pitch = Mathf.Lerp(settings.enginePitch.x, settings.enginePitch.y, t) + settings.engineOverPitch * Mathf.Clamp01(raw - 1f);
                    engine.volume = Mathf.Lerp(settings.engineVolume.x, settings.engineVolume.y, t);
                }
            }

            if (_fx != null)
            {
                if (Parked || Wrecked) _fx.StopAll();
                else
                {
                    var tuning = Tuning;
                    bool sliding = _slip > tuning.driftMinSlip && ChaseGrounded;
                    _fx.Tick(dt, _boost.Boosting, sliding, _boost.WindGrowing, _boost.Wind, _boost.Launching, tuning.keySpin, windClip);
                }
            }

            // Mending while driving (D-0038): after a few seconds without a hit, health climbs back.
            if (settings != null && _driver != null && !Wrecked && _health.Health < _health.MaxHealth && Time.time - _lastHurt >= settings.regenDelay)
                _health.Heal(_health.MaxHealth * settings.regenPerSecond * dt);
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
            // A crash is how hard the car hit INTO the surface (the speed along the contact normal), not its whole speed:
            // rolling onto a ramp or scraping a wall barely counts, a head-on hit or a hard flat landing does (D-0038).
            float impact = Impact(c) / s;
            // Loose, light things (cones, a wall of toy blocks) are there to be driven through: the hit is felt, but only
            // as a tap (D-0058: a cone used to shake the camera like a crash), and it does not hurt the car.
            bool smashed = c.rigidbody != null && !c.rigidbody.isKinematic && c.rigidbody.mass <= settings.smashMass;
            // A wall hit at a shallow angle glances (D-0058): the car slides on along it, felt half as hard, unhurt.
            bool glanced = !smashed && Glance(c, true);
            float felt = smashed ? Mathf.Min(impact, settings.smashFeel) : glanced ? impact * 0.5f : impact;
            bool hurts = !smashed && !glanced && !Wrecked && impact > settings.crashMinSpeed && Time.time >= _nextCrash;
            bool feels = felt > settings.feelMinImpact && Time.time >= _nextImpactFeel;
            if (feels)
            {
                _nextImpactFeel = Time.time + 0.1f;
                Crashed?.Invoke(felt);
            }
            Vector3 hitPoint = transform.position, face = Vector3.up;
            if ((feels || hurts) && c.contactCount > 0)
            {
                hitPoint = Vector3.zero; face = Vector3.zero;
                int n = c.contactCount;
                for (int i = 0; i < n; i++) { var contact = c.GetContact(i); hitPoint += contact.point; face += contact.normal; }
                hitPoint /= n;
                face = face.sqrMagnitude > 1e-6f ? face.normalized : Vector3.up;
            }
            // shown first (the hit's sparks and sound), then taken: the damage's own report is then known to be this hit
            if (feels || hurts) Struck?.Invoke(new CarImpact(hurts ? impact : felt, hitPoint, face, hurts, c.rigidbody == null || c.rigidbody.isKinematic));
            if (hurts)
            {
                _nextCrash = Time.time + 0.35f;
                DropDrift();   // a crash that hurts ends the slide's charge
                _health.Hit(new DamageInfo((impact - settings.crashMinSpeed) * settings.crashDamagePerSpeed, Vector3.zero, hitPoint, c.transform));
            }
        }

        /// <summary>Rubbing along a wall: kept sliding along it, off its face, with the knock's spin taken away.</summary>
        private void OnCollisionStay(Collision c)
        {
            if (settings == null) return;
            if (c.rigidbody != null && !c.rigidbody.isKinematic) return;   // things that move: the physics as it is
            Glance(c, false);
        }

        /// <summary>
        /// The wall rule (D-0058). A box body that touches a wall at an angle is caught by a corner: the physics stops the
        /// speed into the wall, scrubs the rest and twists the car, which read as a hard, unnatural jerk. Here, for
        /// anything fixed (no rigidbody, or a kinematic one) whose face stands up from the road: on the first touch
        /// (<paramref name="first"/>) a hit shallower than settings.wallGlanceAngle keeps its speed along the wall less a
        /// little scrape (settings.wallScrapeLoss, more the steeper it was) and is given settings.wallPushOff back off the
        /// face; while it rubs, nothing heads into the wall. Either way, for settings.wallSettle the steering takes away
        /// the spin the knock gave and eases a nose still pointing into the wall round along it (FixedUpdate). Returns
        /// true for a glance (the caller does not count it as a crash). Steeper hits are left to the physics: crashes.
        /// </summary>
        private bool Glance(Collision c, bool first)
        {
            if (Wrecked || _rb.isKinematic || settings.wallGlanceAngle <= 0f) return false;
            if (c.rigidbody != null && !c.rigidbody.isKinematic) return false;
            int n = c.contactCount;
            if (n == 0) return false;
            Vector3 up = _groundUp, face = Vector3.zero;
            for (int i = 0; i < n; i++) face += c.GetContact(i).normal;
            face.Normalize();
            if (Mathf.Abs(Vector3.Dot(face, up)) > 0.6f) return false;   // a floor, a ramp or a ceiling, not a wall
            Vector3 side = Vector3.ProjectOnPlane(face, up).normalized;     // out of the wall, toward the car
            if (side == Vector3.zero) return false;
            float s = Scale;
            _wallSide = side;
            _wallUntil = Time.fixedTime + settings.wallSettle;

            Vector3 now = _rb.linearVelocity;
            Vector3 nowUp = Vector3.Project(now, up), nowFlat = now - nowUp;
            float push = settings.wallPushOff * s;
            if (!first)
            {
                // rubbing: whatever still heads into the wall, or hugs it, comes off it at the push-off speed
                float outward = Vector3.Dot(nowFlat, side);
                if (outward < push) _rb.linearVelocity = nowFlat + side * (push - outward) + nowUp;
                Vector3 sliding = nowFlat - side * outward;   // the nose eases along the way it slides
                if (sliding.sqrMagnitude > s * s) _wallAlong = sliding.normalized;
                return true;
            }

            _wallAlong = Vector3.zero;   // a new touch: only a glance below sets a way to align to
            Vector3 came = Vector3.ProjectOnPlane(_stepVelocity, up);   // what the car brought into the wall
            float speed = came.magnitude;
            if (speed < 1f * s) return true;   // a nudge at a crawl: settled, nothing to keep
            float into = -Vector3.Dot(came, side);
            if (into <= 0f) return true;       // already moving off the wall: only the spin needs settling
            float sin = into / speed, most = Mathf.Sin(settings.wallGlanceAngle * Mathf.Deg2Rad);
            if (sin > most) { _wallAlong = Vector3.zero; return false; }   // too steep: a crash
            Vector3 along = came + side * into;   // the part of its way that runs along the wall
            if (along.sqrMagnitude < 1e-6f) return true;
            float keep = speed * (1f - settings.wallScrapeLoss * (sin / Mathf.Max(1e-4f, most)));
            Vector3 alongDir = along.normalized;
            // keep the faster of what the physics left and what the glance keeps: never slower than the plain physics
            float left = Mathf.Max(0f, Vector3.Dot(nowFlat, alongDir));
            _rb.linearVelocity = alongDir * Mathf.Max(left, keep) + side * push + nowUp;
            _wallAlong = alongDir;
            return true;
        }

        /// <summary>The fastest closing speed along any contact's normal, world units per second.</summary>
        private static float Impact(Collision c)
        {
            int n = c.contactCount;
            Vector3 rv = c.relativeVelocity;
            if (n == 0) return rv.magnitude;
            float most = 0f;
            for (int i = 0; i < n; i++) most = Mathf.Max(most, Mathf.Abs(Vector3.Dot(rv, c.GetContact(i).normal)));
            return most;
        }
    }
}
