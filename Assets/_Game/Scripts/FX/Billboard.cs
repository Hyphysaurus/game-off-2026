using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Turns a flat thing to face the camera every frame, so a drawn glow reads the same from any angle.
    /// Runs after the camera has moved (LateUpdate), optionally spinning slowly and breathing in scale
    /// so the glow feels alive without a particle system.
    /// </summary>
    public sealed class Billboard : MonoBehaviour
    {
        [SerializeField, Tooltip("Degrees per second of slow spin around the view axis. 0 = none.")] private float spin = 8f;
        [SerializeField, Tooltip("Scale wobble: 0 = steady, 0.1 = ±10%.")] private float breathe = 0.08f;
        [SerializeField, Tooltip("Breaths per second.")] private float breatheRate = 0.7f;

        private Vector3 _baseScale;
        private float _angle;

        private void Awake() { _baseScale = transform.localScale; }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _angle += spin * Time.deltaTime;
            // Face the camera: the quad's +Z looks away from the viewer, so look along the camera's forward.
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, _angle);
            float k = 1f + breathe * Mathf.Sin(Time.time * breatheRate * Mathf.PI * 2f);
            transform.localScale = _baseScale * k;
        }
    }
}
