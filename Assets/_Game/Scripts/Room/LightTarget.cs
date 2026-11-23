using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// Anything that reacts to the flashlight (D-0149, systems plan step 5). It fills while a lit beam is on it (Alan Wake
    /// 2: burning the darkness off), faster with the beam focused or only when focused, and drains when the light leaves.
    /// What it does is wired in the Inspector to its events, so the same part wakes a core, blinds a soldier (ToySoldier
    /// Dazzle), opens a door, reveals a hidden mark (LightReveal) or glows as it charges (LightGlow).
    /// </summary>
    public sealed class LightTarget : MonoBehaviour
    {
        public enum FocusRule { Any, FasterFocused, NeedsFocus }

        [SerializeField, Min(0.01f), Tooltip("Seconds of light to fill (0.4 = a core's old wake).")] private float fillSeconds = 1f;
        [SerializeField, Tooltip("Any: any lit beam fills it. Faster Focused: a focused beam (aim held) fills it Focus Speed times faster. Needs Focus: only a focused beam.")] private FocusRule focus = FocusRule.FasterFocused;
        [SerializeField, Min(1f), Tooltip("How many times faster a fully focused beam fills it (Faster Focused).")] private float focusSpeed = 3f;
        [SerializeField, Min(0f), Tooltip("Seconds to empty once the light leaves. 0 = once filled it stays filled.")] private float drainSeconds = 1.5f;
        [SerializeField, Tooltip("Can fill again after it has emptied (a soldier blinded again). Off: it fills once.")] private bool repeatable = true;
        [SerializeField, Tooltip("Where the beam must land. This object if empty.")] private Transform point;
        [SerializeField, Min(0.01f), Tooltip("Seconds between looks for the beam (not every frame).")] private float interval = 0.05f;

        [Header("What it does")]
        [SerializeField] private UnityEvent onLit;
        [SerializeField] private UnityEvent onUnlit;
        [SerializeField, Tooltip("Full: the main reaction (wake a core, blind a soldier, open a door).")] private UnityEvent onFilled;
        [SerializeField, Tooltip("Back to empty after it was full.")] private UnityEvent onEmptied;
        [SerializeField, Tooltip("How full, 0 to 1, as it changes (a meter, a glow).")] private UnityEvent<float> onFill;

        private float _nextLook, _focus;
        private bool _wasLit, _full, _spent;

        /// <summary>The events, for code that wires a target (a builder placing a beat); in a scene, wire them in the Inspector.</summary>
        public UnityEvent OnLit => onLit;
        public UnityEvent OnUnlit => onUnlit;
        public UnityEvent OnFilled => onFilled;
        public UnityEvent OnEmptied => onEmptied;
        public UnityEvent<float> OnFill => onFill;

        /// <summary>How full, 0 to 1.</summary>
        public float Fill { get; private set; }
        /// <summary>A lit beam is on it now.</summary>
        public bool IsLit { get; private set; }
        /// <summary>It filled and has not emptied since.</summary>
        public bool Full => _full;

        private void Awake() { if (point == null) point = transform; }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _spent) return;
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + interval;
                bool lit = LightQuery.Lit(point.position, transform, out _focus);
                if (lit && focus == FocusRule.NeedsFocus && _focus < 0.5f) lit = false;
                IsLit = lit;
            }
            if (IsLit != _wasLit) { _wasLit = IsLit; if (IsLit) onLit?.Invoke(); else onUnlit?.Invoke(); }

            float was = Fill;
            if (IsLit)
            {
                float rate = 1f / fillSeconds;
                if (focus == FocusRule.FasterFocused) rate *= Mathf.Lerp(1f, focusSpeed, _focus);
                Fill = Mathf.Min(1f, Fill + rate * dt);
            }
            else if (drainSeconds > 0f && !(_full && !repeatable)) Fill = Mathf.Max(0f, Fill - dt / drainSeconds);
            if (Fill != was) onFill?.Invoke(Fill);

            if (!_full && Fill >= 1f) { _full = true; onFilled?.Invoke(); if (!repeatable && drainSeconds <= 0f) _spent = true; }
            else if (_full && Fill <= 0f) { _full = false; onEmptied?.Invoke(); if (!repeatable) _spent = true; }
        }

        /// <summary>Back to empty (a reset).</summary>
        public void Clear() { Fill = 0f; _full = false; _spent = false; onFill?.Invoke(0f); }
    }
}
