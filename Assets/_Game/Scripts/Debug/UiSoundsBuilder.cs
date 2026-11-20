#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Makes Resources/UiSounds (D-0100) and fills each empty cue from the Playroom pack's UI set (a cue already set by hand
    /// is kept). Menu: Shift Jam / Audio / Build UI Sounds. Batch: -executeMethod ShiftJam.UiSoundsBuilder.Build.
    /// </summary>
    public static class UiSoundsBuilder
    {
        private const string Asset = "Assets/_Game/Resources/UiSounds.asset";
        private const string Ui = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/UI/";

        [MenuItem("Shift Jam/Audio/Build UI Sounds")]
        public static void Build()
        {
            var s = AssetDatabase.LoadAssetAtPath<UiSounds>(Asset);
            if (s == null) { s = ScriptableObject.CreateInstance<UiSounds>(); AssetDatabase.CreateAsset(s, Asset); }
            int filled = 0;
            Fill(ref s.move, "UI_Hover_v1", 0.3f, 0.06f, ref filled);
            Fill(ref s.click, "UI_Click_v1", 0.5f, 0.03f, ref filled);
            Fill(ref s.confirm, "UI_Confirm_v2", 0.6f, 0f, ref filled);   // v1 is the end card's choice
            Fill(ref s.back, "UI_Back_v1", 0.5f, 0f, ref filled);
            Fill(ref s.select, "UI_Select_v1", 0.5f, 0.03f, ref filled);
            Fill(ref s.denied, "Error_v2", 0.3f, 0.04f, ref filled);
            Fill(ref s.open, "UI_Select_v2", 0.45f, 0f, ref filled);
            Fill(ref s.toast, "UI_Notification_v2", 0.5f, 0f, ref filled);
            Fill(ref s.win, "Win_v2", 0.65f, 0f, ref filled);
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log($"D-0100 UI SOUNDS: {filled} cues filled in {Asset}");
        }

        private static void Fill(ref CourseSounds.Cue cue, string file, float volume, float spread, ref int filled)
        {
            if (cue.clip != null) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Ui + file + ".wav");
            if (clip == null) { Debug.LogWarning("UI sounds: no " + Ui + file + ".wav"); return; }
            cue = new CourseSounds.Cue(clip, volume, spread);
            filled++;
        }
    }
}
#endif
