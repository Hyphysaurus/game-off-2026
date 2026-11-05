using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A prop that answers E with a small response: a spin (a globe, a pinwheel, the ceiling fan), a bob (a toy),
    /// a squash (a plush), or a light switched on and off (a lamp, a screen). Continuous responses toggle and stay
    /// (the fan keeps turning); one-shots play once and settle. It can demand a size like Switch does, and Use()
    /// lets a Switch or an event drive it from elsewhere (the wall switch runs the fan).
    /// </summary>
    public sealed class Reactor : MonoBehaviour, IInteractable
    {
        public enum Response { Spin, Bob, Squash, ToggleLight, None }
        public enum Need { Any, Toy, Kid }

        [SerializeField] private Response response = Response.Bob;
        [SerializeField, Tooltip("The child that moves; this object if empty.")] private Transform part;
        [SerializeField, Tooltip("For ToggleLight; found in children if empty.")] private Light lamp;
        [SerializeField, Tooltip("A spin or a light that stays until used again, instead of playing once.")] private bool continuous;
        [SerializeField] private string prompt = "Poke";
        [SerializeField, Tooltip("The prompt while a continuous response is on.")] private string promptOff = "Switch off";
        [SerializeField] private Need need = Need.Any;
        [SerializeField] private string wrongSizePrompt = "Too small to reach";
        [SerializeField, Tooltip("Seconds a one-shot bob or squash takes.")] private float duration = 0.6f;
        [SerializeField] private float cooldown = 0.2f;
        [SerializeField, Tooltip("Degrees per second at full spin.")] private float spinSpeed = 540f;
        [SerializeField, Tooltip("Hook sounds and Feel here.")] private UnityEvent onUsed;

        private bool _on;
        private float _t = -1f, _spin, _last = -10f, _size = 1f;
        private Vector3 _baseScale, _basePos;

        public bool On => _on;

        private void Awake()
        {
            if (part == null) part = transform;
            if (lamp == null) lamp = GetComponentInChildren<Light>(true);
            _baseScale = part.localScale; _basePos = part.localPosition;
            var r = GetComponentInChildren<Renderer>();
            if (r != null) _size = Mathf.Max(0.1f, r.bounds.size.y);
        }

        private bool RightSize(Interactor who) => need == Need.Any || who.Shifter == null || (need == Need.Kid) == (who.Shifter.Size == ShiftSize.Kid);

        public string PromptFor(Interactor who)
        {
            if (!RightSize(who)) return wrongSizePrompt;
            return continuous && _on ? promptOff : prompt;
        }

        public void Interact(Interactor who) { if (RightSize(who)) Use(); }

        /// <summary>Plays the response (or toggles it when continuous). Wire a Switch's event here.</summary>
        public void Use()
        {
            if (Time.time - _last < cooldown) return;
            _last = Time.time;
            if (continuous) _on = !_on; else { _t = 0f; if (response == Response.Spin) _spin = 1f; }
            if (response == Response.ToggleLight && lamp != null) lamp.enabled = continuous ? _on : !lamp.enabled;
            onUsed?.Invoke();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (response == Response.Spin)
            {
                float target = continuous && _on ? 1f : 0f;            // one-shots start at full spin and run down
                _spin = Mathf.MoveTowards(_spin, target, dt / 1.2f);
                if (_spin > 0f) part.Rotate(0f, spinSpeed * _spin * dt, 0f, Space.Self);
                return;
            }
            if (_t < 0f) return;
            _t += dt;
            float u = Mathf.Clamp01(_t / Mathf.Max(0.05f, duration)), s = Mathf.Sin(u * Mathf.PI);
            if (response == Response.Bob) part.localPosition = _basePos + Vector3.up * (0.25f * _size * s * (1f - 0.5f * u));
            else if (response == Response.Squash) part.localScale = new Vector3(_baseScale.x * (1f + 0.15f * s), _baseScale.y * (1f - 0.3f * s), _baseScale.z * (1f + 0.15f * s));
            if (u >= 1f) { _t = -1f; part.localPosition = _basePos; part.localScale = _baseScale; }
        }
    }
}
