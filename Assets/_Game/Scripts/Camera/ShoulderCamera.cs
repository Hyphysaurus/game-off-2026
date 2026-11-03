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

        private void OnDisable() { SetCursorLocked(false); }

        private void Update()
        {
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

            Vector2 look = input != null ? input.ConsumeLook(settings.mouseSensitivity, settings.padSensitivity, dt) : Vector2.zero;
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
            float wanted = settings.distance * scale;
            float distance = wanted;
            int count = Physics.SphereCastNonAlloc(pivot, settings.collisionRadius * scale, back, _hits, wanted, collisionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(target)) continue;
                distance = Mathf.Min(distance, Mathf.Max(settings.minDistance * scale, _hits[i].distance));
            }

            transform.SetPositionAndRotation(pivot + back * distance, rotation);
            _camera.fieldOfView = settings.fieldOfView + FovOffset;
            _camera.nearClipPlane = Mathf.Max(0.001f, settings.nearClip * scale);
        }

        /// <summary>Points the camera the given way at once, for spawns and portals.</summary>
        public void Warp(float yaw, float? pitch = null)
        {
            _yaw = yaw;
            if (pitch.HasValue) _pitch = Mathf.Clamp(pitch.Value, settings.minPitch, settings.maxPitch);
        }
    }
}
