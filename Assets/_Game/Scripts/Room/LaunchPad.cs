using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A spring: step on it and you are thrown up. A pillow is a trampoline for a toy. Height is in the
    /// player's own units (a toy flies 8 of its units, a kid 8 of its), size-gated so the kid walks over toy
    /// pads. Works on foot and in first person. The visual squashes on each bounce and is left alone between
    /// bounces, so Build Mode can scale it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LaunchPad : MonoBehaviour, IBuildMoved
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
        private Vector3 _baseScale, _shown;   // the visual's own size, and the squashed size last written over it
        private bool _squashing;
        private static FirstPersonSettings _firstPerson;

        private void Reset() { GetComponent<Collider>().isTrigger = true; }
        private void Awake() { if (visual == null) visual = transform; }

        private void OnTriggerEnter(Collider other) { Try(other); }
        private void OnTriggerStay(Collider other) { Try(other); }

        private void Try(Collider other)
        {
            if (Time.time < _readyAt) return;
            Shifter shifter = PlayerTouch.Find(other, out FirstPersonBody body);
            if (shifter == null || shifter.Shifting) return;
            if (!anySize && shifter.Size != requiredSize) return;
            if (body != null) { if (!Launch(body)) return; }
            else
            {
                PlayerMotor motor = shifter.GetComponent<PlayerMotor>();
                if (motor == null) return;
                motor.Launch(height);
            }
            _readyAt = Time.time + cooldown;
            if (visual != null)
            {
                if (!_squashing) _baseScale = _shown = visual.localScale;   // its size now (Build Mode may have scaled it)
                _squashing = true;
                _squashUntil = Time.time + squashTime;
            }
            onLaunch?.Invoke();
        }

        /// <summary>The same throw for the first-person body, under its own gravity (its settings, read once: the body
        /// keeps them to itself).</summary>
        private bool Launch(FirstPersonBody body)
        {
            if (_firstPerson == null)
            {
                var loaded = Resources.FindObjectsOfTypeAll<FirstPersonSettings>();
                if (loaded.Length > 0) _firstPerson = loaded[0];
            }
            if (_firstPerson == null || _firstPerson.gravity <= 0f) return false;
            float s = body.Scale;
            float up = Mathf.Sqrt(2f * _firstPerson.gravity * s * Mathf.Max(0.01f, height) * s);
            body.AddVelocity(Vector3.up * (up - body.Velocity.y));
            return true;
        }

        private void Update()
        {
            if (!_squashing) return;
            if (visual == null) { _squashing = false; return; }
            float k = Mathf.Clamp01((_squashUntil - Time.time) / Mathf.Max(0.01f, squashTime));   // 1 right after the bounce, 0 after squashTime
            if (k <= 0f) { visual.localScale = _baseScale; _squashing = false; return; }
            float s = Mathf.Lerp(1f, squash, Mathf.Sin(k * Mathf.PI));
            _shown = new Vector3(_baseScale.x * (2f - s), _baseScale.y * s, _baseScale.z * (2f - s));
            visual.localScale = _shown;
        }

        /// <summary>Build Mode moved or scaled the pad or its pillow: the size it has now is its own size.</summary>
        public void OnBuildMoved()
        {
            if (visual == null) return;
            if (_squashing)
            {
                // mid-bounce: take the squash back out of whatever size Build Mode left, and stop there
                Vector3 now = visual.localScale;
                visual.localScale = new Vector3(Unsquash(now.x, _baseScale.x, _shown.x), Unsquash(now.y, _baseScale.y, _shown.y), Unsquash(now.z, _baseScale.z, _shown.z));
                _squashing = false;
            }
            _baseScale = visual.localScale;
        }

        private static float Unsquash(float now, float own, float shown) => Mathf.Abs(shown) > 1e-6f ? now * own / shown : now;
    }
}
