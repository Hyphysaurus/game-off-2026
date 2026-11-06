#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Workshop / Bake Layout Into Scene: turns a layout saved in Build Mode into real objects in the open
    /// scene, so a course built in game becomes a course in the project. Catalogue items become prefab instances (shapes
    /// become BuildShape objects) under "Baked_&lt;name&gt;"; moved or removed scene props are moved or switched off in place;
    /// copies of scene props are copied. A layout saved on top of its own Baked_ group (a baked course edited in Play)
    /// is baked into that group: its edits and new pieces go in, the untouched pieces stay. A Baked_ group the layout was
    /// not saved on is never removed without asking (add into / replace / cancel). One undo step.
    /// Everything baked keeps a Buildable (as a scene prop), so Build Mode can still edit it.
    /// </summary>
    public static class LayoutBaker
    {
        private const string CatalogPath = "Assets/_Game/Resources/BuildCatalog.asset";

        [MenuItem("Shift Jam/Workshop/Bake Layout Into Scene")]
        public static void BakeFromMenu()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Bake Layout", "Stop Play first: baking edits the scene file.", "OK"); return; }
            Directory.CreateDirectory(BuildLayouts.Folder);
            string path = EditorUtility.OpenFilePanel("Bake which layout?", BuildLayouts.Folder, "json");
            if (string.IsNullOrEmpty(path)) return;
            if (!BuildLayouts.TryRead(path, out var file)) { EditorUtility.DisplayDialog("Bake Layout", "That file is not a layout.", "OK"); return; }
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(file.scene) && file.scene != scene.name &&
                !EditorUtility.DisplayDialog("Bake Layout", $"This layout was made in '{file.scene}', the open scene is '{scene.name}'. Bake anyway?", "Bake", "Cancel")) return;
            Bake(file, Path.GetFileNameWithoutExtension(path), scene);
        }

        public static void Bake(LayoutFile file, string name, Scene scene)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(CatalogPath);
            string rootName = LayoutFile.BakedPrefix + name;
            GameObject existing = null;
            foreach (var go in scene.GetRootGameObjects()) if (go.name == rootName) { existing = go; break; }

            // Saved on top of this group (it was in the scene then, or the file edits pieces inside it): the file only lists
            // what changed, so bake into the group. Anything else never removes a group without asking.
            bool into = false;
            if (existing != null)
            {
                if (SavedOnTop(file, rootName)) into = true;
                else
                {
                    int pick = EditorUtility.DisplayDialogComplex("Bake Layout",
                        $"The scene already has {rootName}, and this layout was not saved on top of it.\n\n" +
                        "Add into: keep it and add this layout's pieces to it.\nReplace: delete it and bake this layout fresh.",
                        "Add into", "Cancel", "Replace");
                    if (pick == 1) return;
                    into = pick == 0;
                }
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Bake layout " + name);
            int group = Undo.GetCurrentGroup();

            // Find every scene prop the file names before anything new is added (paths count siblings by name).
            var targets = new Dictionary<string, Transform>();
            foreach (var item in file.items)
            {
                string key = !string.IsNullOrEmpty(item.sceneKey) ? item.sceneKey : item.cloneOf;
                if (!string.IsNullOrEmpty(key) && !targets.ContainsKey(key)) targets[key] = ScenePath.Find(scene, key);
            }

            Transform root;
            if (into) root = existing.transform;
            else
            {
                foreach (var go in scene.GetRootGameObjects()) if (go.name == rootName) Undo.DestroyObjectImmediate(go);
                var fresh = new GameObject(rootName);
                SceneManager.MoveGameObjectToScene(fresh, scene);
                Undo.RegisterCreatedObjectUndo(fresh, "Bake layout");
                root = fresh.transform;
            }

            int made = 0, moved = 0, missing = 0;
            foreach (var item in file.items)
            {
                if (!string.IsNullOrEmpty(item.sceneKey))
                {
                    if (!targets.TryGetValue(item.sceneKey, out var t) || t == null) { missing++; Debug.LogWarning("Bake: scene prop not found: " + item.sceneKey); continue; }
                    Undo.RecordObject(t, "Bake layout");
                    Undo.RecordObject(t.gameObject, "Bake layout");
                    t.SetPositionAndRotation(item.position, item.rotation);
                    t.localScale = item.scale;
                    t.gameObject.SetActive(!item.removed);
                    moved++;
                    continue;
                }

                GameObject go = null;
                if (!string.IsNullOrEmpty(item.cloneOf))
                {
                    if (targets.TryGetValue(item.cloneOf, out var source) && source != null) go = Copy(source.gameObject, root);
                }
                else
                {
                    var entry = catalog != null ? catalog.Find(item.id) : null;
                    // an entry whose prefab was deleted or moved (Build Catalog not re-run) counts as missing
                    if (entry != null && (entry.IsShape || entry.prefab != null))
                    {
                        go = entry.IsShape ? Shape(entry, catalog, root) : (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, root);
                        if (go != null) go.name = entry.displayName;
                    }
                }
                if (go == null) { missing++; Debug.LogWarning($"Bake: could not make '{item.id ?? item.cloneOf}'."); continue; }

                Undo.RegisterCreatedObjectUndo(go, "Bake layout");
                go.transform.SetPositionAndRotation(item.position, item.rotation);
                go.transform.localScale = item.scale;
                var shape = item.hasShape ? go.GetComponent<BuildShape>() : null;
                if (shape != null) shape.Set(shape.Kind, item.shapeSize, item.shapeSteps, item.shapeColor);
                if (go.GetComponent<Buildable>() == null) go.AddComponent<Buildable>();
                // a copy of a prop the file removed is made from the switched-off original, so it starts off too
                if (go.activeSelf == item.removed) go.SetActive(!item.removed);
                made++;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"Baked layout '{name}': {made} made {(into ? "into the existing" : "under")} {rootName}, {moved} scene props moved/removed, {missing} missing. Save the scene to keep it.");
        }

        /// <summary>True when the file was saved with <paramref name="rootName"/> in the scene (version 2 files record it),
        /// or edits or copies something inside that group (older files).</summary>
        private static bool SavedOnTop(LayoutFile file, string rootName)
        {
            if (file.bakedRoots != null && file.bakedRoots.Contains(rootName)) return true;
            string inside = rootName + "/";
            foreach (var item in file.items)
            {
                if (!string.IsNullOrEmpty(item.sceneKey) && item.sceneKey.StartsWith(inside, System.StringComparison.Ordinal)) return true;
                if (!string.IsNullOrEmpty(item.cloneOf) && item.cloneOf.StartsWith(inside, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static GameObject Copy(GameObject source, Transform parent)
        {
            // A plain copy, like Build Mode's own: it keeps every component and value the prop has in the scene.
            var go = Object.Instantiate(source, parent);
            go.name = source.name + " (copy)";
            return go;
        }

        private static GameObject Shape(BuildCatalog.Entry entry, BuildCatalog catalog, Transform parent)
        {
            var go = new GameObject(entry.displayName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = catalog.ShapeMaterial;
            go.AddComponent<MeshCollider>();
            var shape = go.AddComponent<BuildShape>();
            shape.Set(entry.shape, entry.shapeSize, entry.shapeSteps, Color.white);
            return go;
        }
    }
}
#endif
