using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    public enum ShiftSize { Toy, Kid }

    /// <summary>
    /// Owns the player's size. One number, Scale, that the motor, the camera and the interactor all read,
    /// so a shift is a single change in one place. The change is eased over time and moves through
    /// the sizes geometrically (each step the same proportion), which reads right to the eye.
    /// </summary>
    public sealed class Shifter : MonoBehaviour
    {
        [SerializeField] private ShiftSettings settings;
        [SerializeField] private ShiftSize startSize = ShiftSize.Toy;
        [SerializeField, Tooltip("Editor convenience while the room has no shift source yet: this key toggles the size. None to disable.")]
        private Key devToggleKey = Key.T;

        private float _from, _to;
        private float _progress = 1f;

        public ShiftSize Size { get; private set; }
        /// <summary>The player's scale right now: 1 at kid size, settings.toyScale at toy size, in between while shifting.</summary>
        public float Scale { get; private set; } = 1f;
        public bool Shifting => _progress < 1f;
        /// <summary>How far the current shift has run, 0 to 1 (1 when not shifting). ShiftWarp follows this, not the clock.</summary>
        public float Progress => _progress;
        /// <summary>Raised on this player when a shift begins.</summary>
        public event Action<ShiftSize> ShiftStarted;
        /// <summary>Raised on this player when a shift ends.</summary>
        public event Action<ShiftSize> Shifted;

        public float ScaleOf(ShiftSize size) => size == ShiftSize.Kid ? settings.kidScale : settings.toyScale;

        private void Awake()
        {
            // The carried shift (D-0045) rides on every shifting player; TryGetComponent, since ?? cannot see Unity's fake null.
            if (!TryGetComponent(out ShiftCharge charge)) charge = gameObject.AddComponent<ShiftCharge>();
            charge.Init(settings);
            // His yellow bolts mark the end of a shift that no core started (D-0062): they ride along the same way.
            if (!TryGetComponent(out ShiftEndBolts _)) gameObject.AddComponent<ShiftEndBolts>();
            Size = startSize;
            Scale = ScaleOf(Size);
            Apply(Scale);
        }

        /// <summary>Begins growing or shrinking to the given size. Nothing happens if already that size or mid-shift.</summary>
        public void ShiftTo(ShiftSize size)
        {
            if (Shifting || size == Size) return;
            Size = size;
            _from = Scale;
            _to = ScaleOf(size);
            _progress = 0f;
            ShiftStarted?.Invoke(size);
            GameEvents.RaiseShiftStarted(this, size);
        }

        /// <summary>Becomes the size at once: no shift, no events, no warp. Loading a save uses it.</summary>
        public void SetSizeNow(ShiftSize size)
        {
            Size = size; Scale = ScaleOf(size); _progress = 1f; Apply(Scale);
        }

        public void Toggle() => ShiftTo(Size == ShiftSize.Toy ? ShiftSize.Kid : ShiftSize.Toy);

        private void Update()
        {
#if UNITY_EDITOR
            if (devToggleKey != Key.None && Keyboard.current != null && Keyboard.current[devToggleKey].wasPressedThisFrame) Toggle();
#endif
            if (!Shifting) return;
            _progress = Mathf.Min(1f, _progress + Time.deltaTime / Mathf.Max(0.01f, settings.duration));
            float k = settings.ease.Evaluate(_progress);
            // Geometric blend: halfway between 0.125 and 1 is 0.35, not 0.56. Growth looks even that way.
            Scale = _from * Mathf.Pow(_to / _from, k);
            Apply(Scale);
            if (!Shifting)
            {
                Scale = _to;
                Apply(Scale);
                Shifted?.Invoke(Size);
                GameEvents.RaiseShifted(this, Size);
            }
        }

        private void Apply(float scale) => transform.localScale = Vector3.one * scale;
    }
}
