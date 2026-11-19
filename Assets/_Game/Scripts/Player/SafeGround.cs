using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Remembers the last place the player stood on solid ground with room to stand, a few times a second. A car that
    /// cannot find a free spot to let you out falls back to it.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class SafeGround : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds between samples.")] private float interval = 0.4f;

        private PlayerMotor _motor;
        private CharacterController _cc;
        private float _next;
        private readonly Collider[] _overlap = new Collider[8];

        public Vector3 LastSafe { get; private set; }

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _cc = GetComponent<CharacterController>();
            LastSafe = transform.position;
        }

        /// <summary>Overrides the remembered spot (a respawn or a reset).</summary>
        public void Record(Vector3 feet) => LastSafe = feet;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + interval;
            if (!_motor.enabled || !_cc.enabled || !_motor.Grounded) return;
            if (Fits(transform.position)) LastSafe = transform.position;
        }

        /// <summary>True when the player's capsule fits with its feet here.</summary>
        public bool Fits(Vector3 feet)
        {
            float s = transform.lossyScale.y;
            float r = _cc.radius * s * 0.9f, h = _cc.height * s;
            Vector3 a = feet + Vector3.up * (r + 0.05f * s), b = feet + Vector3.up * Mathf.Max(r + 0.05f * s, h - r);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, r, _overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!_overlap[i].transform.IsChildOf(transform) && !HitShapes.IsShape(_overlap[i])) return false;   // a figure's hit shape stops nothing
            return true;
        }
    }
}
