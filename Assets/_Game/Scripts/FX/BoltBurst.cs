using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// His drawn yellow bolts as one short burst: they flick on, shoot outward, flicker in hard steps and are gone.
    /// Sits on the ring that holds the bolts (their pivots are at the ring's centre, so scale = reach). The ring is
    /// flat, so it turns to face the camera while it plays. Used at the end of a shift: around the orb when the shift
    /// came from a core, around the player when it was the carried shift.
    /// </summary>
    public sealed class BoltBurst : MonoBehaviour
    {
        [SerializeField, Tooltip("Left empty, every renderer under this object is a bolt.")] private Renderer[] bolts;
        [SerializeField, Tooltip("Seconds the bolts stay out.")] private float time = 0.55f;
        [SerializeField, Tooltip("Bolt size at the start and end of the burst (1 = as modelled).")] private Vector2 reach = new Vector2(0.8f, 1.3f);

        /// <summary>The bolts flicker in steps, not smoothly: that is what reads as electric.</summary>
        private const float FlickerStep = 0.05f;

        private float _left, _total, _nextFlicker, _roll;
        private Vector2 _reach;

        public bool Playing => _left > 0f;

        private void Awake()
        {
            if (bolts == null || bolts.Length == 0) bolts = GetComponentsInChildren<Renderer>(true);
            Hide();
        }

        /// <summary>One burst with this object's own time and reach.</summary>
        public void Play() => Play(time, reach);

        /// <summary>One burst: the bolts grow from reach.x to reach.y over the given seconds, at a random roll.</summary>
        public void Play(float seconds, Vector2 fromTo)
        {
            _total = Mathf.Max(seconds, 0.05f);
            _left = _total;
            _reach = fromTo;
            _nextFlicker = 0f;
            _roll = Random.Range(0f, 360f);
        }

        private void Update()
        {
            if (_left <= 0f) return;
            _left -= Time.deltaTime;
            if (_left <= 0f) { Hide(); return; }
            float t = 1f - _left / _total;
            float size = Mathf.Lerp(_reach.x, _reach.y, 1f - (1f - t) * (1f - t));
            _nextFlicker -= Time.deltaTime;
            bool reshuffle = _nextFlicker <= 0f;
            if (reshuffle) _nextFlicker = FlickerStep;
            foreach (var bolt in bolts)
            {
                if (bolt == null) continue;
                bolt.transform.localScale = Vector3.one * size;
                if (reshuffle) bolt.enabled = Random.value < 0.8f;
            }
        }

        private void LateUpdate()
        {
            // After the camera has moved: turn the flat ring to face it, rolled by this burst's random angle.
            if (_left <= 0f) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, _roll);
        }

        private void Hide()
        {
            _left = 0f;
            if (bolts == null) return;
            foreach (var bolt in bolts) if (bolt != null) bolt.enabled = false;
        }
    }
}
