#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Audio (D-0040): the course's sounds. Build Course Sounds makes Data/CourseSounds.asset with first picks
    /// from the Playroom pack, found by name (never copied). Re-running fills only what is still empty, so anything he
    /// picks in the Inspector stays. Place Course Audio gives each open scene one "Course Audio" object that plays it.
    /// The music (D-0211) is his picks from his bought Playroom pack: Build fills the house's track and the title's;
    /// Sandbox Music gives the Raceway, the Workshop and the Colour Corner each their own on their Course Audio; Music
    /// Import Settings streams the six music clips from disk. Every entry logs a plain report and returns it.
    /// Batch: -executeMethod ShiftJam.CourseSoundsBuilder.BuildAndPlaceBatch (Greybox and the Workshop, saved),
    /// ShiftJam.CourseSoundsBuilder.SandboxMusicBatch (the three sandboxes, saved), ShiftJam.CourseSoundsBuilder.MusicBatch
    /// (Build, the import settings, then the sandboxes).
    /// </summary>
    public static class CourseSoundsBuilder
    {
        private const string AssetPath = "Assets/_Game/Data/CourseSounds.asset";
        private const string Sfx = "Assets/_Imports/ithappy/Playroom/Playroom_Sound_FX/";
        /// <summary>The game's own weapon sounds (CasePortman), already used by the ammo box. A path from Assets/ is taken as is.</summary>
        private const string Weapons = "Assets/_Imports/CasePortman/Weapons/";
        private static readonly string[] CourseScenes = { "Assets/_Game/Scenes/Greybox.unity", "Assets/_Game/Scenes/Workshop.unity" };

        // ------------------------------------------------------------------ the music (D-0211): his picks
        private const string Music = "Music/";
        private const string HouseMusic = "Calm_Adventure.wav", TitleMusic = "Toy_Room_Menu_Music_v1.wav";
        private const float HouseVolume = 0.30f;   // CourseSounds' own default
        private const float TitleVolume = 0.44f;   // 3 dB over the rooms: no play sounds under a menu

        /// <summary>Each sandbox's own track, on its Course Audio. The volumes bring each file to about -25.4 LUFS played.</summary>
        private static readonly (string scene, string file, float volume)[] SandboxMusic =
        {
            ("Assets/_Game/Scenes/Raceway.unity", "Treasure_Hunt_Time.wav", 0.33f),
            ("Assets/_Game/Scenes/Workshop.unity", "Toy_Box.wav", 0.25f),
            ("Assets/_Game/Scenes/ColourCorner.unity", "Happy_Day.wav", 0.29f),
        };

        /// <summary>Loudness measured in the files (ffmpeg ebur128, D-0211). Played = this + 20 log10(volume).</summary>
        private static readonly (string clip, float lufs)[] Measured =
        {
            ("Toy_Room_Menu_Music_v1", -15.3f),
            ("Calm_Adventure", -14.9f),
            ("Treasure_Hunt_Time", -15.8f),
            ("Toy_Box", -13.4f),
            ("Happy_Day", -14.8f),
        };

        /// <summary>The music clips the game plays: the five picks and the playroom reveal's swell (RoomReveal).</summary>
        private static readonly string[] MusicFiles =
        {
            "Toy_Room_Menu_Music_v1.wav", "Calm_Adventure.wav", "Treasure_Hunt_Time.wav", "Toy_Box.wav", "Happy_Day.wav",
            "A_Secret_Garden_Of_Light_v1.wav",
        };

        // Music is streamed from disk (a few minutes each: decompressed on load they would sit in memory whole).
        private const AudioClipLoadType MusicLoad = AudioClipLoadType.Streaming;
        private const AudioCompressionFormat MusicFormat = AudioCompressionFormat.Vorbis;
        private const float MusicQuality = 0.7f;

        /// <summary>What a run did, counted.</summary>
        private sealed class Tally { public int filled, kept, missing; }

        [MenuItem("Shift Jam/Audio/Build Course Sounds")]
        public static string Build()
        {
            var sounds = AssetDatabase.LoadAssetAtPath<CourseSounds>(AssetPath);
            bool made = sounds == null;
            if (made)
            {
                sounds = ScriptableObject.CreateInstance<CourseSounds>();
                AssetDatabase.CreateAsset(sounds, AssetPath);
            }

            var t = new Tally();
            // The shift: a shimmer that rings out to grow and its twin to shrink. Both start loud and ring past the 0.35 s shift.
            Fill(ref sounds.grow, "Toys/Star_Light_On.wav", 0.7f, 0f, t);
            Fill(ref sounds.shrink, "Toys/Star_Light_Off.wav", 0.7f, 0f, t);
            Fill(ref sounds.coreAwake, "Toys/Star_Light_Activate.wav", 0.6f, 0f, t);
            // The room. One click for a switch: both Light_Switch takes click twice, about 1.5 s apart.
            Fill(ref sounds.switchPress, "Props/Mouse_Click_v2.wav", 0.8f, 0.05f, t);
            Fill(ref sounds.doorOpen, "Toys/Castle_Gate_v1.wav", 0.6f, 0.05f, t);
            Fill(ref sounds.checkpoint, "UI/UI_Notification_v1.wav", 0.6f, 0f, t);
            Fill(ref sounds.reset, "Toys/Wooden_Toy_Drop_v2.wav", 0.6f, 0.05f, t);
            Fill(ref sounds.goal, "UI/Win_v1.wav", 0.8f, 0f, t);
            // The Shift key refused (D-0045): the pack's shortest error blip (0.67 s), kept under the other cues.
            Fill(ref sounds.refused, "UI/Error_v3.wav", 0.45f, 0.05f, t);
            // The player: a soft thump for a hop (a book closing; the toy squeak wore thin within a minute: his ear, D-0055),
            // a wooden tock for a landing from higher than its own jump.
            Fill(ref sounds.jump, "Props/Book_Close.wav", 0.2f, 0.12f, t);
            Fill(ref sounds.land, "Toys/Chess_Fall_v2.wav", 0.5f, 0.1f, t);
            // D-0088: the silent moments. E on something that says no: a softer no than the Shift key's, quieter still.
            Fill(ref sounds.cantUse, "UI/Error_v1.wav", 0.35f, 0.05f, t);
            // Hands: a soft lift and a wooden set-down (not the reset's drop, so the two never read as one).
            Fill(ref sounds.pickUp, "Props/Backpack_Pickup_v1.wav", 0.45f, 0.08f, t);
            Fill(ref sounds.putDown, "Toys/Block_Fall_v1.wav", 0.4f, 0.08f, t);
            // A weapon from a stand: the ammo box's own pick-up (the game's weapon sounds, CasePortman, already imported).
            Fill(ref sounds.weaponTaken, Weapons + "pickup.ogg", 0.6f, 0.05f, t);
            // A soldier back on its feet: a chess piece set upright, quiet (four of them, often).
            Fill(ref sounds.soldierUp, "Toys/Chess_Move_v1.wav", 0.3f, 0.1f, t);
            Fill(ref sounds.endChoice, "UI/UI_Confirm_v1.wav", 0.6f, 0f, t);
            // D-0097 the opening: cardboard flaps lifting, the spark's shimmer on the way down, the core's own wake chime
            // as it strikes (the spark is a tiny shift orb), a soft-toy rustle as the toy gets up.
            Fill(ref sounds.lidOpen, "Props/Paper_Rustle_v2.wav", 0.55f, 0f, t);
            Fill(ref sounds.sparkFall, "Toys/Star_Light_On.wav", 0.45f, 0f, t);
            Fill(ref sounds.sparkHit, "Toys/Star_Light_Activate.wav", 0.8f, 0f, t);
            Fill(ref sounds.getUp, "Toys/SoftToy_Rustle_v1.wav", 0.5f, 0.05f, t);
            // A 30 s room tone (the 10 s one repeats sooner). It is quiet in the file (about -46 dB), so it plays at 0.6.
            Fill(ref sounds.ambience, ref sounds.ambienceVolume, "Ambience/Room_Ambience_Loop_v2.wav", 0.6f, t);

            // D-0211, the music: his picks from his bought Playroom pack. The house's track is also every scene's
            // without one of its own; the title's plays while the title is up.
            var music = new StringBuilder();
            bool house = Fill(ref sounds.music, ref sounds.musicVolume, Music + HouseMusic, HouseVolume, t);
            music.Append("\n  house (and any scene without its own): ").Append(Said(sounds.music, sounds.musicVolume, house));
            bool title = Fill(ref sounds.titleMusic, ref sounds.titleMusicVolume, Music + TitleMusic, TitleVolume, t);
            music.Append("\n  title: ").Append(Said(sounds.titleMusic, sounds.titleMusicVolume, title));
            music.Append($"\n  fades: in {sounds.musicIn:0.0} s, out {sounds.musicOut:0.0} s, dip under a reveal {sounds.musicDip:0.0} s");

            EditorUtility.SetDirty(sounds);
            AssetDatabase.SaveAssets();
            string report = $"[Audio] {(made ? "Made" : "Updated")} {AssetPath}: {t.filled} filled, {t.kept} kept, {t.missing} missing. " +
                            "What is already set is kept (his Inspector picks stay). Music, D-0211 (his picks from his bought packs):" + music;
            Debug.Log(report);
            return report;
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

        // ------------------------------------------------------------------ the sandboxes' own music (D-0211)

        /// <summary>Gives the Raceway, the Workshop and the Colour Corner their own track, in the scenes that are open now
        /// (none is opened or closed: his scene stays). A Course Audio with a track of its own keeps it; a scene without a
        /// Course Audio is reported, never given one. Changed scenes are left unsaved for him to save.</summary>
        [MenuItem("Shift Jam/Audio/Sandbox Music (open scenes)")]
        public static string SandboxMusicInOpenScenes()
        {
            var report = new StringBuilder("[Audio] Sandbox music (D-0211), open scenes:");
            int changed = 0;
            foreach (var pick in SandboxMusic)
            {
                Scene scene = SceneManager.GetSceneByPath(pick.scene);
                if (!scene.IsValid() || !scene.isLoaded) { report.Append($"\n  {Name(pick.scene)}: not open, skipped."); continue; }
                if (!WireMusic(scene, pick.file, pick.volume, report)) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                changed++;
            }
            report.Append(changed > 0 ? $"\n  {changed} scene(s) changed and left unsaved: save to keep." : "\n  Nothing changed.");
            Debug.Log(report.ToString());
            return report.ToString();
        }

        /// <summary>Batch: opens the Raceway, the Workshop and the Colour Corner in turn, gives each Course Audio its own
        /// track where it has none, and saves the scenes it changed.</summary>
        public static void SandboxMusicBatch()
        {
            var report = new StringBuilder("[Audio] Sandbox music (D-0211), batch:");
            foreach (var pick in SandboxMusic)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(pick.scene) == null) { report.Append($"\n  {Name(pick.scene)}: MISSING scene {pick.scene}."); continue; }
                Scene scene = EditorSceneManager.OpenScene(pick.scene, OpenSceneMode.Single);
                if (!WireMusic(scene, pick.file, pick.volume, report)) continue;
                bool saved = EditorSceneManager.SaveScene(scene);
                report.Append(saved ? " Saved." : " NOT saved.");
            }
            Debug.Log(report.ToString());
        }

        /// <summary>Batch, all of the music in one go: Build Course Sounds, Music Import Settings, then SandboxMusicBatch.</summary>
        public static void MusicBatch()
        {
            Build();
            MusicImportSettings();
            SandboxMusicBatch();
        }

        /// <summary>One scene's Course Audio gets its own track when it has none. True = it changed.</summary>
        private static bool WireMusic(Scene scene, string file, float volume, StringBuilder report)
        {
            CourseAudio audio = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                audio = root.GetComponentInChildren<CourseAudio>(true);
                if (audio != null) break;
            }
            if (audio == null) { report.Append($"\n  {scene.name}: MISSING Course Audio, not added (Place Course Audio puts one in)."); return false; }
            var so = new SerializedObject(audio);
            SerializedProperty clipProp = so.FindProperty("music"), volumeProp = so.FindProperty("musicVolume");
            string silent = so.FindProperty("sounds").objectReferenceValue == null ? " Its Sounds is EMPTY: no sound at all there until Place Course Audio wires it." : "";
            if (clipProp.objectReferenceValue is AudioClip his)
            {
                report.Append($"\n  {scene.name}: ").Append(Said(his, volumeProp.floatValue, false)).Append(silent);
                return false;
            }
            AudioClip clip = Clip(Music + file);
            if (clip == null) { report.Append($"\n  {scene.name}: MISSING clip {Sfx + Music + file}, left empty (the house's track plays there)."); return false; }
            clipProp.objectReferenceValue = clip;
            volumeProp.floatValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Append($"\n  {scene.name}: ").Append(Said(clip, volume, true)).Append(silent);
            return true;
        }

        // ------------------------------------------------------------------ the music clips' import settings (D-0211)

        /// <summary>Streams the six music clips from disk (Vorbis, quality 0.7), changing only what differs. Their .meta
        /// files are in the git-ignored _Imports, so this is redone on a fresh checkout.</summary>
        [MenuItem("Shift Jam/Audio/Music Import Settings")]
        public static string MusicImportSettings()
        {
            var report = new StringBuilder($"[Audio] Music import settings (D-0211): {MusicLoad}, {MusicFormat}, quality {MusicQuality:0.0}. " +
                                           "The .meta files are in the git-ignored _Imports.");
            foreach (string file in MusicFiles)
            {
                string path = Sfx + Music + file;
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) { report.Append($"\n  {file}: MISSING ({path})."); continue; }
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                if (s.loadType == MusicLoad && s.compressionFormat == MusicFormat && Mathf.Approximately(s.quality, MusicQuality))
                {
                    report.Append($"\n  {file}: already set: kept.");
                    continue;
                }
                string was = $"{s.loadType}, {s.compressionFormat}, quality {s.quality:0.00}";
                s.loadType = MusicLoad;
                s.compressionFormat = MusicFormat;
                s.quality = MusicQuality;
                importer.defaultSampleSettings = s;
                importer.SaveAndReimport();
                report.Append($"\n  {file}: set (was {was}).");
            }
            Debug.Log(report.ToString());
            return report.ToString();
        }

        // ------------------------------------------------------------------ helpers

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

        private static void Fill(ref CourseSounds.Cue cue, string file, float volume, float pitchSpread, Tally t)
        {
            if (cue.clip != null) { t.kept++; return; }
            AudioClip clip = Clip(file);
            if (clip == null) { t.missing++; return; }
            cue = new CourseSounds.Cue(clip, volume, pitchSpread);
            t.filled++;
        }

        /// <summary>A loop's clip and volume, when its clip is empty. True = filled now.</summary>
        private static bool Fill(ref AudioClip slot, ref float slotVolume, string file, float volume, Tally t)
        {
            if (slot != null) { t.kept++; return false; }
            AudioClip clip = Clip(file);
            if (clip == null) { t.missing++; return false; }
            slot = clip;
            slotVolume = volume;
            t.filled++;
            return true;
        }

        /// <summary>"Calm_Adventure at 0.30 (filled; -14.9 LUFS in the file, -25.4 played)"; "EMPTY (...)" without a clip.</summary>
        private static string Said(AudioClip clip, float volume, bool filled)
        {
            if (clip == null) return "EMPTY (its clip is missing: silence there)";
            string loud = volume > 0f ? "loudness not measured" : "volume 0: silence there";
            if (volume > 0f)
                foreach (var m in Measured)
                {
                    if (m.clip != clip.name) continue;
                    loud = $"{m.lufs:0.0} LUFS in the file, {m.lufs + 20f * Mathf.Log10(volume):0.0} played";
                    break;
                }
            return $"{clip.name} at {volume:0.00} ({(filled ? "filled" : "kept")}; {loud})";
        }

        private static string Name(string scenePath) => System.IO.Path.GetFileNameWithoutExtension(scenePath);

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
