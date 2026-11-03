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
    }
}
