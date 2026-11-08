using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>A hinged door that swings open when told to. Put the door's collider under the hinge so it swings too.</summary>
    public sealed class Door : MonoBehaviour
    {
        [SerializeField, Tooltip("The part that rotates. This object if empty.")] private Transform hinge;
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private UnityEvent onOpened;

        private Quaternion _closed;
        private float _progress = -1f;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (hinge == null) hinge = transform;
            _closed = hinge.localRotation;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _progress = 0f;
            GameEvents.RaiseDoorOpening(this);
        }

        private void Update()
        {
            if (_progress < 0f) return;
            _progress = Mathf.Min(1f, _progress + Time.deltaTime / Mathf.Max(0.01f, duration));
            hinge.localRotation = _closed * Quaternion.Euler(0f, openAngle * ease.Evaluate(_progress), 0f);
            if (_progress >= 1f)
            {
                _progress = -1f;
                onOpened?.Invoke();
            }
        }
    }
}
