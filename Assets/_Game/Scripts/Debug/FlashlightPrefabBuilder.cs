#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// One flashlight part (D-0146, systems plan step 4): Prefabs/Tools/Flashlight.prefab, a variant of the Playroom pack's
    /// Flashlight_01 carrying our parts (Carryable, Flashlight, Highlight, the Beam light), made from the bedroom's toy-box
    /// flashlight with its scene-only values reset (off at start, no core). Each flashlight in the open scenes that is still
    /// a bare pack copy is switched over to it in place, keeping its own values (the toy box's starts on; the wardrobe's
    /// keeps its push switch) as overrides. A new flashlight anywhere: drag the prefab in (set Made At Toy and scale it to a
    /// toy's for a toy-only place). Safe to run again. Menu: Shift Jam / Tools / Make Flashlight Prefab (D-0146).
    /// </summary>
    public static class FlashlightPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Game/Prefabs/Tools/Flashlight.prefab";
        private const string PackPath = "Assets/_Imports/ithappy/Playroom/Prefabs/Other/Flashlight_01.prefab";

        [MenuItem("Shift Jam/Tools/Make Flashlight Prefab (D-0146)")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Flashlight prefab: stop Play first."); return; }
            var log = new System.Text.StringBuilder("FLASHLIGHT PREFAB (D-0146)\n");
            Flashlight[] lights = Object.FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (asset == null)
            {
                Flashlight source = lights.FirstOrDefault(f => f.name == "Flashlight_01") ?? lights.FirstOrDefault();
                if (source == null) { Debug.LogError("Flashlight prefab: no Flashlight in the open scene to make it from (open Greybox)."); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                asset = PrefabUtility.SaveAsPrefabAsset(source.gameObject, PrefabPath, out bool saved);
                if (!saved || asset == null) { Debug.LogError("Flashlight prefab: could not save " + PrefabPath); return; }
                // The scene's own values out of the part: where it stood, on at start, a core inside.
                GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
                root.name = "Flashlight";
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                var so = new SerializedObject(root.GetComponent<Flashlight>());
                so.FindProperty("startOn").boolValue = false;
                so.FindProperty("core").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                log.AppendLine($"made {PrefabPath} ({PrefabUtility.GetPrefabAssetType(asset)}) from {source.name}: {Describe(asset)}");
            }
            else log.AppendLine($"{PrefabPath} already there");

            var settings = new PrefabReplacingSettings
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                changeRootNameToAssetName = false,
                logInfo = false,
            };
            var touched = new System.Collections.Generic.HashSet<Scene>();
            // Each flashlight once (a doubled one has two Flashlight scripts), by its object, gathered before anything is deleted.
            var torches = lights.Where(f => f != null).Select(f => f.gameObject).Distinct().ToList();
            foreach (GameObject go in torches)
            {
                if (go == null) continue;
                string now = PrefabUtility.IsPartOfPrefabInstance(go) ? AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)) : "";
                if (now == PrefabPath)
                {
                    int merged = MergeTwins(go);
                    log.AppendLine($"{go.name}: already the Flashlight prefab{(merged > 0 ? $"; {merged} doubled part(s) merged into the prefab's own" : "")}; parts {Describe(go)}");
                    if (merged > 0) touched.Add(go.scene);
                    continue;
                }
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go) || now != PackPath) { log.AppendLine($"{go.name}: not a pack flashlight copy ({now}), left alone"); continue; }
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go, asset, settings, InteractionMode.AutomatedAction);
                int twins = MergeTwins(go);   // its hand-added parts now doubled by the prefab's own: merged
                bool ok = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)) == PrefabPath;
                var flash = go.GetComponent<Flashlight>();
                if (twins > 0) log.AppendLine($"{go.name}: {twins} doubled part(s) merged into the prefab's own");
                log.AppendLine($"{go.name} ({go.scene.name}): {(ok ? "now the Flashlight prefab" : "FAILED")}; parts {Describe(go)}; beam {(flash.Beam != null && flash.Beam.transform.IsChildOf(go.transform) ? "its own" : "MISSING")}; extras {string.Join(", ", PrefabUtility.GetAddedGameObjects(go).Select(a => a.instanceGameObject.name))}");
                touched.Add(go.scene);
            }
            foreach (Scene s in touched) { EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s); }
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// After a switch-over, the parts that were added by hand to the pack copy (Carryable, Flashlight, Highlight, the
        /// Beam child) sit beside the prefab's own copies of them: Unity never pairs an added part with the prefab's. Each
        /// added one's values go onto the prefab's twin (as this flashlight's own overrides), everything in the scene that
        /// pointed at it points at the twin, and the added one goes. Returns how many were merged.
        /// </summary>
        private static int MergeTwins(GameObject go)
        {
            var map = new System.Collections.Generic.Dictionary<Object, Object>();
            var doomed = new System.Collections.Generic.List<Object>();
            // Added children with a twin of the same name from the prefab (the Beam).
            foreach (var added in PrefabUtility.GetAddedGameObjects(go))
            {
                GameObject extra = added.instanceGameObject;
                Transform parent = extra.transform.parent;
                Transform twin = null;
                foreach (Transform t in parent) if (t != extra.transform && t.name == extra.name && !PrefabUtility.IsAddedGameObjectOverride(t.gameObject)) { twin = t; break; }
                if (twin == null) continue;   // a real extra (the wardrobe's push switch): it stays
                map[extra] = twin.gameObject;
                twin.SetLocalPositionAndRotation(extra.transform.localPosition, extra.transform.localRotation);
                twin.localScale = extra.transform.localScale;
                map[extra.transform] = twin;
                foreach (Component c in extra.GetComponents<Component>())
                {
                    if (c is Transform) continue;
                    Component t2 = twin.GetComponent(c.GetType());
                    if (t2 == null) continue;
                    EditorUtility.CopySerialized(c, t2);
                    map[c] = t2;
                }
                doomed.Add(extra);
            }
            // Added components with a twin of the same kind from the prefab, on the root.
            foreach (var added in PrefabUtility.GetAddedComponents(go))
            {
                Component c = added.instanceComponent;
                if (c == null || c.gameObject != go) continue;
                Component twin = go.GetComponents(c.GetType()).FirstOrDefault(x => x != c && !PrefabUtility.IsAddedComponentOverride(x));
                if (twin == null) continue;
                EditorUtility.CopySerialized(c, twin);
                map[c] = twin;
                doomed.Add(c);
            }
            if (map.Count == 0) return 0;
            // Everything in the scene that pointed at an added part now points at its twin (the copied values too: a
            // Flashlight's beam pointed at the added Beam).
            foreach (GameObject root in go.scene.GetRootGameObjects())
                foreach (Component comp in root.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null || map.ContainsKey(comp) || map.ContainsKey(comp.gameObject)) continue;   // the doomed parts themselves
                    var so = new SerializedObject(comp);
                    var it = so.GetIterator();
                    bool changed = false;
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.propertyPath != "m_GameObject" && it.propertyPath != "m_Father" && !it.propertyPath.StartsWith("m_Children") && it.objectReferenceValue != null && map.TryGetValue(it.objectReferenceValue, out Object to))
                        { it.objectReferenceValue = to; changed = true; }
                    if (changed) so.ApplyModifiedPropertiesWithoutUndo();
                }
            int merged = 0;
            foreach (Object o in doomed) { if (o is GameObject g) Object.DestroyImmediate(g); else Object.DestroyImmediate(o); merged++; }
            return merged;
        }

        private static string Describe(GameObject go) =>
            string.Join(", ", go.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
    }
}
#endif
