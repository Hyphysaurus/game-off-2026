using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Who touched a trigger: the player on foot (its own capsule) or, in first person, the body that stands in for it
    /// (the player's own capsule is off then). Pads, fans, checkpoints and hazards ask this so they work in both.
    /// </summary>
    public static class PlayerTouch
    {
        /// <summary>The player's Shifter behind the collider, or null. <paramref name="body"/> is the first-person body
        /// when that is what touched it, else null.</summary>
        public static Shifter Find(Collider other, out FirstPersonBody body)
        {
            body = other != null ? other.GetComponentInParent<FirstPersonBody>() : null;
            if (body != null) return body.Owner != null ? body.Owner.GetComponentInParent<Shifter>() : null;
            return other != null ? other.GetComponentInParent<Shifter>() : null;
        }
    }
}
