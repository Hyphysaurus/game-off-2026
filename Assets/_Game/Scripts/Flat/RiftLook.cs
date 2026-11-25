using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A rift that is alive at rest (D-0207): it sways a little about its own up and bobs, on game time. Put it on the
    /// rift's look (the child that holds its model), not on the gate itself, so the gate's trigger stays still. The
    /// gate (FlatGate) does the flash and the sound; this is only the calm motion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RiftLook : MonoBehaviour
    {
        [SerializeField, Range(0f, 45f), Tooltip("How far it sways about its up, degrees each way.")] private float swayDegrees = 8f;
        [SerializeField, Min(0.1f), Tooltip("Seconds for one sway there and back.")] private float swaySeconds = 6f;
        [SerializeField, Min(0f), Tooltip("How far it bobs up and down, in its parent's units.")] private float bob = 0.08f;
        [SerializeField, Min(0.1f), Tooltip("Seconds for one bob.")] private float bobSeconds = 3.3f;

        private Vector3 _restPlace;
        private Quaternion _restTurn;

        private void Awake()
        {
            _restPlace = transform.localPosition;
            _restTurn = transform.localRotation;
        }

        private void LateUpdate()
        {
            float t = Time.time;
            float sway = swayDegrees * Mathf.Sin(t * (2f * Mathf.PI / swaySeconds));
            transform.localRotation = Quaternion.AngleAxis(sway, Vector3.up) * _restTurn;
            transform.localPosition = _restPlace + Vector3.up * (bob * Mathf.Sin(t * (2f * Mathf.PI / bobSeconds)));
        }
    }
}
