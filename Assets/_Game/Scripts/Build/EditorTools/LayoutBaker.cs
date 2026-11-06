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
    /// copies of scene props are copied. Baking the same layout again replaces its Baked_ group. One undo step.
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

            string rootName = "Baked_" + name;
            foreach (var go in scene.GetRootGameObjects()) if (go.name == rootName) Undo.DestroyObjectImmediate(go);
            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Bake layout");

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
                    if (targets.TryGetValue(item.cloneOf, out var source) && source != null) go = Copy(source.gameObject, root.transform);
                }
                else
                {
                    var entry = catalog != null ? catalog.Find(item.id) : null;
                    if (entry != null) go = entry.IsShape ? Shape(entry, catalog, root.transform) : (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, root.transform);
                    if (go != null) go.name = entry.displayName;
                }
                if (go == null) { missing++; Debug.LogWarning($"Bake: could not make '{item.id ?? item.cloneOf}'."); continue; }

                Undo.RegisterCreatedObjectUndo(go, "Bake layout");
                go.transform.SetPositionAndRotation(item.position, item.rotation);
                go.transform.localScale = item.scale;
                if (item.hasShape && go.TryGetComponent(out BuildShape shape)) shape.Set(shape.Kind, item.shapeSize, item.shapeSteps, item.shapeColor);
                if (!go.TryGetComponent(out Buildable _)) go.AddComponent<Buildable>();
                made++;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log($"Baked layout '{name}': {made} made under {rootName}, {moved} scene props moved/removed, {missing} missing. Save the scene to keep it.");
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
