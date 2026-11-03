using System;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// Something a kid-sized player can pick up, aim and put down. It knows nothing about hands: the Carrier
    /// on the Player moves it. It only says how to hold it (which way is its front) and tells listeners when
    /// it is picked up, used (the Attack button while held) or put down.
    /// </summary>
    public sealed class Carryable : MonoBehaviour
    {
        [SerializeField, Tooltip("Rotation applied in the hand so the thing's business end points where the player looks. The flashlight's lens is on its local -Z, so (0, 180, 0).")]
        private Vector3 holdRotation = Vector3.zero;
        [SerializeField] private UnityEvent onPickedUp;
        [SerializeField] private UnityEvent onPutDown;

        public Quaternion HoldRotation => Quaternion.Euler(holdRotation);
        public bool IsCarried { get; private set; }
        /// <summary>Raised when the Attack button is pressed while this is held. The flashlight toggles on it.</summary>
        public event Action Used;

        internal void SetCarried(bool carried)
        {
            if (IsCarried == carried) return;
            IsCarried = carried;
            if (carried) onPickedUp?.Invoke(); else onPutDown?.Invoke();
        }

        internal void Use() => Used?.Invoke();
    }
}
