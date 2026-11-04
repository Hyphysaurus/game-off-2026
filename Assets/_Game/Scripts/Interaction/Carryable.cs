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
        [SerializeField, Tooltip("Yaw-only rotation used when the thing is put down, so its front faces the way the player faces.")]
        private Vector3 holdRotation = Vector3.zero;
        [Header("Grip (in the right hand)")]
        [SerializeField, Tooltip("Where the palm centre is, in the hand bone's own space (rig metres): the fingers run along the hand's +Y.")]
        private Vector3 gripPalm = new Vector3(0f, 0.08f, 0f);
        [SerializeField, Tooltip("Slide of the thing along its own axes from the palm, world units: the flashlight's barrel sits 1.5 behind its head so the fist closes on the barrel.")]
        private Vector3 gripOffset = new Vector3(0f, 0f, -1.5f);
        [SerializeField, Tooltip("Extra turn of the thing in the hand. Zero = the thing's +Z follows the hand's +Z (the thumb side).")]
        private Vector3 gripRotation = Vector3.zero;
        [SerializeField, Tooltip("When put down, lie along the ground: the far end finds its own support, so a ruler laid from a table edge toward a box becomes a ramp.")]
        private bool plank;
        [SerializeField] private UnityEvent onPickedUp;
        [SerializeField] private UnityEvent onPutDown;

        public Quaternion HoldRotation => Quaternion.Euler(holdRotation);
        public Vector3 GripPalm => gripPalm;
        public Vector3 GripOffset => gripOffset;
        public Quaternion GripRotation => Quaternion.Euler(gripRotation);
        public bool Plank => plank;
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
