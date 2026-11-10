#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Mat beds (D-0049). The pack's foam mats collide with their exact toothed shape: a quarter-turned neighbour's teeth
    /// do not mesh, which leaves wheel-wide channels between mats, and every outer edge is a 0.73 wall to a car that
    /// clears 0.4 and to a toy that steps up 0.3. A bed is one smooth collider under each group of touching mats: flat
    /// on top at the mats' own height, with a slope all round, so cars and the walking toy roll on and off. The mats'
    /// own colliders are switched off (not removed); how the mats look does not change.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string MatBedDir = "Assets/_Game/Data/MatBeds";
        const float MatBedSkirt = 2.2f;     // how far the slope reaches out past the mats' edge: 0.73 up over 2.2 is about 18 degrees
        const float MatBedJoin = 0.6f;      // mats closer than this share a bed
        const float MatBedSink = 0.1f;      // the bed's foot goes this far into the floor, so no slit shows under the slope

        static bool IsMat(Transform t) => t.name == "Mat" && t.GetComponentInChildren<Renderer>() != null;

        /// <summary>Lays a bed under every group of touching mats among <paramref name="parent"/>'s children, replacing
        /// any beds already there. Returns how many beds.</summary>
        static int MatBeds(Transform parent)
        {
            foreach (var old in parent.Cast<Transform>().Where(t => t.name == "MatBed").ToArray()) Object.DestroyImmediate(old.gameObject);
            var mats = parent.Cast<Transform>().Where(IsMat).ToList();
            var boxes = mats.Select(m => B(m.gameObject)).ToList();
            // groups of touching mats
            var group = Enumerable.Range(0, mats.Count).ToArray();
            int Root(int i) { while (group[i] != i) i = group[i] = group[group[i]]; return i; }
            for (int i = 0; i < mats.Count; i++)
                for (int j = i + 1; j < mats.Count; j++)
                {
                    var reach = boxes[i]; reach.Expand(MatBedJoin * 2f);
                    if (reach.Intersects(boxes[j])) group[Root(i)] = Root(j);
                }
            int beds = 0;
            foreach (var members in Enumerable.Range(0, mats.Count).GroupBy(Root))
            {
                Bounds all = boxes[members.First()];
                foreach (int i in members) all.Encapsulate(boxes[i]);
                float foot = all.min.y - MatBedSink, height = all.max.y - foot;
                var bed = new GameObject("MatBed");
                bed.transform.SetParent(parent, false);
                bed.transform.position = new Vector3(all.center.x, foot, all.center.z);
                var collider = bed.AddComponent<MeshCollider>();
                collider.sharedMesh = MatBedMesh(all.extents.x, all.extents.z, height);
                collider.convex = true;
                foreach (int i in members)
                    foreach (var own in mats[i].GetComponentsInChildren<Collider>()) own.enabled = false;
                beds++;
                // is the top really flat and whole: rays down across the mats, seams included, must all land on the bed at the mats' height
                Physics.SyncTransforms();
                int rays = 0, off = 0;
                for (int ix = 0; ix <= 16; ix++)
                    for (int iz = 0; iz <= 16; iz++)
                    {
                        Vector3 from = new Vector3(Mathf.Lerp(all.min.x + 0.2f, all.max.x - 0.2f, ix / 16f), all.max.y + 2f, Mathf.Lerp(all.min.z + 0.2f, all.max.z - 0.2f, iz / 16f));
                        rays++;
                        if (!collider.Raycast(new Ray(from, Vector3.down), out RaycastHit hit, 5f) || Mathf.Abs(hit.point.y - all.max.y) > 0.01f) off++;
                        foreach (int i in members)
                            foreach (var own in mats[i].GetComponentsInChildren<Collider>())
                                if (own.enabled && own.Raycast(new Ray(from, Vector3.down), out _, 5f)) off++;
                    }
                Log.AppendLine($"  bed check: {rays} rays, {off} off the top or on a mat's own collider");
                Log.AppendLine($"mat bed under {members.Count()} mats at {bed.transform.position}: {all.size.x:F1} x {all.size.z:F1}, top {all.max.y:F2}, slope {Mathf.Atan2(height, MatBedSkirt) * Mathf.Rad2Deg:F0} deg over {MatBedSkirt}");
            }
            return beds;
        }

        /// <summary>The bed's shape: a flat top the size of the mats, a foot wider by the skirt, straight slopes between.
        /// One mesh asset per size, remade in place.</summary>
        static Mesh MatBedMesh(float halfX, float halfZ, float height)
        {
            if (!AssetDatabase.IsValidFolder(MatBedDir)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(MatBedDir).Replace('\\', '/'), System.IO.Path.GetFileName(MatBedDir));
            string name = $"MatBed_{halfX * 2f:F1}x{halfZ * 2f:F1}x{height:F2}", path = MatBedDir + "/" + name + ".asset";
            float fx = halfX + MatBedSkirt, fz = halfZ + MatBedSkirt;
            var verts = new[]
            {
                new Vector3(-fx, 0f, -fz), new Vector3(fx, 0f, -fz), new Vector3(fx, 0f, fz), new Vector3(-fx, 0f, fz),                         // foot
                new Vector3(-halfX, height, -halfZ), new Vector3(halfX, height, -halfZ), new Vector3(halfX, height, halfZ), new Vector3(-halfX, height, halfZ),   // top
            };
            var tris = new[]
            {
                4, 6, 5, 4, 7, 6,     // top
                0, 1, 2, 0, 2, 3,     // foot
                0, 4, 5, 0, 5, 1,     // south slope
                1, 5, 6, 1, 6, 2,     // east
                2, 6, 7, 2, 7, 3,     // north
                3, 7, 4, 3, 4, 0,     // west
            };
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = mesh == null;
            if (fresh) mesh = new Mesh(); else mesh.Clear();
            mesh.name = name;
            mesh.vertices = verts; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Menu: beds under the mats of every open scene (Greybox's playroom, the Workshop). Safe to run again.
        /// Save the scenes afterwards.</summary>
        [MenuItem("Shift Jam/Add Mat Beds (open scenes)")]
        public static void AddMatBeds()
        {
            Log.Clear();
            int beds = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s); if (!scene.isLoaded) continue;
                var parents = new HashSet<Transform>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t.parent != null && IsMat(t)) parents.Add(t.parent);
                foreach (var parent in parents) beds += MatBeds(parent);
                if (parents.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"MAT BEDS: {beds} laid. Save the scene (Ctrl+S).\n" + Log);
        }

        /// <summary>Batch: -executeMethod ShiftJam.CourseBuilder.LandMatBeds (-nographics is fine). D-0049 without a course
        /// rebuild: beds in Greybox and in the Workshop, both saved.</summary>
        public static void LandMatBeds()
        {
            foreach (var path in new[] { ScenePath, "Assets/_Game/Scenes/Workshop.unity" })
            {
                EditorSceneManager.OpenScene(path);
                AddMatBeds();
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            Debug.Log("D-0049 LANDED");
        }
    }
}
#endif
