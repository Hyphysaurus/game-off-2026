#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShiftJam
{
    /// <summary>
    /// Editor makers for the house characters (D-0185). Two menus:
    /// "Build House Character (D-0185)" (batch: -executeMethod ShiftJam.HouseCharacterPieces.BuildAll) does, in order:
    ///  1. MakeClips: the idle and the look-round are the player's own copies, used where they are (PlayerPortBuilder
    ///     makes them); the wave and the talking gesture are copied from the Christmas pack into the git-ignored
    ///     _Imports/ShiftJam/CharacterClips (loop off, set here), refreshed in place on every run. A clip that is missing
    ///     is named in the log and its state is left out.
    ///  2. MakeController: Animation/HouseCharacter.controller, rebuilt on every run over the old file (it keeps its id):
    ///     Idle, and LookAround, Wave and Talk each started by a trigger of its name and going back to Idle when it ends.
    ///  3. MakePrefab: Prefabs/Characters/HouseCharacter, a variant of FunnyBody with HouseCharacter, a solid capsule, the
    ///     controller on the rig's Animator and that Animator culled off screen. Made once in a scene of its own (the open
    ///     scene never holds it); later runs only bring the controller, the culling and the capsule up to date, never the
    ///     wording or numbers set on it.
    ///  It ends with one line, "HouseCharacterPieces: built ..." (or "... NOT built ...").
    /// "Place House Characters (D-0185)" (Place) stands characters in the ACTIVE scene under one root, HouseCharacters:
    /// one by each hub door of its table, and the talking toy Gap where the bare figure GapNpc stood. Undoable, refused in
    /// Play, it saves nothing; it ends with "HouseCharacterPieces: placed N ...".
    /// The packs are git-ignored: the clip copies sit in an ignored folder and the tracked controller only points at them.
    /// </summary>
    public static class HouseCharacterPieces
    {
        private const string Tag = "[HouseCharacters]";

        // ---- the clips
        private const string IdleClipPath = "Assets/_Imports/ShiftJam/PlayerClips/Male/Idle_Breathing.anim";
        private const string LookAroundClipPath = "Assets/_Imports/ShiftJam/PlayerClips/Male/Idle_Look_Around.anim";
        private const string GestureSources = "Assets/ithappy/Christmas_Pack_FREE/Animations/Other_Animations/";
        private const string WaveSource = GestureSources + "Santa_WaveHello.anim";
        private const string TalkSource = GestureSources + "Santa_TalkGestureActive.anim";
        private const string ClipCopies = "Assets/_Imports/ShiftJam/CharacterClips";

        // ---- the controller
        private const string AnimationDir = "Assets/_Game/Animation";
        /// <summary>Where the house character's Animator controller is written.</summary>
        public const string ControllerPath = AnimationDir + "/HouseCharacter.controller";
        private const string TempController = AnimationDir + "/_HouseCharacterTemp.controller";
        private const float Blend = 0.2f;          // seconds of blend into and out of every state
        private const float WaveSeconds = 3.5f;    // the pack's wave is 7 s (arm up at 0.3 s, down by 6.5 s): played whole, faster
        private const float TalkSeconds = 4f;      // the talking gesture is 20 s: its first 4 s are played, then back to idle
        private const float EarliestExit = 0.05f;  // a one-shot never leaves before this share of its clip

        // ---- the prefab
        private const string PrefabDir = "Assets/_Game/Prefabs/Characters";
        /// <summary>Where the house character prefab (a variant of FunnyBody) is made.</summary>
        public const string PrefabPath = PrefabDir + "/HouseCharacter.prefab";
        private const string PrefabName = "HouseCharacter";

        // ---- placing
        /// <summary>The one root the placed characters live under in a scene.</summary>
        public const string RootName = "HouseCharacters";
        private const string GapFigureName = "GapNpc";   // the pack's bare figure CourseBuilder.Downstairs used to stand by the gap
        private const string GapHintName = "GapHint";
        private const string GapName = "Gap";
        private const int GapSeed = 3;
        private const string PlayerName = "Player";
        private const float FallbackToyHeight = 1.8f;   // the Player prefab's capsule at toy size
        private const float FallbackHeadTop = 1.75f;    // a dressed figure without a hat, at scale 1 (CHARS_CONTRACT FACTS)
        // Beside a door, in toy heights: past the door's edge, a step into the room, the floor looked for below.
        private const float DoorPerTrigger = 1.25f;     // a hub door's trigger is 0.8 of the door's width (CourseBuilder.HubDoorAt)
        private const float SideOffset = 0.8f;
        // How far into the room from the door's line a character may stand, in toy heights, nearest first: a door's own
        // dressing (the cones each side of the garage door) can take the nearest spots.
        private static readonly float[] StepsIn = { 0.6f, 1.4f, 2.2f, 3f };
        private const float BodyRadius = 0.25f;
        private const float RayAbove = 1.5f, RayBelow = 1.5f;
        private const float MaxStep = 0.3f;             // the floor found may be this much above or below the door's foot
        private const float FloorClear = 0.1f;          // the open-ground capsule starts this far above the floor
        private const float GapRayAbove = 1f;           // the gap's floor is looked for from this many toy heights up

        /// <summary>A character by a hub door: the door's Id, its name and lines (placeholder wording, his to change), the
        /// seed and colour word of its look, and the side of the door it prefers (+1 its right looking into the room,
        /// -1 its left; the other side is tried when that one is not open ground).</summary>
        private sealed class DoorCharacter
        {
            public string door, name, colour;
            public string[] lines;
            public int seed, side;
        }

        private static readonly DoorCharacter[] Doors =
        {
            new DoorCharacter
            {
                door = "playroom.raceway", name = "Pit", seed = 5, colour = "Red", side = 1,
                lines = new[] { "The Raceway is through this door.", "Toy cars, a track and a finish line.", "The gate by the start line brings you back." },
            },
            new DoorCharacter
            {
                door = "playroom.colours", name = "Dab", seed = 8, colour = "", side = -1,
                lines = new[] { "The Colour Corner is through this door.", "Paint balls, and walls that want colour." },
            },
        };

        /// <summary>The clips the controller is made from; any may be null (missing), the controller leaves its state out.</summary>
        public struct Clips
        {
            public AnimationClip idle, lookAround, wave, talk;
        }

        // =====================================================================================
        // Build

        /// <summary>The three steps in order (see the class notes) and one closing line. Refused in Play.
        /// Batch: -executeMethod ShiftJam.HouseCharacterPieces.BuildAll</summary>
        [MenuItem("Shift Jam/Characters/Build House Character (D-0185)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("HouseCharacterPieces: NOT built: stop Play first; nothing was changed.");
                return;
            }
            Clips clips = MakeClips();
            AnimatorController controller = MakeController(clips);
            GameObject prefab = MakePrefab(controller);
            if (prefab == null)
            {
                Debug.LogError("HouseCharacterPieces: NOT built: the prefab could not be made (see the error above).");
                return;
            }
            var animator = prefab.GetComponentInChildren<Animator>(true);
            RuntimeAnimatorController on = animator != null ? animator.runtimeAnimatorController : null;
            Debug.Log("HouseCharacterPieces: built " + PrefabPath + " (controller " + (on != null ? AssetDatabase.GetAssetPath(on) + ": " + StateList(on as AnimatorController) : "NONE") + ").");
        }

        // ---------- 1. the clips ----------

        /// <summary>The four clips: the player's idle and look-round as they are, the wave and the talk copied (see the class
        /// notes). Each one missing is logged and comes back null.</summary>
        public static Clips MakeClips()
        {
            var clips = new Clips
            {
                idle = Existing(IdleClipPath, "idle"),
                lookAround = Existing(LookAroundClipPath, "look-round"),
                wave = CopyGesture(WaveSource, "wave"),
                talk = CopyGesture(TalkSource, "talking gesture"),
            };
            AssetDatabase.SaveAssets();
            return clips;
        }

        /// <summary>One of the player's clip copies, where it is; null (logged) when it is missing or not humanoid.</summary>
        private static AnimationClip Existing(string path, string what)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                Debug.LogWarning(Tag + " no " + what + " clip at " + path + " (the player's clip copies, made by PlayerPortBuilder): its state is left out.");
                return null;
            }
            if (!clip.humanMotion)
            {
                Debug.LogWarning(Tag + " " + path + " is not a humanoid clip, so it cannot play on the funny rig: the " + what + " state is left out.");
                return null;
            }
            return clip;
        }

        /// <summary>Copies a pack clip into CharacterClips (made, or refreshed in place so it keeps its id), loop off and its
        /// root kept in the pose as PlayerPortBuilder settles the player's. Null (logged) when the source is missing.</summary>
        private static AnimationClip CopyGesture(string source, string what)
        {
            var from = AssetDatabase.LoadAssetAtPath<AnimationClip>(source);
            if (from == null)
            {
                Debug.LogWarning(Tag + " no " + what + " clip at " + source + " (the free Christmas pack is not in this copy): its state is left out.");
                return null;
            }
            if (!from.humanMotion)
            {
                Debug.LogWarning(Tag + " " + source + " is not a humanoid clip, so it cannot play on the funny rig: the " + what + " state is left out.");
                return null;
            }
            WorkshopPieces.EnsureFolder(ClipCopies);
            string path = ClipCopies + "/" + Path.GetFileName(source);
            var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (copy == null)
            {
                copy = Object.Instantiate(from);
                AssetDatabase.CreateAsset(copy, path);
            }
            else EditorUtility.CopySerialized(from, copy);
            copy.name = Path.GetFileNameWithoutExtension(path);
            Settle(copy);
            return copy;
        }

        /// <summary>A one-shot's root settings: no loop, and nothing of the clip moves or turns the character (its owner does),
        /// as PlayerPortBuilder.Settle does for a clip that does not travel.</summary>
        private static void Settle(AnimationClip clip)
        {
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = false;
            s.loopBlend = false;
            s.loopBlendOrientation = true; s.keepOriginalOrientation = true;
            s.loopBlendPositionY = true; s.keepOriginalPositionY = true; s.heightFromFeet = false;
            s.loopBlendPositionXZ = true; s.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            EditorUtility.SetDirty(clip);
        }

        // ---------- 2. the controller ----------

        /// <summary>Writes Animation/HouseCharacter.controller from the clips (see the class notes) over the old file, so
        /// it keeps its id. With no idle clip nothing is written and the old controller (or null) is returned.</summary>
        public static AnimatorController MakeController(Clips clips)
        {
            if (clips.idle == null)
            {
                var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
                Debug.LogError(Tag + " the controller was NOT rebuilt: it needs the idle clip (" + IdleClipPath + "; run the player port's clip copy first). "
                    + (old != null ? "The old one is kept." : "There is none, so the character will stand in its saved pose."));
                return old;
            }
            WorkshopPieces.EnsureFolder(AnimationDir);
            AssetDatabase.DeleteAsset(TempController);
            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(TempController);
            AnimatorStateMachine sm = c.layers[0].stateMachine;

            AnimatorState idle = State(sm, HouseCharacter.IdleState, clips.idle, 1f);
            sm.defaultState = idle;

            if (clips.lookAround != null)
            {
                c.AddParameter(HouseCharacter.LookAroundTrigger, AnimatorControllerParameterType.Trigger);
                AnimatorState look = State(sm, HouseCharacter.LookAroundTrigger, clips.lookAround, 1f);
                AnimatorStateTransition into = idle.AddTransition(look);
                Quick(into);
                into.AddCondition(AnimatorConditionMode.If, 0f, HouseCharacter.LookAroundTrigger);
                Back(look, idle, clips.lookAround, 1f, 1f);
            }
            if (clips.wave != null)
            {
                float speed = Mathf.Max(1f, clips.wave.length / WaveSeconds);
                OneShot(c, sm, idle, HouseCharacter.WaveTrigger, clips.wave, speed, 1f);
            }
            if (clips.talk != null)
            {
                float played = clips.talk.length > 0f ? Mathf.Min(1f, TalkSeconds / clips.talk.length) : 1f;
                OneShot(c, sm, idle, HouseCharacter.TalkTrigger, clips.talk, 1f, played);
            }

            c.name = Path.GetFileNameWithoutExtension(ControllerPath);
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();

            // Over the old file when there is one, so the controller keeps its id and the prefab keeps pointing at it.
            bool had = File.Exists(ControllerPath);
            if (had)
            {
                FileUtil.ReplaceFile(TempController, ControllerPath);
                AssetDatabase.DeleteAsset(TempController);
                AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                string error = AssetDatabase.MoveAsset(TempController, ControllerPath);
                if (!string.IsNullOrEmpty(error)) Debug.LogError(Tag + " the controller could not be moved to " + ControllerPath + ": " + error);
            }
            var built = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Debug.Log(Tag + " controller " + (had ? "rebuilt" : "made") + " at " + ControllerPath + ": " + StateList(built)
                + (clips.wave != null ? "; the wave plays at " + Mathf.Max(1f, clips.wave.length / WaveSeconds).ToString("F2") + "x" : "")
                + (clips.talk != null ? "; the talk plays its first " + Mathf.Min(TalkSeconds, clips.talk.length).ToString("F1") + " s" : "") + ".");
            return built;
        }

        /// <summary>A state started from any state by its trigger (not from itself), back to Idle when it ends.</summary>
        private static void OneShot(AnimatorController c, AnimatorStateMachine sm, AnimatorState idle, string trigger, AnimationClip clip, float speed, float played)
        {
            c.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            AnimatorState state = State(sm, trigger, clip, speed);
            AnimatorStateTransition into = sm.AddAnyStateTransition(state);
            Quick(into);
            into.canTransitionToSelf = false;
            into.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            Back(state, idle, clip, speed, played);
        }

        private static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion, float speed)
        {
            AnimatorState state = sm.AddState(name);
            state.motion = motion;
            state.speed = speed;
            state.writeDefaultValues = true;
            return state;
        }

        /// <summary>A transition on a condition: at once, blended over Blend seconds.</summary>
        private static void Quick(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = Blend;
            t.offset = 0f;
        }

        /// <summary>Back to Idle when <paramref name="played"/> of the clip (a share of it) has played: the blend ends there.</summary>
        private static void Back(AnimatorState from, AnimatorState idle, AnimationClip clip, float speed, float played)
        {
            AnimatorStateTransition t = from.AddTransition(idle);
            t.hasExitTime = true;
            float length = Mathf.Max(0.01f, clip.length);
            t.exitTime = Mathf.Max(EarliestExit, played - Blend * speed / length);
            t.hasFixedDuration = true;
            t.duration = Blend;
            t.offset = 0f;
        }

        /// <summary>"Idle, LookAround, ..." (for the log).</summary>
        private static string StateList(AnimatorController controller)
        {
            if (controller == null || controller.layers.Length == 0) return "no states";
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            var names = new List<string>();
            foreach (ChildAnimatorState s in states) names.Add(s.state.name);
            return states.Length + " states (" + string.Join(", ", names) + ")";
        }

        // ---------- 3. the prefab ----------

        /// <summary>Makes Prefabs/Characters/HouseCharacter (a variant of FunnyBody) or brings it up to date in place (see
        /// the class notes) and returns the saved prefab; null (logged) when FunnyBody is missing or it could not be saved.
        /// <paramref name="controller"/> null leaves the Animator's controller as it is.</summary>
        public static GameObject MakePrefab(RuntimeAnimatorController controller)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(Tag + " HouseCharacter: stop Play first; nothing was made.");
                return null;
            }
            var funny = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPieces.BodyPath);
            if (funny == null)
            {
                Debug.LogError(Tag + " HouseCharacter was NOT made: no " + CharacterPieces.BodyPath + "; run Shift Jam > Characters > Build Characters first.");
                return null;
            }
            WorkshopPieces.EnsureFolder(PrefabDir);
            bool made = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            bool saved;
            string report;
            if (made)
            {
                // The first time: a FunnyBody copy put together in a scene of its own (the open scene never holds it) and
                // saved as a variant of FunnyBody.
                Scene bench = EditorSceneManager.NewPreviewScene();
                try
                {
                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(funny, bench);
                    copy.name = PrefabName;
                    report = Configure(copy, controller);
                    PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath, out saved);
                    Object.DestroyImmediate(copy);
                }
                finally { EditorSceneManager.ClosePreviewScene(bench); }
            }
            else
            {
                // Later: opened and brought up to date in place. Only what this tool owns is set; the words and numbers on
                // HouseCharacter are his and are never touched.
                GameObject open = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    report = Configure(open, controller);
                    PrefabUtility.SaveAsPrefabAsset(open, PrefabPath, out saved);
                }
                finally { PrefabUtility.UnloadPrefabContents(open); }
            }
            if (!saved)
            {
                Debug.LogError(Tag + " HouseCharacter was NOT saved at " + PrefabPath + ".");
                return null;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool variant = prefab != null && PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant;
            Debug.Log(Tag + " HouseCharacter " + (made ? "made" : "brought up to date") + " at " + PrefabPath + (variant ? ", a variant of " + CharacterPieces.BodyPath : ", NOT a variant of FunnyBody (unpacked by hand?)") + ": " + report);
            return prefab;
        }

        /// <summary>What the tool owns on the prefab: HouseCharacter (added once, with its defaults), the solid capsule
        /// fitted to the body, the controller on the rig's Animator, root motion off, culled off screen. Returns a short
        /// report for the log.</summary>
        private static string Configure(GameObject root, RuntimeAnimatorController controller)
        {
            if (!root.TryGetComponent(out CapsuleCollider capsule)) capsule = root.AddComponent<CapsuleCollider>();
            if (!root.TryGetComponent(out HouseCharacter house)) house = root.AddComponent<HouseCharacter>();
            string animatorReport;
            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                if (controller != null) animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // scenery need not animate off screen
                if (PrefabUtility.IsPartOfPrefabInstance(animator)) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                animatorReport = "controller " + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "none") + ", culled off screen";
            }
            else animatorReport = "NO Animator under it (is the pack's rig missing?)";
            house.FitCapsule();
            return "HouseCharacter \"" + house.CharacterName + "\", capsule " + capsule.height.ToString("F2") + " tall, radius " + capsule.radius.ToString("F2") + ", " + animatorReport;
        }

        // =====================================================================================
        // Place

        /// <summary>Stands house characters in the ACTIVE scene (see the class notes): under the root HouseCharacters, whose
        /// own characters an earlier run made are removed first; one by each hub door of the table that the scene has; and
        /// Gap where GapNpc (the pack's bare figure) stood: its hint (ProblemHint and trigger box, with their values) moves
        /// to the root's child GapHint, the same place and size in the world, Gap says the hint's sentences, and GapNpc is
        /// deleted (a later run stands Gap at GapHint again). Refused in Play; with none of the table's doors and no gap
        /// figure it logs and does nothing. Undoable as one step; the scene is marked changed and NOT saved.</summary>
        [MenuItem("Shift Jam/Characters/Place House Characters (D-0185)")]
        public static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("HouseCharacterPieces: placed 0: stop Play first; nothing was changed.");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("HouseCharacterPieces: placed 0: no open scene.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError("HouseCharacterPieces: placed 0: no " + PrefabPath + "; run Shift Jam > Characters > Build House Character first. Nothing was changed.");
                return;
            }

            GameObject root = RootOf(scene);
            GameObject gapFigure = FindByName(scene, GapFigureName, root != null ? root.transform : null);
            Transform gapHint = root != null ? root.transform.Find(GapHintName) : null;
            Dictionary<string, HubDoor> doors = DoorsIn(scene);
            bool anyDoor = false;
            foreach (DoorCharacter row in Doors) anyDoor |= doors.ContainsKey(row.door);
            if (!anyDoor && gapFigure == null && gapHint == null)
            {
                Debug.Log("HouseCharacterPieces: placed 0 in " + scene.name + ": it has none of the doors (" + DoorIds() + ") and no " + GapFigureName + "; nothing was changed.");
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Place House Characters");
            int group = Undo.GetCurrentGroup();
            var log = new StringBuilder();
            float toy = ToyHeight(scene);
            log.AppendLine("a toy here is " + toy.ToString("F2") + " tall");

            root = EnsureRoot(scene, root);
            RemoveCharacters(root.transform, log);

            int placed = 0;
            // The figure by the gap: its hint moves to GapHint, the figure goes, and Gap stands where it stood.
            if (gapFigure != null)
            {
                if (gapFigure.GetComponent<HouseCharacter>() != null) log.AppendLine(GapFigureName + " is already a house character: left as it is");
                else if (PrefabUtility.IsPartOfPrefabInstance(gapFigure) && !PrefabUtility.IsOutermostPrefabInstanceRoot(gapFigure))
                    log.AppendLine(GapFigureName + " sits inside another prefab copy and cannot be deleted from here: left as it is (unpack that copy, then run again)");
                else
                {
                    gapHint = MoveHint(gapFigure, root.transform);
                    log.AppendLine(GapFigureName + " at " + gapFigure.transform.position + ": its hint moved to " + RootName + "/" + GapHintName + ", the figure deleted");
                    Undo.DestroyObjectImmediate(gapFigure);
                }
            }
            if (gapHint != null && StandGapAt(root.transform, gapHint, toy, log) != null) placed++;

            // One by each door of the table that this scene has.
            Physics.SyncTransforms();
            foreach (DoorCharacter row in Doors)
            {
                if (!doors.TryGetValue(row.door, out HubDoor door)) { log.AppendLine("no door " + row.door + " here: no " + row.name); continue; }
                if (StandByDoor(row, door, root.transform, toy, log)) placed++;
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log(Tag + " " + scene.name + ":\n" + log);
            Debug.Log("HouseCharacterPieces: placed " + placed + " house characters in " + scene.name + " under " + RootName + " (undoable; the scene is NOT saved).");
        }

        /// <summary>Gap and its hint by the gap, under the scene's HouseCharacters root (an earlier Gap and GapHint there are
        /// replaced): GapHint at <paramref name="at"/> facing <paramref name="facing"/>, its trigger box
        /// <paramref name="boxSize"/> wide round <paramref name="boxCentre"/> (in its own space) and its ProblemHint showing
        /// <paramref name="words"/> to any size; Gap standing there at the scene's toy height, saying the words'
        /// sentences. CourseBuilder.Downstairs (Land Downstairs) calls it. Returns Gap, or null (logged) when there is no
        /// HouseCharacter prefab: the hint is made all the same.</summary>
        public static GameObject StandGap(Scene scene, Vector3 at, Quaternion facing, string words, Vector3 boxSize, Vector3 boxCentre)
        {
            var log = new StringBuilder();
            GameObject root = EnsureRoot(scene, RootOf(scene));
            Transform old = root.transform.Find(GapName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            old = root.transform.Find(GapHintName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var hintObject = new GameObject(GapHintName);
            hintObject.transform.SetParent(root.transform, false);
            hintObject.transform.SetPositionAndRotation(at, facing);
            var box = hintObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = boxSize;
            box.center = boxCentre;
            var hint = hintObject.AddComponent<ProblemHint>();
            var so = new SerializedObject(hint);
            so.FindProperty("message").stringValue = words ?? "";
            so.FindProperty("anySize").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(hintObject, "Gap's hint");

            GameObject gap = StandGapAt(root.transform, hintObject.transform, ToyHeight(scene), log);
            Debug.Log(Tag + " " + log.ToString().TrimEnd());
            return gap;
        }

        /// <summary>Stands one HouseCharacter (a prefab copy, undoable) under <paramref name="parent"/>: feet at
        /// <paramref name="feet"/>, facing <paramref name="facing"/> (yaw), dressed by <paramref name="seed"/> and
        /// <paramref name="colour"/>, scaled so the top of its head is <paramref name="toyHeight"/> up, with its name and
        /// lines and its capsule fitted. Null when there is no HouseCharacter prefab.</summary>
        public static GameObject Stand(Transform parent, string objectName, string shownName, string[] lines, int seed, string colour,
                                       Vector3 feet, Quaternion facing, float toyHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return null;
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(copy, "Place " + objectName);
            copy.name = objectName;
            copy.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            copy.transform.localScale = Vector3.one;

            if (copy.TryGetComponent(out CharacterBody body))
            {
                body.WearRolled(seed, null, colour ?? "");
                float head = HeadTop(body);
                float parentScale = parent != null ? Mathf.Max(1e-4f, Mathf.Abs(parent.lossyScale.y)) : 1f;
                copy.transform.localScale = Vector3.one * (toyHeight / head / parentScale);
            }
            if (copy.TryGetComponent(out HouseCharacter house))
            {
                house.SetWords(shownName, lines);
                PrefabUtility.RecordPrefabInstancePropertyModifications(house);
                house.FitCapsule();
                if (copy.TryGetComponent(out CapsuleCollider capsule)) PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(copy.transform);
            return copy;
        }

        /// <summary>How tall the scene's player toy is: the object named Player, its CharacterController's height times its
        /// size; 1.8 when there is none.</summary>
        public static float ToyHeight(Scene scene)
        {
            GameObject player = FindByName(scene, PlayerName, null);
            if (player != null && player.TryGetComponent(out CharacterController cc))
            {
                float tall = cc.height * Mathf.Abs(player.transform.lossyScale.y);
                if (tall > 0f) return tall;
            }
            return FallbackToyHeight;
        }

        // ---------- placing, piece by piece ----------

        /// <summary>Gap at GapHint's place, facing its way, saying its hint's sentences (none when it has no hint), feet on
        /// the floor found under it. Null when there is no prefab.</summary>
        private static GameObject StandGapAt(Transform root, Transform hint, float toy, StringBuilder log)
        {
            string[] lines = hint.TryGetComponent(out ProblemHint problem) ? Sentences(new SerializedObject(problem).FindProperty("message").stringValue) : new string[0];
            Vector3 feet = hint.position;
            Physics.SyncTransforms();
            if (Physics.Raycast(feet + Vector3.up * (toy * GapRayAbove), Vector3.down, out RaycastHit hit, toy * (GapRayAbove + RayBelow), ~0, QueryTriggerInteraction.Ignore))
                feet = hit.point;
            GameObject gap = Stand(root, GapName, GapName, lines, GapSeed, "", feet, hint.rotation, toy);
            if (gap == null) { log.AppendLine("no " + PrefabPath + ": no Gap (the hint stays)"); return null; }
            log.AppendLine(Placed(gap, "by the gap") + ", " + lines.Length + " lines from its hint");
            return gap;
        }

        /// <summary>A character beside a hub door: past the door's edge by Side Offset on its preferred side (else the
        /// other), a step into the room, feet on the floor (a ray down; the floor must be within a step of the door's
        /// foot), facing into the room, on open ground only. False (logged) when neither side is open.</summary>
        private static bool StandByDoor(DoorCharacter row, HubDoor door, Transform root, float toy, StringBuilder log)
        {
            Transform t = door.transform;
            Vector3 inward = t.forward;
            inward.y = 0f;
            inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.forward;
            Vector3 along = Vector3.Cross(Vector3.up, inward);   // the room's right, looking in from the door
            float halfWidth = 0f, foot = t.position.y;
            if (door.TryGetComponent(out BoxCollider box))
            {
                Vector3 size = Vector3.Scale(box.size, t.lossyScale);
                halfWidth = Mathf.Abs(size.x) * 0.5f * DoorPerTrigger;
                foot = t.TransformPoint(box.center).y - Mathf.Abs(size.y) * 0.5f;
            }
            float radius = toy * BodyRadius;
            int first = row.side >= 0 ? 1 : -1;
            var tried = new StringBuilder();
            for (int k = 0; k < 2; k++)
            {
                int side = k == 0 ? first : -first;
                string sideName = side > 0 ? " right" : " left";
                foreach (float step in StepsIn)
                {
                    Vector3 spot = t.position + along * (side * (halfWidth + toy * SideOffset + radius)) + inward * (toy * step);
                    if (!Floor(spot, foot, toy, out Vector3 feet)) { tried.Append(sideName + " " + step.ToString("0.0", CultureInfo.InvariantCulture) + " in: no floor within a step;"); continue; }
                    if (!OpenGround(feet, toy, radius)) { tried.Append(sideName + " " + step.ToString("0.0", CultureInfo.InvariantCulture) + " in: not open ground;"); continue; }
                    GameObject copy = Stand(root, row.name, row.name, row.lines, row.seed, row.colour, feet, Quaternion.LookRotation(inward), toy);
                    if (copy == null) { log.AppendLine("no " + PrefabPath + ": no " + row.name); return false; }
                    log.AppendLine(Placed(copy, "by " + row.door + " (" + (side > 0 ? "right" : "left") + " side, " + step.ToString("0.0", CultureInfo.InvariantCulture) + " toy heights into the room" + (k > 0 ? "; the other side was taken" : "") + ")"));
                    return true;
                }
            }
            log.AppendLine("NOT placed: " + row.name + " by " + row.door + " (" + tried.ToString().Trim() + ")");
            return false;
        }

        /// <summary>The floor under <paramref name="spot"/> within a step of the door's foot.</summary>
        private static bool Floor(Vector3 spot, float foot, float toy, out Vector3 feet)
        {
            feet = spot;
            Vector3 from = new Vector3(spot.x, foot + toy * RayAbove, spot.z);
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, toy * (RayAbove + RayBelow), ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y - foot) > toy * MaxStep) return false;
            feet = hit.point;
            return true;
        }

        /// <summary>True when nothing solid stands where a toy-sized body would (the floor itself not counted), as
        /// StadiumLayout.OpenGround checks a spawn.</summary>
        private static bool OpenGround(Vector3 feet, float toy, float radius)
        {
            Vector3 low = feet + Vector3.up * (radius + toy * FloorClear);
            Vector3 high = feet + Vector3.up * Mathf.Max(radius + toy * FloorClear, toy - radius);
            return !Physics.CheckCapsule(low, high, radius, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>The top of the character's head above its soles at scale 1: its body part's top in the parts list (a
        /// hat or tall hair does not count); with no parts list, what it wears; failing that, 1.75.</summary>
        private static float HeadTop(CharacterBody body)
        {
            CharacterCatalog catalog = CharacterCatalog.Load();
            if (catalog != null && catalog.Ready)
            {
                CharacterCatalog.Part part = catalog.PartById(body.Look.Get(CharacterSlot.Body));
                if (part != null && part.top > 0f) return part.top;
            }
            float scale = Mathf.Abs(body.Rig.lossyScale.y);
            float worn = scale > 1e-4f ? body.Height / scale : 0f;
            return worn > 0f ? worn : FallbackHeadTop;
        }

        /// <summary>The figure's hint (ProblemHint and its trigger box, with every value) on a new child of the root named
        /// GapHint, at the figure's place and turn and the same size in the world. An earlier GapHint is replaced.
        /// Undoable.</summary>
        private static Transform MoveHint(GameObject figure, Transform root)
        {
            Transform old = root.Find(GapHintName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var hintObject = new GameObject(GapHintName);
            hintObject.transform.SetParent(root, false);
            hintObject.transform.SetPositionAndRotation(figure.transform.position, figure.transform.rotation);
            Vector3 rootScale = root.lossyScale;
            hintObject.transform.localScale = new Vector3(Inverse(rootScale.x), Inverse(rootScale.y), Inverse(rootScale.z));   // 1 in the world

            Vector3 scale = figure.transform.lossyScale;
            bool hasBox = figure.TryGetComponent(out BoxCollider oldBox);
            bool hasHint = figure.TryGetComponent(out ProblemHint oldHint);
            if (hasBox || hasHint)
            {
                var box = hintObject.AddComponent<BoxCollider>();
                if (hasBox)
                {
                    box.isTrigger = oldBox.isTrigger;
                    box.center = Vector3.Scale(oldBox.center, scale);
                    Vector3 size = Vector3.Scale(oldBox.size, scale);
                    box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                }
                else box.isTrigger = true;
            }
            if (hasHint)
            {
                var hint = hintObject.AddComponent<ProblemHint>();
                var from = new SerializedObject(oldHint);
                var to = new SerializedObject(hint);
                SerializedProperty field = from.GetIterator();
                bool enter = true;
                while (field.NextVisible(enter))
                {
                    enter = false;
                    if (field.propertyPath == "m_Script") continue;
                    to.CopyFromSerializedProperty(field);
                }
                to.ApplyModifiedPropertiesWithoutUndo();
            }
            Undo.RegisterCreatedObjectUndo(hintObject, "Gap's hint");
            return hintObject.transform;
        }

        private static float Inverse(float v) => Mathf.Abs(v) > 1e-6f ? 1f / v : 1f;

        /// <summary>The scene's root object HouseCharacters, made (undoable) when there is none.</summary>
        private static GameObject EnsureRoot(Scene scene, GameObject root)
        {
            if (root != null) return root;
            root = new GameObject(RootName);
            if (root.scene != scene) SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "House characters");
            return root;
        }

        /// <summary>The scene's top-level object named HouseCharacters, or null.</summary>
        private static GameObject RootOf(Scene scene)
        {
            foreach (GameObject top in scene.GetRootGameObjects()) if (top.name == RootName) return top;
            return null;
        }

        /// <summary>Removes the root's own characters (an earlier run's), undoable. GapHint stays: it says where Gap stands.</summary>
        private static void RemoveCharacters(Transform root, StringBuilder log)
        {
            int removed = 0;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.GetComponent<HouseCharacter>() == null) continue;
                Undo.DestroyObjectImmediate(child.gameObject);
                removed++;
            }
            if (removed > 0) log.AppendLine(removed + " characters of an earlier run removed (placed afresh below: wording changed on them by hand is not kept)");
        }

        /// <summary>The first object of that name in the scene (top-level objects first, then depth first, inactive ones
        /// too), never inside <paramref name="skip"/>. Null when there is none.</summary>
        private static GameObject FindByName(Scene scene, string objectName, Transform skip)
        {
            GameObject[] tops = scene.GetRootGameObjects();
            foreach (GameObject top in tops) if (top.name == objectName && top.transform != skip) return top;
            foreach (GameObject top in tops)
            {
                if (top.transform == skip) continue;
                Transform found = FindUnder(top.transform, objectName, skip);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        private static Transform FindUnder(Transform parent, string objectName, Transform skip)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == skip) continue;
                if (child.name == objectName) return child;
                Transform deeper = FindUnder(child, objectName, skip);
                if (deeper != null) return deeper;
            }
            return null;
        }

        /// <summary>The scene's hub doors by Id (the first of each).</summary>
        private static Dictionary<string, HubDoor> DoorsIn(Scene scene)
        {
            var doors = new Dictionary<string, HubDoor>();
            foreach (HubDoor door in Object.FindObjectsByType<HubDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (door.gameObject.scene != scene || string.IsNullOrEmpty(door.Id) || doors.ContainsKey(door.Id)) continue;
                doors.Add(door.Id, door);
            }
            return doors;
        }

        private static string DoorIds()
        {
            var ids = new List<string>();
            foreach (DoorCharacter row in Doors) ids.Add(row.door);
            return string.Join(", ", ids);
        }

        /// <summary>"Pit by ... at (x, y, z), 1.80 to the head, 2.05 with what it wears" (for the log).</summary>
        private static string Placed(GameObject copy, string where)
        {
            float head = 0f, tall = 0f;
            if (copy.TryGetComponent(out CharacterBody body))
            {
                tall = body.Height;
                head = HeadTop(body) * Mathf.Abs(body.Rig.lossyScale.y);
            }
            return copy.name + " " + where + " at " + copy.transform.position + ", facing " + copy.transform.eulerAngles.y.ToString("F0")
                + ", " + head.ToString("F2") + " to the head, " + tall.ToString("F2") + " with what it wears (" + (body != null ? body.LookText : "no body") + ")";
        }

        /// <summary>The words cut into sentences (after . ! or ? followed by a space or the end), line breaks read as
        /// spaces. "A. B." gives "A." and "B.".</summary>
        public static string[] Sentences(string words)
        {
            var found = new List<string>();
            if (string.IsNullOrWhiteSpace(words)) return found.ToArray();
            string flat = words.Replace("\r", " ").Replace("\n", " ");
            var sentence = new StringBuilder();
            for (int i = 0; i < flat.Length; i++)
            {
                char c = flat[i];
                if (c == ' ' && (sentence.Length == 0 || sentence[sentence.Length - 1] == ' ')) continue;   // no doubled spaces
                sentence.Append(c);
                bool end = (c == '.' || c == '!' || c == '?') && (i + 1 == flat.Length || flat[i + 1] == ' ');
                if (!end) continue;
                string done = sentence.ToString().Trim();
                if (done.Length > 0) found.Add(done);
                sentence.Length = 0;
            }
            string rest = sentence.ToString().Trim();
            if (rest.Length > 0) found.Add(rest);
            return found.ToArray();
        }
    }
}
#endif
