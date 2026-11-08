#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Shift Jam / Animation / Build Carry Pose (D-0042): the animator side of carrying in the arms, made with the
    /// AnimatorController API so it can be run again at any time. Adds (or refreshes) a "Carry" layer on top of
    /// PlayerAnimator.controller: Override, default weight 0, IK pass on, masked to both arms and hands (TwoArmMask.mask),
    /// one state playing CarryPose.anim (a still two-arm carry pose made of humanoid muscle curves). CarryPose fades the
    /// layer and places the hands in its IK pass. The Base and Arms layers are left as they are (the weapons and the
    /// flashlight still raise the right arm through Arms), except that HoldPose.anim's finger curves are renamed to the
    /// names a humanoid clip binds (RightHand.Index.1 Stretched, not Right Index 1 Stretched), so the fist really closes.
    /// Batch: -batchmode -quit -executeMethod ShiftJam.CarryPoseBuilder.Build
    /// </summary>
    public static class CarryPoseBuilder
    {
        private const string ControllerPath = "Assets/_Game/Animation/PlayerAnimator.controller";
        private const string MaskPath = "Assets/_Game/Animation/TwoArmMask.mask";
        private const string ClipPath = "Assets/_Game/Animation/CarryPose.anim";
        private const string HoldPath = "Assets/_Game/Animation/HoldPose.anim";
        private const string LayerName = "Carry";
        private const string StateName = "Carry";

        private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

        [MenuItem("Shift Jam/Animation/Build Carry Pose")]
        public static void BuildFromMenu() => Build();

        public static void Build()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[CarryPose] No controller at " + ControllerPath); return; }
            AvatarMask mask = BuildMask();
            AnimationClip clip = BuildClip();
            int renamed = FixHoldPoseFingers();

            AnimatorControllerLayer[] layers = controller.layers;
            int index = System.Array.FindIndex(layers, l => l.name == LayerName);
            bool added = index < 0;
            if (added)
            {
                controller.AddLayer(LayerName);   // appended on top, after Arms, so it wins where both pose the arms
                layers = controller.layers;
                index = layers.Length - 1;
            }
            AnimatorControllerLayer layer = layers[index];
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = true;
            layer.syncedLayerIndex = -1;

            AnimatorStateMachine machine = layer.stateMachine;
            AnimatorState state = null;
            foreach (ChildAnimatorState child in machine.states)
                if (child.state.name == StateName) state = child.state;
            if (state == null) state = machine.AddState(StateName);
            state.motion = clip;
            state.writeDefaultValues = true;
            machine.defaultState = state;
            controller.layers = layers;   // layers come back as copies: write them back

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CarryPose] {(added ? "Added" : "Refreshed")} the '{LayerName}' layer (index {index}, IK pass on, mask {MaskPath}, clip {ClipPath}); " +
                      $"HoldPose finger curves renamed: {renamed}. Layers now: {string.Join(", ", System.Array.ConvertAll(controller.layers, l => l.name + (l.iKPass ? " (IK)" : "")))}");
        }

        /// <summary>Both arms, both hands' fingers and both hand IK goals; nothing else.</summary>
        private static AvatarMask BuildMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool on = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                          || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                          || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            mask.transformCount = 0;
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>
        /// A still two-arm carry: upper arms down and forward, elbows bent, hands relaxed and a little curled, the same on both
        /// sides (humanoid muscles mirror). Measured against the pack's Idle on this rig: Arm Down-Up -0.5 hangs the arm,
        /// Forearm Stretch 0.85 is a straight elbow; HoldPose (0, -0.35) holds the forearm out. The IK pass puts the hands on the
        /// thing; this pose sets where the elbows and shoulders start from.
        /// </summary>
        private static AnimationClip BuildClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.ClearCurves();
            var muscles = new List<KeyValuePair<string, float>>();
            foreach (string side in new[] { "Left", "Right" })
            {
                muscles.Add(new KeyValuePair<string, float>(side + " Shoulder Down-Up", 0f));
                muscles.Add(new KeyValuePair<string, float>(side + " Shoulder Front-Back", 0.1f));
                muscles.Add(new KeyValuePair<string, float>(side + " Arm Down-Up", -0.3f));
                muscles.Add(new KeyValuePair<string, float>(side + " Arm Front-Back", 0.35f));
                muscles.Add(new KeyValuePair<string, float>(side + " Arm Twist In-Out", 0f));
                muscles.Add(new KeyValuePair<string, float>(side + " Forearm Stretch", -0.15f));
                muscles.Add(new KeyValuePair<string, float>(side + " Forearm Twist In-Out", 0f));
                muscles.Add(new KeyValuePair<string, float>(side + " Hand Down-Up", 0f));
                muscles.Add(new KeyValuePair<string, float>(side + " Hand In-Out", 0f));
                foreach (string finger in Fingers)
                {
                    float curl = finger == "Thumb" ? 0.2f : 0.35f;   // open enough to lie flat on a side, not splayed
                    for (int n = 1; n <= 3; n++) muscles.Add(new KeyValuePair<string, float>($"{side}Hand.{finger}.{n} Stretched", curl));
                    muscles.Add(new KeyValuePair<string, float>($"{side}Hand.{finger}.Spread", finger == "Thumb" ? 0.2f : 0f));
                }
            }
            foreach (var m in muscles)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), m.Key), AnimationCurve.Constant(0f, 1f, m.Value));
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>
        /// HoldPose was written with HumanTrait's finger names ("Right Index 1 Stretched"), which a clip does not bind; the
        /// pack's clips use "RightHand.Index.1 Stretched". Renames them in place (values kept). Returns how many it renamed.
        /// </summary>
        private static int FixHoldPoseFingers()
        {
            var hold = AssetDatabase.LoadAssetAtPath<AnimationClip>(HoldPath);
            if (hold == null) return 0;
            var stretched = new Regex(@"^(Left|Right) (Thumb|Index|Middle|Ring|Little) ([123]) Stretched$");
            var spread = new Regex(@"^(Left|Right) (Thumb|Index|Middle|Ring|Little) Spread$");
            int renamed = 0;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(hold))
            {
                string name = null;
                Match m = stretched.Match(binding.propertyName);
                if (m.Success) name = $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value} Stretched";
                else
                {
                    m = spread.Match(binding.propertyName);
                    if (m.Success) name = $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.Spread";
                }
                if (name == null) continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(hold, binding);
                AnimationUtility.SetEditorCurve(hold, binding, null);
                AnimationUtility.SetEditorCurve(hold, EditorCurveBinding.FloatCurve(binding.path, binding.type, name), curve);
                renamed++;
            }
            if (renamed > 0) EditorUtility.SetDirty(hold);
            return renamed;
        }
    }
}
#endif
