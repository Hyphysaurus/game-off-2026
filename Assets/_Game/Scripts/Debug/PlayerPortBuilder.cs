#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Player / Build Player Port: everything the player's port from the test room needs, in one run that can
    /// be repeated. (1) Copies of the Playroom characters' clips with the loop and root settings the game needs, kept
    /// under git-ignored _Imports (the pack's own files are not touched), plus a crouch idle made from the crouch walk's
    /// first frame with a slow breath in the hips, as the test room's was, and the jump's arc (Jump_Arc, D-0169): the old
    /// pack's running jump cut into one clip the motor shows at the point the jump is at. (2) Measures each body: the scale that puts
    /// its head at the player's head height, its palm, how fast each clip covers ground, how long the one-shot clips
    /// are, and when a punch and a kick are furthest out (on the bones). (3) Writes Resources/PlayerBody (measured
    /// numbers, rewritten every run; the leg-speed cap is kept) and Resources/PlayerMoves (made once, never overwritten).
    /// (4) Builds one Animator controller per body with the AnimatorController API. (5) In the four scenes: takes the
    /// old body off the Player, puts the Male and Female on, adds the move scripts, sets first person to V, and saves.
    /// Every step is checked; a failed check fails the run (exit code 1 in batch).
    /// Batch: -batchmode -quit -executeMethod ShiftJam.PlayerPortBuilder.Build
    /// <para>TWO MENUS, and only one of them touches scenes. "Build Player Port" (Build) is the full run above: it
    /// REWRITES SCENES. It opens Greybox, Workshop, Raceway and Colour Corner one after the other without asking to
    /// save what is open, changes the Player in each, saves them, and rewrites the body numbers. Started by hand it asks
    /// first. "Rebuild Clips + Controllers" (RebuildClipsAndControllers, D-0169) does NOT: it opens and saves no scene
    /// and writes no settings asset. It does steps (1) and (4) only, from the numbers Resources/PlayerBody already
    /// holds, checks the jump on the bones, and puts both controller files back if anything fails.</para>
    /// </summary>
    public static partial class PlayerPortBuilder
    {
        private const string Tag = "[PlayerPort]";
        private const string PackRoot = "Assets/_Imports/ithappy/Playroom/";
        private const string OldBodyPrefab = "Assets/_Imports/ithappy/Funny_Characters/Prefabs/Basic_Character.prefab";
        private const string CopyRoot = "Assets/_Imports/ShiftJam/PlayerClips";
        private const string AnimationFolder = "Assets/_Game/Animation/";
        private const string BodySettingsPath = "Assets/_Game/Resources/PlayerBody.asset";
        private const string MoveSettingsPath = "Assets/_Game/Resources/PlayerMoves.asset";
        private const string FirstPersonSettingsPath = "Assets/_Game/Data/FirstPersonSettings.asset";
        private const float PalmShare = 0.75f;          // the palm centre: this far from the wrist to the middle knuckle
        private const float CrouchIdleSeconds = 2.4f;   // one breath of the crouch idle, as the test room's
        private const float CrouchIdleBreath = 0.004f;  // how far the hips rise and fall in it, in the clip's own units
        private const float OldBodyHeight = 1.85f;      // only if the old body's prefab is gone: how tall it was drawn at toy size

        // The jump's arc (D-0169): one clip cut from the old pack's running jump, its time = the jump's phase.
        private const string ArcSources = "Assets/_Imports/ithappy/Funny_Characters/Animations/Animation_Clips/";
        private const string ArcStartClip = "Jump run start .anim";   // the pack's own file name: a space before .anim
        private const string ArcMiddleClip = "Jump run middle.anim";
        private const string ArcEndClip = "Jump run end.anim";
        private const float ArcSeconds = 1f;            // the arc clip's length: one second, so its time is the phase (0 take-off, 0.5 the top, 1 landing)
        private const float ArcRise = 0.35f;            // phase where the push-off clip ends and the hang begins
        private const float ArcFall = 0.65f;            // phase where the hang ends and the coming-down clip begins
        private const float StartFrom = 0.10f;          // seconds into "Jump run start" the arc begins at: the push-off, the foot still on the floor
        private const float MiddleSlice = 0.30f;        // seconds of "Jump run middle" the hang shows, from its start
        private const float EndUntil = 0.39f;           // seconds of "Jump run end" the arc shows: up to touch-down
        private const float ArcJoinEase = 0.5f;         // last share of the hang that eases into the coming-down clip's first pose (the hang is a loop: a slice of it does not end there); 0 = no easing
        private const int ArcSamplesPerSecond = 60;     // keys written per second of the arc (the source clips have 24)
        private const int ArcSmoothReach = 1;           // keys each side averaged into a key of the rewritten root height (1 = a 3-key average)
        private const int ArcSmoothPasses = 2;          // times that average is run, so the rewrite leaves no kink

        // The check of the jump (D-0169): the body is posed through the arc and read on its bones, own units of the body as the pack has it.
        private static readonly float[] CheckPhases = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        private const float CheckRise = 5f;             // VerticalVelocity given to the Animator: well over a jump's threshold, so it picks Air and not Fall
        private const float CheckSpeed = 1f;            // Speed given to it: a full run
        private const float CheckStep = 1f / 60f;       // seconds per Animator step while the blend into Air runs
        private const float CheckSettle = ToAir * 3f;   // seconds stepped before looking: three times the blend into Air
        private const float CheckNudge = 0.0001f;       // seconds stepped after AirPhase is set: enough for the Animator to pose, too short to move anything
        private const float FootLowest = -0.05f;        // the lowest foot bone under the body's origin by more than this: a foot through the floor
        private const float FootHighest = 0.5f;         // the lowest foot bone over it by more than this: the clips' own jump height is back and the body floats
        private const float HipsSwing = 0.25f;          // most the hips' height may differ between two phases (the motor lifts the body, not the clip)
        private const float SamePose = 0.02f;           // hands and feet all within this between two phases: the same pose, nothing follows the jump
        private const float YawLimit = 30f;             // degrees the pelvis may be turned off the body's facing

        private static readonly string[] Scenes =
        {
            "Assets/_Game/Scenes/Greybox.unity", "Assets/_Game/Scenes/Workshop.unity",
            "Assets/_Game/Scenes/Raceway.unity", "Assets/_Game/Scenes/ColourCorner.unity",
        };

        /// <summary>One body to build: its names and where the pack keeps it.</summary>
        private sealed class BodySpec
        {
            public string name, child, prefab, clips, controller;
            /// <summary>What the pack calls this character: its drawn parts' names start with it (the scene step checks the right one is on).</summary>
            public string pack;
        }

        private static readonly BodySpec[] Bodies =
        {
            // The pack's Character_02 is the man (beanie, yellow top), Character_01 the woman (hair bun, glasses, blue hoodie).
            new BodySpec { name = "Male", child = "Body_Male", pack = "Character_02", prefab = PackRoot + "Prefabs/Characters/Character_02.prefab", clips = PackRoot + "Animations/Other_Animations/Character_02/", controller = AnimationFolder + "PlayerMale.controller" },
            new BodySpec { name = "Female", child = "Body_Female", pack = "Character_01", prefab = PackRoot + "Prefabs/Characters/Character_01.prefab", clips = PackRoot + "Animations/Other_Animations/Character_01/", controller = AnimationFolder + "PlayerFemale.controller" },
        };

        /// <summary>How a copied clip is set up. Travel: the clip carries the body along, and that travel is left out (the
        /// motor moves the body). Feet: the height is taken from the feet (the pack's jump loop floats a body height up).</summary>
        private readonly struct ClipSpec
        {
            public readonly string name; public readonly bool loop, travel, feet;
            public ClipSpec(string name, bool loop, bool travel, bool feet = false) { this.name = name; this.loop = loop; this.travel = travel; this.feet = feet; }
        }

        private static readonly ClipSpec[] Clips =
        {
            new ClipSpec("Idle_Breathing", true, false), new ClipSpec("Idle_Look_Around", false, false),
            new ClipSpec("Walk_Forward", true, true), new ClipSpec("Walk_Backward", true, true),
            new ClipSpec("Strafe_Left", true, true), new ClipSpec("Strafe_Right", true, true),
            new ClipSpec("Run_Forward", true, true), new ClipSpec("Run_Backward", true, true),
            new ClipSpec("Run_Left", true, true), new ClipSpec("Run_Right", true, true),
            new ClipSpec("Crouch_Walk_Forward", true, true), new ClipSpec("Crouch_Walk_Backward", true, true),
            new ClipSpec("Crouch_Walk_Left", true, true), new ClipSpec("Crouch_Walk_Right", true, true),
            // Jump_Loop: the hang on a rope (the state RopeHang, D-0170) holds its first frame. Air has not used it since
            // D-0169: Air shows Jump_Arc, made in JumpArc below.
            new ClipSpec("Jump_Loop", true, false, true), new ClipSpec("Jump_End", false, false), new ClipSpec("Fall", true, false),
            new ClipSpec("Attack_Punch", false, false), new ClipSpec("Attack_Kick", false, false),
            new ClipSpec("Block_With_Hands", false, false), new ClipSpec("Dodge_Roll", false, true),
            new ClipSpec("Hit_Reaction_Light", false, false), new ClipSpec("Hit_Reaction_Heavy", false, false),
            new ClipSpec("Death_Forward", false, true),
        };

        /// <summary>What the narrow run must leave as it found them: read for the body numbers, never written. It will not
        /// start while one of them has unsaved changes, and it checks their bytes after.</summary>
        private static readonly string[] NeverWritten = { BodySettingsPath, MoveSettingsPath };

        private static int _failed;

        private static void Say(string what) => Debug.Log($"{Tag} {what}");

        private static bool Check(bool ok, string what)
        {
            if (ok) Debug.Log($"{Tag} ok: {what}");
            else { _failed++; Debug.LogError($"{Tag} FAIL: {what}"); }
            return ok;
        }

        /// <summary>
        /// The full run. It REWRITES SCENES: it opens Greybox, Workshop, Raceway and Colour Corner one after the other
        /// with no save prompt for what is open, changes the Player in each and saves them, and it rewrites the body
        /// numbers in Resources/PlayerBody. So it refuses in Play, and started by hand it asks first (in batch mode nobody
        /// can answer and no scene is open: it runs as before). For clips and controllers only, use
        /// RebuildClipsAndControllers below, which touches no scene.
        /// </summary>
        [MenuItem("Shift Jam/Player/Build Player Port (clips, bodies, controllers, scenes)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning($"{Tag} Build Player Port does not run in Play: stop the game first. Nothing was done.");
                return;
            }
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Build Player Port",
                    "This opens Greybox, Workshop, Raceway and Colour Corner one after the other WITHOUT asking to save what is open. "
                    + "It changes the Player in each of them and saves them, and it rewrites the body numbers (Resources/PlayerBody).\n\n"
                    + "For the clips and controllers only, cancel and use Rebuild Clips + Controllers: it touches no scene.",
                    "Do it", "Cancel"))
                return;
            _failed = 0;
            try
            {
                var settings = LoadOrCreate<PlayerBodySettings>(BodySettingsPath);
                LoadOrCreate<PlayerMoveSettings>(MoveSettingsPath);
                if (settings.bodyHeight <= 0f) settings.bodyHeight = MeasureOldBody();
                var bodies = new List<PlayerBodySettings.Body>();
                var controllers = new List<RuntimeAnimatorController>();
                foreach (BodySpec spec in Bodies)
                {
                    Dictionary<string, AnimationClip> clips = CopyClips(spec);
                    if (clips == null) continue;
                    // Both bodies share one skeleton: both get the fit of the first (a hat or tall hair must not resize the bones).
                    PlayerBodySettings.Body body = Measure(spec, clips, settings.bodyHeight, bodies.Count > 0 ? bodies[0].fit : 0f);
                    if (body == null) continue;
                    bodies.Add(body);
                    controllers.Add(BuildController(spec, clips, body));
                }
                if (Check(bodies.Count == Bodies.Length, $"{bodies.Count} of {Bodies.Length} bodies measured and given a controller"))
                {
                    settings.bodies = bodies.ToArray();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    foreach (string scene in Scenes) LandInScene(scene, settings, controllers);
                }
            }
            catch (Exception e)
            {
                _failed++;
                Debug.LogException(e);
            }
            AssetDatabase.SaveAssets();
            Say(_failed == 0 ? "DONE: every check passed" : $"DONE with {_failed} failed check(s)");
            if (Application.isBatchMode) EditorApplication.Exit(_failed > 0 ? 1 : 0);
        }

        /// <summary>
        /// Shift Jam / Player / Rebuild Clips + Controllers (D-0169): the narrow run. The full menu above rewrites scenes;
        /// THIS ONE DOES NOT. It opens no scene, saves no scene, and writes neither Resources/PlayerBody nor
        /// Resources/PlayerMoves. For both bodies it copies the clips again, makes the jump's arc clip, and rebuilds the
        /// controller in place (same file, same GUID) from the body numbers Resources/PlayerBody ALREADY holds; then it
        /// poses each body through the new jump in a preview scene and reads the bones. All or nothing: the bytes of both
        /// controller files are kept first and written back if any step or check fails. It shows no dialog and takes no
        /// arguments, so code can call it too.
        /// It refuses, with nothing changed, when one of those two settings files has unsaved changes: saving the
        /// controllers saves every unsaved asset in the project, and that would write the file this run must not write.
        /// (Other unsaved assets, a material or a prefab, are still saved along with the controllers.)
        /// </summary>
        [MenuItem("Shift Jam/Player/Rebuild Clips + Controllers (D-0169)")]
        public static void RebuildClipsAndControllers()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning($"{Tag} Rebuild Clips + Controllers does not run in Play: stop the game first. Nothing was done.");
                return;
            }
            _failed = 0;

            // Before anything is touched: both bodies' numbers and both controller files must be there.
            var settings = AssetDatabase.LoadAssetAtPath<PlayerBodySettings>(BodySettingsPath);
            var numbers = new PlayerBodySettings.Body[Bodies.Length];
            for (int i = 0; i < Bodies.Length; i++)
            {
                numbers[i] = StoredBody(settings, Bodies[i]);
                if (numbers[i] != null && File.Exists(Bodies[i].controller)) continue;
                Debug.LogError(numbers[i] == null
                    ? $"{Tag} {BodySettingsPath} holds no measured numbers for {Bodies[i].child}: the controllers cannot be rebuilt from it. Nothing was changed. (The full Build Player Port measures them; it rewrites scenes.)"
                    : $"{Tag} {Bodies[i].controller} is not there: this run only rebuilds a controller in place. Nothing was changed. (The full Build Player Port makes it; it rewrites scenes.)");
                return;
            }
            // Also before anything is touched: saving the controllers saves every unsaved asset in the project
            // (BuildController's SaveAssets). An unsaved edit in one of the two settings files would be written with them,
            // and the controllers built from numbers that are not on disk. So the run refuses instead.
            foreach (string path in NeverWritten)
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || !EditorUtility.IsDirty(asset)) continue;
                Debug.LogError($"{Tag} {path} has unsaved changes: save the project or undo them first. This run never writes that file, and saving the controllers would. Nothing was changed.");
                return;
            }

            var kept = new byte[Bodies.Length][];
            for (int i = 0; i < Bodies.Length; i++) kept[i] = File.ReadAllBytes(Bodies[i].controller);
            var before = new byte[NeverWritten.Length][];
            for (int k = 0; k < NeverWritten.Length; k++) before[k] = Bytes(NeverWritten[k]);
            bool[] unsaved = UnsavedScenes();

            var jumps = new List<string>();
            try
            {
                for (int i = 0; i < Bodies.Length && _failed == 0; i++)
                {
                    Dictionary<string, AnimationClip> clips = CopyClips(Bodies[i]);
                    if (clips == null) break;
                    RuntimeAnimatorController controller = BuildController(Bodies[i], clips, numbers[i]);
                    if (_failed > 0 || controller == null) break;
                    string jump = CheckJump(Bodies[i], controller);
                    if (jump != null) jumps.Add(jump);
                }
                AssetDatabase.SaveAssets();   // the clips (each controller was saved as it was built)

                Check(unsaved.AsSpan().SequenceEqual(UnsavedScenes()), "the open scenes' unsaved marks are as they were: no scene was opened, changed or saved");
                for (int k = 0; k < NeverWritten.Length; k++)
                    Check(Same(before[k], Bytes(NeverWritten[k])), $"{NeverWritten[k]} is byte for byte as it was");
            }
            catch (Exception e)
            {
                _failed++;
                Debug.LogException(e);
            }

            string who = string.Join(" and ", Array.ConvertAll(Bodies, b => b.name));
            string read = jumps.Count > 0 ? " The jump on the bones: " + string.Join(" ", jumps) : "";
            if (_failed == 0)
            {
                Say($"DONE (D-0169): clips copied, Jump_Arc made and controllers rebuilt in place for {who}; every check passed; no scene opened or saved, the body numbers read and not written.{read}");
                return;
            }
            string files = string.Join(" and ", Array.ConvertAll(Bodies, b => b.controller));
            Debug.LogError(PutBack(kept)
                ? $"{Tag} FAILED (D-0169) with {_failed} failed check(s), see the FAIL lines above: BOTH CONTROLLERS WERE PUT BACK as they were before this run ({files}). The clip copies were written again and Jump_Arc.anim stays in the clip folders; nothing uses it.{read}"
                : $"{Tag} FAILED (D-0169) with {_failed} failed check(s), see the FAIL lines above, and the controllers could NOT be put back ({files}): restore the two files from git before playing.{read}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>The numbers the full build measured and stored for a body: read here, never written. Null if the asset
        /// has none for it or they were never measured (a controller built from zeros would stand still).</summary>
        private static PlayerBodySettings.Body StoredBody(PlayerBodySettings settings, BodySpec spec)
        {
            if (settings == null || settings.bodies == null) return null;
            foreach (PlayerBodySettings.Body body in settings.bodies)
            {
                if (body == null || body.child != spec.child) continue;
                bool measured = body.walk > 0f && body.walkBack > 0f && body.strafe > 0f && body.run > 0f && body.runBack > 0f && body.runSide > 0f
                                && body.crouch > 0f && body.crouchBack > 0f && body.crouchSide > 0f && body.death > 0f;
                return measured ? body : null;
            }
            return null;
        }

        /// <summary>Writes the kept bytes of both controller files back and has Unity read them again. False if a file
        /// could not be written or does not read back the same.</summary>
        private static bool PutBack(byte[][] kept)
        {
            bool back = true;
            try
            {
                if (File.Exists(TempController)) AssetDatabase.DeleteAsset(TempController);   // a build that died half-way leaves it behind
                for (int i = 0; i < Bodies.Length; i++)
                {
                    File.WriteAllBytes(Bodies[i].controller, kept[i]);
                    AssetDatabase.ImportAsset(Bodies[i].controller, ImportAssetOptions.ForceUpdate);
                    back &= Same(kept[i], Bytes(Bodies[i].controller));
                }
            }
            catch (Exception e)
            {
                back = false;
                Debug.LogException(e);
            }
            return back;
        }

        /// <summary>A file's bytes, or null if it is not there.</summary>
        private static byte[] Bytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        private static bool Same(byte[] a, byte[] b) => a == null || b == null ? a == b : a.AsSpan().SequenceEqual(b);

        /// <summary>Whether each open scene has unsaved changes. Taken before and after the narrow run: they must match.</summary>
        private static bool[] UnsavedScenes()
        {
            var marks = new bool[SceneManager.sceneCount];
            for (int i = 0; i < marks.Length; i++) marks[i] = SceneManager.GetSceneAt(i).isDirty;
            return marks;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Say("made " + path + " with its defaults");
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---------- clips ----------

        /// <summary>Copies the body's clips out of the pack with the game's settings on each. Null if one is missing.</summary>
        private static Dictionary<string, AnimationClip> CopyClips(BodySpec spec)
        {
            string folder = CopyRoot + "/" + spec.name;
            EnsureFolder(folder);
            var made = new Dictionary<string, AnimationClip>();
            foreach (ClipSpec c in Clips)
            {
                var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.clips + c.name + ".anim");
                if (!Check(source != null, $"{spec.name}: the pack has {c.name}")) return null;
                string path = folder + "/" + c.name + ".anim";
                var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (copy == null) { copy = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(copy, path); }
                else EditorUtility.CopySerialized(source, copy);
                copy.name = c.name;
                Settle(copy, c.loop, c.travel, c.feet);
                made[c.name] = copy;
            }
            made["Crouch_Idle"] = CrouchIdle(made["Crouch_Walk_Forward"], folder + "/Crouch_Idle.anim");
            AnimationClip arc = JumpArc(spec.name, folder + "/Jump_Arc.anim");
            if (arc == null) return null;
            made["Jump_Arc"] = arc;
            return made;
        }

        /// <summary>
        /// The root settings of a copied clip. The motor moves and turns the body, so nothing of the clip may: the turn
        /// and the height are kept in the pose as the clip has them (a crouch is low, a death ends on the floor), and
        /// the travel of a walk, a roll or a fall forward is left out, which keeps the body over its feet.
        /// </summary>
        private static void Settle(AnimationClip clip, bool loop, bool travel, bool feet)
        {
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop;
            s.loopBlend = false;
            s.loopBlendOrientation = true; s.keepOriginalOrientation = true;
            s.loopBlendPositionY = true; s.keepOriginalPositionY = !feet; s.heightFromFeet = feet;
            s.loopBlendPositionXZ = !travel; s.keepOriginalPositionXZ = !travel;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            EditorUtility.SetDirty(clip);
        }

        /// <summary>The crouch idle: the crouch walk's first frame held, with a slow breath in the hips' height.</summary>
        private static AnimationClip CrouchIdle(AnimationClip walk, string path)
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (idle == null) { idle = new AnimationClip(); AssetDatabase.CreateAsset(idle, path); }
            idle.ClearCurves();
            idle.name = "Crouch_Idle";
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(walk))
            {
                float value = AnimationUtility.GetEditorCurve(walk, binding).Evaluate(0f);
                AnimationCurve curve = binding.propertyName == "RootT.y"
                    ? new AnimationCurve(new Keyframe(0f, value), new Keyframe(CrouchIdleSeconds * 0.5f, value + CrouchIdleBreath * 2f), new Keyframe(CrouchIdleSeconds, value))
                    : AnimationCurve.Constant(0f, CrouchIdleSeconds, value);
                AnimationUtility.SetEditorCurve(idle, binding, curve);
            }
            Settle(idle, loop: true, travel: false, feet: false);
            return idle;
        }

        /// <summary>
        /// The jump's arc (D-0169): one clip, one second long, that IS the jump from push-off to touch-down. The state Air
        /// shows it at the time the AirPhase parameter names, and the motor works that out from the jump's own speed, so
        /// the body is always at the point of the jump it is really at (before, Air held one frozen tuck all the way).
        /// Made curve by curve from the old pack's running jump: its start from the push-off on, the first of its hang,
        /// and its end up to touch-down. Three things are changed on the way.
        /// The root height: the source clips carry the jump's own height, and the motor already moves the body, so
        /// played as they are the body would float a body height over its capsule. It is rewritten to the standing
        /// height, and raised over it only where a foot would otherwise go under the floor (the push-off, the landing).
        /// The quaternions (the root's turn, the hand and foot goals): q and -q are the same turn, and a clip may begin
        /// on the other sign from where the last one ended; a part that does is negated whole, or the curve would swing
        /// through zero at the join.
        /// The end of the hang: the hang clip is a loop, and a slice of it does not end where the coming-down clip
        /// begins (legs and arms would snap at the join). Its last part eases into that clip's first pose.
        /// Null (with a failed check) if a source clip is missing or does not fit.
        /// </summary>
        private static AnimationClip JumpArc(string who, string path)
        {
            string[] files = { ArcStartClip, ArcMiddleClip, ArcEndClip };
            var parts = new AnimationClip[files.Length];
            for (int p = 0; p < files.Length; p++)
            {
                parts[p] = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArcSources + files[p]);
                if (!Check(parts[p] != null, $"{who}: the old pack has '{files[p]}' (the jump's arc is cut from it)")) return null;
            }

            // Key i of the arc: 0..rise from the push-off clip, on to fall from the hang, the rest from the coming-down clip.
            int last = Mathf.RoundToInt(ArcSeconds * ArcSamplesPerSecond), rise = Mathf.RoundToInt(ArcRise * last), fall = Mathf.RoundToInt(ArcFall * last);
            bool fits = rise > 0 && fall > rise && last > fall && StartFrom < parts[0].length && MiddleSlice <= parts[1].length && EndUntil <= parts[2].length;
            if (!Check(fits, $"{who}: the arc's three parts fit their clips (push-off {StartFrom} s on of {parts[0].length:F3} s, {MiddleSlice} s of the {parts[1].length:F3} s hang, coming down to {EndUntil} s of {parts[2].length:F3} s)")) return null;

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(parts[0]);
            var curves = new AnimationCurve[parts.Length][];
            var at = new Dictionary<string, int>();
            for (int b = 0; b < bindings.Length; b++) at[bindings[b].path + bindings[b].propertyName] = b;
            int rootY = -1, leftY = -1, rightY = -1;
            bool same = at.TryGetValue("RootT.y", out rootY) && at.TryGetValue("LeftFootT.y", out leftY) && at.TryGetValue("RightFootT.y", out rightY);
            for (int p = 0; p < parts.Length && same; p++)
            {
                same = AnimationUtility.GetCurveBindings(parts[p]).Length == bindings.Length;
                curves[p] = new AnimationCurve[bindings.Length];
                for (int b = 0; b < bindings.Length && same; b++) same = (curves[p][b] = AnimationUtility.GetEditorCurve(parts[p], bindings[b])) != null;
            }
            if (!Check(same, $"{who}: the three jump clips move the same {bindings.Length} things, the root's height and both feet among them")) return null;

            var values = new float[bindings.Length][];
            var landing = new float[bindings.Length];   // the coming-down clip's first pose: what the hang eases into
            for (int b = 0; b < bindings.Length; b++)
            {
                values[b] = new float[last + 1];
                landing[b] = curves[2][b].Evaluate(0f);
                for (int i = 0; i <= last; i++)
                {
                    int p = i <= rise ? 0 : i <= fall ? 1 : 2;
                    float t = p == 0 ? Mathf.Lerp(StartFrom, parts[0].length, i / (float)rise)
                        : p == 1 ? Mathf.Lerp(0f, MiddleSlice, (i - rise) / (float)(fall - rise))
                        : Mathf.Lerp(0f, EndUntil, (i - fall) / (float)(last - fall));
                    values[b][i] = curves[p][b].Evaluate(t);
                }
            }

            // Quaternions, each as its four curves. At a join: if the part coming in begins on the other sign from the key
            // written last, the whole part is negated (and with the last part, the pose the hang eases into).
            var quaternions = new List<int[]>();
            foreach (KeyValuePair<string, int> x in at)
            {
                if (!x.Key.EndsWith("Q.x", StringComparison.Ordinal)) continue;
                string stem = x.Key.Substring(0, x.Key.Length - 1);
                if (at.TryGetValue(stem + "y", out int y) && at.TryGetValue(stem + "z", out int z) && at.TryGetValue(stem + "w", out int w))
                    quaternions.Add(new[] { x.Value, y, z, w });
            }
            int turned = 0;
            foreach (int[] q in quaternions)
            {
                foreach ((int first, int end) in new[] { (rise + 1, fall), (fall + 1, last) })
                {
                    float dot = 0f;
                    foreach (int b in q) dot += values[b][first] * values[b][first - 1];
                    if (dot >= 0f) continue;
                    turned++;
                    foreach (int b in q)
                    {
                        for (int i = first; i <= end; i++) values[b][i] = -values[b][i];
                        if (end == last) landing[b] = -landing[b];
                    }
                }
            }

            // The hang's last part eases into the coming-down clip's first pose, so the two meet with no step.
            float gap = 0f;
            for (int b = 0; b < bindings.Length; b++) gap = Mathf.Max(gap, Mathf.Abs(values[b][fall] - landing[b]));
            int easeFrom = fall - Mathf.RoundToInt((fall - rise) * ArcJoinEase);
            var lengths = new float[quaternions.Count];
            for (int i = easeFrom + 1; i <= fall; i++)
            {
                float share = Mathf.SmoothStep(0f, 1f, (i - easeFrom) / (float)(fall - easeFrom));
                for (int g = 0; g < lengths.Length; g++)
                {
                    float from = 0f, to = 0f;
                    foreach (int b in quaternions[g]) { from += values[b][i] * values[b][i]; to += landing[b] * landing[b]; }
                    lengths[g] = Mathf.Lerp(Mathf.Sqrt(from), Mathf.Sqrt(to), share);
                }
                for (int b = 0; b < bindings.Length; b++) values[b][i] = Mathf.Lerp(values[b][i], landing[b], share);
                // A mix of two quaternions is shorter than either: each is brought back to the length it should have.
                for (int g = 0; g < lengths.Length; g++)
                {
                    float mixed = 0f;
                    foreach (int b in quaternions[g]) mixed += values[b][i] * values[b][i];
                    if (mixed <= Mathf.Epsilon) continue;
                    foreach (int b in quaternions[g]) values[b][i] *= lengths[g] / Mathf.Sqrt(mixed);
                }
            }

            // The root height: standing height (the push-off clip's first frame), and no lower than what keeps the lower
            // foot on the floor that frame stands on. The foot curves are measured from the root, so root + foot = the
            // foot's height. Then smoothed lightly, so the "no lower than" leaves no kink.
            float standing = curves[0][rootY].Evaluate(0f);
            float floor = standing + Mathf.Min(curves[0][leftY].Evaluate(0f), curves[0][rightY].Evaluate(0f));
            float[] height = values[rootY];
            float carried = Mathf.Max(height);
            for (int i = 0; i <= last; i++) height[i] = Mathf.Max(standing, floor - Mathf.Min(values[leftY][i], values[rightY][i]));
            var rough = new float[last + 1];
            for (int pass = 0; pass < ArcSmoothPasses; pass++)
            {
                Array.Copy(height, rough, rough.Length);
                for (int i = 0; i <= last; i++)
                {
                    float sum = 0f;
                    for (int k = -ArcSmoothReach; k <= ArcSmoothReach; k++) sum += rough[Mathf.Clamp(i + k, 0, last)];
                    height[i] = sum / (2 * ArcSmoothReach + 1);
                }
            }

            // Written with straight lines between the keys: they are dense (more than two for each of the source's), and
            // a straight line cannot overshoot.
            var built = new AnimationClip { name = "Jump_Arc", frameRate = ArcSamplesPerSecond };
            var keys = new Keyframe[last + 1];
            float step = ArcSeconds / last;
            for (int b = 0; b < bindings.Length; b++)
            {
                float[] v = values[b];
                for (int i = 0; i <= last; i++)
                {
                    int a = Mathf.Max(i, 1), z = Mathf.Min(i, last - 1);
                    keys[i] = new Keyframe(ArcSeconds * i / last, v[i], (v[a] - v[a - 1]) / step, (v[z + 1] - v[z]) / step);
                }
                AnimationUtility.SetEditorCurve(built, bindings[b], new AnimationCurve(keys));
            }

            // Over the old file when there is one, as the copies are: its GUID holds and the controllers keep pointing at it.
            var arc = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (arc == null) { AssetDatabase.CreateAsset(built, path); arc = built; }
            else { EditorUtility.CopySerialized(built, arc); UnityEngine.Object.DestroyImmediate(built); }
            arc.name = "Jump_Arc";
            // No loop. The height stays in the pose as rewritten above. The travel is left out, as for the runs (the motor
            // moves the body). The turn stays as the clips have it: it is the stride's own twist.
            Settle(arc, loop: false, travel: true, feet: false);
            AnimationClipSettings range = AnimationUtility.GetAnimationClipSettings(arc);
            range.startTime = 0f; range.stopTime = ArcSeconds;
            AnimationUtility.SetAnimationClipSettings(arc, range);
            EditorUtility.SetDirty(arc);

            bool whole = Mathf.Abs(arc.length - ArcSeconds) < step && !arc.isLooping && arc.events.Length == 0 && AnimationUtility.GetCurveBindings(arc).Length == bindings.Length;
            return Check(whole, $"{who}: Jump_Arc is {arc.length:F2} s, {bindings.Length} curves of {last + 1} keys, no loop, no events; root height {Mathf.Min(height):F3}..{Mathf.Max(height):F3} (the source clips carry it up to {carried:F3}); {turned} quaternion part(s) negated to keep their sign across a join; the hang eases into the landing over a gap of {gap:F2}")
                ? arc : null;
        }

        // ---------- the check of the jump ----------

        /// <summary>
        /// Poses a body through the rebuilt jump and reads it on the bones (D-0169): the pack's own prefab with the new
        /// controller, off the ground and rising so the controller picks Air, then AirPhase set to each of the check's
        /// phases. A failed check here means the jump would look wrong in play: a foot through the floor, the body
        /// floating a jump's height over its capsule, the hips pumping, a pose that does not follow the phase (the frozen
        /// jump is back), or a pelvis turned away from where the body faces. The body stands in a preview scene: an object
        /// made in the open scene, even a hidden one, can mark that scene as changed. Returns the numbers for the
        /// summary; null if the body could not be posed at all (with a failed check).
        /// </summary>
        private static string CheckJump(BodySpec spec, RuntimeAnimatorController controller)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefab);
            if (!Check(prefab != null, $"{spec.name}: the pack has {spec.prefab}")) return null;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = PrefabUtility.InstantiatePrefab(prefab, preview) as GameObject;
                Animator animator = go != null ? go.GetComponentInChildren<Animator>(true) : null;
                if (!Check(animator != null && animator.avatar != null && animator.avatar.isHuman, $"{spec.name}: the body stands in a preview scene with its humanoid Animator")) return null;
                Transform root = animator.transform, hips = Bone(animator, HumanBodyBones.Hips);
                Transform leftLeg = Bone(animator, HumanBodyBones.LeftUpperLeg), rightLeg = Bone(animator, HumanBodyBones.RightUpperLeg);
                // the feet first: the lowest of the first two is "the lowest foot"
                Transform[] ends = { Bone(animator, HumanBodyBones.LeftFoot), Bone(animator, HumanBodyBones.RightFoot), Bone(animator, HumanBodyBones.LeftHand), Bone(animator, HumanBodyBones.RightHand) };
                bool bones = hips != null && leftLeg != null && rightLeg != null && Array.TrueForAll(ends, e => e != null);
                if (!Check(bones, $"{spec.name}: hips, upper legs, feet and hands found on the body")) return null;

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // nothing draws a preview scene: a culled Animator would move no bone
                animator.enabled = true;
                animator.Rebind();
                animator.SetBool("Grounded", false);
                animator.SetFloat("VerticalVelocity", CheckRise);
                animator.SetFloat("Speed", CheckSpeed);
                animator.Update(0f);
                for (float t = 0f; t < CheckSettle; t += CheckStep) animator.Update(CheckStep);
                bool inAir = animator.GetCurrentAnimatorStateInfo(0).IsName("Air") && !animator.IsInTransition(0);
                if (!Check(inAir, $"{spec.name}: off the ground and rising, the controller is in Air after {CheckSettle:F2} s")) return null;

                int n = CheckPhases.Length;
                var foot = new float[n]; var hip = new float[n]; var yaw = new float[n];
                var pose = new Vector3[n][];
                for (int p = 0; p < n; p++)
                {
                    animator.SetFloat(AirPhaseParam, CheckPhases[p]);
                    animator.Update(CheckNudge);
                    pose[p] = Array.ConvertAll(ends, e => root.InverseTransformPoint(e.position));
                    foot[p] = Mathf.Min(pose[p][0].y, pose[p][1].y);
                    hip[p] = root.InverseTransformPoint(hips.position).y;
                    Vector3 across = root.InverseTransformPoint(rightLeg.position) - root.InverseTransformPoint(leftLeg.position);
                    across.y = 0f;
                    yaw[p] = Vector3.SignedAngle(Vector3.right, across, Vector3.up);
                }

                bool feet = true, facing = true;
                float least = float.PositiveInfinity;   // the least any two neighbouring phases differ
                for (int p = 0; p < n; p++)
                {
                    feet &= foot[p] >= FootLowest && foot[p] <= FootHighest;
                    facing &= Mathf.Abs(yaw[p]) <= YawLimit;
                    if (p > 0) least = Mathf.Min(least, Apart(pose[p - 1], pose[p]));
                }
                bool follows = least >= SamePose;
                float swing = Mathf.Max(hip) - Mathf.Min(hip), wrap = Apart(pose[0], pose[n - 1]);
                string phases = Row(CheckPhases, "0.##");
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Air"), $"{spec.name}: still in Air after the phases were set");
                Check(feet, $"{spec.name}: the lowest foot bone stays {FootLowest}..{FootHighest} over the body's origin at AirPhase {phases}: {Row(foot, "F2")}");
                Check(swing <= HipsSwing, $"{spec.name}: the hips' height differs by {swing:F2} at most between phases (limit {HipsSwing}): {Row(hip, "F2")}");
                Check(follows, $"{spec.name}: the pose follows the phase: hands and feet move at least {least:F2} between neighbouring phases (under {SamePose} = the same pose)");
                Check(wrap >= SamePose, $"{spec.name}: phase 1 is not phase 0 again (hands and feet {wrap:F2} apart): the clip does not wrap");
                Check(facing, $"{spec.name}: the pelvis stays within {YawLimit} degrees of the body's facing: {Row(yaw, "F0")}");
                return $"{spec.name} at AirPhase {phases}: lowest foot {Row(foot, "F2")}, hips {Row(hip, "F2")}, pelvis yaw {Row(yaw, "F0")}.";
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        /// <summary>How far apart two poses are: the most any one of their points moved.</summary>
        private static float Apart(Vector3[] a, Vector3[] b)
        {
            float most = 0f;
            for (int i = 0; i < a.Length; i++) most = Mathf.Max(most, Vector3.Distance(a[i], b[i]));
            return most;
        }

        private static string Row(float[] values, string format) => string.Join(" / ", Array.ConvertAll(values, v => v.ToString(format)));

        // ---------- measuring ----------

        /// <summary>How tall the body the player had before is drawn at toy size (soles to top): what the new bodies are fitted to.</summary>
        private static float MeasureOldBody()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OldBodyPrefab);
            if (prefab == null) { Say($"the old body's prefab is gone: fitting to a height of {OldBodyHeight}"); return OldBodyHeight; }
            var go = UnityEngine.Object.Instantiate(prefab);
            try
            {
                if (!Drawn(go, out float sole, out float top) || top - sole < 0.5f) { Say($"could not measure the old body: fitting to {OldBodyHeight}"); return OldBodyHeight; }
                Say($"the old body is drawn {top - sole:F3} tall (soles {sole:F3}, top {top:F3}): the new bodies are fitted to that");
                return top - sole;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>The lowest and highest drawn point of a body as it stands in its rest pose, from its skinned meshes'
        /// own vertices (a renderer's bounds are looser than the mesh).</summary>
        private static bool Drawn(GameObject go, out float sole, out float top)
        {
            sole = float.PositiveInfinity; top = float.NegativeInfinity;
            var baked = new Mesh();
            var points = new List<Vector3>();
            foreach (SkinnedMeshRenderer skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin.sharedMesh == null) continue;
                skin.BakeMesh(baked, true);
                baked.GetVertices(points);
                Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
                foreach (Vector3 p in points)
                {
                    float y = toWorld.MultiplyPoint3x4(p).y - go.transform.position.y;
                    sole = Mathf.Min(sole, y); top = Mathf.Max(top, y);
                }
            }
            UnityEngine.Object.DestroyImmediate(baked);
            return top > sole;
        }

        /// <summary>A humanoid bone, by the Animator when it answers outside Play, else by the avatar's own bone names.</summary>
        private static Transform Bone(Animator animator, HumanBodyBones bone)
        {
            if (animator == null || animator.avatar == null) return null;
            Transform found = animator.GetBoneTransform(bone);
            if (found != null) return found;
            string human = HumanTrait.BoneName[(int)bone];
            foreach (HumanBone map in animator.avatar.humanDescription.human)
            {
                if (map.humanName != human) continue;
                foreach (Transform t in animator.GetComponentsInChildren<Transform>(true)) if (t.name == map.boneName) return t;
            }
            return null;
        }

        private static float Travel(AnimationClip clip)
        {
            AnimationCurve x = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"));
            AnimationCurve z = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z"));
            if (x == null || z == null || clip.length <= 0f) return 0f;
            float dx = x.Evaluate(clip.length) - x.Evaluate(0f), dz = z.Evaluate(clip.length) - z.Evaluate(0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Fits one body and measures its clips. Null (with a failed check) if it cannot be measured.</summary>
        private static PlayerBodySettings.Body Measure(BodySpec spec, Dictionary<string, AnimationClip> clips, float bodyHeight, float fit)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefab);
            if (!Check(prefab != null, $"{spec.name}: the pack has {spec.prefab}")) return null;
            var go = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var animator = go.GetComponentInChildren<Animator>(true);
                if (!Check(animator != null && animator.avatar != null && animator.avatar.isHuman, $"{spec.name}: the body has a humanoid Animator")) return null;
                Transform hand = Bone(animator, HumanBodyBones.RightHand), knuckle = Bone(animator, HumanBodyBones.RightMiddleProximal);
                if (!Check(hand != null, $"{spec.name}: right hand bone found")) return null;
                if (!Check(Drawn(go, out float sole, out float top) && top - sole > 0.5f, $"{spec.name}: drawn {top - sole:F3} tall as the pack has it (soles {sole:F3}, top {top:F3})")) return null;

                var body = new PlayerBodySettings.Body { name = spec.name, child = spec.child, fit = fit > 0f ? fit : bodyHeight / (top - sole) };
                body.lift = -sole * body.fit;
                body.palm = knuckle != null ? hand.InverseTransformPoint(knuckle.position) * PalmShare : Vector3.zero;
                Check(knuckle != null, $"{spec.name}: palm at {body.palm.x:F3}, {body.palm.y:F3}, {body.palm.z:F3} in the hand bone's space");

                // A clip's travel is in the avatar's own units: times the avatar's scale and the fit it is world units.
                float units = animator.humanScale * body.fit;
                Check(animator.humanScale > 0.3f && animator.humanScale < 3f, $"{spec.name}: the avatar's human scale is {animator.humanScale:F3}, fit {body.fit:F3}");
                float Speed(string clip) => Travel(clips[clip]) / clips[clip].length * units;
                body.walk = Speed("Walk_Forward"); body.walkBack = Speed("Walk_Backward"); body.strafe = (Speed("Strafe_Left") + Speed("Strafe_Right")) * 0.5f;
                body.run = Speed("Run_Forward"); body.runBack = Speed("Run_Backward"); body.runSide = (Speed("Run_Left") + Speed("Run_Right")) * 0.5f;
                body.crouch = Speed("Crouch_Walk_Forward"); body.crouchBack = Speed("Crouch_Walk_Backward");
                body.crouchSide = (Speed("Crouch_Walk_Left") + Speed("Crouch_Walk_Right")) * 0.5f;
                Check(body.walk > 0.3f && body.run > body.walk && body.crouch > 0.3f,
                    $"{spec.name}: ground speed at 1x: walk {body.walk:F2}, back {body.walkBack:F2}, strafe {body.strafe:F2}; run {body.run:F2}, back {body.runBack:F2}, side {body.runSide:F2}; crouch {body.crouch:F2}, back {body.crouchBack:F2}, side {body.crouchSide:F2}");

                body.roll = clips["Dodge_Roll"].length; body.punch = clips["Attack_Punch"].length; body.kick = clips["Attack_Kick"].length;
                body.block = clips["Block_With_Hands"].length; body.hitLight = clips["Hit_Reaction_Light"].length;
                body.hitHeavy = clips["Hit_Reaction_Heavy"].length; body.death = clips["Death_Forward"].length; body.fidget = clips["Idle_Look_Around"].length;

                body.punchContact = Contact(spec, animator, clips["Attack_Punch"], "punch", HumanBodyBones.RightHand, HumanBodyBones.LeftHand, "HandT.z");
                body.kickContact = Contact(spec, animator, clips["Attack_Kick"], "kick", HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot, "FootT.z");
                return body;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// The share of a strike clip at which the hand or foot is furthest ahead of the hips, read on the bones by posing
        /// the body through the clip. If posing does nothing outside Play, the clip's own hand and foot curves are read.
        /// </summary>
        private static float Contact(BodySpec spec, Animator animator, AnimationClip clip, string what, HumanBodyBones right, HumanBodyBones left, string curve)
        {
            const int steps = 120;
            Transform hips = Bone(animator, HumanBodyBones.Hips), a = Bone(animator, right), b = Bone(animator, left);
            float best = float.NegativeInfinity, least = float.PositiveInfinity, at = 0f;
            if (hips != null && a != null && b != null)
            {
                Vector3 forward = animator.transform.forward;
                for (int i = 0; i <= steps; i++)
                {
                    float t = clip.length * i / steps;
                    clip.SampleAnimation(animator.gameObject, t);
                    float reach = Mathf.Max(Vector3.Dot(a.position - hips.position, forward), Vector3.Dot(b.position - hips.position, forward));
                    if (reach > best) { best = reach; at = t; }
                    least = Mathf.Min(least, reach);
                }
            }
            float height = Mathf.Max(0.1f, animator.humanScale);
            if (best - least > 0.05f * height)
            {
                Say($"{spec.name}: the {what} is furthest out {best:F2} ahead of the hips at {at:F2} s of {clip.length:F2} s ({at / clip.length:F2} of the clip), read on the bones");
                return at / clip.length;
            }
            // Posing did not move the bones: read the clip's own goal curves (the hand or foot against the body).
            best = float.NegativeInfinity;
            foreach (string side in new[] { "Right", "Left" })
            {
                AnimationCurve c = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), side + curve));
                if (c == null) continue;
                for (int i = 0; i <= steps; i++)
                {
                    float t = clip.length * i / steps, v = c.Evaluate(t);
                    if (v > best) { best = v; at = t; }
                }
            }
            Check(best > float.NegativeInfinity, $"{spec.name}: the {what} is furthest out at {at:F2} s of {clip.length:F2} s ({at / clip.length:F2}), read from the clip's curves (posing outside Play moved nothing)");
            return clip.length > 0f ? at / clip.length : 0.5f;
        }
    }
}
#endif
