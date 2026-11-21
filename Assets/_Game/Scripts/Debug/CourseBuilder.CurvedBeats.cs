#if UNITY_EDITOR
using System.Collections.Generic;
using AmazingAssets.CurvedWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The Curved World beats (D-0102): the garden outside the window gets Curved World copies of its materials
    /// (Data/CurvedWorld, one per pack material, the pack's own left as they are), and a "WorldBend" (Little Planet Y,
    /// ID 1, pivoted at the window) under R6_Window rounds it into a little planet; the same bend pulses round the player
    /// on every shift (WorldBend). Idempotent. Menu: Shift Jam / Curved World / Land Beats.
    /// Curved World is a licensed pack kept out of git: on a clone without it these materials draw pink.
    /// </summary>
    public static partial class CourseBuilder
    {
        const string CurvedLit = "Amazing Assets/Curved World/Lit";
        const string CurvedFolder = "Assets/_Game/Data/CurvedWorld";

        /// <summary>The garden's ground as a fine grid (its top face only, in the cube's own space): Curved World moves
        /// vertices, and a box has eight. The box's collider stays as it was.</summary>
        static void GardenGround(Transform ground)
        {
            const int nx = 100, nz = 80;
            string path = CurvedFolder + "/GardenGround.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "GardenGround" }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            var v = new Vector3[(nx + 1) * (nz + 1)]; var uv = new Vector2[v.Length]; var n = new Vector3[v.Length];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    int i = z * (nx + 1) + x;
                    v[i] = new Vector3(x / (float)nx - 0.5f, 0.5f, z / (float)nz - 0.5f);
                    uv[i] = new Vector2(x / (float)nx, z / (float)nz); n[i] = Vector3.up;
                }
            var tris = new int[nx * nz * 6]; int t = 0;
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int a = z * (nx + 1) + x, b = a + nx + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            mesh.vertices = v; mesh.uv = uv; mesh.normals = n; mesh.triangles = tris;
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var mf = ground.GetComponent<MeshFilter>(); if (mf != null) mf.sharedMesh = mesh;
            Log.AppendLine($"garden ground: a {nx} x {nz} grid ({v.Length} points)");
        }

        [MenuItem("Shift Jam/Curved World/Land Beats")]
        public static void LandCurvedBeats()
        {
            Log.Clear();
            var active = SceneManager.GetActiveScene();
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
            var r6 = GameObject.Find("Room/Course/R6_Window");
            var shader = Shader.Find(CurvedLit);
            if (r6 == null || shader == null) { Debug.LogError($"D-0102: R6_Window {(r6 != null)}, shader {(shader != null)}"); return; }
            if (!AssetDatabase.IsValidFolder(CurvedFolder)) AssetDatabase.CreateFolder("Assets/_Game/Data", "CurvedWorld");

            // the garden: the ground and the trees, their materials swapped for bending copies
            var made = new Dictionary<Material, Material>();
            Material template = null; int renderers = 0;
            foreach (Transform t in r6.transform)
            {
                if (t.name != "Ground" && !t.name.StartsWith("GardenTree")) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    var own = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < own.Length; i++)
                    {
                        var m = own[i];
                        if (m == null) continue;
                        if (m.shader == shader) { made[m] = m; template = m; continue; }   // switched on an earlier run: checked below
                        if (m.shader.name != "Universal Render Pipeline/Lit") { Log.AppendLine($"garden: {r.name} keeps {m.name} ({m.shader.name}: not Lit)"); continue; }
                        if (!made.TryGetValue(m, out var bent))
                        {
                            string path = $"{CurvedFolder}/{m.name}_Bend.mat";
                            bent = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (bent == null) { bent = new Material(shader); AssetDatabase.CreateAsset(bent, path); }
                            bent.shader = shader;
                            bent.CopyPropertiesFromMaterial(m);
                            WorldBend.Bendify(bent);
                            EditorUtility.SetDirty(bent);
                            made[m] = bent;
                        }
                        own[i] = bent; changed = true; template = bent;
                    }
                    if (changed) { r.sharedMaterials = own; renderers++; }
                }
            }
            Log.AppendLine($"garden: {renderers} renderers on {made.Count} bending materials");
            var ground = r6.transform.Find("Ground");
            if (ground != null) GardenGround(ground);

            // the bend, pivoted at the window
            var old = r6.transform.Find("WorldBend"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var window = r6.transform.Find("Window");
            var go = new GameObject("WorldBend"); go.transform.SetParent(r6.transform, false);
            go.transform.position = window != null ? window.position : r6.transform.position;
            var cw = go.AddComponent<CurvedWorldController>();
            cw.bendType = BendType.LittlePlanet_Y; cw.bendID = 1;
            cw.bendPivotPoint = go.transform;
            var wb = go.AddComponent<WorldBend>();
            Set(wb, "restPivot", go.transform);
            if (template != null) Set(wb, "template", template);
            Log.AppendLine($"WorldBend at {go.transform.position} (Little Planet Y, ID 1)");

            AssetDatabase.SaveAssets();
            // checked after saving: the bend type must have stuck on every one
            foreach (var bent in made.Values)
            {
                if (!bent.IsKeywordEnabled(WorldBend.TypeKeyword) || bent.shaderKeywords.Length != 3) { WorldBend.Bendify(bent); EditorUtility.SetDirty(bent); AssetDatabase.SaveAssetIfDirty(bent); }
                Log.AppendLine($"  {bent.name}: [{string.Join(",", bent.shaderKeywords)}] {bent.GetVector("_CurvedWorldBendSettings")}");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("D-0102 CURVED BEATS LANDED\n" + Log);
        }
    }
}
#endif
