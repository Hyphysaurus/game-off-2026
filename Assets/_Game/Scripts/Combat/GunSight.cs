using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Finds the middle of a gun's sight from its model, the test room's way. The pack's guns carry a marker where a
    /// sight is mounted (Place_..._Sight), and the sight fitted there has a see-through part exactly where you look
    /// through it (its Sight_Glass and Sight_Projection materials): the middle of that part is the line of sight. A
    /// sight without glass (iron sights) gives the mount itself, and the caller looks a little above it.
    /// </summary>
    public static class GunSight
    {
        private static readonly string[] Glass = { "Sight_Glass", "Sight_Projection" };

        /// <summary>The sight of the gun under <paramref name="weapon"/>, in that transform's own space. False when the
        /// model has no sight mount. <paramref name="onMount"/>: no glass was found, the point is the mount.</summary>
        public static bool Find(Transform weapon, out Vector3 point, out bool onMount)
        {
            point = Vector3.zero;
            onMount = false;
            Transform mount = null;
            foreach (Transform child in weapon.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Place_") && child.name.EndsWith("_Sight")) { mount = child; break; }
            if (mount == null) return false;

            // The glass you look through: the box round every see-through sight surface, in the weapon's space.
            Matrix4x4 toWeapon = weapon.worldToLocalMatrix;
            bool any = false;
            var box = new Bounds();
            foreach (MeshRenderer part in mount.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!part.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                Material[] looks = part.sharedMaterials;
                Matrix4x4 toHere = toWeapon * part.transform.localToWorldMatrix;
                for (int surface = 0; surface < looks.Length && surface < mesh.subMeshCount; surface++)
                {
                    if (looks[surface] == null || !IsGlass(looks[surface].name)) continue;
                    Bounds b = mesh.GetSubMesh(surface).bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var p = new Vector3((corner & 1) == 0 ? b.min.x : b.max.x, (corner & 2) == 0 ? b.min.y : b.max.y, (corner & 4) == 0 ? b.min.z : b.max.z);
                        p = toHere.MultiplyPoint3x4(p);
                        if (any) box.Encapsulate(p); else { box = new Bounds(p, Vector3.zero); any = true; }
                    }
                }
            }
            if (any) { point = box.center; return true; }
            point = toWeapon.MultiplyPoint3x4(mount.position);
            onMount = true;
            return true;
        }

        private static bool IsGlass(string material)
        {
            foreach (string name in Glass) if (material.StartsWith(name)) return true;
            return false;
        }
    }
}
