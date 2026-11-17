using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The small marks around his portal drawing, sent outward as a ripple after a warp (D-0073, his "after-effect post
    /// ripples"). The marks are rings of the drawing's own strokes, inside ring first: each ring swells in, drifts
    /// outward, then every mark shrinks to its own middle and is gone. Between ripples the rings are switched off, so
    /// the portal is the swirl alone and Build Mode sizes the piece by the swirl. The timing is the loop set up in
    /// portal_ring.blend (96 frames at 24 a second).
    /// </summary>
    public sealed class PortalRipple : MonoBehaviour
    {
        private static readonly int GrowId = Shader.PropertyToID("_Grow");

        [SerializeField, Tooltip("The rings of marks, inside ring first. Switched off between ripples.")] private Renderer[] rings;
        [Header("Timing, seconds")]
        [SerializeField, Tooltip("Between one ring starting and the next.")] private float stagger = 0.58f;
        [SerializeField, Tooltip("A ring's marks growing in.")] private float swellTime = 0.5f;
        [SerializeField, Tooltip("A ring at full size, drifting outward.")] private float holdTime = 1.42f;
        [SerializeField, Tooltip("A ring's marks shrinking away.")] private float fadeTime = 0.75f;
        [SerializeField, Tooltip("Between ripples with no car going through. 0 = only after a warp.")] private float idleEvery;
        [Header("Size (1 = where he drew them)")]
        [SerializeField, Tooltip("A ring's size as it appears.")] private float startSize = 0.9f;
        [SerializeField, Tooltip("A ring's size as it goes.")] private float endSize = 1.12f;

        private MaterialPropertyBlock _block;
        private Vector3[] _drawn;      // each ring's own scale, as built
        private float _startedAt;
        private float _nextIdle;
        private bool _playing;

        private float RingTime => Mathf.Max(0.01f, swellTime) + Mathf.Max(0f, holdTime) + Mathf.Max(0.01f, fadeTime);

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            int n = rings != null ? rings.Length : 0;
            _drawn = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (rings[i] == null) continue;
                _drawn[i] = rings[i].transform.localScale;
                rings[i].gameObject.SetActive(false);
            }
            _nextIdle = Time.time + idleEvery;
            enabled = idleEvery > 0f;
        }

        /// <summary>Starts a ripple now (again from the inside ring if one is already going).</summary>
        public void Play()
        {
            if (rings == null || rings.Length == 0 || _drawn == null) return;
            _startedAt = Time.time;
            _playing = true;
            enabled = true;
        }

        /// <summary>
        /// One ring <paramref name="t"/> seconds after it started: how far its marks have grown (0 gone, 1 as drawn) and
        /// its size (1 = where he drew it). It grows over <paramref name="swell"/> while coming out from
        /// <paramref name="startSize"/>, holds for <paramref name="hold"/>, shrinks over <paramref name="fade"/>, and
        /// drifts on to <paramref name="endSize"/> from the end of the swell. False before it starts and once it is gone.
        /// </summary>
        public static bool RingAt(float t, float swell, float hold, float fade, float startSize, float endSize, out float grow, out float size)
        {
            swell = Mathf.Max(0.01f, swell); hold = Mathf.Max(0f, hold); fade = Mathf.Max(0.01f, fade);
            grow = 0f; size = t < 0f ? startSize : endSize;
            if (t < 0f || t >= swell + hold + fade) return false;
            grow = t < swell ? Mathf.SmoothStep(0f, 1f, t / swell)
                : t < swell + hold ? 1f
                : Mathf.SmoothStep(1f, 0f, (t - swell - hold) / fade);
            size = t < swell ? Mathf.Lerp(startSize, 1f, t / swell) : Mathf.Lerp(1f, endSize, (t - swell) / (hold + fade));
            return true;
        }

        private void Update()
        {
            if (!_playing)
            {
                if (idleEvery > 0f && Time.time >= _nextIdle) Play();
                return;
            }
            float ringTime = RingTime;
            bool any = false;
            for (int i = 0; i < rings.Length; i++)
            {
                var ring = rings[i];
                if (ring == null) continue;
                float t = Time.time - _startedAt - i * stagger;
                bool shown = RingAt(t, swellTime, holdTime, fadeTime, startSize, endSize, out float grow, out float size);
                if (ring.gameObject.activeSelf != shown) ring.gameObject.SetActive(shown);
                if (t < ringTime) any = true;   // still to come, or showing
                if (!shown) continue;

                // wider in the drawing's plane only: the marks keep their thickness
                ring.transform.localScale = new Vector3(_drawn[i].x * size, _drawn[i].y * size, _drawn[i].z);
                ring.GetPropertyBlock(_block);
                _block.SetFloat(GrowId, grow);
                ring.SetPropertyBlock(_block);
            }
            if (any) return;
            _playing = false;
            _nextIdle = Time.time + idleEvery;
            enabled = idleEvery > 0f;
        }
    }
}
