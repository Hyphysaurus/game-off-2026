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

            bool lit = LightQuery.Lit(point.position, transform, out _);   // the shared light check (D-0149)
            _litFor = lit ? _litFor + interval : 0f;

            if (_litFor >= dwell) _source.Awaken();
            else if (repeatable && !lit) _source.Sleep();
        }

    }
}
