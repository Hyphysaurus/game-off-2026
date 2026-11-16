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

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Player / Build Player Port: everything the player's port from the test room needs, in one run that can
    /// be repeated. (1) Copies of the Playroom characters' clips with the loop and root settings the game needs, kept
    /// under git-ignored _Imports (the pack's own files are not touched), plus a crouch idle made from the crouch walk's
    /// first frame with a slow breath in the hips, as the test room's was. (2) Measures each body: the scale that puts
    /// its head at the player's head height, its palm, how fast each clip covers ground, how long the one-shot clips
    /// are, and when a punch and a kick are furthest out (on the bones). (3) Writes Resources/PlayerBody (measured
    /// numbers, rewritten every run; the leg-speed cap is kept) and Resources/PlayerMoves (made once, never overwritten).
    /// (4) Builds one Animator controller per body with the AnimatorController API. (5) In the four scenes: takes the
    /// old body off the Player, puts the Male and Female on, adds the move scripts, sets first person to V, and saves.
    /// Every step is checked; a failed check fails the run (exit code 1 in batch).
    /// Batch: -batchmode -quit -executeMethod ShiftJam.PlayerPortBuilder.Build
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
            new ClipSpec("Jump_Loop", true, false, true), new ClipSpec("Jump_End", false, false), new ClipSpec("Fall", true, false),
            new ClipSpec("Attack_Punch", false, false), new ClipSpec("Attack_Kick", false, false),
            new ClipSpec("Block_With_Hands", false, false), new ClipSpec("Dodge_Roll", false, true),
            new ClipSpec("Hit_Reaction_Light", false, false), new ClipSpec("Hit_Reaction_Heavy", false, false),
            new ClipSpec("Death_Forward", false, true),
        };

        private static int _failed;

        private static void Say(string what) => Debug.Log($"{Tag} {what}");

        private static bool Check(bool ok, string what)
        {
            if (ok) Debug.Log($"{Tag} ok: {what}");
            else { _failed++; Debug.LogError($"{Tag} FAIL: {what}"); }
            return ok;
        }

        [MenuItem("Shift Jam/Player/Build Player Port (clips, bodies, controllers, scenes)")]
        public static void Build()
        {
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
