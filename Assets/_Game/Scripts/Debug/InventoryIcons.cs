#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Gives the open scenes' hand tools their inventory picture (D-0108): every flashlight gets the Playroom flashlight's
    /// thumbnail. The weapons get theirs from the Workshop (Fit Weapon Holds). Menu: Shift Jam / Inventory / Wire Tool Icons.
    /// </summary>
    public static class InventoryIcons
    {
        const string FlashlightIcon = "Assets/_Game/Art/Thumbs/Playroom_Other_Flashlight_01.png";

        [MenuItem("Shift Jam/Inventory/Wire Tool Icons (D-0108)")]
        public static void Wire()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(FlashlightIcon);
            if (icon == null) { Debug.LogError("D-0108: no " + FlashlightIcon); return; }
            int n = 0;
            foreach (var torch in Object.FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var c = torch.GetComponent<Carryable>(); if (c == null) continue;
                var so = new SerializedObject(c); so.FindProperty("icon").objectReferenceValue = icon; so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene); n++;
            }
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"D-0108 TOOL ICONS: {n} flashlights");
        }
    }
}
#endif
