using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Size and resting maths by what is drawn, not by the pivot: pack prefabs put their pivots anywhere.
    /// World = axis-aligned box around the renderers; Local = a tighter box in the object's own axes (for the overlap
    /// test and the selection box).
    /// </summary>
    public static class BuildBounds
    {
        private static readonly List<Renderer> Renderers = new List<Renderer>();
        private static readonly Collider[] Overlap = new Collider[64];
        private static readonly Vector3[] Corners = new Vector3[8];

        private static bool Counts(Renderer r) =>
            r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer) && !(r is LineRenderer) && !(r is TrailRenderer);

        public static bool World(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            go.GetComponentsInChildren(false, Renderers);
            foreach (var r in Renderers)
            {
                if (!Counts(r)) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>The renderers' box in <paramref name="root"/>'s local space (so it turns with the object).</summary>
        public static bool Local(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            root.GetComponentsInChildren(false, Renderers);
            foreach (var r in Renderers)
            {
                if (!Counts(r)) continue;
                Bounds mb; Matrix4x4 m;
                if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null) { mb = skinned.sharedMesh.bounds; m = toRoot * r.transform.localToWorldMatrix; }
                else if (r is MeshRenderer && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null) { mb = mf.sharedMesh.bounds; m = toRoot * r.transform.localToWorldMatrix; }
                else { mb = r.bounds; m = toRoot; }
                CornersOf(mb, Corners);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(Corners[i]);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
                }
            }
            return any;
        }

        public static void CornersOf(Bounds b, Vector3[] into)
        {
            Vector3 min = b.min, max = b.max;
            into[0] = new Vector3(min.x, min.y, min.z); into[1] = new Vector3(max.x, min.y, min.z);
            into[2] = new Vector3(max.x, min.y, max.z); into[3] = new Vector3(min.x, min.y, max.z);
            into[4] = new Vector3(min.x, max.y, min.z); into[5] = new Vector3(max.x, max.y, min.z);
            into[6] = new Vector3(max.x, max.y, max.z); into[7] = new Vector3(min.x, max.y, max.z);
        }

        /// <summary>Grid step that suits the thing: 1 unit for toy-sized pieces, 5 for furniture.</summary>
        public static float GridFor(Bounds world) => Mathf.Max(world.size.x, world.size.z) > 12f ? 5f : 1f;

        /// <summary>
        /// Moves <paramref name="t"/> so its drawn box sits on a surface: on a floor-like hit its bottom touches the point
        /// and it is centred over it; on a wall-like hit it is pushed out of the wall, centred on the point.
        /// </summary>
        public static void Rest(Transform t, Vector3 point, Vector3 normal)
        {
            if (!World(t.gameObject, out var b)) { t.position = point; return; }
            Vector3 offset;
            if (normal.y >= 0.5f) offset = new Vector3(point.x - b.center.x, point.y - b.min.y, point.z - b.center.z);
            else
            {
                Vector3 flat = new Vector3(normal.x, 0f, normal.z).normalized;
                float reach = Mathf.Abs(flat.x) * b.extents.x + Mathf.Abs(flat.z) * b.extents.z;
                offset = point + flat * reach - b.center;
            }
            t.position += offset;
        }

        /// <summary>Moves <paramref name="t"/> sideways so its drawn centre lands on the grid.</summary>
        public static void SnapToGrid(Transform t, float grid)
        {
            if (grid <= 0f || !World(t.gameObject, out var b)) return;
            float x = Mathf.Round(b.center.x / grid) * grid, z = Mathf.Round(b.center.z / grid) * grid;
            t.position += new Vector3(x - b.center.x, 0f, z - b.center.z);
        }

        /// <summary>
        /// True when the thing's own box (slightly shrunk, and lifted off what it stands on) touches any solid collider
        /// that is not its own, not in <paramref name="ignore"/>, and not under <paramref name="ignoreRoot"/>.
        /// </summary>
        public static bool Blocked(Transform t, HashSet<Transform> ignore, Transform ignoreRoot)
        {
            if (!Local(t, out var lb)) return false;
            Vector3 lossy = t.lossyScale;
            Vector3 half = Vector3.Scale(lb.extents, new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
            float lift = Mathf.Max(0.05f, half.y * 0.08f);
            half.x *= 0.96f; half.z *= 0.96f;
            half.y = Mathf.Max(0.01f, half.y - lift / 2f - half.y * 0.02f);
            Vector3 centre = t.TransformPoint(lb.center) + t.up * (lift / 2f);
            int n = Physics.OverlapBoxNonAlloc(centre, half, Overlap, t.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Overlap[i];
                if (c.transform.IsChildOf(t)) continue;
                if (ignoreRoot != null && c.transform.IsChildOf(ignoreRoot)) continue;
                if (ignore != null && Ignored(c.transform, ignore)) continue;
                return true;
            }
            return false;
        }

        private static bool Ignored(Transform c, HashSet<Transform> ignore)
        {
            for (var at = c; at != null; at = at.parent) if (ignore.Contains(at)) return true;
            return false;
        }
    }
}
