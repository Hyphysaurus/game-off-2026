#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Play in the editor starts from the title (the first scene in Build Settings, Greybox), whatever scene is open,
    /// the way a build boots. Menu: Shift Jam / Play From Title (on by default; off = Play runs the open scene, for
    /// trying the Workshop, the Raceway or the Colour Corner straight in). Batch runs (the walk, probes) are left alone.
    /// Editor only; nothing of this ships.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromTitle
    {
        const string Menu = "Shift Jam/Play From Title";
        const string Pref = "ShiftJam.PlayFromTitle";

        static bool On
        {
            get => EditorPrefs.GetBool(Pref, true);
            set => EditorPrefs.SetBool(Pref, value);
        }

        static PlayFromTitle() => EditorApplication.delayCall += Apply;

        [MenuItem(Menu, priority = 0)]
        static void Toggle()
        {
            On = !On;
            Apply();
        }

        [MenuItem(Menu, true)]
        static bool ToggleCheck()
        {
            UnityEditor.Menu.SetChecked(Menu, On);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        static void Apply()
        {
            if (Application.isBatchMode || !On)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }
            var scenes = EditorBuildSettings.scenes;
            var first = scenes.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path) : null;
            if (first == null) Debug.LogWarning("Play From Title: no first scene in Build Settings; Play runs the open scene.");
            EditorSceneManager.playModeStartScene = first;
        }
    }
}
#endif
