using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Third-person follow camera: orbits a pivot above the player's feet, clamps pitch,
    /// and pulls in when a wall is between the pivot and the camera. Runs in LateUpdate, after
    /// the player has moved this frame, which is why it never jitters.
    /// All distances scale with the Shifter, so the framing feels the same at toy size.
    /// Three views on foot, eased into one another (CameraSettings): wide with empty hands, over the shoulder with a
    /// weapon drawn, closer and narrower while aim is held. The way to the camera is swept with a ball as big as the
    /// near plane's reach, up from the body's middle, then sideways to the shoulder, then back: so the camera stops
    /// short of a wall and never starts inside one. When that brings it into the player's own body, InsideBody says so
    /// and BodyFade hides the body.
    /// While a vehicle is driven it frames the car instead (D-0036, VehicleCameraSettings): its middle, by its own size,
    /// trailing it a little, swinging in behind the way it travels, slower in the air; with speed the view widens and the
    /// camera moves in and flattens, more so past top speed (a boost); in the garage (menu up) right-drag, A/D and the
    /// wheel orbit and zoom round the parked car. Getting out blends back.
    /// <see cref="Shake"/> rattles the finished view for a moment and <see cref="PunchFov"/> kicks the field of view
    /// (DrivingFeel calls them for boosts, landings and crashes).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ShoulderCamera : MonoBehaviour
    {
        [SerializeField] private CameraSettings settings;
        [SerializeField, Tooltip("The player's root (feet).")] private Transform target;
        [SerializeField] private PlayerInput input;
        [SerializeField, Tooltip("Optional. Found on the target if empty.")] private Shifter shifter;
        [SerializeField, Tooltip("What the camera probe collides with. Leave the player's own layer in; its colliders are skipped by hand.")]
        private LayerMask collisionMask = ~0;

        private Camera _camera;
        private WeaponHolder _holder;        // on the target: a drawn weapon picks the view
        private PlayerStance _stance;        // on the target: a crouch lowers the pivot
        private CharacterController _capsule;
        private float _side, _rise, _reach, _fovScale = 1f, _heightShare = 1f; private bool _viewReady;   // the view now, eased
        private float _yaw, _pitch;
        private bool _cursorLocked;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        // vehicle mode (D-0036): while a vehicle is driven the camera frames the car, not the driver
        [SerializeField, Tooltip("Vehicle camera tuning. Resources/VehicleCamera is loaded if empty (built-in defaults if there is none).")]
        private VehicleCameraSettings vehicle;
        private Transform _chase; private float _chaseYawOffset, _lastLookTime = -10f;
        private IChaseTarget _chaseInfo;
        private IChaseHold _chaseHold;      // the same vehicle, if it can hold on to the road (null: it cannot)
        private float _holdShare;           // 0 = the ordinary upright view, 1 = riding in the road's frame
        private Quaternion _holdRot = Quaternion.identity; private bool _holdReady;   // the road's frame, eased
        private Vector3 _chaseCentreLocal;                     // the car's middle, in its own space
        private float _chaseLength = 1f, _chaseHeight = 1f;   // its size in world units, measured when the chase starts
        private Vector3 _aim, _aimVelocity, _lastChasePos;
        private bool _aimReady;
        private float _yawVelocity, _speedShare, _overShare, _showroomZoom = 1f;   // over: how far past top speed, 0..1
        // feedback on top of the finished view: a decaying shake and a field-of-view kick
        private const float PunchAttack = 0.2f;   // share of a punch's time spent swinging out (the rest eases back)
        private const float ShakeRoom = 0.8f;     // share of the camera's known clearance a shake may move it through
        private const float ShakeRoll = 0.5f;     // the roll part of a shake, against its tilt up-down and sideways
        private float _shakeAmount, _shakeTime, _shakeLeft;
        private float _punchDegrees, _punchTime, _punchLeft;
        private float _leave; private Vector3 _leavePos; private Quaternion _leaveRot;   // blending back to the shoulder

        private VehicleCameraSettings Vehicle
        {
            get
            {
                if (vehicle == null) vehicle = Resources.Load<VehicleCameraSettings>("VehicleCamera");
                if (vehicle == null) vehicle = ScriptableObject.CreateInstance<VehicleCameraSettings>();   // built-in defaults
                return vehicle;
            }
        }

        public float Yaw => _yaw;
        public float Pitch => _pitch;
        /// <summary>True while the on-foot camera is so close to the player's body that it would look out from inside it
        /// (CameraSettings.hideBodyWithin, with slack on the way back). BodyFade hides the body meanwhile.</summary>
        public bool InsideBody { get; private set; }
        /// <summary>The settings in use (BodyFade reads the fade time).</summary>
        public CameraSettings Settings => settings;
        /// <summary>Degrees added to the settings' field of view this frame. ShiftWarp punches it during a shift; 0 otherwise.</summary>
        public float FovOffset { get; set; }

        /// <summary>What is left of the running shake, 0..1.</summary>
        private float ShakeLevel
        {
            get
            {
                if (_shakeLeft <= 0f || _shakeTime <= 0f) return 0f;
                float k = _shakeLeft / _shakeTime;
                return _shakeAmount * k * k;
            }
        }

        /// <summary>The field-of-view kick now, degrees: out fast, back slower.</summary>
        private float PunchNow
        {
            get
            {
                if (_punchLeft <= 0f || _punchTime <= 0f) return 0f;
                float p = 1f - _punchLeft / _punchTime;   // 0 at the start, 1 at the end
                float k = p < PunchAttack ? p / PunchAttack : 1f - (p - PunchAttack) / (1f - PunchAttack);
                return _punchDegrees * k * k * (3f - 2f * k);
            }
        }

        /// <summary>
        /// Rattles the view for <paramref name="seconds"/>, dying away. <paramref name="amount"/> runs 0..1 (1 = the
        /// strongest the settings allow: VehicleCameraSettings.shakeOffset and shakeAngle). It is added after the camera
        /// has found its place, mostly as a tilt; the small sideways part stays inside the camera's own clearance and is
        /// dropped while a wall has pushed the camera in, so a shake never puts the view through anything. A weaker shake
        /// never cuts a stronger one short.
        /// </summary>
        public void Shake(float amount, float seconds)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f || seconds <= 0f || amount < ShakeLevel) return;
            _shakeAmount = amount; _shakeTime = seconds; _shakeLeft = seconds;
        }

        /// <summary>Kicks the field of view out by <paramref name="degrees"/> and eases it back over
        /// <paramref name="seconds"/> (a boost starting). A smaller kick never cuts a bigger one short.</summary>
        public void PunchFov(float degrees, float seconds)
        {
            if (degrees <= 0f || seconds <= 0f || degrees < PunchNow) return;
            _punchDegrees = degrees; _punchTime = seconds; _punchLeft = seconds;
        }

        /// <summary>
        /// The shake for this frame, added to a finished pose. <paramref name="unit"/> is the length shakeOffset is a share
        /// of (the car's length, or the player's size on foot); <paramref name="clearance"/> is the free room known to be
        /// round the camera (its probe's radius), 0 when a wall has pushed it in.
        /// </summary>
        private void AddShake(ref Vector3 position, ref Quaternion rotation, float unit, float clearance, float dt)
        {
            if (_shakeLeft <= 0f) return;
            _shakeLeft = Mathf.Max(0f, _shakeLeft - dt);
            float level = ShakeLevel;
            if (level <= 0f) return;
            var v = Vehicle;
            float t = Time.unscaledTime * v.shakeFrequency;
            float x = Mathf.PerlinNoise(t, 0.37f) * 2f - 1f, y = Mathf.PerlinNoise(0.71f, t) * 2f - 1f, z = Mathf.PerlinNoise(t, 7.13f) * 2f - 1f;
            float reach = Mathf.Min(unit * v.shakeOffset * level, clearance * ShakeRoom);
            if (reach > 0f) position += rotation * new Vector3(x * reach, y * reach, 0f);   // never along the view
            float tilt = v.shakeAngle * level;
            rotation *= Quaternion.Euler(y * tilt, x * tilt, z * tilt * ShakeRoll);
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (shifter == null && target != null) shifter = target.GetComponent<Shifter>();
            if (target != null)
            {
                _holder = target.GetComponent<WeaponHolder>();
                _stance = target.GetComponent<PlayerStance>();
                _capsule = target.GetComponent<CharacterController>();
            }
        }

        private void Start()
        {
            _yaw = target != null ? target.eulerAngles.y : 0f;
            _pitch = settings.startPitch;
            SetCursorLocked(true);
        }

        private void OnEnable() { SetCursorLocked(!_menuHold); }   // coming back from a menu, Build Mode or first person
        private void OnDisable() { SetCursorLocked(false); InsideBody = false; }

        private bool _menuHold;

        /// <summary>The garage menu holds the mouse (PauseMenu leaves Esc and Start to it).</summary>
        public bool MenuHeld => _menuHold;

        /// <summary>
        /// A menu on screen (the garage) holds the mouse: the cursor stays free and a click goes to the menu instead of
        /// locking the mouse again. The camera keeps running (it follows the car, mouse look is off while the cursor is
        /// free). Released, it takes the mouse back if it is running; if Build Mode switched it off, its OnEnable will.
        /// </summary>
        public void SetMenuHold(bool held)
        {
            _menuHold = held;
            if (!held) _showroomZoom = 1f;   // leaving the garage: back to the driving distance (a body swap keeps the zoom)
            if (held) SetCursorLocked(false);
            else if (isActiveAndEnabled) SetCursorLocked(true);
        }

        private void Update()
        {
            if (_menuHold) return;
            // Escape opens the pause menu, which frees the mouse by switching this camera off (D-0044); a click in the
            // game view takes the mouse back if it got free some other way.
            if (!_cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) SetCursorLocked(true);
        }

        private void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void LateUpdate()
        {
            if (target == null || settings == null) return;
            float scale = shifter != null ? shifter.Scale : 1f;
            float dt = Time.unscaledDeltaTime;
            // 0 at toy size, 1 at kid size, in between while shifting (on the same geometric curve as the scale).
            float kid = 0f;
            if (shifter != null)
            {
                float toyScale = shifter.ScaleOf(ShiftSize.Toy), kidScale = shifter.ScaleOf(ShiftSize.Kid);
                if (kidScale > toyScale) kid = Mathf.InverseLerp(Mathf.Log(toyScale), Mathf.Log(kidScale), Mathf.Log(Mathf.Max(scale, 1e-4f)));
            }
            float distanceMul = Mathf.Lerp(1f, settings.kidDistanceMultiplier, kid);
            float fov = Mathf.Lerp(settings.fieldOfView, settings.kidFieldOfView, kid);

            Vector2 look = input != null ? input.ConsumeLook(settings.mouseSensitivity * PlayerOptions.LookScale, settings.padSensitivity * PlayerOptions.LookScale, dt) : Vector2.zero;
            if (look.sqrMagnitude > 0.0001f) _lastLookTime = Time.unscaledTime;
            if (_chase != null) { InsideBody = false; _viewReady = false; VehicleView(look, fov, dt); return; }
            if (_cursorLocked && Time.timeScale > 0f)
            {
                _yaw = Mathf.Repeat(_yaw + look.x, 360f);
                float up = settings.invertY != PlayerOptions.InvertY ? look.y : -look.y;
                _pitch = Mathf.Clamp(_pitch + up, settings.minPitch, settings.maxPitch);
            }

            // Which view: wide with empty hands, over the shoulder with a weapon drawn, closer and narrower while aiming.
            bool armed = _holder != null && _holder.Armed;
            bool aiming = armed && _holder.AimHeld;
            float wantSide = armed ? settings.armedShoulderOffset : settings.shoulderOffset;
            float wantRise = armed ? settings.armedRise : 0f;
            float wantReach = aiming ? settings.aimDistance : armed ? settings.armedDistance : settings.distance;
            float wantFov = aiming ? settings.aimFovScale : 1f;
            float wantShare = _stance != null ? _stance.HeightShare : 1f;   // a crouch lowers the pivot with the head
            float ease = _viewReady ? 1f - Mathf.Exp(-Mathf.Max(0.01f, settings.viewBlend) * dt) : 1f;
            _viewReady = true;
            _side = Mathf.Lerp(_side, wantSide, ease);
            _rise = Mathf.Lerp(_rise, wantRise, ease);
            _reach = Mathf.Lerp(_reach, wantReach, ease);
            _fovScale = Mathf.Lerp(_fovScale, wantFov, ease);
            _heightShare = Mathf.Lerp(_heightShare, wantShare, ease);
            fov *= _fovScale;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Quaternion heading = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            // The ball the way to the camera is swept with: at least as big as the reach of the near plane's corners, so
            // the camera stops short of a wall instead of on it with its near plane through it.
            float near = Mathf.Max(0.001f, settings.nearClip * scale);
            float half = Mathf.Tan(Mathf.Clamp(fov + FovOffset + PunchNow, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
            float nearReach = near * Mathf.Sqrt(1f + half * half * (1f + _camera.aspect * _camera.aspect));
            float ball = Mathf.Max(settings.collisionRadius * scale, nearReach * settings.wallGap);

            // Up from the body's middle (free: inside the capsule), sideways to the shoulder, then back. Each leg is cut
            // short by what is in the way, so a low ceiling, a wall on the shoulder side or one behind never has the
            // camera start inside it.
            float middleHeight = _capsule != null ? _capsule.center.y * scale : settings.pivotHeight * 0.5f * scale;
            Vector3 middle = target.position + Vector3.up * middleHeight;
            float lift = Mathf.Max(0f, (settings.pivotHeight * _heightShare + _rise) * scale - middleHeight);
            Vector3 pivot = middle + Vector3.up * Sweep(middle, Vector3.up, lift, ball);
            Vector3 right = heading * Vector3.right * Mathf.Sign(_side);
            pivot += right * Sweep(pivot, right, Mathf.Abs(_side) * scale, ball);
            float wanted = _reach * scale * distanceMul;
            float distance = Sweep(pivot, back, wanted, ball);

            Vector3 position = pivot + back * distance;
            if (_leave > 0f)
            {
                // just got out: ease from the car's framing to the shoulder instead of cutting
                _leave = Mathf.Max(0f, _leave - dt / Mathf.Max(0.01f, Vehicle.leaveBlend));
                float k = _leave * _leave * (3f - 2f * _leave);
                position = Vector3.Lerp(position, _leavePos, k); rotation = Quaternion.Slerp(rotation, _leaveRot, k);
            }
            // a shake still running (thrown out of a wreck) dies away on foot too; tilt only while a wall or the blend
            // from the car decides where the camera is
            bool free = _leave <= 0f && distance >= wanted - 1e-4f;
            AddShake(ref position, ref rotation, scale, free ? settings.collisionRadius * scale : 0f, dt);
            transform.SetPositionAndRotation(position, rotation);
            // Inside the player's own body? Measured to the body's middle line, feet to the top of the capsule.
            float tall = _capsule != null ? _capsule.height * scale : settings.pivotHeight * scale;
            float along = Mathf.Clamp(position.y - target.position.y, 0f, tall);
            float gap = Vector3.Distance(position, target.position + Vector3.up * along);
            float limit = settings.hideBodyWithin * scale;
            InsideBody = gap < (InsideBody ? limit * (1f + settings.hideBodySlack) : limit);
            if (_punchLeft > 0f) _punchLeft = Mathf.Max(0f, _punchLeft - dt);
            _camera.fieldOfView = fov + FovOffset + PunchNow;
            _camera.nearClipPlane = near;
        }

        /// <summary>The share of the ball's radius a leg of the sweep stops short of what it meets. Without it the ball
        /// rests on the wall that cut the sideways step, the step back then starts in contact and gets nowhere, and a
        /// wall beside the player puts the camera in the player's head.</summary>
        private const float SweepSkin = 0.15f;

        /// <summary>How far a ball of <paramref name="radius"/> gets from <paramref name="from"/> along <paramref name="direction"/>
        /// before it meets something (never further than <paramref name="length"/>), stopping a skin short of it so the
        /// next leg starts clear. The player's own colliders do not count. A ball that is already in something at the
        /// start gets nowhere.</summary>
        private float Sweep(Vector3 from, Vector3 direction, float length, float radius)
        {
            if (length <= 1e-5f) return 0f;
            float skin = radius * SweepSkin;
            float free = length;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, _hits, length + skin, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(target)) continue;
                free = Mathf.Min(free, _hits[i].distance - skin);
            }
            return Mathf.Max(0f, free);
        }

        /// <summary>Chase a vehicle while it is driven (null stops): the camera frames the car by its own size
        /// (VehicleCameraSettings). <paramref name="yawOffset"/> turns the vehicle's yaw into its nose's yaw.
        /// <paramref name="distanceMultiplier"/> is from before the vehicle camera and is no longer used.</summary>
        public void SetChase(Transform vehicle, float yawOffset = 0f, float distanceMultiplier = 1.6f)
        {
            if (vehicle == null && _chase != null) { _leave = 1f; _leavePos = transform.position; _leaveRot = transform.rotation; }
            if (vehicle != null) _leave = 0f;
            _chase = vehicle; _chaseYawOffset = yawOffset;
            _lastLookTime = -10f; _aimReady = false; _yawVelocity = 0f;
            _overShare = 0f;
            _chaseInfo = vehicle != null ? vehicle.GetComponentInParent<IChaseTarget>() : null;
            _chaseHold = _chaseInfo as IChaseHold; _holdShare = 0f; _holdReady = false;
            if (vehicle != null) MeasureChase();
        }

        /// <summary>The car's middle (in its own space) and its length and height, from what is drawn (the driver left out).</summary>
        private void MeasureChase()
        {
            Bounds local = default; bool any = false;
            Matrix4x4 toLocal = _chase.worldToLocalMatrix;
            foreach (var r in _chase.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.GetComponentInParent<PlayerMotor>(true) != null) continue;
                Bounds wb = r.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 c = wb.center + Vector3.Scale(wb.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                    Vector3 p = toLocal.MultiplyPoint3x4(c);
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
                }
            }
            if (!any) local = new Bounds(Vector3.zero, Vector3.one);
            _chaseCentreLocal = local.center;
            Vector3 lossy = _chase.lossyScale;
            Vector3 size = Vector3.Scale(local.size, new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
            _chaseLength = Mathf.Max(0.1f, Mathf.Max(size.x, size.z));
            _chaseHeight = Mathf.Max(0.05f, size.y);
        }

        /// <summary>The vehicle camera: aim at the car's middle (trailing it a little), swing in behind the way it travels when
        /// the mouse rests, slower in the air; wider, closer and flatter with speed, and more so past top speed (a boost);
        /// the garage menu turns it into an orbit. The shake and the field-of-view kick go on last.</summary>
        private void VehicleView(Vector2 look, float baseFov, float dt)
        {
            var v = Vehicle;
            Vector3 centre = _chase.TransformPoint(_chaseCentreLocal);
            Vector3 want = centre + Vector3.up * (_chaseHeight * v.lookHeight);
            Vector3 velocity = _chaseInfo != null ? _chaseInfo.ChaseVelocity : (_aimReady && dt > 1e-5f ? (centre - _lastChasePos) / dt : Vector3.zero);
            _lastChasePos = centre;
            if (!_aimReady) { _aim = want; _aimVelocity = Vector3.zero; _aimReady = true; }
            if (dt > 0f)
            {
                _aim.x = Mathf.SmoothDamp(_aim.x, want.x, ref _aimVelocity.x, v.positionLag, Mathf.Infinity, dt);
                _aim.z = Mathf.SmoothDamp(_aim.z, want.z, ref _aimVelocity.z, v.positionLag, Mathf.Infinity, dt);
                _aim.y = Mathf.SmoothDamp(_aim.y, want.y, ref _aimVelocity.y, v.heightLag, Mathf.Infinity, dt);
            }

            Vector3 flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float top = _chaseInfo != null ? _chaseInfo.ChaseTopSpeed : _chaseLength * 6f;
            float raw = top > 1e-3f ? flat.magnitude / top : 0f;   // 1 = top speed; a boost goes past it
            float share = Mathf.Clamp01(raw);
            _speedShare = Mathf.MoveTowards(_speedShare, share, dt * 2f);
            _overShare = Mathf.MoveTowards(_overShare, Mathf.InverseLerp(1f, Mathf.Max(1.01f, v.boostAt), raw), dt * Mathf.Max(0.1f, v.boostResponse));

            bool showroom = _menuHold;   // the garage menu is up and the car parked
            if (showroom) Showroom(look, v, dt);
            else
            {
                if (_cursorLocked && Time.timeScale > 0f)
                {
                    _yaw = Mathf.Repeat(_yaw + look.x, 360f);
                    float up = settings.invertY != PlayerOptions.InvertY ? look.y : -look.y;
                    _pitch = Mathf.Clamp(_pitch + up, settings.minPitch, settings.maxPitch);
                }
                if (Time.unscaledTime - _lastLookTime > v.recenterDelay)
                {
                    // where the nose points; at speed, going forwards, where the car is going (reversing keeps the nose)
                    float noseYaw = _chase.eulerAngles.y + _chaseYawOffset;
                    float behind = noseYaw;
                    Vector3 nose = Quaternion.Euler(0f, noseYaw, 0f) * Vector3.forward;
                    if (flat.sqrMagnitude > 1e-4f && Vector3.Dot(flat, nose) > 0f)
                    {
                        float travel = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                        behind = Mathf.LerpAngle(noseYaw, travel, Mathf.InverseLerp(v.followTravelFrom, Mathf.Min(1f, v.followTravelFrom + 0.15f), share));
                    }
                    bool grounded = _chaseInfo == null || _chaseInfo.ChaseGrounded;
                    float lag = Mathf.Max(0.01f, v.turnLag * (grounded ? 1f : Mathf.Max(1f, v.airTurnSlowdown)));
                    if (dt > 0f) _yaw = Mathf.SmoothDampAngle(_yaw, behind, ref _yawVelocity, lag, Mathf.Infinity, dt);
                    // settles flatter (lower behind the car) as speed rises
                    _pitch = Mathf.MoveTowards(_pitch, v.pitch - v.speedPitch * _speedShare - v.boostPitch * _overShare, v.pitchReturn * dt);
                }
                else _yawVelocity = 0f;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            // closer as speed rises: with the wider view the car keeps its size while the room rushes past
            float closer = Mathf.Max(0.3f, 1f - v.speedCloser * _speedShare - v.boostCloser * _overShare);
            float wanted = _chaseLength * v.distance * (showroom ? _showroomZoom : closer);
            float distance = wanted;
            float probe = Mathf.Max(0.02f, _chaseHeight * 0.12f);
            int count = Physics.SphereCastNonAlloc(_aim, probe, back, _hits, wanted, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.transform.IsChildOf(_chase) || (target != null && hit.transform.IsChildOf(target)) || ToyCar.OwnsBall(hit.rigidbody)) continue;
                distance = Mathf.Min(distance, Mathf.Max(_chaseLength * v.minDistance, hit.distance));
            }
            Vector3 position = _aim + back * distance;
            bool free = distance >= wanted - 1e-4f;

            // On road the car holds on to (a loop, a spiral, a banked bend) the camera rides in the road's own frame: behind
            // the car and above it by the ROAD's up, rolling with it. The upright view above looks along the flat part of the
            // car's travel, which turns back on itself at the top of a loop; this one sees the loop from behind all the way
            // round. The two are blended, so going on and coming off is one move.
            bool holds = !showroom && _chaseHold != null && _chaseHold.ChaseHolds;
            _holdShare = Mathf.MoveTowards(_holdShare, holds ? 1f : 0f, dt * Mathf.Max(0.1f, v.holdBlend));
            if (_holdShare > 0f)
            {
                Vector3 roadUp = _chase.up;
                Quaternion road = Quaternion.LookRotation(Quaternion.AngleAxis(_chaseYawOffset, roadUp) * _chase.forward, roadUp);
                _holdRot = _holdReady && dt > 0f ? Quaternion.Slerp(_holdRot, road, 1f - Mathf.Exp(-Mathf.Max(0.1f, v.holdFollow) * dt)) : road;
                _holdReady = true;
                Quaternion held = _holdRot * Quaternion.Euler(_pitch, 0f, 0f);
                Vector3 heldAim = centre + _holdRot * Vector3.up * (_chaseHeight * v.lookHeight), heldBack = held * Vector3.back;
                float heldDistance = wanted;
                count = Physics.SphereCastNonAlloc(heldAim, probe, heldBack, _hits, wanted, collisionMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = _hits[i];
                    if (hit.transform.IsChildOf(_chase) || (target != null && hit.transform.IsChildOf(target)) || ToyCar.OwnsBall(hit.rigidbody)) continue;
                    heldDistance = Mathf.Min(heldDistance, Mathf.Max(_chaseLength * v.minDistance, hit.distance));
                }
                float k = _holdShare * _holdShare * (3f - 2f * _holdShare);
                position = Vector3.Lerp(position, heldAim + heldBack * heldDistance, k);
                rotation = Quaternion.Slerp(rotation, held, k);
                free = free && heldDistance >= wanted - 1e-4f;
                // while the car is the right way up the upright view keeps facing its nose, so coming off the held road
                // blends into a view that is already behind the car
                if (roadUp.y > 0.5f) { _yaw = _chase.eulerAngles.y + _chaseYawOffset; _yawVelocity = 0f; }
            }
            else _holdReady = false;

            // the shake goes on the finished pose; its sideways part only where the probe found the camera free room
            AddShake(ref position, ref rotation, _chaseLength, free ? probe : 0f, dt);
            transform.SetPositionAndRotation(position, rotation);
            if (_punchLeft > 0f) _punchLeft = Mathf.Max(0f, _punchLeft - dt);
            _camera.fieldOfView = baseFov + v.speedFov * _speedShare + v.boostFov * _overShare + PunchNow + FovOffset;
            _camera.nearClipPlane = Mathf.Max(0.001f, _chaseLength * 0.02f);
        }

        /// <summary>Garage: right or middle drag orbits and tilts (the cursor stays free for the menu), A/D or the left stick
        /// orbit, W/S tilt, the wheel zooms when not over the menu. Nothing swings back meanwhile.</summary>
        private void Showroom(Vector2 look, VehicleCameraSettings v, float dt)
        {
            var mouse = Mouse.current;
            bool drag = mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed);
            if (drag)
            {
                _yaw = Mathf.Repeat(_yaw + look.x, 360f);
                float up = settings.invertY != PlayerOptions.InvertY ? look.y : -look.y;
                _pitch = Mathf.Clamp(_pitch + up, settings.minPitch, settings.maxPitch);
            }
            Vector2 move = input != null ? input.Move : Vector2.zero;
            _yaw = Mathf.Repeat(_yaw + move.x * v.showroomOrbitSpeed * dt, 360f);
            _pitch = Mathf.Clamp(_pitch + move.y * v.showroomOrbitSpeed * 0.5f * dt, settings.minPitch, settings.maxPitch);
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            bool overMenu = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Mathf.Abs(wheel) > 0.01f && !overMenu)
                _showroomZoom = Mathf.Clamp(_showroomZoom * (1f - Mathf.Sign(wheel) * v.showroomZoomStep), v.showroomZoom.x, v.showroomZoom.y);
            _yawVelocity = 0f;
        }

        /// <summary>Points the camera the given way at once, for spawns and portals. While it follows a car it also jumps to
        /// the car instead of gliding across the room (a car portal).</summary>
        public void Warp(float yaw, float? pitch = null)
        {
            _yaw = yaw;
            _yawVelocity = 0f;
            _aimReady = false;
            if (pitch.HasValue) _pitch = Mathf.Clamp(pitch.Value, settings.minPitch, settings.maxPitch);
        }
    }
}
