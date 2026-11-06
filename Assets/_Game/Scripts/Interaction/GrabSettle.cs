using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Puts a grabbed thing back the way it was before the grab once it has come to rest: no body (or a kinematic one) and
    /// its mesh colliders concave again. The third-person grab gives static props (pillows, the bear, boxes) a moving body
    /// so they can be held and thrown; setting one down settles it at once, a throw settles when it lands.
    /// </summary>
    public sealed class GrabSettle : MonoBehaviour
    {
        /// <summary>How a thing was before the grab, and the two steps around holding it.</summary>
        public sealed class Original
        {
            public bool addedBody, wasKinematic;
            public CollisionDetectionMode mode;
            public readonly List<MeshCollider> madeConvex = new List<MeshCollider>();

            /// <summary>Gets <paramref name="go"/> ready to hold: a moving body (added if it has none), its mesh colliders
            /// convex (a moving body takes no other). Returns the body.</summary>
            public static Rigidbody Prepare(GameObject go, out Original was)
            {
                was = new Original();
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) { rb = go.AddComponent<Rigidbody>(); rb.mass = 1f; was.addedBody = true; }
                was.wasKinematic = rb.isKinematic;
                was.mode = rb.collisionDetectionMode;
                foreach (var mc in go.GetComponentsInChildren<MeshCollider>())
                    if (mc.enabled && !mc.isTrigger && !mc.convex) { mc.convex = true; was.madeConvex.Add(mc); }
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = false;
                return rb;
            }

            /// <summary>Back to how it was: still, kinematic first (a moving body may not keep concave colliders), the
            /// colliders concave again, an added body removed.</summary>
            public void Restore(Rigidbody rb)
            {
                if (rb == null) return;
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                if (addedBody || wasKinematic)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.isKinematic = true;
                }
                foreach (var mc in madeConvex) if (mc != null) mc.convex = false;
                madeConvex.Clear();
                if (addedBody) Destroy(rb);
                else rb.collisionDetectionMode = mode;
            }
        }

        private Original _was;
        private Rigidbody _rb;
        private float _stillFor, _until;

        /// <summary>Restores <paramref name="rb"/> as <paramref name="was"/> once it lies still (or after a few seconds).</summary>
        public static void After(Rigidbody rb, Original was, float timeout = 6f)
        {
            if (rb == null || was == null) return;
            var s = rb.gameObject.AddComponent<GrabSettle>();
            s._rb = rb; s._was = was; s._until = Time.time + timeout;
        }

        /// <summary>A grab takes the thing back before it settled: the restore it was waiting for goes with it.</summary>
        public Original TakeOver()
        {
            var was = _was;
            _was = null;
            Destroy(this);
            return was;
        }

        private void FixedUpdate()
        {
            if (_rb == null || _was == null) { Destroy(this); return; }
            Bounds b = SpringHold.BoundsOf(_rb);
            float slow = 0.1f * Mathf.Max(0.05f, b.extents.magnitude);   // a tenth of its own size per second
            bool still = _rb.linearVelocity.magnitude < slow && _rb.angularVelocity.magnitude < 0.5f;
            _stillFor = still ? _stillFor + Time.fixedDeltaTime : 0f;
            if (_stillFor < 0.3f && Time.time < _until) return;
            _was.Restore(_rb);
            _was = null;
            Destroy(this);
        }
    }
}
