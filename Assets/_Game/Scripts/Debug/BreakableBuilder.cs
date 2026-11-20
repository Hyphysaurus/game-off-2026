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
    /// "Build Breakables" (D-0101): every prop in Greybox's course whose name is on the list (blocks, cubes, cups, cans,
    /// matchboxes, the hallway's cover box) gets a Breakable and a Damageable sized to it, and each distinct mesh among
    /// them is cut once with Fraggo (as the car wrecks are, CarWreckBuilder) into pieces kept in
    /// _Imports/Generated/Breakables (made from the licensed pack: out of git) and listed in Resources/Breakables. Nothing
    /// anyone walks on is on the list. Idempotent; tuning on the asset is kept.
    /// Menu: Shift Jam / Breakables / Build Breakables. Batch: -executeMethod ShiftJam.BreakableBuilder.Build
    /// </summary>
    public static class BreakableBuilder
    {
        private const string Asset = "Assets/_Game/Resources/" + BreakSettings.ResourceName + ".asset";
        private const string Generated = "Assets/_Imports/Generated";
        private const string Folder = Generated + "/Breakables";
        private const string Scene = "Assets/_Game/Scenes/Greybox.unity";
        private const string DissolveMat = "Assets/ShaderGraph_Dissolve/URP/Materials/Shader Graphs_Dissolve_Dissolve_Metallic.mat";
        private const string InsideMat = "Assets/_Game/Data/BreakInside.mat";

        /// <summary>Name starts that make a prop breakable. Never the book stairs, the step box, a shelf or a mat.</summary>
        private static readonly string[] Kinds =
        {
            "Wooden_Cube_", "Wooden_Cylinder_", "Wooden_Pyramid_", "Wooden_HalfSphere_", "Wooden_Cone_", "Wooden_Prism_",
            "Color_Cube_", "Cube_Alphabet_", "Paper_Glass_", "Cup_", "Can_", "Matchbox_", "Lost_Wooden_Cube", "Lost_Wooden_Cylinder",
            "Lost_Color_Cube", "Lost_Matchbox", "Cover",
        };

        [MenuItem("Shift Jam/Breakables/Build Breakables")]
        public static void Build()
        {
            var fraggo = CarWreckBuilder.Fraggo.Find(out string why);
            if (fraggo == null) { Debug.LogError("BREAKABLES " + why + ": nothing changed"); return; }
            var active = SceneManager.GetActiveScene();
            var scene = active.path == Scene ? active : EditorSceneManager.OpenScene(Scene);
            var course = GameObject.Find("Room/Course");
            if (course == null) { Debug.LogError("BREAKABLES no Room/Course in " + Scene); return; }

            var s = AssetDatabase.LoadAssetAtPath<BreakSettings>(Asset);
            if (s == null) { s = ScriptableObject.CreateInstance<BreakSettings>(); AssetDatabase.CreateAsset(s, Asset); }
            if (s.dissolve == null) s.dissolve = AssetDatabase.LoadAssetAtPath<Material>(DissolveMat);
            if (s.inside == null) s.inside = InsideMaterial();
            if (s.breakSounds == null || s.breakSounds.Length == 0)
                s.breakSounds = AssetDatabase.FindAssets("Impact_Dull_ t:AudioClip", new[] { "Assets/_Imports/CasePortman" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).ToArray();

            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder("Assets/_Imports", "Generated");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Generated, "Breakables");

            // the props, and their meshes
            var props = new List<GameObject>();
            foreach (Transform t in course.GetComponentsInChildren<Transform>(true))
            {
                if (!Kinds.Any(k => t.name.StartsWith(k))) continue;
                if (t.parent != null && Kinds.Any(k => t.parent.name.StartsWith(k))) continue;   // a part of one already listed
                var mf = t.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (t.GetComponentsInChildren<MeshFilter>().Length > 1) continue;   // one mesh only: a cut is of one mesh
                props.Add(t.gameObject);
            }

            var cuts = new Dictionary<string, CarDamageFxSettings.Cut>();
            foreach (var c in s.cuts ?? new CarDamageFxSettings.Cut[0]) if (c != null && c.pieces != null && c.pieces.Length > 0 && c.pieces[0] != null) cuts[c.mesh] = c;
            int made = 0, failed = 0;
            foreach (var go in props)
            {
                Mesh mesh = go.GetComponentInChildren<MeshFilter>().sharedMesh;
                if (cuts.ContainsKey(mesh.name)) continue;
                Vector3 size = mesh.bounds.size;
                int cells = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(size.x, size.y, size.z) * 1.3f), 4, 9);
                var cut = CarWreckBuilder.CutBody(fraggo, mesh, cells, out float area);
                if (cut == null) { Debug.LogWarning($"BREAKABLES Fraggo made no pieces of {mesh.name}: it will dissolve whole"); cuts[mesh.name] = null; failed++; continue; }
                string file = mesh.name; foreach (char ch in System.IO.Path.GetInvalidFileNameChars()) file = file.Replace(ch, '_');
                string path = $"{Folder}/{file}.asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(cut.pieces[0], path);
                for (int i = 1; i < cut.pieces.Length; i++) AssetDatabase.AddObjectToAsset(cut.pieces[i], path);
                cuts[mesh.name] = cut; made++;
            }
            s.cuts = cuts.Values.Where(c => c != null).ToArray();
            EditorUtility.SetDirty(s);

            // every prop: a Damageable sized to it (one dart for the small, four for the big) and a Breakable
            int marked = 0;
            foreach (var go in props)
            {
                var r = go.GetComponentInChildren<Renderer>();
                float across = r != null ? Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z) : 2f;
                float health = across < 3f ? 15f : across < 6f ? 30f : 60f;
                var hp = go.GetComponent<Damageable>(); if (hp == null) hp = go.AddComponent<Damageable>();
                var so = new SerializedObject(hp);
                so.FindProperty("maxHealth").floatValue = health;
                var knock = so.FindProperty("takeKnockback"); if (knock != null) knock.boolValue = true;
                var nums = so.FindProperty("showNumbers"); if (nums != null) nums.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (go.GetComponent<Breakable>() == null) go.AddComponent<Breakable>();
                marked++;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"D-0101 BREAKABLES: {marked} props breakable, {made} meshes cut now ({s.cuts.Length} in all), {failed} dissolve whole");
        }

        private static Material InsideMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(InsideMat);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BreakInside" };
            m.SetColor("_BaseColor", new Color(0.86f, 0.74f, 0.55f));   // raw wood, plain card
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(m, InsideMat);
            return m;
        }
    }
}
#endif
