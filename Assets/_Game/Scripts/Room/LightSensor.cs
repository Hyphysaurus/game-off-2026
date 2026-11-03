using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Wakes a dormant ShiftSource when a lit flashlight's beam rests on it: inside the cone, within range,
    /// and with nothing in the way, for a short dwell. Checked a few times a second, not every frame.
    /// Once woken it stops looking, so the core stays awake after the beam moves on.
    /// </summary>
    [RequireComponent(typeof(ShiftSource))]
    public sealed class LightSensor : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds the beam must rest on this before it wakes.")] private float dwell = 0.4f;
        [SerializeField, Tooltip("Seconds between checks.")] private float interval = 0.1f;
        [SerializeField, Tooltip("Keep watching after waking, so the core sleeps again when the beam leaves? Off = wake once and stay awake.")]
        private bool repeatable;
        [SerializeField, Tooltip("Where the beam must land. This object if empty.")] private Transform point;

        private ShiftSource _source;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private float _nextCheck, _litFor;

        private void Awake()
        {
            _source = GetComponent<ShiftSource>();
            if (point == null) point = transform;
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + interval;
            if (!_source.Dormant && !repeatable) { enabled = false; return; }

            bool lit = false;
            for (int i = 0; i < Flashlight.Lit.Count && !lit; i++) lit = Lights(Flashlight.Lit[i]);
            _litFor = lit ? _litFor + interval : 0f;

            if (_litFor >= dwell) _source.Awaken();
            else if (repeatable && !lit) _source.Sleep();
        }

        private bool Lights(Flashlight torch)
        {
            Light beam = torch != null ? torch.Beam : null;
            if (beam == null || !beam.enabled) return false;
            Vector3 origin = beam.transform.position;
            Vector3 to = point.position - origin;
            float distance = to.magnitude;
            if (distance > beam.range) return false;
            float half = beam.type == LightType.Spot ? beam.spotAngle * 0.5f : 180f;
            if (Vector3.Angle(beam.transform.forward, to) > half) return false;

            // Nothing solid between the lens and the core, other than the core itself or the torch.
            int count = Physics.RaycastNonAlloc(origin, to / distance, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = _hits[i].transform;
                if (t.IsChildOf(transform) || t.IsChildOf(torch.transform)) continue;
                return false;
            }
            return true;
        }
    }
}
