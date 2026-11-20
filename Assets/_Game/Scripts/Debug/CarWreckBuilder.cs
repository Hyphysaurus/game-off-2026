#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// "Build Car Wrecks" (D-0060): cuts every car piece's body into pieces with Fraggo Fracture Engine's cutter and writes
    /// them into Resources/CarDamageFx (CarDamageFxSettings.cuts), where CarWreck finds them by the body mesh's name when a
    /// car is wrecked. The piece meshes go to _Imports/Generated/CarWrecks: they are made from the licensed car packs, so
    /// they stay out of git like the packs themselves. It also makes the cut faces' material (Data/WreckInside).
    /// Fraggo is a licensed tool that is not in git either, so it is called by name: without it in the project this says
    /// so and changes nothing. The cuts are repeatable (seeded by the body's name). Tuning on the asset is kept.
    /// Batch: -executeMethod ShiftJam.CarWreckBuilder.Build
    /// </summary>
    public static class CarWreckBuilder
    {
        private const string Asset = "Assets/_Game/Resources/" + CarDamageFxSettings.ResourceName + ".asset";
        private const string Vehicles = "Assets/_Game/Prefabs/Pieces/Vehicles";
        private const string Generated = "Assets/_Imports/Generated";
        private const string Folder = Generated + "/CarWrecks";
        private const string InsideMat = "Assets/_Game/Data/WreckInside.mat";
        private const int Tries = 6;                 // seeds tried per body; the cut with the most pieces is kept
        internal const float SliverShare = 0.04f;     // a piece smaller than this share of the body's length is dropped
        private const float AreaSlack = 0.05f;       // the pieces' outer faces must add up to the body's, this near

        [MenuItem("Shift Jam/Workshop/Build Car Wrecks")]
        public static void Build()
        {
            var fx = AssetDatabase.LoadAssetAtPath<CarDamageFxSettings>(Asset);
            if (fx == null) { Debug.LogError($"WRECKS no {Asset}: run Shift Jam > Workshop > Build Car Damage FX first"); return; }
            Fraggo fraggo = Fraggo.Find(out string why);
            if (fraggo == null) { Debug.LogError("WRECKS " + why + ": nothing changed"); return; }

            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder("Assets/_Imports", "Generated");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Generated, "CarWrecks");
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { Folder }))   // last run's pieces
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));

            var cuts = new List<CarDamageFxSettings.Cut>();
            var seen = new HashSet<string>();
            int least = int.MaxValue, most = 0, faults = 0;
            var kept = UnityEngine.Random.state;
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Vehicles }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string piece = System.IO.Path.GetFileNameWithoutExtension(path);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        if (root.GetComponent<ArcadeCar>() == null) continue;
                        MeshFilter body = CarDamageFx.BodyOf(root.transform);
                        if (body == null || body.sharedMesh == null) { Debug.LogWarning($"WRECKS {piece}: no body mesh"); faults++; continue; }
                        Mesh mesh = body.sharedMesh;
                        if (!seen.Add(mesh.name)) { Debug.Log($"WRECKS {piece}: body {mesh.name} already cut"); continue; }

                        CarDamageFxSettings.Cut cut = CutBody(fraggo, mesh, fx.cutCells, out float areaShare);
                        if (cut == null) { Debug.LogWarning($"WRECKS {piece}: Fraggo made no pieces of {mesh.name}"); faults++; continue; }
                        Save(cut);
                        cuts.Add(cut);
                        least = Mathf.Min(least, cut.pieces.Length); most = Mathf.Max(most, cut.pieces.Length);
                        bool whole = Mathf.Abs(areaShare - 1f) <= AreaSlack;
                        if (!whole) faults++;
                        Others(root.transform, body, fx.partNames, out int parts, out int rest);
                        string line = $"WRECKS {piece}: body {mesh.name} ({mesh.triangles.Length / 3} tris, {mesh.subMeshCount} submesh) -> " +
                                      $"{cut.pieces.Length} pieces, outer faces {areaShare:P1} of the body's; besides the body: {parts} loose parts, {rest} other drawn meshes";
                        if (whole) Debug.Log(line); else Debug.LogWarning(line + "  <-- PIECES DO NOT ADD UP TO THE BODY");
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            finally { UnityEngine.Random.state = kept; }

            fx.cuts = cuts;
            fx.wreckInside = MakeInsideMaterial();
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            Debug.Log($"WRECKS built: {cuts.Count} bodies cut ({(cuts.Count > 0 ? least : 0)} to {most} pieces each) into {Folder}, " +
                      $"inside material {(fx.wreckInside != null ? fx.wreckInside.shader.name : "none")}, faults {faults}");
        }

        // ---------- cutting ----------

        /// <summary>Cuts <paramref name="body"/> into about <paramref name="cells"/> pieces: the seed giving the most
        /// pieces out of a few. <paramref name="areaShare"/>: the pieces' outer faces over the body's own (1 = nothing lost).</summary>
        internal static CarDamageFxSettings.Cut CutBody(Fraggo fraggo, Mesh body, int cells, out float areaShare)
        {
            areaShare = 0f;
            int subMeshes = body.subMeshCount;
            float sliver = Mathf.Max(body.bounds.size.x, body.bounds.size.y, body.bounds.size.z) * SliverShare;
            List<Mesh> best = null; List<Vector3> bestCentres = null;
            for (int attempt = 0; attempt < Tries; attempt++)
            {
                UnityEngine.Random.InitState(Seed(body.name) + attempt * 7919);
                var meshes = new List<Mesh>(); var centres = new List<Vector3>();
                foreach (List<Plane> cell in fraggo.Cells(body, cells))
                {
                    Mesh shard = fraggo.Shard(body, subMeshes, subMeshes + 1, cell, out Vector3 centre);
                    if (shard == null) continue;
                    Vector3 size = shard.bounds.size;
                    if (Mathf.Max(size.x, size.y, size.z) < sliver) { UnityEngine.Object.DestroyImmediate(shard); continue; }
                    meshes.Add(shard); centres.Add(centre);
                }
                if (best == null || meshes.Count > best.Count)
                {
                    if (best != null) foreach (var m in best) UnityEngine.Object.DestroyImmediate(m);
                    best = meshes; bestCentres = centres;
                }
                else foreach (var m in meshes) UnityEngine.Object.DestroyImmediate(m);
                if (best.Count >= cells) break;   // every cell made a piece
            }
            if (best == null || best.Count < 2) return null;

            float bodyArea = Area(body, subMeshes), pieceArea = 0f;
            for (int i = 0; i < best.Count; i++)
            {
                best[i].name = $"{body.name} piece {i + 1:00}";
                pieceArea += Area(best[i], subMeshes);
                MeshUtility.Optimize(best[i]);
                best[i].UploadMeshData(false);
            }
            areaShare = bodyArea > 1e-9f ? pieceArea / bodyArea : 0f;
            return new CarDamageFxSettings.Cut { mesh = body.name, subMeshes = subMeshes, pieces = best.ToArray(), centres = bestCentres.ToArray() };
        }

        /// <summary>The area of <paramref name="mesh"/>'s first <paramref name="subMeshes"/> submeshes (a piece's cut
        /// faces are the submesh after those, so they are left out).</summary>
        private static float Area(Mesh mesh, int subMeshes)
        {
            Vector3[] v = mesh.vertices;
            float area = 0f;
            for (int sub = 0; sub < Mathf.Min(subMeshes, mesh.subMeshCount); sub++)
            {
                int[] t = mesh.GetTriangles(sub);
                for (int i = 0; i + 2 < t.Length; i += 3)
                    area += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude * 0.5f;
            }
            return area;
        }

        /// <summary>The same number for the same name on every run and every machine (string.GetHashCode is not).</summary>
        private static int Seed(string name)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in name) { h ^= c; h *= 16777619; }
                return (int)(h & 0x7fffffff);
            }
        }

        private static void Save(CarDamageFxSettings.Cut cut)
        {
            string file = cut.mesh;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) file = file.Replace(c, '_');
            string path = $"{Folder}/{file}.asset";
            AssetDatabase.CreateAsset(cut.pieces[0], path);
            for (int i = 1; i < cut.pieces.Length; i++) AssetDatabase.AddObjectToAsset(cut.pieces[i], path);
        }

        /// <summary>What else the car piece draws besides its body and wheels: the parts CarDamageFx can knock off by
        /// name, and the rest (all of it is hidden when the body breaks up).</summary>
        private static void Others(Transform car, MeshFilter body, string[] partNames, out int parts, out int rest)
        {
            parts = 0; rest = 0;
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.transform == body.transform) continue;
                bool wheel = false, part = false;
                for (Transform t = r.transform; t != null && t != car; t = t.parent)
                {
                    if (t.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0) wheel = true;
                    if (partNames != null)
                        foreach (string n in partNames)
                            if (!string.IsNullOrEmpty(n) && t.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) part = true;
                }
                if (wheel) continue;
                if (part) parts++; else rest++;
            }
        }

        /// <summary>The cut faces: plain, matt plastic. CarWreck tints it per car to a darker shade of the car's paint.</summary>
        private static Material MakeInsideMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogWarning("WRECKS no URP Lit shader: the cut faces use the body's own material"); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(InsideMat);
            if (mat == null)
            {
                mat = new Material(shader) { name = "WreckInside" };
                AssetDatabase.CreateAsset(mat, InsideMat);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.25f);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Fraggo, called by name ----------

        /// <summary>Fraggo Fracture Engine's mesh cutter (UnluckSoftware.FraggoMeshFracturerUtils, an editor class of a
        /// licensed tool kept out of git), found at run time so this project compiles without it.</summary>
        internal sealed class Fraggo
        {
            private const string Cutter = "UnluckSoftware.FraggoMeshFracturerUtils";
            private MethodInfo _cells, _shard, _valid;

            public static Fraggo Find(out string why)
            {
                why = null;
                Type type = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = a.GetType(Cutter, false);
                    if (type != null) break;
                }
                if (type == null) { why = "Fraggo Fracture Engine is not in the project (import it from the Asset Store: Assets/Unluck Software)"; return null; }
                const BindingFlags open = BindingFlags.Public | BindingFlags.Static;
                var found = new Fraggo
                {
                    _cells = type.GetMethod("GenerateVoronoiPlaneSets", open, null, new[] { typeof(Mesh), typeof(int) }, null),
                    _shard = type.GetMethod("BuildShardMesh", open, null,
                        new[] { typeof(Mesh), typeof(int), typeof(int), typeof(List<Plane>), typeof(Vector3).MakeByRefType() }, null),
                    _valid = type.GetMethod("IsValidMesh", open, null, new[] { typeof(Mesh) }, null),
                };
                if (found._cells == null || found._shard == null || found._valid == null)
                {
                    why = "this Fraggo Fracture Engine has no GenerateVoronoiPlaneSets / BuildShardMesh / IsValidMesh as CarWreckBuilder knows them (a newer version?)";
                    return null;
                }
                return found;
            }

            /// <summary>The cutting planes of each of <paramref name="count"/> cells scattered through the mesh's box.</summary>
            public List<List<Plane>> Cells(Mesh mesh, int count) =>
                (List<List<Plane>>)_cells.Invoke(null, new object[] { mesh, count }) ?? new List<List<Plane>>();

            /// <summary>The part of <paramref name="mesh"/> inside one cell, its cut faces in submesh <paramref name="cap"/>,
            /// moved so its own middle is at zero (<paramref name="centre"/> says where that was). Null when the cell
            /// misses the mesh.</summary>
            public Mesh Shard(Mesh mesh, int cap, int total, List<Plane> cell, out Vector3 centre)
            {
                var args = new object[] { mesh, cap, total, cell, Vector3.zero };
                var made = (Mesh)_shard.Invoke(null, args);
                centre = (Vector3)args[4];
                if (made != null && (bool)_valid.Invoke(null, new object[] { made })) return made;
                if (made != null) UnityEngine.Object.DestroyImmediate(made);
                return null;
            }
        }
    }
}
#endif
