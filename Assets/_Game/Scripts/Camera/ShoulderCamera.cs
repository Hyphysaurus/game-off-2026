using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Over-the-shoulder follow camera: orbits a pivot above the player's feet, clamps pitch,
    /// and pulls in when a wall is between the pivot and the camera. Runs in LateUpdate, after
    /// the player has moved this frame, which is why it never jitters.
    /// All distances scale with the Shifter, so the framing feels the same at toy size.
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
        // chase mode (a vehicle is driven): the camera swings in behind the vehicle's nose when the mouse rests
        private Transform _chase; private float _chaseYawOffset, _chaseDistance = 1f, _lastLookTime = -10f;
        [SerializeField, Tooltip("While driving: seconds without mouse look before the camera swings behind the vehicle.")] private float chaseRecenterDelay = 0.6f;
        [SerializeField, Tooltip("While driving: degrees per second the camera swings behind the vehicle.")] private float chaseRecenterSpeed = 140f;
        [SerializeField, Tooltip("While driving: the pitch the camera settles to.")] private float chasePitch = 14f;

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
            if (_chase != null && Time.unscaledTime - _lastLookTime > chaseRecenterDelay)
            {
                float behind = _chase.eulerAngles.y + _chaseYawOffset;
                _yaw = Mathf.MoveTowardsAngle(_yaw, behind, chaseRecenterSpeed * dt);
                _pitch = Mathf.MoveTowards(_pitch, chasePitch, chaseRecenterSpeed * 0.3f * dt);
            }
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
            float wanted = settings.distance * scale * distanceMul * (_chase != null ? _chaseDistance : 1f);
            float distance = wanted;
            int count = Physics.SphereCastNonAlloc(pivot, settings.collisionRadius * scale, back, _hits, wanted, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(target) || (_chase != null && _hits[i].transform.IsChildOf(_chase))) continue;
                distance = Mathf.Min(distance, Mathf.Max(settings.minDistance * scale, _hits[i].distance));
            }

            transform.SetPositionAndRotation(pivot + back * distance, rotation);
            _camera.fieldOfView = fov + FovOffset;
            _camera.nearClipPlane = Mathf.Max(0.001f, settings.nearClip * scale);
        }

        /// <summary>Chase a vehicle while it is driven: <paramref name="yawOffset"/> turns the vehicle's yaw into its nose's
        /// yaw, <paramref name="distanceMultiplier"/> pulls the camera back. Pass null to stop.</summary>
        public void SetChase(Transform vehicle, float yawOffset = 0f, float distanceMultiplier = 1.6f)
        {
            _chase = vehicle; _chaseYawOffset = yawOffset; _chaseDistance = vehicle != null ? distanceMultiplier : 1f;
            _lastLookTime = -10f;
        }

        /// <summary>Points the camera the given way at once, for spawns and portals.</summary>
        public void Warp(float yaw, float? pitch = null)
        {
            _yaw = yaw;
            if (pitch.HasValue) _pitch = Mathf.Clamp(pitch.Value, settings.minPitch, settings.maxPitch);
        }
    }
}
