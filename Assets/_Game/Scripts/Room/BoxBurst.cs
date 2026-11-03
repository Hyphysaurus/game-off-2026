using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The toy box the player wakes in. When a shift to kid size begins, every panel tumbles outward around
    /// its bottom edge (the lid flips over its back edge) and stays down as debris, so the kid grows into
    /// open air. Panels are plain cubes parented here; this script works out each hinge from where the
    /// panel sits relative to the box centre, so nothing needs a pivot set by hand.
    /// </summary>
    public sealed class BoxBurst : MonoBehaviour
    {
        [SerializeField, Tooltip("The box's panels: floor is fine to include, it is left alone.")] private Transform[] panels;
        [SerializeField, Tooltip("The lid, if any: it flips over its back edge (the box's -Z side).")] private Transform lid;
        [SerializeField] private float wallAngle = 92f;
        [SerializeField] private float lidAngle = 150f;
        [SerializeField, Tooltip("Seconds a panel takes to fall.")] private float duration = 0.5f;
        [SerializeField, Tooltip("Each panel starts up to this many seconds after the first, for a less uniform fall.")] private float stagger = 0.12f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Tooltip("Burst on the first shift to kid size. Off = only Burst() fires it.")] private bool burstOnShift = true;
        [SerializeField, Tooltip("Hook Feel feedbacks here: the crack, the dust.")] private UnityEvent onBurst;

        private struct Fall { public Transform t; public Vector3 point; public Vector3 axis; public float angle; public float delay; public float turned; }
        private readonly List<Fall> _falls = new List<Fall>();
        private float _elapsed;

        public bool Burst { get; private set; }

        private void OnEnable() { GameEvents.ShiftStarted += OnShiftStarted; }
        private void OnDisable() { GameEvents.ShiftStarted -= OnShiftStarted; }

        private void OnShiftStarted(Shifter who, ShiftSize size)
        {
            if (burstOnShift && size == ShiftSize.Kid) BurstNow();
        }

        /// <summary>Tumbles the panels. Safe to call twice; the second call does nothing.</summary>
        public void BurstNow()
        {
            if (Burst) return;
            Burst = true;
            _elapsed = 0f;
            Vector3 centre = transform.position;
            foreach (Transform panel in panels)
            {
                if (panel == null || panel == lid) continue;
                Vector3 outward = Vector3.ProjectOnPlane(panel.position - centre, Vector3.up);
                if (outward.sqrMagnitude < 0.0001f) continue;                  // the floor: leave it
                outward.Normalize();
                float halfHeight = Extents(panel).y;
                _falls.Add(new Fall
                {
                    t = panel,
                    point = panel.position + Vector3.down * halfHeight,        // bottom edge, where cardboard would crease
                    axis = Vector3.Cross(Vector3.up, outward),                  // positive angle = top falls outward
                    angle = wallAngle,
                    delay = Random.Range(0f, stagger),
                });
                Collider c = panel.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }
            if (lid != null)
            {
                Vector3 back = -transform.forward;
                _falls.Add(new Fall
                {
                    t = lid,
                    point = lid.position + back * Extents(lid).z,
                    axis = Vector3.Cross(transform.forward, Vector3.up),        // the lid's front rises, then flips over the back
                    angle = lidAngle,
                    delay = 0f,
                });
                Collider c = lid.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }
            onBurst?.Invoke();
        }

        private static Vector3 Extents(Transform t)
        {
            Renderer r = t.GetComponent<Renderer>();
            return r != null ? r.bounds.extents : t.lossyScale * 0.5f;
        }

        private void Update()
        {
            if (!Burst || _falls.Count == 0) return;
            _elapsed += Time.deltaTime;
            bool done = true;
            for (int i = 0; i < _falls.Count; i++)
            {
                Fall f = _falls[i];
                float k = Mathf.Clamp01((_elapsed - f.delay) / Mathf.Max(0.01f, duration));
                float target = f.angle * ease.Evaluate(k);
                float step = target - f.turned;
                if (Mathf.Abs(step) > 0.0001f) f.t.RotateAround(f.point, f.axis, step);
                f.turned = target;
                _falls[i] = f;
                if (k < 1f) done = false;
            }
            if (done) _falls.Clear();
        }
    }
}
