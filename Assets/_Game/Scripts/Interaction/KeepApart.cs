using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Never boxes the player in (D-0041). A thing let go while it overlaps the player's body (a kid's flashlight set down by
    /// a toy, a big prop let go close to the chest) ignores the body until the two have parted, then touches it again.
    /// Added to the thing only when they overlap; holding the thing again cancels it.
    /// </summary>
    public sealed class KeepApart : MonoBehaviour
    {
        private readonly List<Collider> _mine = new List<Collider>();
        private Collider _body;

        /// <summary>
        /// Lets <paramref name="colliders"/> (the thing's) and <paramref name="body"/> (the player's) touch again, at once if
        /// they do not overlap, or once they have parted if they do. Returns true when it had to keep them apart.
        /// </summary>
        public static bool Until(GameObject thing, IEnumerable<Collider> colliders, Collider body)
        {
            Cancel(thing);
            if (body == null) return false;
            Physics.SyncTransforms();   // the thing was just moved by its transform: overlap tests read the moved colliders
            bool overlap = false;
            foreach (Collider c in colliders)
                if (c != null && c.enabled && !c.isTrigger && Overlaps(c, body)) { overlap = true; break; }

            if (!overlap || thing == null)
            {
                foreach (Collider c in colliders) if (c != null) Physics.IgnoreCollision(c, body, false);
                return false;
            }
            var keep = thing.AddComponent<KeepApart>();
            keep._body = body;
            foreach (Collider c in colliders)
            {
                if (c == null) continue;
                keep._mine.Add(c);
                Physics.IgnoreCollision(c, body, true);
            }
            return true;
        }

        /// <summary>Stops keeping <paramref name="thing"/> apart without touching its collision state (a new hold sets its own).</summary>
        public static void Cancel(GameObject thing)
        {
            if (thing == null || !thing.TryGetComponent(out KeepApart keep)) return;
            keep._mine.Clear();
            keep._body = null;
            DestroyImmediate(keep);
        }

        private void FixedUpdate()
        {
            if (_body == null) { Destroy(this); return; }
            foreach (Collider c in _mine)
                if (c != null && c.enabled && Overlaps(c, _body)) return;   // still inside: keep ignoring
            foreach (Collider c in _mine) if (c != null) Physics.IgnoreCollision(c, _body, false);
            _mine.Clear();
            Destroy(this);
        }

        // Boxes, so a body inside a hollow mesh (no triangle crossing it) still counts as overlapping.
        private static bool Overlaps(Collider a, Collider body) => a.bounds.Intersects(body.bounds);
    }
}
