using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The first-person eyes: mouse look (yaw turns the body, pitch tilts the view), the camera at the body's eye height
    /// (easing through crouches), a subtle walking bob, view punch from recoil and hits, and an HL2 field of view (90 across
    /// a 4:3 screen; wide screens keep that vertical angle). Holding the rotate key while carrying a prop turns the prop
    /// instead of the view. A hit taken kicks the view and flashes the screen red. Drives the scene's main camera directly
    /// while first person is on.
    /// </summary>
    [DefaultExecutionOrder(50)]   // after the body moved, before the weapon view model is posed
    public sealed class FirstPersonLook : MonoBehaviour
    {
        private const float PunchStiffness = 120f, PunchDamping = 16f, HurtFade = 0.35f;

        private FirstPersonSettings _s;
        private PlayerInput _input;
        private FirstPersonBody _body;
        private Camera _camera;
        private PhysicsGrabber _hands;
        private Vector2 _punch, _punchVelocity;
        private float _bobPhase, _bobWeight;
        private float _hurtAlpha, _hurtAt = -10f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        public void Setup(FirstPersonSettings settings, PlayerInput input, FirstPersonBody body, Camera camera, PhysicsGrabber hands)
        {
            _s = settings; _input = input; _body = body; _camera = camera; _hands = hands;
        }

        public void Place(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, -_s.maxPitch, _s.maxPitch);
            _punch = _punchVelocity = Vector2.zero;
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        }

        /// <summary>Kicks the view by these degrees (x = pitch, negative is up; y = yaw); it springs back.</summary>
        public void Punch(Vector2 degrees) => _punchVelocity += degrees * 18f;

        /// <summary>A hit taken, as a share of full health: the view kicks up and the screen flashes red, harder for bigger hits.</summary>
        public void Hurt(float share)
        {
            float k = Mathf.Clamp(0.5f + share * 5f, 0.5f, 2f);
            Punch(new Vector2(-3f * k, Random.Range(-2f, 2f) * k));
            _hurtAlpha = Mathf.Clamp(0.2f + share * 1.5f, 0.2f, 0.5f);
            _hurtAt = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float a = _hurtAlpha * (1f - (Time.unscaledTime - _hurtAt) / HurtFade);
            if (a <= 0f) return;
            var old = GUI.color;
            GUI.color = new Color(0.8f, 0.05f, 0.02f, a);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnEnable() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

        private void LateUpdate()
        {
            if (_s == null || _camera == null) return;
            float dt = Time.unscaledDeltaTime;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && Time.timeScale > 0f)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            Vector2 look = _input != null ? _input.ConsumeLook(_s.mouseSensitivity * PlayerOptions.LookScale, _s.padSensitivity * PlayerOptions.LookScale, dt) : Vector2.zero;
            if (locked && Time.timeScale > 0f)
            {
                if (_hands != null && _hands.RotatingHeld) _hands.RotateBy(look);
                else
                {
                    Yaw = Mathf.Repeat(Yaw + look.x, 360f);
                    Pitch = Mathf.Clamp(Pitch + (_s.invertY != PlayerOptions.InvertY ? look.y : -look.y), -_s.maxPitch, _s.maxPitch);
                }
            }
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);

            // View punch: a damped spring back to zero.
            float gdt = Time.deltaTime;
            _punchVelocity += (-PunchStiffness * _punch - PunchDamping * _punchVelocity) * gdt;
            _punch += _punchVelocity * gdt;

            float scale = _body.Scale;
            float bob = 0f;
            if (_s.viewBob)
            {
                Vector3 v = _body.Velocity; v.y = 0f;
                float speed = v.magnitude;
                float target = _body.Grounded ? Mathf.Clamp01(speed / (_s.runSpeed * scale)) : 0f;
                _bobWeight = Mathf.MoveTowards(_bobWeight, target, gdt * 4f);
                _bobPhase += speed / Mathf.Max(1e-4f, scale) * _s.bobPerUnit * Mathf.PI * 2f * gdt;
                bob = Mathf.Sin(_bobPhase) * _s.bobAmount * scale * _bobWeight;
            }

            _camera.transform.SetPositionAndRotation(
                _body.transform.position + Vector3.up * (_body.EyeHeight + bob),
                Quaternion.Euler(Pitch + _punch.x, Yaw + _punch.y, 0f));
            // HL2's 90 is horizontal on 4:3; keep that vertical angle on any screen.
            _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(_s.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.75f) * Mathf.Rad2Deg;
            _camera.nearClipPlane = Mathf.Max(0.0005f, _s.nearClip * scale);
        }
    }
}
