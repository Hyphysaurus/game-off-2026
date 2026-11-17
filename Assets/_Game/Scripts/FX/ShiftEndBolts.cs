using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// His yellow bolts as the full stop at the end of a shift that no core started (the carried shift on F, the dev
    /// keys): they burst around the player, sized to the size the player has just become. A shift that began at a core
    /// gets its bolts around that core's orb instead (ShiftOrbLink). Rides on every shifting player: Shifter adds it,
    /// like the carried shift itself. The bolts are a copy of the ring inside Resources/FX/ShiftOrbVisual, made on
    /// first use and kept, standing in the world (not under the player, whose scale is changing).
    /// </summary>
    public sealed class ShiftEndBolts : MonoBehaviour
    {
        [SerializeField, Tooltip("Burst size against the player's scale (the ring reaches about 3.3 at size 1; the toy is 1.8 tall).")] private float size = 0.65f;
        [SerializeField, Tooltip("How high above the player's feet the burst sits, in the player's own units.")] private float height = 0.9f;
        [SerializeField, Tooltip("Seconds the bolts stay out.")] private float time = 0.45f;
        [SerializeField, Tooltip("Bolt size at the start and end of the burst.")] private Vector2 reach = new Vector2(0.5f, 1.15f);

        private const string VisualResource = "FX/ShiftOrbVisual";
        private const string RingName = "Bolts";

        private Shifter _shifter;
        private BoltBurst _burst;
        private float _startedAt;
        private bool _looked;

        private void Awake() => TryGetComponent(out _shifter);

        private void OnEnable()
        {
            if (_shifter == null) return;
            _shifter.ShiftStarted += OnStarted;
            _shifter.Shifted += OnShifted;
        }

        private void OnDisable()
        {
            if (_shifter == null) return;
            _shifter.ShiftStarted -= OnStarted;
            _shifter.Shifted -= OnShifted;
        }

        private void OnDestroy()
        {
            if (_burst != null) Destroy(_burst.gameObject);
        }

        private void OnStarted(ShiftSize size) => _startedAt = Time.time;

        private void OnShifted(ShiftSize now)
        {
            // A core raises its Used in the same frame the shift starts: that shift's bolts belong to the core's orb.
            if (ShiftOrbLink.LastUse >= _startedAt - 0.05f) return;
            BoltBurst burst = Burst();
            if (burst == null) return;
            float scale = _shifter.Scale;
            burst.transform.position = transform.position + Vector3.up * (height * scale);
            burst.transform.localScale = Vector3.one * (size * scale);
            burst.Play(time, reach);
        }

        /// <summary>The ring of bolts, copied once out of the orb's visual prefab. Null if the orb has not been built.</summary>
        private BoltBurst Burst()
        {
            if (_burst != null || _looked) return _burst;
            _looked = true;
            var visual = Resources.Load<GameObject>(VisualResource);
            if (visual == null) return null;
            Transform ring = null;
            foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                if (t.name == RingName) { ring = t; break; }
            if (ring == null) return null;
            var copy = Instantiate(ring.gameObject);
            copy.name = "ShiftEndBolts";
            if (!copy.TryGetComponent(out _burst)) _burst = copy.AddComponent<BoltBurst>();
            return _burst;
        }
    }
}
