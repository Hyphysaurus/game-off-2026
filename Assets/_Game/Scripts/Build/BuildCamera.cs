using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The management-game camera (Two Point Museum / Planet Zoo): it orbits a focus point on the ground.
    /// WASD / arrows pan along the view, Q / E turn, the wheel zooms, middle-drag orbits, right-drag grabs and pans,
    /// Shift is faster. Speeds follow the zoom distance, so the same keys work over a toy's shelf and the whole room.
    /// It drives the same Camera as the shoulder camera: Begin keeps that camera's pose and lens, End puts them back.
    /// Off (disabled) outside Build Mode.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BuildCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("Closest zoom, world units from the focus.")] private float minDistance = 3f;
        [SerializeField, Tooltip("Farthest zoom.")] private float maxDistance = 1800f;
        [SerializeField, Tooltip("Lowest and highest look-down angle, degrees.")] private Vector2 pitchRange = new Vector2(10f, 88f);
        [SerializeField, Tooltip("Keyboard pan, in zoom distances per second.")] private float panSpeed = 1.1f;
        [SerializeField, Tooltip("Q / E turn, degrees per second.")] private float turnSpeed = 100f;
        [SerializeField, Tooltip("Middle-drag orbit, degrees per pixel.")] private float orbitPerPixel = 0.25f;
        [SerializeField, Tooltip("Each wheel notch zooms by this fraction.")] private float zoomStep = 0.14f;
        [SerializeField, Tooltip("Shift multiplies pan, turn and zoom by this.")] private float fastMultiplier = 3f;
        [SerializeField, Tooltip("How quickly the view catches up with the input; higher is snappier.")] private float smoothing = 14f;
        [SerializeField, Tooltip("Field of view while building.")] private float fieldOfView = 50f;

        private const float ClickSlop = 6f;   // pixels a right-press may move and still count as a click

        private Camera _camera;
        private Vector3 _focus, _focusNow;
        private float _yaw, _yawNow, _pitch = 50f, _pitchNow = 50f, _distance = 40f, _distanceNow = 40f;
        private Vector3 _savedPosition; private Quaternion _savedRotation;
        private float _savedFov, _savedNear, _savedFar;
        private bool _rightDown, _rightDragging, _middleDown;
        private Vector2 _rightPressAt;

        public Vector3 Focus => _focus;
        public float Yaw => _yaw;
        public float Distance => _distance;
        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());
        /// <summary>True on the frame the right button is let go without having dragged: a "cancel" click.</summary>
        public bool RightClicked { get; private set; }
        /// <summary>Set each frame by Build Mode: the keyboard belongs to a text box or a held modifier tool.</summary>
        public bool KeysBlocked { get; set; }
        /// <summary>Set each frame by Build Mode: the wheel is being used for something else (rotate / raise / UI list).</summary>
        public bool WheelBlocked { get; set; }

        private void Awake() { _camera = GetComponent<Camera>(); }

        /// <summary>Takes over the camera, looking at <paramref name="focus"/> from <paramref name="yaw"/>.</summary>
        public void Begin(Vector3 focus, float yaw, float distance)
        {
            var cam = Camera;
            _savedPosition = transform.position; _savedRotation = transform.rotation;
            _savedFov = cam.fieldOfView; _savedNear = cam.nearClipPlane; _savedFar = cam.farClipPlane;
            _focus = _focusNow = focus;
            _yaw = _yawNow = yaw;
            _pitch = _pitchNow = Mathf.Clamp(50f, pitchRange.x, pitchRange.y);
            _distance = _distanceNow = Mathf.Clamp(distance, minDistance, maxDistance);
            _rightDown = _rightDragging = _middleDown = false;
            enabled = true;
            Apply();
        }

        /// <summary>Gives the camera back exactly as Begin found it.</summary>
        public void End()
        {
            enabled = false;
            var cam = Camera;
            transform.SetPositionAndRotation(_savedPosition, _savedRotation);
            cam.fieldOfView = _savedFov; cam.nearClipPlane = _savedNear; cam.farClipPlane = _savedFar;
        }

        /// <summary>Glides to look at a point from a distance that fits something of the given size.</summary>
        public void FocusOn(Vector3 point, float size)
        {
            _focus = point;
            _distance = Mathf.Clamp(size * 2.2f + minDistance, minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            var keys = Keyboard.current; var mouse = Mouse.current;
            bool fast = BuildInput.Shift;
            float speed = fast ? fastMultiplier : 1f;
            Quaternion heading = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 forward = heading * Vector3.forward, right = heading * Vector3.right;

            // keyboard: pan and turn (Ctrl is for shortcuts like Ctrl+D, so it stops the pan)
            if (keys != null && !KeysBlocked && !BuildInput.Ctrl)
            {
                Vector2 move = Vector2.zero;
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) move.y += 1f;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) move.y -= 1f;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) move.x += 1f;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) move.x -= 1f;
                if (move != Vector2.zero) _focus += (forward * move.y + right * move.x).normalized * (_distance * panSpeed * speed * dt);
                if (keys.qKey.isPressed) _yaw += turnSpeed * speed * dt;
                if (keys.eKey.isPressed) _yaw -= turnSpeed * speed * dt;
            }

            // wheel: zoom toward the focus
            if (!WheelBlocked)
            {
                float notches = BuildInput.WheelNotches;
                if (Mathf.Abs(notches) > 0.001f) _distance = Mathf.Clamp(_distance * Mathf.Pow(1f - zoomStep * (fast ? 2f : 1f), notches), minDistance, maxDistance);
            }

            RightClicked = false;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                // middle-drag: orbit
                if (mouse.middleButton.wasPressedThisFrame && !BuildInput.OverUI) _middleDown = true;
                if (!mouse.middleButton.isPressed) _middleDown = false;
                if (_middleDown)
                {
                    _yaw += delta.x * orbitPerPixel;
                    _pitch = Mathf.Clamp(_pitch - delta.y * orbitPerPixel, pitchRange.x, pitchRange.y);
                }
                // right-drag: grab the ground and pull it (one pixel moves the world one pixel at the focus)
                if (mouse.rightButton.wasPressedThisFrame && !BuildInput.OverUI) { _rightDown = true; _rightDragging = false; _rightPressAt = mouse.position.ReadValue(); }
                if (_rightDown)
                {
                    if (!_rightDragging && (mouse.position.ReadValue() - _rightPressAt).magnitude > ClickSlop) _rightDragging = true;
                    if (_rightDragging)
                    {
                        float worldPerPixel = 2f * _distanceNow * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
                        _focus -= (right * delta.x + forward * delta.y) * worldPerPixel;
                        _focusNow = _focus;   // grab-pan follows the hand at once
                    }
                    if (mouse.rightButton.wasReleasedThisFrame || !mouse.rightButton.isPressed)
                    {
                        RightClicked = !_rightDragging;
                        _rightDown = false;
                    }
                }
            }

            float k = 1f - Mathf.Exp(-smoothing * dt);
            _focusNow = Vector3.Lerp(_focusNow, _focus, k);
            _yawNow = Mathf.LerpAngle(_yawNow, _yaw, k);
            _pitchNow = Mathf.Lerp(_pitchNow, _pitch, k);
            _distanceNow = Mathf.Lerp(_distanceNow, _distance, k);
            Apply();
        }

        private void Apply()
        {
            var cam = Camera;
            Quaternion rotation = Quaternion.Euler(_pitchNow, _yawNow, 0f);
            transform.SetPositionAndRotation(_focusNow - rotation * Vector3.forward * _distanceNow, rotation);
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = Mathf.Clamp(_distanceNow * 0.004f, 0.01f, 2f);
            cam.farClipPlane = Mathf.Max(_savedFar, _distanceNow * 12f);
        }
    }
}
