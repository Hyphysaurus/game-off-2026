using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A spring: step on it and you are thrown up. A pillow is a trampoline for a toy. Height is in the
    /// player's own units (a toy flies 8 of its units, a kid 8 of its), size-gated so the kid walks over toy
    /// pads. The visual squashes on each bounce.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LaunchPad : MonoBehaviour
    {
        [SerializeField] private bool anySize;
        [SerializeField] private ShiftSize requiredSize = ShiftSize.Toy;
        [SerializeField, Tooltip("How high it throws, in the player's own units (toy: 1 = half its height).")] private float height = 8f;
        [SerializeField, Tooltip("Seconds before it fires again.")] private float cooldown = 0.35f;
        [SerializeField, Tooltip("The part that squashes. This object if empty.")] private Transform visual;
        [SerializeField, Range(0.2f, 1f)] private float squash = 0.55f;
        [SerializeField] private float squashTime = 0.22f;
        [SerializeField] private UnityEvent onLaunch;

        private float _readyAt, _squashUntil;
        private Vector3 _baseScale;

        private void Reset() { GetComponent<Collider>().isTrigger = true; }
        private void Awake() { if (visual == null) visual = transform; _baseScale = visual.localScale; }

        private void OnTriggerEnter(Collider other) { Try(other); }
        private void OnTriggerStay(Collider other) { Try(other); }

        private void Try(Collider other)
        {
            if (Time.time < _readyAt) return;
            Shifter shifter = other.GetComponentInParent<Shifter>();
            if (shifter == null || shifter.Shifting) return;
            if (!anySize && shifter.Size != requiredSize) return;
            PlayerMotor motor = shifter.GetComponent<PlayerMotor>();
            if (motor == null) return;
            _readyAt = Time.time + cooldown;
            _squashUntil = Time.time + squashTime;
            motor.Launch(height);
            onLaunch?.Invoke();
        }

        private void Update()
        {
            if (visual == null) return;
            float k = Mathf.Clamp01((_squashUntil - Time.time) / Mathf.Max(0.01f, squashTime));   // 1 right after the bounce, 0 after squashTime
            float s = Mathf.Lerp(1f, squash, Mathf.Sin(k * Mathf.PI));
            visual.localScale = new Vector3(_baseScale.x * (2f - s), _baseScale.y * s, _baseScale.z * (2f - s)) ;
        }
    }
}
