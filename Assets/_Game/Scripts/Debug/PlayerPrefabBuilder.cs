#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// One player for every scene (D-0143, his "stop hardcoding... make each system modular so it fits into any section").
    /// Until now each scene had its own hand-edited Player (Greybox, Workshop, Raceway, Colour Corner) and the online
    /// NetPlayer was a copy of Greybox's: a new part added to one never reached the others.
    ///
    /// This makes Prefabs/Player/Player.prefab from Greybox's player (plus SafeGround, which the house lacked: it is what
    /// lets you out of a car on safe ground) and turns each scene's Player INTO an instance of it, in place. The object
    /// stays the same object, so the camera, HUD and everything else that points at it keeps pointing at it; whatever a
    /// scene sets differently (its start, its own extras such as the Colour Corner's paint belt and loadout) stays as that
    /// scene's override. The NetPlayer is rebuilt as a variant of the same prefab (PartyBuilder).
    ///
    /// From now on a part every player needs goes on Player.prefab once. Safe to run again: a scene already linked is
    /// left alone. Menu: Shift Jam / Player / Make One Player Prefab (D-0143). Batch (Unity closed):
    /// -executeMethod ShiftJam.PlayerPrefabBuilder.Batch
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Game/Prefabs/Player/Player.prefab";
        private const string SourceScene = "Assets/_Game/Scenes/Greybox.unity";
        private static readonly string[] Scenes =
        {
            "Assets/_Game/Scenes/Greybox.unity",
            "Assets/_Game/Scenes/Workshop.unity",
            "Assets/_Game/Scenes/Raceway.unity",
            "Assets/_Game/Scenes/ColourCorner.unity",
        };

        [MenuItem("Shift Jam/Player/Make One Player Prefab (D-0143)")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Player prefab: stop Play first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string back = SceneManager.GetActiveScene().path;
            bool ok = Run(out string report);
            Debug.Log(report);
            if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
            if (!ok) Debug.LogError("Player prefab: see the report above.");
        }

        public static void Batch()
        {
            bool ok = Run(out string report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(out string report)
        {
            var log = new System.Text.StringBuilder("PLAYER PREFAB (D-0143)\n");
            bool ok = true;

            // 1. The prefab, from Greybox's player (only made once: after that it is the one to edit).
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (asset == null)
            {
                EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
                GameObject source = FindPlayer(SceneManager.GetActiveScene());
                if (source == null) { report = log.Append("NO Player in ").Append(SourceScene).ToString(); return false; }
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                GameObject copy = Object.Instantiate(source);
                copy.name = "Player";
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (!copy.TryGetComponent(out SafeGround _)) copy.AddComponent<SafeGround>();   // the house's player lacked it
                asset = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath, out bool saved);
                Object.DestroyImmediate(copy);
                if (!saved || asset == null) { report = log.Append("could not save ").Append(PrefabPath).ToString(); return false; }
                log.AppendLine($"made {PrefabPath} from Greybox's Player: {Describe(asset)}");
            }
            else log.AppendLine($"{PrefabPath} already there: {Describe(asset)}");

            // 2. Each scene's Player becomes an instance of it, in place.
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                GameObject player = FindPlayer(scene);
                if (player == null) { log.AppendLine($"{scene.name}: NO Player"); ok = false; continue; }
                if (PrefabUtility.IsPartOfPrefabInstance(player))
                {
                    var linked = PrefabUtility.GetCorrespondingObjectFromSource(player);
                    bool ours = linked != null && AssetDatabase.GetAssetPath(linked) == PrefabPath;
                    log.AppendLine($"{scene.name}: already {(ours ? "an instance of the Player prefab" : "an instance of " + AssetDatabase.GetAssetPath(linked) + " (left alone)")}");
                    if (!ours) ok = false;
                    continue;
                }
                // A part the prefab has and this player lacks is added first, so the two match (it would otherwise count as
                // removed here, and the scene would still miss it).
                var added = new List<string>();
                foreach (Component c in asset.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    System.Type t = c.GetType();
                    if (player.GetComponents(t).Length < asset.GetComponents(t).Length) { player.AddComponent(t); added.Add(t.Name); }
                }
                var settings = new ConvertToPrefabInstanceSettings
                {
                    objectMatchMode = ObjectMatchMode.ByHierarchy,
                    componentsNotMatchedBecomesOverride = true,     // a scene's own extras stay, as its overrides
                    gameObjectsNotMatchedBecomesOverride = true,
                    recordPropertyOverridesOfMatches = true,        // a scene's own values stay, as its overrides
                    changeRootNameToAssetName = false,
                    logInfo = false,
                };
                PrefabUtility.ConvertToPrefabInstance(player, asset, settings, InteractionMode.AutomatedAction);
                if (!PrefabUtility.IsPartOfPrefabInstance(player)) { log.AppendLine($"{scene.name}: conversion FAILED"); ok = false; continue; }
                var extra = PrefabUtility.GetAddedComponents(player).Select(a => a.instanceComponent.GetType().Name).ToList();
                var gone = PrefabUtility.GetRemovedComponents(player).Select(r => r.assetComponent.GetType().Name).ToList();
                int values = PrefabUtility.GetObjectOverrides(player).Count;
                log.AppendLine($"{scene.name}: linked to the Player prefab{(added.Count > 0 ? "; gained " + string.Join(", ", added) : "")}"
                    + $"; its own extras: {(extra.Count > 0 ? string.Join(", ", extra) : "none")}"
                    + $"{(gone.Count > 0 ? "; WITHOUT " + string.Join(", ", gone) : "")}; {values} objects with their own values");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            // 3. The online player: a variant of the same prefab.
            if (!PartyBuilder.SaveNetPlayerVariant(asset, log)) ok = false;

            report = log.ToString();
            return ok;
        }

        /// <summary>The scene's player: the root object named Player with a PlayerMotor.</summary>
        private static GameObject FindPlayer(Scene scene) =>
            scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player" && g.GetComponent<PlayerMotor>() != null);

        private static string Describe(GameObject go) =>
            string.Join(", ", go.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
    }
}
#endif
