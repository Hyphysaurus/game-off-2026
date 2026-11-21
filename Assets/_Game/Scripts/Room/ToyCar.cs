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
    /// Mode lets go, the ball goes back under the body (sized to it) and takes over again. Hold (the cannon) parks the ball
    /// the same way.
    /// Boosts (D-0037, IBoostable): bursts from pads, gates and the launch raise the top speed and push along the nose;
    /// Sprint (Shift) burns the nitro meter, which air time, slides and pickups fill; stopped with forward held, Jump winds
    /// the pull-back spring instead of hopping (the body tips back; Jump held first winds once the car lands still) and
    /// letting go of Jump launches.
    /// </summary>
    public sealed class ToyCar : MonoBehaviour, IInteractable, IBuildMoved, IChaseTarget, IBoostable
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
        [Header("Boosts (D-0037)")]
        [SerializeField, Tooltip("Bursts, nitro, the pull-back launch and the effects' size.")] private BoostTuning boost = new BoostTuning();
        [SerializeField, Tooltip("Degrees the body tips back (nose up) at full wind.")] private float windSquat = 6f;
        [SerializeField, Tooltip("Exhaust flames while boosting (the Cyber pack's BoostFX), made at the rear at run time.")] private GameObject boostFxPrefab;
        [SerializeField, Tooltip("Tyre smoke while sliding (the Cyber pack's DriftFX), made across the rear axle at run time.")] private GameObject driftFxPrefab;
        [SerializeField, Tooltip("A wind-up key that turns while the spring winds (optional).")] private Transform windKey;
        [SerializeField, Tooltip("The axis the key turns about, in the key's own space.")] private Vector3 windKeyAxis = Vector3.up;
        [SerializeField, Tooltip("Played when a burst starts and when nitro lights.")] private AudioClip boostClip;
        [SerializeField, Tooltip("Looped while the spring winds.")] private AudioClip windClip;
        [SerializeField, Tooltip("Played on the pull-back launch.")] private AudioClip launchClip;

        private Rigidbody _ball; private SphereCollider _sphere; private float _radius, _yaw, _flip, _scaleAt;
        private bool _held, _ignoreDirty;   // held: the ball waits and the body leads; dirty: the ball must skip the body's colliders again
        private bool _buildHold, _outsideHold;   // why it is held: Build Mode is on, or Hold(true) (the cannon)
        private Vector3 _startPos; private Quaternion _startRot;
        private PlayerMotor _driverMotor; private PlayerInput _input; private Interactor _interactor; private CharacterController _cc; private Animator _anim;
        private Renderer[] _body; private Vector3 _bodyOffset; private ShoulderCamera _camera;
        private Transform _driver; private float _enteredAt; private bool _grounded; private AudioSource _audio; private Vector3 _groundNormal = Vector3.up;
        private CarBoost _boost; private CarFx _fx;
        private float _slip, _squat, _lastWhoosh = -10f; private bool _wasNitro;

        /// <summary>The physics ball, or null while Build Mode holds it.</summary>
        public Transform Ball => _ball != null && !_held ? _ball.transform : null;
        public float BallRadius => _radius;
        /// <summary>The car's own pull down, units/s² (its ball does not use Unity's gravity): jump pads and the cannon aim with it.</summary>
        public float Gravity => gravity;
        public bool Driving => _driver != null;
        public Vector3 ChaseVelocity => _ball != null && !_held ? _ball.linearVelocity : Vector3.zero;
        public float ChaseTopSpeed => topSpeed;
        public bool ChaseGrounded => _grounded;
        private readonly List<Renderer> _hiddenDriver = new List<Renderer>();   // only the car shows while driving

        // ---------- IBoostable ----------
        public float Nitro => _boost != null ? _boost.Nitro : 0f;
        public bool Boosting => _boost != null && _boost.Boosting && _ball != null && !_held;
        public float Wind => _boost != null ? _boost.Wind : 0f;
        public bool Driven => _driver != null;

        private void Awake()
        {
            _boost = new CarBoost(boost);
        }

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
            SetDriving(false);
        }

        private void OnDestroy()
        {
            if (_ball != null) Destroy(_ball.gameObject);
        }

        private void Start()
        {
            _body = BodyRenderers();
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
            _fx = new CarFx(transform, boostFxPrefab, driftFxPrefab, windKey, windKeyAxis, boostClip != null || windClip != null || launchClip != null);
            PlaceFx();
            _buildHold = GameMode.Current == GameMode.Mode.Build;
            if (_buildHold || _outsideHold) HoldBall();
        }

        /// <summary>The body's renderers: not effects (a Build Mode copy carries the original's flames).</summary>
        private Renderer[] BodyRenderers()
        {
            var all = GetComponentsInChildren<Renderer>();
            var own = new List<Renderer>(all.Length);
            foreach (var r in all) if (!(r is ParticleSystemRenderer)) own.Add(r);
            return own.ToArray();
        }

        private Bounds Bounds()
        {
            var rs = _body != null && _body.Length > 0 ? _body : GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(transform.position, Vector3.one);
            foreach (var r in rs) if (r != null) b.Encapsulate(r.bounds); return b;
        }

        /// <summary>Flames at the rear end at the rear wheels' height, smoke across the rear axle, sized by the body's length.</summary>
        private void PlaceFx()
        {
            if (_fx == null) return;
            if (!BuildBounds.Local(transform, out Bounds b)) return;
            float nose = _flip > 90f ? -1f : 1f;   // the body's +Z is its nose unless flipped
            float mid = 0f; int count = 0;
            foreach (var w in wheels) if (w != null) { mid += transform.InverseTransformPoint(w.position).z * nose; count++; }
            Vector3 axle = Vector3.zero; int rear = 0;
            if (count > 0)
            {
                mid /= count;
                foreach (var w in wheels)
                {
                    if (w == null) continue;
                    Vector3 p = transform.InverseTransformPoint(w.position);
                    if (p.z * nose < mid - 1e-4f) { axle += p; rear++; }
                }
            }
            if (rear > 0) axle /= rear;
            else axle = new Vector3(b.center.x, b.min.y + b.size.y * 0.25f, nose > 0f ? b.min.z + b.size.z * 0.2f : b.max.z - b.size.z * 0.2f);
            float r = Mathf.Max(0.01f, axle.y - b.min.y);
            _fx.Place(new Vector3(b.center.x, axle.y, nose > 0f ? b.min.z : b.max.z), new Vector3(b.center.x, b.min.y + 0.5f * r, axle.z), b.size.z, boost.fxScale);
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
            _camera = PlayerSlot.RigFor(_input); if (_camera != null) _camera.SetChase(transform, _flip, chaseDistance);
            SetDriving(true);
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
            // Unity refuses a reparent while this car is being switched off (Play stopping, a scene unloading): skip it then.
            if (!global::ShiftJam.Driving.Quitting && gameObject.activeInHierarchy) _driver.SetParent(null, true);
            foreach (var r in _hiddenDriver) if (r != null) r.forceRenderingOff = false;
            _hiddenDriver.Clear();
            _cc.enabled = true; _driverMotor.enabled = true; _interactor.enabled = true;
            _driverMotor.Warp(at, Quaternion.Euler(0f, transform.eulerAngles.y - _flip, 0f));
            _driver = null;
            if (_camera != null) _camera.SetChase(null);
            SetDriving(false);
            onExit?.Invoke();
        }

        /// <summary>Tells the static Driving record this car is driven, or no longer (only if it was this car). Named in full:
        /// this class's own Driving property hides it.</summary>
        private void SetDriving(bool on)
        {
            if (on) global::ShiftJam.Driving.Set(transform);
            else if (global::ShiftJam.Driving.Vehicle == transform) global::ShiftJam.Driving.Set(null);
        }

        private void Hop()
        {
            if (!Driving || !_grounded || _held) return;
            Vector3 v = _ball.linearVelocity;
            Vector3 fwd = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            // stopped with forward held: Jump winds the pull-back spring instead (held until Jump lets go)
            if (_boost.CanWind(Feed(_input.Move, Vector3.Dot(new Vector3(v.x, 0f, v.z), fwd)))) { _boost.StartWind(); return; }
            _ball.AddForce(Vector3.up * Mathf.Sqrt(2f * gravity * hopHeight), ForceMode.VelocityChange);
        }

        /// <summary>What the boost needs to know this step.</summary>
        private CarBoost.Feed Feed(Vector2 move, float along)
        {
            bool live = _driver != null && _input != null;
            return new CarBoost.Feed
            {
                Driven = live && _input.isActiveAndEnabled,
                Grounded = _grounded, Slip = _slip,
                Sprint = live && _input.Sprint, Brake = live && _input.JumpHeld,
                Throttle = move.y, Speed = along / Mathf.Max(0.01f, topSpeed),
            };
        }

        // ------------------------------------------------------------------ boosts (IBoostable)

        /// <summary>A burst: up to <paramref name="topSpeedMultiplier"/> times top speed for <paramref name="seconds"/>, pushed
        /// along the nose, fading at the end. Ignored while held.</summary>
        public void Boost(float topSpeedMultiplier, float seconds)
        {
            if (_boost == null || _ball == null || _held) return;
            if (_boost.Burst(topSpeedMultiplier, seconds)) Whoosh();
        }

        public void AddNitro(float amount) { if (_boost != null) _boost.AddNitro(amount); }

        /// <summary>Sets the car flying (jump pads, the cannon). A cannon's hold is let go first; Build Mode's is not.</summary>
        public void Launch(Vector3 velocity)
        {
            if (_ball == null) return;
            if (_outsideHold) Hold(false);
            if (_held) return;
            _ball.linearVelocity = velocity;
        }

        /// <summary>A portal: the body goes to the pose, the ball goes under it, and both carry on at <paramref name="velocity"/>.
        /// Not while held (Build Mode, the cannon) or before Start has made the ball.</summary>
        public bool Teleport(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (_ball == null || _held) return false;
            transform.SetPositionAndRotation(position, rotation);
            PutBallUnder();   // also turns the steering heading to the new facing and stops the ball
            _ball.linearVelocity = velocity;
            return true;
        }

        /// <summary>Holds the car still where it is, driver and all, the way Build Mode does (the body leads, the ball waits),
        /// or lets it go: the ball goes back under the body wherever it was left.</summary>
        public void Hold(bool held)
        {
            _outsideHold = held;
            ApplyHold();
        }

        private void ApplyHold()
        {
            if (_ball == null) return;   // Start applies it
            bool want = _buildHold || _outsideHold;
            if (want && !_held) HoldBall();
            else if (!want && _held) ReleaseBall();
        }

        /// <summary>The boost sound, at most four times a second (chained pads).</summary>
        private void Whoosh()
        {
            if (_fx == null || Time.time - _lastWhoosh < 0.25f) return;
            _lastWhoosh = Time.time;
            _fx.Play(boostClip, 0.8f);
        }

        // ------------------------------------------------------------------ Build Mode

        /// <summary>Build Mode placed, moved, turned or scaled the car (or undid or loaded it): it starts from here now.</summary>
        public void OnBuildMoved()
        {
            if (_ball == null) return;   // not started yet: Start measures the pose it is given
            _startPos = PutBallUnder();
            _startRot = transform.rotation;
            PlaceFx();   // effects ride in car space; this re-fits their sound range to a new scale
        }

        /// <summary>Build Mode is on (or the cannon holds it): the ball stops and switches its collider off, so Build Mode's
        /// rays and drags never land on it, and the body stays where it is put.</summary>
        private void HoldBall()
        {
            _held = true;
            if (!_ball.isKinematic) _ball.linearVelocity = Vector3.zero;
            _ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;   // the only mode a kinematic body takes
            _ball.isKinematic = true;
            _sphere.enabled = false;
            _boost.Halt();
            if (_fx != null) _fx.StopAll();
        }

        /// <summary>Let go: the ball goes under the body where it was left and drives it again.</summary>
        private void ReleaseBall()
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
            Vector3 side = planar - fwd * along;
            _slip = _grounded ? side.magnitude / Mathf.Max(0.01f, topSpeed) : 0f;

            // Boosts: bursts run down, nitro fills and burns, the pull-back spring winds or launches.
            _boost.Tuning = boost;
            var feed = Feed(move, along);
            if (_boost.CanWind(feed)) _boost.StartWind();   // Jump held first (a hop, then landed still): winds too
            float launched = _boost.Step(dt, feed);
            if (launched > 0f && _fx != null) _fx.Play(launchClip, 0.6f + 0.4f * launched);
            if (_boost.NitroOn && !_wasNitro) Whoosh();
            _wasNitro = _boost.NitroOn;
            float mult = _boost.Multiplier, push = _boost.Push;

            if (_grounded)
            {
                float want = move.y > 0f ? move.y * topSpeed * mult : move.y * topSpeed;   // a boost raises the top speed
                float rate = Mathf.Approximately(move.y, 0f) ? coastDrag : (Mathf.Sign(want) != Mathf.Sign(along) && Mathf.Abs(along) > 0.5f ? braking : acceleration);
                if (_boost.Winding) { want = 0f; rate = braking; }   // winding: held still while the spring tightens
                float next = Mathf.MoveTowards(along, want, rate * dt);
                // a boost pushes along the nose up to the raised top speed (never against the brakes)
                if (push > 0f && !_boost.Winding && move.y > -0.05f)
                {
                    float cap = topSpeed * mult;
                    if (next < cap) next = Mathf.Min(cap, next + boost.push * acceleration * push * dt);
                }
                // grip: the sideways slide dies quickly, so the car goes where it points
                Vector3 newPlanar = fwd * next + Vector3.MoveTowards(side, Vector3.zero, 80f * dt);
                _ball.linearVelocity = new Vector3(newPlanar.x, v.y, newPlanar.z);
                float speed01 = Mathf.Clamp01(Mathf.Abs(next) / topSpeed);
                // steering flips in reverse like a real car; standing still it turns the way the stick says
                float dir = Mathf.Abs(next) > 0.5f ? Mathf.Sign(next) : (move.y < -0.1f ? -1f : 1f);
                _yaw += move.x * turnRate * dt * dir * Mathf.Lerp(pivotTurn, 1f, Mathf.Clamp01(speed01 * 3f));
            }
            else _yaw += move.x * turnRate * airTurn * dt;
            if (_ball.position.y < -20f)
            {
                if (Driving) GetOut();
                _ball.position = _startPos; _ball.linearVelocity = Vector3.zero; _yaw = _startRot.eulerAngles.y + _flip;
                _boost.Reset();
            }
        }

        private void Update()
        {
            if (_ball == null) return;
            bool build = GameMode.Current == GameMode.Mode.Build;
            if (build != _buildHold) { _buildHold = build; ApplyHold(); }
            if (_held)
            {
                if (_audio != null) _audio.volume = Mathf.MoveTowards(_audio.volume, 0f, Time.deltaTime * 3f);
                if (_fx != null) _fx.StopAll();
                return;
            }
            Vector3 bottom = _ball.position + Vector3.down * _radius;
            // winding tips the body back, nose up, about the way it drives
            _squat = Mathf.MoveTowards(_squat, _boost.Winding ? windSquat * _boost.Wind : 0f, Mathf.Max(1f, windSquat) * 4f * Time.deltaTime);
            Quaternion facing = Quaternion.Euler(0f, _yaw, 0f) * Quaternion.Euler(-_squat, 0f, 0f) * Quaternion.Euler(0f, -_flip, 0f);
            Quaternion lean = Quaternion.FromToRotation(Vector3.up, _grounded ? _groundNormal : Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lean * facing, 12f * Time.deltaTime);
            transform.position = bottom + transform.rotation * _bodyOffset;
            float speed = new Vector3(_ball.linearVelocity.x, 0f, _ball.linearVelocity.z).magnitude;
            foreach (var w in wheels) if (w != null) w.Rotate(transform.right, speed * Time.deltaTime * 60f, Space.World);
            if (_audio != null) { _audio.volume = Mathf.MoveTowards(_audio.volume, _grounded ? Mathf.Clamp01(speed / topSpeed) * 0.6f : 0f, Time.deltaTime * 3f); _audio.pitch = 0.8f + speed / topSpeed * 0.6f; }
            if (_fx != null) _fx.Tick(Time.deltaTime, _boost.Boosting, _grounded && _slip > boost.driftMinSlip, _boost.WindGrowing, _boost.Wind, _boost.Launching, boost.keySpin, windClip);
        }

    }
}
