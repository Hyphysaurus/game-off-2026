#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Editor maker for a figure's hit shapes (D-0080): gives a target its HitShapes, one fitted collider for each mesh
    /// it draws. The fitted collider is the drawn mesh itself (a mesh collider on the model's own object: the packs
    /// ship one on every model, which our builders had switched off), so a shot lands on the drawn surface and passes
    /// where nothing is drawn. The mesh is named, never copied: nothing of a pack goes into our own files, and a
    /// project without the pack has no hit shapes and lands its shots on the capsule as before.
    /// "Fit Hit Shapes" changes the four figure targets' prefabs in place (their ids stay, scenes keep their copies);
    /// the full builders call Fit too, so a rebuild of a target makes the same thing.
    /// A NEW TARGET: call HitShapePieces.Fit(root) from its builder (or add its prefab's path to Figures and run the
    /// menu). A figure drawn by a skinned mesh gets no shape from this (a mesh collider does not follow bones): it
    /// would need a box on each bone, as the test room's dummy has, and says so in the log.
    /// Batch: -executeMethod ShiftJam.HitShapePieces.Build
    /// </summary>
    public static class HitShapePieces
    {
        private static readonly string[] Figures =
        {
            "Assets/_Game/Prefabs/Pieces/Interactables/PracticeSkittle.prefab",
            "Assets/_Game/Prefabs/Pieces/Interactables/PracticeBear.prefab",
            "Assets/_Game/Prefabs/Pieces/Interactables/ToySoldier.prefab",
            "Assets/_Game/Prefabs/Combat/MischiefSoldier.prefab",
        };

        [MenuItem("Shift Jam/Workshop/Fit Hit Shapes (targets)")]
        public static void Build()
        {
            var done = new List<string>();
            foreach (string path in Figures)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("[HitShapes] " + path + " is not there: skipped."); continue; }
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int count = Fit(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done.Add($"{root.name} ({count})");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[HitShapes] fitted {done.Count} figures (hit shapes each): {string.Join(", ", done)}.");
        }

        /// <summary>Gives <paramref name="root"/> its HitShapes: every mesh drawn under it becomes a hit shape, on the
        /// mesh's own object so it rides whatever part that is (a target's rocking pivot, a soldier's tipping body).
        /// Hands back how many it has. Safe to run again.</summary>
        internal static int Fit(GameObject root)
        {
            var shapes = new List<Object>();
            foreach (Renderer drawn in root.GetComponentsInChildren<Renderer>(true))
            {
                if (drawn is SkinnedMeshRenderer)
                {
                    Debug.LogWarning($"[HitShapes] {root.name}: {drawn.name} is a skinned mesh, which a mesh collider does not follow: it gets no hit shape.");
                    continue;
                }
                if (!(drawn is MeshRenderer) || !drawn.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
                if (!drawn.TryGetComponent(out MeshCollider shape)) shape = drawn.gameObject.AddComponent<MeshCollider>();
                if (shape.sharedMesh != filter.sharedMesh) shape.sharedMesh = filter.sharedMesh;
                shape.convex = false;        // the mesh as it is drawn, hollows and all
                shape.isTrigger = false;
                shape.excludeLayers = ~0;    // it touches nothing (HitShapes sets this again as it wakes)
                shape.enabled = true;
                shapes.Add(shape);
            }
            if (!root.TryGetComponent(out HitShapes hit)) hit = root.AddComponent<HitShapes>();
            WorkshopPieces.SetArray(hit, "shapes", shapes);
            if (shapes.Count == 0) Debug.LogWarning($"[HitShapes] {root.name} draws no mesh that can be a hit shape: shots land on its own colliders.");
            return shapes.Count;
        }
    }
}
#endif
