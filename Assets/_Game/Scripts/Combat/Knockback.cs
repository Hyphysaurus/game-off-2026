using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Passes a shove to whatever moves the thing that was hit: the first-person body, the player's motor, or a Rigidbody
    /// (as an impulse, so heavy props barely budge). The velocity is in world units per second, already scaled by the
    /// attacker's size.
    /// </summary>
    public static class Knockback
    {
        /// <summary>Returns true if something took the shove.</summary>
        public static bool Apply(GameObject target, Vector3 velocity, Vector3 point)
        {
            if (target == null || velocity.sqrMagnitude <= 0f) return false;
            var body = target.GetComponentInParent<FirstPersonBody>();
            if (body != null && body.isActiveAndEnabled) { body.AddVelocity(velocity); return true; }
            var motor = target.GetComponentInParent<PlayerMotor>();
            if (motor != null)
            {
                if (!motor.isActiveAndEnabled) return false;   // driving or in first person: the car / body takes it instead
                motor.Knock(velocity);
                return true;
            }
            var rb = target.GetComponentInParent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                // An impulse sized for a 1-mass prop: lighter flies, heavier shrugs.
                rb.AddForceAtPosition(velocity, point, ForceMode.Impulse);
                return true;
            }
            return false;
        }
    }
}
