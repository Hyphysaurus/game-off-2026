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
        /// <summary>The game's own weapon sounds (CasePortman), already used by the ammo box. A path from Assets/ is taken as is.</summary>
        private const string Weapons = "Assets/_Imports/CasePortman/Weapons/";
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
            // The Shift key refused (D-0045): the pack's shortest error blip (0.67 s), kept under the other cues.
            Fill(ref sounds.refused, "UI/Error_v3.wav", 0.45f, 0.05f, ref filled);
            // The player: a soft thump for a hop (a book closing; the toy squeak wore thin within a minute: his ear, D-0055),
            // a wooden tock for a landing from higher than its own jump.
            Fill(ref sounds.jump, "Props/Book_Close.wav", 0.2f, 0.12f, ref filled);
            Fill(ref sounds.land, "Toys/Chess_Fall_v2.wav", 0.5f, 0.1f, ref filled);
            // D-0088: the silent moments. E on something that says no: a softer no than the Shift key's, quieter still.
            Fill(ref sounds.cantUse, "UI/Error_v1.wav", 0.35f, 0.05f, ref filled);
            // Hands: a soft lift and a wooden set-down (not the reset's drop, so the two never read as one).
            Fill(ref sounds.pickUp, "Props/Backpack_Pickup_v1.wav", 0.45f, 0.08f, ref filled);
            Fill(ref sounds.putDown, "Toys/Block_Fall_v1.wav", 0.4f, 0.08f, ref filled);
            // A weapon from a stand: the ammo box's own pick-up (the game's weapon sounds, CasePortman, already imported).
            Fill(ref sounds.weaponTaken, Weapons + "pickup.ogg", 0.6f, 0.05f, ref filled);
            // A soldier back on its feet: a chess piece set upright, quiet (four of them, often).
            Fill(ref sounds.soldierUp, "Toys/Chess_Move_v1.wav", 0.3f, 0.1f, ref filled);
            Fill(ref sounds.endChoice, "UI/UI_Confirm_v1.wav", 0.6f, 0f, ref filled);
            // D-0097 the opening: cardboard flaps lifting, the spark's shimmer on the way down, the core's own wake chime
            // as it strikes (the spark is a tiny shift orb), a soft-toy rustle as the toy gets up.
            Fill(ref sounds.lidOpen, "Props/Paper_Rustle_v2.wav", 0.55f, 0f, ref filled);
            Fill(ref sounds.sparkFall, "Toys/Star_Light_On.wav", 0.45f, 0f, ref filled);
            Fill(ref sounds.sparkHit, "Toys/Star_Light_Activate.wav", 0.8f, 0f, ref filled);
            Fill(ref sounds.getUp, "Toys/SoftToy_Rustle_v1.wav", 0.5f, 0.05f, ref filled);
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
            Build();
            for (int i = 0; i < SceneManager.sceneCount; i++) Place(SceneManager.GetSceneAt(i));
        }

        /// <summary>Batch: builds the sounds, then gives Greybox and the Workshop their Course Audio and saves both.</summary>
        public static void BuildAndPlaceBatch()
        {
            Build();
            foreach (string path in CourseScenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Place(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void Place(Scene scene)
        {
            if (!scene.isLoaded) return;
            // Loaded by path here, after the scene opened: opening a scene let go of the object Build() returned, and the
            // first batch saved both scenes with an empty reference (8 Nov).
            var sounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(AssetPath);
            if (sounds == null) { Debug.LogError("[Audio] No " + AssetPath + ": run Build Course Sounds first."); return; }
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
            bool wired = new SerializedObject(audio).FindProperty("sounds").objectReferenceValue == sounds;
            if (wired) Debug.Log($"[Audio] {scene.name}: Course Audio plays {AssetPath}.");
            else Debug.LogError($"[Audio] {scene.name}: Course Audio did NOT take {AssetPath}.");
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
            string path = file.StartsWith("Assets/") ? file : Sfx + file;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning("[Audio] Missing sound, left empty: " + path);
            return clip;
        }
    }
}
#endif
