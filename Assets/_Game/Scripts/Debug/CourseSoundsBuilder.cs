#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Audio (D-0040): the course's sounds. Build Course Sounds makes Data/CourseSounds.asset with first picks
    /// from the Playroom pack, found by name (never copied). Re-running fills only cues that are still empty, so anything
    /// he picks in the Inspector stays. Place Course Audio gives each open scene one "Course Audio" object that plays it.
    /// Music is his and stays empty here. Batch: -executeMethod ShiftJam.CourseSoundsBuilder.BuildAndPlaceBatch (both
    /// course scenes, saved).
    /// </summary>
    public static class CourseSoundsBuilder
    {
        private const string AssetPath = "Assets/_Game/Data/CourseSounds.asset";
        private const string Sfx = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/";
        private static readonly string[] CourseScenes = { "Assets/_Game/Scenes/Greybox.unity", "Assets/_Game/Scenes/Workshop.unity" };

        [MenuItem("Shift Jam/Audio/Build Course Sounds")]
        public static CourseSounds Build()
        {
            var sounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(AssetPath);
            bool made = sounds == null;
            if (made)
            {
                sounds = ScriptableObject.CreateInstance<CourseSounds>();
                AssetDatabase.CreateAsset(sounds, AssetPath);
            }

            int filled = 0;
            // The shift: a shimmer that rings out to grow and its twin to shrink. Both start loud and ring past the 0.35 s shift.
            Fill(ref sounds.grow, "Toys/Star_Light_On.wav", 0.7f, 0f, ref filled);
            Fill(ref sounds.shrink, "Toys/Star_Light_Off.wav", 0.7f, 0f, ref filled);
            Fill(ref sounds.coreAwake, "Toys/Star_Light_Activate.wav", 0.6f, 0f, ref filled);
            // The room. One click for a switch: both Light_Switch takes click twice, about 1.5 s apart.
            Fill(ref sounds.switchPress, "Props/Mouse_Click_v2.wav", 0.8f, 0.05f, ref filled);
            Fill(ref sounds.doorOpen, "Toys/Castle_Gate_v1.wav", 0.6f, 0.05f, ref filled);
            Fill(ref sounds.checkpoint, "UI/UI_Notification_v1.wav", 0.6f, 0f, ref filled);
            Fill(ref sounds.reset, "Toys/Wooden_Toy_Drop_v2.wav", 0.6f, 0.05f, ref filled);
            Fill(ref sounds.goal, "UI/Win_v1.wav", 0.8f, 0f, ref filled);
            // The player: a small squeak for a hop, a wooden tock for a landing from higher than its own jump.
            Fill(ref sounds.jump, "Toys/Toy_Squeak_Single_v2.wav", 0.3f, 0.08f, ref filled);
            Fill(ref sounds.land, "Toys/Chess_Fall_v2.wav", 0.5f, 0.1f, ref filled);
            if (sounds.ambience == null)
            {
                // A 30 s room tone (the 10 s one repeats sooner). It is quiet in the file (about -46 dB), so it plays at 0.6.
                sounds.ambience = Clip("Ambience/Room_Ambience_Loop_v2.wav");
                sounds.ambienceVolume = 0.6f;
                if (sounds.ambience != null) filled++;
            }

            EditorUtility.SetDirty(sounds);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Audio] {(made ? "Made" : "Updated")} {AssetPath}: {filled} filled. Cues already set are kept; music is left empty for him.");
            return sounds;
        }

        [MenuItem("Shift Jam/Audio/Place Course Audio (open scenes)")]
        public static void PlaceInOpenScenes()
        {
            CourseSounds sounds = Build();
            for (int i = 0; i < SceneManager.sceneCount; i++) Place(SceneManager.GetSceneAt(i), sounds);
        }

        /// <summary>Batch: builds the sounds, then gives Greybox and the Workshop their Course Audio and saves both.</summary>
        public static void BuildAndPlaceBatch()
        {
            CourseSounds sounds = Build();
            foreach (string path in CourseScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Place(scene, sounds);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void Place(Scene scene, CourseSounds sounds)
        {
            if (!scene.isLoaded) return;
            CourseAudio audio = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                audio = root.GetComponentInChildren<CourseAudio>(true);
                if (audio != null) break;
            }
            if (audio == null)
            {
                var go = new GameObject("Course Audio");
                SceneManager.MoveGameObjectToScene(go, scene);
                audio = go.AddComponent<CourseAudio>();
            }
            var so = new SerializedObject(audio);
            so.FindProperty("sounds").objectReferenceValue = sounds;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Audio] {scene.name}: Course Audio plays {AssetPath}.");
        }

        private static void Fill(ref CourseSounds.Cue cue, string file, float volume, float pitchSpread, ref int filled)
        {
            if (cue.clip != null) return;
            AudioClip clip = Clip(file);
            if (clip == null) return;
            cue = new CourseSounds.Cue(clip, volume, pitchSpread);
            filled++;
        }

        private static AudioClip Clip(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + file);
            if (clip == null) Debug.LogWarning("[Audio] Missing sound, left empty: " + Sfx + file);
            return clip;
        }
    }
}
#endif
