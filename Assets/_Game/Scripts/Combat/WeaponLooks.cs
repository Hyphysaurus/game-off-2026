using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A weapon's two lives: in a hand (no physics, no colliders, not a pickup) and loose in the world (a light physics
    /// prop E takes again). Remembers the authored scale so a weapon dropped by a kid still fits a toy's hand. Added by the
    /// WeaponHolder if the prefab has none.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponLooks : MonoBehaviour
    {
        private Vector3 _baseScale;
        private bool _captured;

        /// <summary>The weapon's own scale, as authored (for a holder at scale 1).</summary>
        public Vector3 BaseScale { get { Capture(); return _baseScale; } }

        private void Awake() => Capture();

        private void Capture()
        {
            if (_captured) return;
            _captured = true;
            _baseScale = transform.localScale;
        }

        public void SetLoose(bool loose)
        {
            Capture();
            var rb = GetComponent<Rigidbody>();
            if (loose && rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
            }
            if (rb != null)
            {
                rb.isKinematic = !loose;
                rb.interpolation = loose ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                rb.collisionDetectionMode = loose ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            }
            var cols = GetComponentsInChildren<Collider>(true);
            bool anyUsable = false;
            foreach (var c in cols) if (Usable(c)) { anyUsable = true; break; }
            if (loose && !anyUsable)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                var rs = GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    box.center = transform.InverseTransformPoint(b.center);
                    Vector3 ls = transform.lossyScale;
                    box.size = new Vector3(b.size.x / Mathf.Max(1e-4f, ls.x), b.size.y / Mathf.Max(1e-4f, ls.y), b.size.z / Mathf.Max(1e-4f, ls.z));
                }
                cols = GetComponentsInChildren<Collider>(true);
            }
            foreach (var c in cols) c.enabled = loose && Usable(c);
            var pickup = GetComponent<WeaponPickup>();
            if (loose && pickup == null) { pickup = gameObject.AddComponent<WeaponPickup>(); pickup.Offer(GetComponent<Weapon>()); }
            if (pickup != null) pickup.enabled = loose;
        }

        /// <summary>A concave mesh collider cannot be on a moving body (Unity logs an error and ignores it), and a pack
        /// model can carry one (the Foam Finger's does). It stays off; the weapon's own box is what lands.</summary>
        private static bool Usable(Collider c) => !(c is MeshCollider mesh && !mesh.convex);
    }
}
