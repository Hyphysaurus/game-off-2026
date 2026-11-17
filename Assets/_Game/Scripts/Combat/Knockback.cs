using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Passes a shove to whatever moves the thing that was hit: the first-person body, the player's motor, or a Rigidbody
    /// (as an impulse, so heavy props barely budge). The velocity is in world units per second, already scaled by the
    /// attacker's size. A shot passes on less than all of it to a loose thing when its shooter is big (Projectile):
    /// that share is <c>loose</c>; the player and the first-person body always take all of it.
    /// </summary>
    public static class Knockback
    {
        /// <summary>Returns true if something took the shove.</summary>
        public static bool Apply(GameObject target, Vector3 velocity, Vector3 point, float loose = 1f)
        {
            if (target == null || velocity.sqrMagnitude <= 0f) return false;
            var body = target.GetComponentInParent<FirstPersonBody>();
            if (body != null && body.isActiveAndEnabled) { body.AddVelocity(velocity); return true; }
            var motor = target.GetComponentInParent<PlayerMotor>();
            if (motor != null)
            {
                if (motor.isActiveAndEnabled) { motor.Knock(velocity); return true; }
                // Parked. In first person the body stands in for the player (a separate object, so not found above);
                // driving, the car takes the hits instead.
                var fp = motor.GetComponent<FirstPersonMode>();
                if (fp != null && fp.Body != null && fp.Body.isActiveAndEnabled) { fp.Body.AddVelocity(velocity); return true; }
                return false;
            }
            var rb = target.GetComponentInParent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                // An impulse sized for a 1-mass prop: lighter flies, heavier shrugs.
                rb.AddForceAtPosition(velocity * loose, point, ForceMode.Impulse);
                return true;
            }
            return false;
        }
    }
}
