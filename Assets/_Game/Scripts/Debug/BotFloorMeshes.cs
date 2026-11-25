#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Keeps the bots' floor whole in a BUILT game (D-0192). BotGround maps the floor from a scene's colliders as the
    /// scene starts. A mesh collider's mesh can be read in a built game only if its model was imported with Read/Write
    /// on; in the editor every mesh can be read, so Play there works either way and Unity only says "will work in
    /// playmode in the editor but not in player" (42 of those in his match of 24 Nov). In a build those pieces would be
    /// holes in the bots' floor.
    /// This turns Read/Write on for the model behind every solid mesh collider in each scene of the build that has a
    /// BotGround. It runs by itself before every build, and from the menu Shift Jam > Bots > Make Floor Meshes Readable.
    /// The setting is kept in the models' .meta files. The bought packs are not in git: a fresh import of a pack loses
    /// it, and the next build (or a run of the menu) puts it back.
    /// </summary>
    public sealed class BotFloorMeshes : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => MakeReadable();

        [MenuItem("Shift Jam/Bots/Make Floor Meshes Readable (D-0192)")]
        public static void MakeReadableMenu() => MakeReadable();

        /// <summary>Does it now. Returns how many models were changed (0 when every floor mesh could be read already).</summary>
        public static int MakeReadable()
        {
            string groundScript = GroundScriptPath();
            if (groundScript == null) { Debug.LogError("[Bots] Floor meshes: the BotGround script was not found."); return 0; }
            var models = new SortedSet<string>();
            var notModels = new SortedSet<string>();
            int scenes = 0, colliders = 0;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || string.IsNullOrEmpty(entry.path)) continue;
                if (System.Array.IndexOf(AssetDatabase.GetDependencies(entry.path, true), groundScript) < 0) continue;   // no BotGround in it, not even inside a prefab
                scenes++;
                Scene open = SceneManager.GetSceneByPath(entry.path);
                if (open.IsValid() && open.isLoaded) colliders += Collect(open, models, notModels);
                else
                {
                    Scene preview = EditorSceneManager.OpenPreviewScene(entry.path);   // read without showing it or touching what is open
                    try { colliders += Collect(preview, models, notModels); }
                    finally { EditorSceneManager.ClosePreviewScene(preview); }
                }
            }
            int changed = 0;
            if (models.Count > 0)
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string path in models)
                    {
                        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (importer == null || importer.isReadable) continue;
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                        changed++;
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
            }
            string told = "[Bots] Floor meshes: " + scenes + " scene(s) with a BotGround, " + colliders + " solid mesh collider(s) looked at, " + changed + " model(s) set to Read/Write"
                + (changed > 0 ? ": " + string.Join(", ", models) : " (every one could be read already)") + ".";
            Debug.Log(told);
            if (notModels.Count > 0)
                Debug.LogError("[Bots] Floor meshes: " + notModels.Count + " mesh(es) cannot be read and are not from a model file, so a built game leaves them out of the bots' floor: " + string.Join(", ", notModels) + ".");
            return changed;
        }

        /// <summary>The models behind the scene's solid mesh colliders whose meshes cannot be read. Returns how many
        /// mesh colliders were looked at.</summary>
        private static int Collect(Scene scene, SortedSet<string> models, SortedSet<string> notModels)
        {
            int seen = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshCollider collider in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    Mesh mesh = collider.sharedMesh;
                    if (collider.isTrigger || mesh == null) continue;
                    seen++;
                    if (mesh.isReadable) continue;
                    string path = AssetDatabase.GetAssetPath(mesh);
                    if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter) models.Add(path);
                    else notModels.Add(mesh.name + (string.IsNullOrEmpty(path) ? " (in " + scene.name + ")" : " (" + path + ")"));
                }
            return seen;
        }

        /// <summary>Where the BotGround script is, found by its class (not by a typed path).</summary>
        private static string GroundScriptPath()
        {
            foreach (string guid in AssetDatabase.FindAssets("BotGround t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(BotGround)) return path;
            }
            return null;
        }
    }
}
#endif
