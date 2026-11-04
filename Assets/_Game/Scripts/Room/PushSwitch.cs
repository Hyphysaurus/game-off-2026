using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A switch you press with your body: a toy walking or hopping into it pushes the cap in, it clicks, and it
    /// pops back out when the body leaves. Each press fires once. Lives on a trigger volume over the switch;
    /// the cap is the visible part that slides. Size-gated so the kid uses the flashlight's own controls.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class PushSwitch : MonoBehaviour
    {
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("Who can push it, unless Any Size is on.")] private ShiftSize requiredSize = ShiftSize.Toy;
        [SerializeField, Tooltip("The part that slides. Optional.")] private Transform cap;
        [SerializeField, Tooltip("How far the cap slides, along this object's local -Y (into the body).")] private float travel = 0.25f;
        [SerializeField, Tooltip("Seconds for the cap to slide in or out.")] private float slideTime = 0.06f;
        [SerializeField] private UnityEvent onPressed;
        [SerializeField] private UnityEvent onReleased;

        private Vector3 _capOut;
        private float _k;          // 0 = out, 1 = in
        private int _inside;

        public bool Pressed => _inside > 0;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            if (cap != null) _capOut = cap.localPosition;
        }

        private bool Counts(Collider other)
        {
            Shifter shifter = other.GetComponentInParent<Shifter>();
            if (shifter == null || shifter.Shifting) return false;
            return anySize || shifter.Size == requiredSize;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Counts(other)) return;
            _inside++;
            if (_inside == 1) onPressed?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!Counts(other)) return;
            _inside = Mathf.Max(0, _inside - 1);
            if (_inside == 0) onReleased?.Invoke();
        }

        private void Update()
        {
            if (cap == null) return;
            float target = Pressed ? 1f : 0f;
            _k = Mathf.MoveTowards(_k, target, Time.deltaTime / Mathf.Max(0.01f, slideTime));
            cap.localPosition = _capOut + transform.InverseTransformDirection(-transform.up) * (travel * _k);
        }
    }
}
