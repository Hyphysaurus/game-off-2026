using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The moment a punch or kick lands: everything in a ball in front of the body takes the hit, once. The ball's place
    /// and size are in body heights, so a kid's fist lands where the kid's fist is. A Damageable loses health and is
    /// shoved; a loose prop is shoved. The shove grows with the body by the square root of its size (the test room's
    /// rule: a kid moves a crate about as many kid sizes as a toy moves it toy sizes); the damage stays.
    /// </summary>
    public static class PlayerStrike
    {
        private static readonly Collider[] Overlap = new Collider[32];
        private static readonly HashSet<Object> Struck = new HashSet<Object>();

        /// <summary>Where the hit ball is for a body standing at <paramref name="feet"/> facing <paramref name="forward"/>.</summary>
        public static Vector3 Centre(PlayerMoveSettings.Strike strike, Vector3 feet, Vector3 forward, float bodyHeight)
            => feet + forward * (strike.reach * bodyHeight) + Vector3.up * (strike.height * bodyHeight);

        /// <summary>
        /// Lands the strike. <paramref name="bodyHeight"/> is the standing body's height in world units and
        /// <paramref name="scale"/> the body's size (1 = toy). Returns how many things were hit.
        /// </summary>
        public static int Land(Transform owner, PlayerMoveSettings settings, PlayerMoveSettings.Strike strike, float bodyHeight, float scale)
        {
            Vector3 forward = Vector3.ProjectOnPlane(owner.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = owner.forward;
            Vector3 at = Centre(strike, owner.position, forward, bodyHeight);
            float toyHeight = bodyHeight / Mathf.Max(1e-4f, scale);
            float grow = settings.shoveGrowsWithSize ? Mathf.Sqrt(Mathf.Max(0f, scale)) : 1f;
            Vector3 shove = (forward + Vector3.up * settings.shoveUp) * (strike.shove * toyHeight * grow);

            int count = Physics.OverlapSphereNonAlloc(at, strike.width * bodyHeight, Overlap, settings.strikeMask, QueryTriggerInteraction.Ignore);
            Struck.Clear();
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                Collider col = Overlap[i];
                if (col.transform.IsChildOf(owner)) continue;
                Damageable health = Damageable.Find(col);
                if (health != null && health.transform.IsChildOf(owner)) continue;   // our own first-person stand-in
                Rigidbody body = col.attachedRigidbody;
                Object key = health != null ? health : (Object)body;
                if (key == null || !Struck.Add(key)) continue;   // a body made of several colliders is hit once
                // Unity only answers ClosestPoint for simple shapes; for the rest the box round it is near enough.
                bool simple = col is BoxCollider || col is SphereCollider || col is CapsuleCollider || (col is MeshCollider mesh && mesh.convex);
                Vector3 point = simple ? col.ClosestPoint(at) : col.bounds.ClosestPoint(at);
                if (health != null) { if (health.Hit(new DamageInfo(strike.damage, shove, point, owner))) hits++; }
                else if (Knockback.Apply(col.gameObject, shove, point)) hits++;
            }
            Struck.Clear();
            return hits;
        }
    }
}
