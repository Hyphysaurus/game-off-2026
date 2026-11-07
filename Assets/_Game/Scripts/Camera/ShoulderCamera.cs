using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Over-the-shoulder follow camera: orbits a pivot above the player's feet, clamps pitch,
    /// and pulls in when a wall is between the pivot and the camera. Runs in LateUpdate, after
    /// the player has moved this frame, which is why it never jitters.
    /// All distances scale with the Shifter, so the framing feels the same at toy size.
    /// While a vehicle is driven it frames the car instead (D-0036, VehicleCameraSettings): its middle, by its own size,
    /// trailing it a little, swinging in behind the way it travels, widening slightly with speed, slower in the air; in the
    /// garage (menu up) right-drag, A/D and the wheel orbit and zoom round the parked car. Getting out blends back.
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
        private float _yaw, _pitch;
        private bool _cursorLocked;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        // vehicle mode (D-0036): while a vehicle is driven the camera frames the car, not the driver
        [SerializeField, Tooltip("Vehicle camera tuning. Resources/VehicleCamera is loaded if empty (built-in defaults if there is none).")]
        private VehicleCameraSettings vehicle;
        private Transform _chase; private float _chaseYawOffset, _lastLookTime = -10f;
        private IChaseTarget _chaseInfo;
        private Vector3 _chaseCentreLocal;                     // the car's middle, in its own space
        private float _chaseLength = 1f, _chaseHeight = 1f;   // its size in world units, measured when the chase starts
        private Vector3 _aim, _aimVelocity, _lastChasePos;
        private bool _aimReady;
        private float _yawVelocity, _speedShare, _showroomZoom = 1f;
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
        /// <summary>Degrees added to the settings' field of view this frame. ShiftWarp punches it during a shift; 0 otherwise.</summary>
        public float FovOffset { get; set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (shifter == null && target != null) shifter = target.GetComponent<Shifter>();
        }

        private void Start()
        {
            _yaw = target != null ? target.eulerAngles.y : 0f;
            _pitch = settings.startPitch;
            SetCursorLocked(true);
        }

        private void OnEnable() { SetCursorLocked(!_menuHold); }   // coming back from a menu, Build Mode or first person
        private void OnDisable() { SetCursorLocked(false); }

        private bool _menuHold;

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
            // Escape frees the mouse; a click in the game view takes it back.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            else if (!_cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) SetCursorLocked(true);
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

            Vector2 look = input != null ? input.ConsumeLook(settings.mouseSensitivity, settings.padSensitivity, dt) : Vector2.zero;
            if (look.sqrMagnitude > 0.0001f) _lastLookTime = Time.unscaledTime;
            if (_chase != null) { VehicleView(look, fov, dt); return; }
            if (_cursorLocked && Time.timeScale > 0f)
            {
                _yaw = Mathf.Repeat(_yaw + look.x, 360f);
                float up = settings.invertY ? look.y : -look.y;
                _pitch = Mathf.Clamp(_pitch + up, settings.minPitch, settings.maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Quaternion heading = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * (settings.pivotHeight * scale) + heading * Vector3.right * (settings.shoulderOffset * scale);
            Vector3 back = rotation * Vector3.back;

            // Keep the camera out of walls: the nearest thing between the pivot and the wanted spot wins.
            float wanted = settings.distance * scale * distanceMul;
            float distance = wanted;
            int count = Physics.SphereCastNonAlloc(pivot, settings.collisionRadius * scale, back, _hits, wanted, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(target)) continue;
                distance = Mathf.Min(distance, Mathf.Max(settings.minDistance * scale, _hits[i].distance));
            }

            Vector3 position = pivot + back * distance;
            if (_leave > 0f)
            {
                // just got out: ease from the car's framing to the shoulder instead of cutting
                _leave = Mathf.Max(0f, _leave - dt / Mathf.Max(0.01f, Vehicle.leaveBlend));
                float k = _leave * _leave * (3f - 2f * _leave);
                transform.SetPositionAndRotation(Vector3.Lerp(position, _leavePos, k), Quaternion.Slerp(rotation, _leaveRot, k));
            }
            else transform.SetPositionAndRotation(position, rotation);
            _camera.fieldOfView = fov + FovOffset;
            _camera.nearClipPlane = Mathf.Max(0.001f, settings.nearClip * scale);
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
            _chaseInfo = vehicle != null ? vehicle.GetComponentInParent<IChaseTarget>() : null;
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
        /// the mouse rests, slower in the air, a touch wider and further at speed; the garage menu turns it into an orbit.</summary>
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
            float share = top > 1e-3f ? Mathf.Clamp01(flat.magnitude / top) : 0f;
            _speedShare = Mathf.MoveTowards(_speedShare, share, dt * 2f);

            bool showroom = _menuHold;   // the garage menu is up and the car parked
            if (showroom) Showroom(look, v, dt);
            else
            {
                if (_cursorLocked && Time.timeScale > 0f)
                {
                    _yaw = Mathf.Repeat(_yaw + look.x, 360f);
                    float up = settings.invertY ? look.y : -look.y;
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
                    _pitch = Mathf.MoveTowards(_pitch, v.pitch, v.pitchReturn * dt);
                }
                else _yawVelocity = 0f;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float wanted = _chaseLength * v.distance * (showroom ? _showroomZoom : 1f) * (1f + v.speedPullBack * _speedShare);
            float distance = wanted;
            int count = Physics.SphereCastNonAlloc(_aim, Mathf.Max(0.02f, _chaseHeight * 0.12f), back, _hits, wanted, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.transform.IsChildOf(_chase) || (target != null && hit.transform.IsChildOf(target)) || ToyCar.OwnsBall(hit.rigidbody)) continue;
                distance = Mathf.Min(distance, Mathf.Max(_chaseLength * v.minDistance, hit.distance));
            }
            transform.SetPositionAndRotation(_aim + back * distance, rotation);
            _camera.fieldOfView = baseFov + v.speedFov * _speedShare + FovOffset;
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
                float up = settings.invertY ? look.y : -look.y;
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
