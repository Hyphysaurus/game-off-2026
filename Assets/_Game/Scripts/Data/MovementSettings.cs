using UnityEngine;

namespace ShiftJam
{
    /// <summary>The on-foot feel, as numbers. Every value is for kid size; the motor multiplies by the current shift scale.</summary>
    [CreateAssetMenu(menuName = "Shift Jam/Movement Settings", fileName = "MovementSettings")]
    public sealed class MovementSettings : ScriptableObject
    {
        [Header("Ground (metres per second at kid size)")]
        public float walkSpeed = 4f;
        public float sprintSpeed = 7f;
        [Tooltip("How quickly the velocity reaches the stick's target, m/s per second.")]
        public float acceleration = 40f;
        [Tooltip("How quickly the velocity dies when the stick is released.")]
        public float deceleration = 50f;
        [Tooltip("Degrees per second the body turns to face the way it travels.")]
        public float turnSpeed = 720f;

        [Header("Air")]
        public float jumpHeight = 1.2f;
        public float gravity = 25f;
        [Range(0f, 1f), Tooltip("Share of ground acceleration available in the air.")]
        public float airControl = 0.5f;
        [Tooltip("Seconds after walking off a ledge during which a jump still works.")]
        public float coyoteTime = 0.12f;
        [Tooltip("Seconds a jump press is remembered while still in the air, so a slightly early press lands.")]
        public float jumpBuffer = 0.12f;
        public float maxFallSpeed = 30f;
        [Range(0f, 1f), Tooltip("Releasing Jump while still rising multiplies the upward speed by this: 1 = every jump is full height, 0.5 = a tap is a short hop.")]
        public float jumpCutMultiplier = 0.5f;

        [Header("Animation")]
        [Tooltip("Seconds the Animator keeps Grounded true after the capsule loses the floor, unless a jump started. Hides the one-frame ground flicker on steps and slopes.")]
        public float groundedGrace = 0.1f;

        [Header("Props")]
        [Tooltip("How fast a shoved loose prop is set moving, m/s at kid size; a toy shoves at toy speed.")]
        public float pushSpeed = 3f;
        [Tooltip("Gravity for loose props (rigidbodies), in world units per second squared. 0 leaves the project's physics alone. Also the box burst's panels.")]
        public float propGravity = 60f;
    }
}
