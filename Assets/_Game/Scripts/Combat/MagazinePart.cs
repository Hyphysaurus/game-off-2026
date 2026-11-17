using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The magazine you can see on a gun's model, for the reload: the part fitted under the pack's marker
    /// (Place_..._Magazine). It can be slid down out of the gun and hidden for the moment it is swapped, and it knows
    /// where its bottom is so the free hand can take hold of it. A model without the marker has none: nothing moves and
    /// the hand goes to a spot just under the grip.
    /// Not a component: a BlasterWeapon finds its own once, from its model.
    /// </summary>
    public sealed class MagazinePart
    {
        private readonly Transform _part;
        private readonly Vector3 _home, _down;
        private readonly Renderer[] _renderers;
        private bool _hidden;

        /// <summary>The bottom of the magazine as it sits in the gun, in the weapon's own (root) space.</summary>
        public Vector3 Bottom { get; }

        public MagazinePart(Transform weapon)
        {
            Bottom = new Vector3(0f, -0.05f, 0f);   // no marker: a spot just under the grip (the test room's fallback)
            _renderers = new Renderer[0];
            Transform marker = null;
            foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Place_") && child.name.EndsWith("_Magazine")) { marker = child; break; }
            if (marker == null) return;
            // What is fitted there: the marker's first child that carries a mesh.
            for (int i = 0; i < marker.childCount && _part == null; i++)
                if (marker.GetChild(i).GetComponentInChildren<MeshFilter>(true) != null) _part = marker.GetChild(i);
            if (_part == null) return;

            _home = _part.localPosition;
            // "Down" is the weapon's own down, in the units the part's parent measures in.
            _down = _part.parent.InverseTransformVector(weapon.TransformVector(Vector3.down));
            _renderers = _part.GetComponentsInChildren<Renderer>(true);

            // Its bottom: the lowest point of its meshes, under their middle, in the weapon's space.
            Matrix4x4 toWeapon = weapon.worldToLocalMatrix;
            bool any = false; var box = new Bounds();
            foreach (MeshFilter mesh in _part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.sharedMesh == null) continue;
                Bounds b = mesh.sharedMesh.bounds;
                Matrix4x4 m = toWeapon * mesh.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? b.min.x : b.max.x, (corner & 2) == 0 ? b.min.y : b.max.y, (corner & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; } else box.Encapsulate(p);
                }
            }
            if (any) Bottom = new Vector3(box.center.x, box.min.y, box.center.z);
        }

        /// <summary>Slides the magazine <paramref name="drop"/> down out of the gun (the weapon's own units, 0 = seated)
        /// and shows or hides it.</summary>
        public void Set(float drop, bool hidden)
        {
            if (_part == null) return;
            _part.localPosition = _home + _down * drop;
            if (hidden == _hidden) return;
            _hidden = hidden;
            foreach (Renderer r in _renderers) if (r != null) r.enabled = !hidden;
        }

        /// <summary>Back in the gun and showing.</summary>
        public void Seat() => Set(0f, false);
    }
}
