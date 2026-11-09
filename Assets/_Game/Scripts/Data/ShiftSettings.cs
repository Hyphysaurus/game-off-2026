using UnityEngine;

namespace ShiftJam
{
    /// <summary>The two sizes the player can be, and how the change between them plays out.</summary>
    [CreateAssetMenu(menuName = "Shift Jam/Shift Settings", fileName = "ShiftSettings")]
    public sealed class ShiftSettings : ScriptableObject
    {
        [Tooltip("Scale of the player at toy size. 0.125 = one eighth of kid size, an action figure.")]
        public float toyScale = 0.125f;
        [Tooltip("Scale of the player at kid size; the room is built at this scale.")]
        public float kidScale = 1f;
        [Tooltip("Seconds the grow or shrink takes.")]
        public float duration = 0.35f;
        [Tooltip("Shape of the change over time, 0 to 1.")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("The carried shift (D-0045)")]
        [Tooltip("Charges the player can hold for the Shift key (F, pad RB). Cores, fall resets and loaded saves refill to this. 0 = the key does nothing; a big number = shift any time.")]
        public int maxCharges = 1;
        [Tooltip("Seconds after a shift ends before the Shift key works again.")]
        public float freeShiftWait = 0.5f;
    }
}
