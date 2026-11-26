#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// Films a footage take of the game (D-0242, ported from Nectar Rush's recorder, for the demo reel). A shot list,
    /// docs/footage/&lt;name&gt;.json (a FootageClip, in git), says what to film; FootageRig films it in play mode.
    /// Shift Jam > Footage > Preview Shots: stills at the start, middle and end of each shot the take's own camera flies,
    /// out of play, to check the framing before any Play (a contact sheet of them too). Shift Jam > Footage > Film Shots:
    /// the save slots are copied aside and the take gets a slot of its own (an empty one when there is one), the editor is
    /// muted, Play runs the clip's scene itself, and the take plays by itself and leaves play when it is done. Then every
    /// slot is put back exactly as it was, the sound and the start scene come back, a contact sheet of the take is made and
    /// ffmpeg makes one video a shot in the background: Builds/Footage/&lt;date&gt;/&lt;clip&gt;-&lt;time&gt;/ (git-ignored). A take
    /// that never finished (the editor died) has the slots put back the next time the editor loads.
    /// From the command line (no window): -executeMethod ShiftJam.FootageRecorder.PreviewBatch or FilmBatch, with
    /// -footageClip &lt;path&gt; (docs/footage/greybox.json when left out); never -nographics (the frames are drawn).
    /// </summary>
    [InitializeOnLoad]
    public static class FootageRecorder
    {
        private const string RunningKey = "ShiftJam.Footage.Running", PlayedKey = "ShiftJam.Footage.Played", StartedKey = "ShiftJam.Footage.StartedAt";
        private const string MuteKey = "ShiftJam.Footage.MuteWas", StartSceneKey = "ShiftJam.Footage.StartSceneWas", ReopenKey = "ShiftJam.Footage.Reopen", BatchKey = "ShiftJam.Footage.Batch";
        /// <summary>Where the copy of the save slots is while a take runs (EditorPrefs: it outlives an editor that dies).</summary>
        private const string BackupPref = "ShiftJam.Footage.SavesBackup";
        /// <summary>The shot list the menus and the command line use when nothing else is named.</summary>
        public const string DefaultClip = "docs/footage/greybox.json";
        private const int PreviewWidth = 960, PreviewHeight = 540, ThumbWidth = 384, ThumbHeight = 216, Columns = 5;
        /// <summary>Seconds after Film for play mode to start; past them the take is given up (a compile error) and put back.</summary>
        private const float NeverPlayed = 20f;

        // The save slots' keys as Core/SaveGame.cs writes them (its own are private). CheckSaveKeys proves they still match
        // before a take touches anything; change them with SaveGame's.
        private const string SlotKey = "ShiftJam.Slot";
        private static string SaveKey(int slot) => "ShiftJam.Save.v2." + slot;
        private static string SeenKey(int slot) => "ShiftJam.Seen.v1." + slot;

        static FootageRecorder()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Watch;
            EditorApplication.delayCall += RecoverSaves;
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Shift Jam/Footage/Preview Shots")]
        private static void PreviewMenu() => Debug.Log(Preview(MenuClip()));

        [MenuItem("Shift Jam/Footage/Film Shots")]
        private static void FilmMenu() => Debug.Log(Film(MenuClip()));

        /// <summary>The open scene's own shot list (docs/footage/&lt;scene&gt;.json, in lower case), else Greybox's.</summary>
        private static string MenuClip()
        {
            string own = Path.Combine("docs", "footage", SceneManager.GetActiveScene().name.ToLowerInvariant() + ".json");
            return File.Exists(Path.Combine(ProjectRoot, own)) ? own : DefaultClip;
        }

        /// <summary>Preview stills from the command line: Unity.exe -batchmode -projectPath &lt;project&gt; -executeMethod
        /// ShiftJam.FootageRecorder.PreviewBatch [-footageClip &lt;path&gt;] -logFile &lt;log&gt;.</summary>
        public static void PreviewBatch()
        {
            Debug.Log(Preview(ClipArgument()));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>A take from the command line: as PreviewBatch with FilmBatch. The editor quits once the take has left play
        /// and the videos are being made (ffmpeg carries on by itself). A take with no window draws no speech stickers: the
        /// game draws none in a batch run.</summary>
        public static void FilmBatch()
        {
            SessionState.SetBool(BatchKey, true);
            string said = Film(ClipArgument());
            Debug.Log(said);
            if (said.StartsWith("Footage: filming")) return;
            SessionState.EraseBool(BatchKey);
            Debug.LogError(said);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static string ClipArgument()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int k = 0; k + 1 < args.Length; k++) if (args[k] == "-footageClip") return args[k + 1];
            return DefaultClip;
        }

        // ── Preview ──────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Stills of each shot the take's own camera flies (start, middle and end), out of play, into
        /// Builds/Footage/&lt;date&gt;/&lt;clip&gt;-preview/ with a contact sheet of them. A game-camera shot has no still (that camera
        /// is the game's, in play). Out of play nothing moves and the bedroom is lit (its Dark Zone darkens it only in play):
        /// they check the framing, not the light. Returns what it made.
        /// </summary>
        public static string Preview(string clipPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Footage: leave play mode first.";
            if (!ReadClip(clipPath, out string full, out FootageClip clip, out string problem)) return problem;
            if (!OpenScene(clip.scene, out _, out problem)) return problem;
            string name = Path.GetFileNameWithoutExtension(full);
            string folder = Path.Combine(ProjectRoot, "Builds", "Footage", DateTime.Now.ToString("yyyy-MM-dd"), name + "-preview");
            Directory.CreateDirectory(folder);
            foreach (string old in Directory.GetFiles(folder, "*.jpg")) File.Delete(old);
            Camera main = Camera.main;
            var notes = new StringBuilder();
            var rows = new List<List<string>>();
            int made = 0;
            for (int k = 0; k < clip.shots.Length; k++)
            {
                FootageClip.Shot shot = clip.shots[k];
                string move = shot.Move;
                if (move == FootageClip.Game) { notes.Append($" Shot {k + 1} ({shot.name}) is the game's own camera: it shows only in the take."); continue; }
                Transform target = FootageRig.Find(shot.target, Vector3.zero);
                bool wantsTarget = move == FootageClip.Follow || (move == FootageClip.Orbit && !string.IsNullOrEmpty(shot.target));
                if (wantsTarget && target == null) notes.Append($" Shot {k + 1}: nothing here is called \"{shot.target}\"; its stills use \"around\".");
                var row = new List<string>();
                foreach ((char part, float share) in new[] { ('a', 0f), ('b', 0.5f), ('c', 1f) })
                {
                    FootageClip.CameraAt(shot, shot.seconds * share, wantsTarget && target != null ? target.position : (Vector3?)null, out Vector3 at, out Vector3 look);
                    string file = Path.Combine(folder, $"{FootageRig.ShotFolder(k, shot.name)}-{part}.jpg");
                    Still(at, look, shot.fov > 0f ? shot.fov : clip.fov, clip.near, main, file);
                    row.Add(file);
                    made++;
                }
                rows.Add(row);
            }
            string sheet = rows.Count > 0 ? Sheet(rows, Path.Combine(folder, "contact.jpg"), PreviewWidth / 2, PreviewHeight / 2) : "none";
            return $"Footage: {made} preview stills of {name} in {folder} (the contact sheet: {sheet}). Out of play nothing moves and the bedroom is lit: check the framing, not the light.{notes}";
        }

        /// <summary>One still of the open scene from <paramref name="at"/>, looking at <paramref name="lookAt"/>, through a camera
        /// with the game camera's look, its shadows reaching as far as the take's would. Not hidden: a pack's own render passes
        /// leave out a hidden camera. Destroyed at once, never saved; the pipeline's shadow distance is put back at once.</summary>
        private static void Still(Vector3 at, Vector3 lookAt, float fieldOfView, float near, Camera look, string file)
        {
            var holder = new GameObject("Footage preview camera");
            var target = new RenderTexture(PreviewWidth, PreviewHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset;
            float shadows = pipeline != null ? pipeline.shadowDistance : 0f;
            Quaternion facing = FootageClip.Facing(at, lookAt);
            try
            {
                if (pipeline != null) pipeline.shadowDistance = FootageRig.ShadowReachFor(shadows, Vector3.Distance(at, lookAt));
                var camera = holder.AddComponent<Camera>();
                FootageRig.Dress(camera, look, near);
                camera.transform.SetPositionAndRotation(at, facing);
                camera.fieldOfView = fieldOfView;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var picture = new Texture2D(PreviewWidth, PreviewHeight, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, PreviewWidth, PreviewHeight), 0, 0, false);
                RenderTexture.active = previous;
                File.WriteAllBytes(file, picture.EncodeToJPG(85));
                Object.DestroyImmediate(picture);
            }
            finally
            {
                if (pipeline != null) pipeline.shadowDistance = shadows;
                Object.DestroyImmediate(holder);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        // ── Film ─────────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts a take of <paramref name="clipPath"/>: the save slots copied aside and a scratch slot chosen, the editor
        /// muted, the clip's scene opened if it is not (the one that was open comes back after), Play run on that scene
        /// itself (not through the title's first scene). The take leaves play by itself; then everything comes back and the
        /// videos are made. Returns what it did (or why it will not).
        /// </summary>
        public static string Film(string clipPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "Footage: leave play mode first.";
            if (EditorApplication.isCompiling) return "Footage: wait for the scripts to finish compiling.";
            if (SessionState.GetBool(RunningKey, false)) return "Footage: a take is already under way.";
            if (!ReadClip(clipPath, out string full, out FootageClip clip, out string problem)) return problem;
            if (EditorPrefs.HasKey(BackupPref))
            {
                RecoverSaves();
                if (EditorPrefs.HasKey(BackupPref)) return "Footage: the save slots from the last take are not back yet (see the console's warnings); no take until they are.";
            }
            if (!CheckSaveKeys(out problem)) return problem;
            for (int s = 0; s < SceneManager.sceneCount; s++)
                if (SceneManager.GetSceneAt(s).isDirty) return "Footage: save the open scene first (a take films the scene as it is saved).";
            if (!OpenScene(clip.scene, out string left, out problem)) return problem;
            DateTime now = DateTime.Now;
            string output = Path.Combine(ProjectRoot, "Builds", "Footage", now.ToString("yyyy-MM-dd"), $"{Path.GetFileNameWithoutExtension(full)}-{now:HHmm}");
            if (Directory.Exists(output)) output += now.ToString("ss");
            Directory.CreateDirectory(output);
            if (!Backup(output, out int scratch, out problem)) return problem;
            SessionState.SetString(FootageRig.ClipKey, full);
            SessionState.SetString(FootageRig.OutKey, output);
            SessionState.SetFloat(FootageRig.ArmedKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(MuteKey, EditorUtility.audioMasterMute);
            SessionState.SetString(StartSceneKey, EditorSceneManager.playModeStartScene != null ? AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) : "");
            SessionState.SetString(ReopenKey, left ?? "");
            SessionState.EraseBool(PlayedKey);
            SessionState.SetBool(RunningKey, true);
            // The game's sounds would play in the room: the editor is muted for the take.
            EditorUtility.audioMasterMute = true;
            // Play runs the clip's scene itself, not the first scene of the build (Play From Title); it comes back after.
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.isPlaying = true;
            return $"Footage: filming {clip.title}: {clip.shots.Length} shots, {clip.Length:F1} s of footage and at least {clip.Unfilmed:F0} s unfilmed, "
                + $"save slot {scratch + 1} as its scratch slot, into {output}. Keep a Unity window in front until it leaves play.";
        }

        // ── The save slots ───────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>One save key as it was before the take (saves-before.json).</summary>
        [Serializable]
        private sealed class SavedKey
        {
            [Tooltip("The PlayerPrefs key.")] public string key = "";
            [Tooltip("Whether it existed (one that did not is removed after the take).")] public bool had;
            [Tooltip("Whether it holds a whole number (the active slot) rather than words.")] public bool number;
            [Tooltip("Its whole number.")] public int whole;
            [Tooltip("Its words (a slot's save, or its seen moments).")] public string text = "";
        }

        /// <summary>Every save key as it was before the take.</summary>
        [Serializable]
        private sealed class SavedSlots
        {
            [Tooltip("The active slot's key, then each slot's save and seen keys.")] public SavedKey[] keys = Array.Empty<SavedKey>();
        }

        private static IEnumerable<string> Keys()
        {
            yield return SlotKey;
            for (int s = 0; s < SaveGame.Slots; s++) { yield return SaveKey(s); yield return SeenKey(s); }
        }

        /// <summary>Proves the keys above are still the ones SaveGame writes, before anything is copied or cleared: each slot's
        /// save as SaveGame sees it, its first seen moment, and the active slot.</summary>
        private static bool CheckSaveKeys(out string problem)
        {
            problem = null;
            for (int s = 0; s < SaveGame.Slots; s++)
            {
                bool off = SaveGame.Has(s) != PlayerPrefs.HasKey(SaveKey(s));
                string first = PlayerPrefs.GetString(SeenKey(s), "").Split('|').FirstOrDefault(id => id.Length > 0);
                if (first != null && !SaveGame.Seen(s, first)) off = true;
                if (!off) continue;
                problem = "Footage: Core/SaveGame.cs keeps its slots under other keys now; FootageRecorder's copy of them (SlotKey, SaveKey, SeenKey) must change with it. No take.";
                return false;
            }
            if (SaveGame.Slot != Mathf.Clamp(PlayerPrefs.GetInt(SlotKey, 0), 0, SaveGame.Slots - 1))
            {
                problem = "Footage: Core/SaveGame.cs keeps the active slot under another key now; FootageRecorder's SlotKey must change with it. No take.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Every save slot copied aside (saves-before.json in the take's folder; where it is, in EditorPrefs), then the take's
        /// scratch slot made the active one and emptied: an empty slot when there is one (nothing of his is cleared), else the
        /// active slot (emptied for the take, put back after with the rest).
        /// </summary>
        private static bool Backup(string output, out int scratch, out string problem)
        {
            scratch = -1;
            problem = null;
            var saved = new SavedSlots
            {
                keys = Keys().Select(key => new SavedKey
                {
                    key = key, had = PlayerPrefs.HasKey(key), number = key == SlotKey,
                    whole = key == SlotKey ? PlayerPrefs.GetInt(key, 0) : 0, text = key == SlotKey ? "" : PlayerPrefs.GetString(key, ""),
                }).ToArray(),
            };
            string file = Path.Combine(output, "saves-before.json");
            try { File.WriteAllText(file, JsonUtility.ToJson(saved, true)); }
            catch (Exception error)
            {
                problem = $"Footage: could not copy the save slots aside ({error.Message}). No take.";
                return false;
            }
            EditorPrefs.SetString(BackupPref, file);
            for (int s = 0; s < SaveGame.Slots && scratch < 0; s++)
                if (!PlayerPrefs.HasKey(SaveKey(s)) && !PlayerPrefs.HasKey(SeenKey(s))) scratch = s;
            if (scratch < 0) scratch = SaveGame.Slot;
            SaveGame.Slot = scratch;
            SaveGame.Clear(scratch);
            return true;
        }

        /// <summary>Every save slot exactly as the copy has it (a key the copy did not have is removed), checked; the copy
        /// goes once it matches. False, with warnings, when it could not be done.</summary>
        private static bool Restore(string file)
        {
            if (!File.Exists(file))
            {
                Debug.LogWarning($"Footage: the copy of the save slots is gone ({file}); the slots stay as the take left them.");
                EditorPrefs.DeleteKey(BackupPref);
                return false;
            }
            SavedSlots saved;
            try { saved = JsonUtility.FromJson<SavedSlots>(File.ReadAllText(file)); }
            catch (Exception error)
            {
                Debug.LogWarning($"Footage: could not read the copy of the save slots ({file}: {error.Message}); it is kept.");
                return false;
            }
            if (saved == null || saved.keys == null || saved.keys.Length == 0)
            {
                Debug.LogWarning($"Footage: the copy of the save slots is empty ({file}); it is kept.");
                return false;
            }
            foreach (SavedKey k in saved.keys)
            {
                if (!k.had) PlayerPrefs.DeleteKey(k.key);
                else if (k.number) PlayerPrefs.SetInt(k.key, k.whole);
                else PlayerPrefs.SetString(k.key, k.text);
            }
            PlayerPrefs.Save();
            var off = new List<string>();
            foreach (SavedKey k in saved.keys)
            {
                bool same = k.had == PlayerPrefs.HasKey(k.key) && (!k.had || (k.number ? PlayerPrefs.GetInt(k.key, 0) == k.whole : PlayerPrefs.GetString(k.key, "") == k.text));
                if (!same) off.Add(k.key);
            }
            if (off.Count > 0)
            {
                Debug.LogWarning($"Footage: these save keys did not come back as they were: {string.Join(", ", off)}. The copy is kept: {file}.");
                return false;
            }
            File.Delete(file);
            EditorPrefs.DeleteKey(BackupPref);
            return true;
        }

        /// <summary>A take that did not finish (the editor died, or Play never started): its copy of the save slots is put
        /// back as the editor loads.</summary>
        private static void RecoverSaves()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(RunningKey, false)) return;
            string file = EditorPrefs.GetString(BackupPref, "");
            if (file.Length == 0) return;
            if (Restore(file)) Debug.LogWarning("Footage: a take did not finish last time; the save slots are back as they were before it.");
        }

        // ── After the take ───────────────────────────────────────────────────────────────────────────────────────────

        private static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) SessionState.SetBool(PlayedKey, true);
            else if (change == PlayModeStateChange.EnteredEditMode) Finish();
        }

        /// <summary>Play never started (a compile error, a refusal): the take is given up and everything put back.</summary>
        private static void Watch()
        {
            if (!SessionState.GetBool(RunningKey, false) || SessionState.GetBool(PlayedKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartedKey, 0f) < NeverPlayed) return;
            Debug.LogWarning("Footage: play mode never started (a compile error?); the take is given up.");
            Finish();
        }

        /// <summary>The take is over: the save slots, the sound, the start scene and the open scene back as they were; then
        /// the contact sheet and the videos. Says what it did.</summary>
        private static string Finish()
        {
            SessionState.EraseBool(RunningKey);
            SessionState.EraseBool(PlayedKey);
            var said = new StringBuilder("Footage: ");
            string backup = EditorPrefs.GetString(BackupPref, "");
            said.Append(backup.Length > 0 && Restore(backup) ? "the save slots are back as they were; " : "THE SAVE SLOTS ARE NOT BACK (see the warnings above); ");
            EditorUtility.audioMasterMute = SessionState.GetBool(MuteKey, false);
            SessionState.EraseBool(MuteKey);
            string start = SessionState.GetString(StartSceneKey, "");
            SessionState.EraseString(StartSceneKey);
            EditorSceneManager.playModeStartScene = start.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(start) : null;
            string reopen = SessionState.GetString(ReopenKey, "");
            SessionState.EraseString(ReopenKey);
            // The scene that was open before the take, once the editor is back in edit mode for good.
            if (reopen.Length > 0) EditorApplication.delayCall += () => { if (SceneManager.GetActiveScene().path != reopen && !EditorApplication.isPlayingOrWillChangePlaymode) EditorSceneManager.OpenScene(reopen, OpenSceneMode.Single); };
            string output = SessionState.GetString(FootageRig.OutKey, ""), clipPath = SessionState.GetString(FootageRig.ClipKey, "");
            SessionState.EraseString(FootageRig.OutKey);
            SessionState.EraseString(FootageRig.ClipKey);
            SessionState.EraseFloat(FootageRig.ArmedKey);
            SessionState.EraseFloat(StartedKey);
            int fps = File.Exists(clipPath) ? Mathf.Max(1, FootageClip.Parse(File.ReadAllText(clipPath)).fps) : 30;
            said.Append(output.Length > 0 ? Videos(output, fps) : "no take folder.");
            string report = said.ToString();
            Debug.Log(report);
            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.EraseBool(BatchKey);
            // From the command line the editor's work is done once the videos have been started.
            if (batch && Application.isBatchMode) EditorApplication.Exit(0);
            return report;
        }

        /// <summary>The contact sheet (a row a shot, five frames across) and the videos (one a shot, in the background).</summary>
        private static string Videos(string output, int fps)
        {
            string frames = Path.Combine(output, "frames");
            List<string> shots = Directory.Exists(frames)
                ? Directory.GetDirectories(frames).Where(d => File.Exists(Path.Combine(d, "frame_00000.jpg"))).OrderBy(d => d, StringComparer.Ordinal).ToList()
                : new List<string>();
            if (shots.Count == 0) return $"the take wrote no frames ({frames}).";
            var rows = new List<List<string>>();
            int count = 0;
            foreach (string shot in shots)
            {
                List<string> all = Directory.GetFiles(shot, "frame_*.jpg").OrderBy(f => f, StringComparer.Ordinal).ToList();
                count += all.Count;
                rows.Add(Spread(all, Columns));
            }
            string sheet = Sheet(rows, Path.Combine(output, "contact.jpg"), ThumbWidth, ThumbHeight);
            File.WriteAllLines(Path.Combine(output, "contact.txt"), shots.Select((d, k) => $"row {k + 1}: {Path.GetFileName(d)}"));
            return $"{count} frames in {shots.Count} shots ({count / (float)fps:F1} s) in {output}; the contact sheet is {sheet}; {Encode(output, shots, fps)}.";
        }

        /// <summary><paramref name="n"/> of <paramref name="all"/>, evenly spread from the first to the last.</summary>
        private static List<string> Spread(List<string> all, int n)
        {
            var picked = new List<string>();
            if (all.Count == 0) return picked;
            for (int k = 0; k < n; k++)
            {
                string file = all[Mathf.RoundToInt(k * (all.Count - 1) / (float)Mathf.Max(1, n - 1))];
                if (picked.Count == 0 || picked[picked.Count - 1] != file) picked.Add(file);
            }
            return picked;
        }

        /// <summary>A contact sheet: each row's pictures shrunk to <paramref name="w"/> x <paramref name="h"/>, side by side,
        /// rows top to bottom, as a JPG at <paramref name="file"/>. Returns its path.</summary>
        private static string Sheet(List<List<string>> rows, string file, int w, int h)
        {
            int columns = Mathf.Max(1, rows.Max(r => r.Count));
            var sheet = new Texture2D(columns * w, rows.Count * h, TextureFormat.RGB24, false);
            sheet.SetPixels32(new Color32[columns * w * rows.Count * h]);
            RenderTexture small = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var picture = new Texture2D(2, 2) { filterMode = FilterMode.Trilinear };
            RenderTexture before = RenderTexture.active;
            try
            {
                for (int r = 0; r < rows.Count; r++)
                    for (int c = 0; c < rows[r].Count; c++)
                    {
                        if (!picture.LoadImage(File.ReadAllBytes(rows[r][c]))) continue;
                        Graphics.Blit(picture, small);
                        RenderTexture.active = small;
                        sheet.ReadPixels(new Rect(0, 0, w, h), c * w, (rows.Count - 1 - r) * h, false);
                    }
                RenderTexture.active = before;
                File.WriteAllBytes(file, sheet.EncodeToJPG(88));
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(small);
                Object.DestroyImmediate(picture);
                Object.DestroyImmediate(sheet);
            }
            return file;
        }

        /// <summary>
        /// One video a shot (1080p as filmed, H.264, 30 fps), made by ffmpeg in the background from a script written beside
        /// the frames (encode.cmd on Windows, encode.sh elsewhere): run it again to remake them. It leaves encoded.txt ("ok",
        /// or "failed" with ffmpeg's words in encode.log) when it is done.
        /// </summary>
        private static string Encode(string output, List<string> shots, int fps)
        {
            bool windows = Application.platform == RuntimePlatform.WindowsEditor;
            string scriptName = windows ? "encode.cmd" : "encode.sh";
            var script = new StringBuilder();
            if (windows)
            {
                script.AppendLine("@echo off");
                script.AppendLine("rem The take's videos (D-0242): one a shot, from its frames. Made by the footage recorder; run it again to remake them.");
                script.AppendLine("cd /d \"%~dp0\"");
                script.AppendLine("set FAILED=0");
            }
            else
            {
                script.AppendLine("#!/bin/sh");
                script.AppendLine("# The take's videos (D-0242): one a shot, from its frames. Made by the footage recorder; run it again to remake them.");
                script.AppendLine("cd \"$(dirname \"$0\")\"");
                script.AppendLine("FAILED=0");
            }
            foreach (string shot in shots)
            {
                string name = Path.GetFileName(shot);
                string input = windows ? $"frames\\{name}\\frame_%%05d.jpg" : $"frames/{name}/frame_%05d.jpg";
                string args = $"-y -loglevel error -framerate {fps} -i \"{input}\" -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart \"{name}.mp4\"";
                script.AppendLine(windows ? $"ffmpeg {args} 2>> encode.log || set FAILED=1" : $"ffmpeg {args} 2>> encode.log || FAILED=1");
            }
            script.AppendLine(windows
                ? "if \"%FAILED%\"==\"0\" (echo ok> encoded.txt) else (echo failed: see encode.log> encoded.txt)"
                : "if [ \"$FAILED\" = 0 ]; then echo ok > encoded.txt; else echo \"failed: see encode.log\" > encoded.txt; fi");
            string scriptPath = Path.Combine(output, scriptName);
            File.WriteAllText(scriptPath, script.ToString());
            try
            {
                // It runs on its own; the editor carries on (and from the command line, quits).
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(windows ? "cmd.exe" : "/bin/sh", windows ? "/c " + scriptName : scriptName)
                {
                    WorkingDirectory = output, UseShellExecute = false, CreateNoWindow = true,
                });
                return $"ffmpeg is making {shots.Count} videos in the background ({scriptName}; encoded.txt says ok when they are done)";
            }
            catch (Exception error)
            {
                return $"the videos were not started ({error.Message}): run {scriptPath} with ffmpeg on the PATH";
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

        private static bool ReadClip(string clipPath, out string full, out FootageClip clip, out string problem)
        {
            clip = null;
            problem = null;
            full = string.IsNullOrEmpty(clipPath) ? "" : Path.IsPathRooted(clipPath) ? clipPath : Path.Combine(ProjectRoot, clipPath);
            if (!File.Exists(full))
            {
                problem = $"Footage: no shot list at {full}.";
                return false;
            }
            clip = FootageClip.Parse(File.ReadAllText(full));
            string[] problems = clip.Problems();
            if (problems.Length == 0) return true;
            problem = $"Footage: {Path.GetFileName(full)}: " + string.Join("; ", problems) + ".";
            return false;
        }

        /// <summary>The clip's scene open (opened if another is; never over unsaved changes). <paramref name="left"/> is the
        /// scene that was open before, or null.</summary>
        private static bool OpenScene(string scenePath, out string left, out string problem)
        {
            left = null;
            problem = null;
            Scene open = SceneManager.GetActiveScene();
            if (open.path == scenePath) return true;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                problem = $"Footage: no scene at {scenePath}.";
                return false;
            }
            for (int s = 0; s < SceneManager.sceneCount; s++)
                if (SceneManager.GetSceneAt(s).isDirty)
                {
                    problem = $"Footage: save the open scene first (the take opens {scenePath}).";
                    return false;
                }
            left = open.path;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            return true;
        }
    }
}
#endif
