#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Animator controller of one player body, made with the AnimatorController API (never by hand). Base layer:
    /// Locomotion and Crouch are blend trees over the way the body moves against its facing (MoveX, MoveZ, in own units a
    /// second, each clip placed at the speed it covers ground at); the air (the jump, the fall, the landing); the
    /// one-shot moves the scripts start and end (Roll, Punch, Kick, Block, HitLight, HitHeavy, Death, Fidget), the hang
    /// on a rope (RopeHang, D-0170: the rope rider starts and ends it) and the
    /// wake-up (the death played backwards). On top, as on the old body: the Arms layer (one arm holding) and the Carry
    /// layer (two arms, IK pass), with the same masks and pose clips.
    /// The jump (D-0169): the state Air does not play by the clock. It shows the Jump_Arc clip at the point the AirPhase
    /// parameter names (0 take-off, 0.5 the top, 1 coming down at a full jump's landing speed), and PlayerMotor works
    /// that out from the body's own vertical speed, so the pose follows the jump: a full jump runs the whole arc, and
    /// a hop cut short moves on to the part of the arc its speed belongs to.
    /// </summary>
    public static partial class PlayerPortBuilder
    {
        private const string ArmMask = AnimationFolder + "RightArmMask.mask";
        private const string HoldPose = AnimationFolder + "HoldPose.anim";
        private const string CarryMask = AnimationFolder + "TwoArmMask.mask";
        private const string CarryPoseClip = AnimationFolder + "CarryPose.anim";
        private const string TempController = AnimationFolder + "_PlayerPortTemp.controller";

        // Timings of the air and the wake-up. The motor leads and the clips follow (D-0006).
        private const float ToAir = 0.08f, ToFall = 0.15f, ToGroundMoving = 0.12f, ToLand = 0.06f, LandOut = 0.2f, ToCrouch = 0.15f, AirToFall = 0.25f;
        private const float JumpAbove = 0.5f;          // VerticalVelocity above this is a jump, below it a fall
        private const float LongFall = -12f;           // falling faster than a jump lands: the falling clip takes over
        private const float MovingAbove = 0.1f;        // the motor's Speed above this lands straight into the run
        private const float LandFrom = 0.25f;          // share of the landing clip where the feet touch down
        private const float LandPlayback = 1.6f, LandExit = 0.8f;
        private const float WakeFrom = 0.9f;           // share of the death clip where the body lies still
        // A state played backwards counts its time from the clip's END (measured: started at 0.9 it showed the clip's
        // first tenth), so the get-up starts at 1 - WakeFrom and runs to 1, where the clip's first frame stands.
        private const float WakeStartAt = 1f - WakeFrom;
        private const float WakeSeconds = 1.7f;        // WakeUp.duration: the get-up plays the death backwards in this time
        private const string AirPhaseParam = "AirPhase";   // where the jump is in its arc; PlayerMotor writes it by the same name
        // Locomotion, Crouch; Air, Fall, Land; the eight moves; Wake; RopeHang (D-0170). The build is checked against it.
        private const int BaseStates = 15;

        private static RuntimeAnimatorController BuildController(BodySpec spec, Dictionary<string, AnimationClip> clips, PlayerBodySettings.Body body)
        {
            AssetDatabase.DeleteAsset(TempController);
            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(TempController);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            c.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            c.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            c.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            c.AddParameter("Crouched", AnimatorControllerParameterType.Bool);
            c.AddParameter(new AnimatorControllerParameter { name = "Pace", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            c.AddParameter(new AnimatorControllerParameter { name = "MoveSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            c.AddParameter(new AnimatorControllerParameter { name = AirPhaseParam, type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
            // Grounded starts true, or the first frame would fall.
            AnimatorControllerParameter[] ps = c.parameters;
            foreach (AnimatorControllerParameter p in ps) if (p.name == "Grounded") p.defaultBool = true;
            c.parameters = ps;

            AnimatorStateMachine sm = c.layers[0].stateMachine;

            // Standing: still in the middle, the walk and the run clips out at the speed each covers ground at.
            AnimatorState loco = Tree(c, "Locomotion", out BlendTree stand);
            stand.AddChild(clips["Idle_Breathing"], Vector2.zero);
            stand.AddChild(clips["Walk_Forward"], new Vector2(0f, body.walk));
            stand.AddChild(clips["Walk_Backward"], new Vector2(0f, -body.walkBack));
            stand.AddChild(clips["Strafe_Left"], new Vector2(-body.strafe, 0f));
            stand.AddChild(clips["Strafe_Right"], new Vector2(body.strafe, 0f));
            stand.AddChild(clips["Run_Forward"], new Vector2(0f, body.run));
            stand.AddChild(clips["Run_Backward"], new Vector2(0f, -body.runBack));
            stand.AddChild(clips["Run_Left"], new Vector2(-body.runSide, 0f));
            stand.AddChild(clips["Run_Right"], new Vector2(body.runSide, 0f));
            sm.defaultState = loco;

            AnimatorState crouch = Tree(c, "Crouch", out BlendTree low);
            low.AddChild(clips["Crouch_Idle"], Vector2.zero);
            low.AddChild(clips["Crouch_Walk_Forward"], new Vector2(0f, body.crouch));
            low.AddChild(clips["Crouch_Walk_Backward"], new Vector2(0f, -body.crouchBack));
            low.AddChild(clips["Crouch_Walk_Left"], new Vector2(-body.crouchSide, 0f));
            low.AddChild(clips["Crouch_Walk_Right"], new Vector2(body.crouchSide, 0f));

            // Air shows the jump's arc at the time AirPhase names (D-0169). It used to hold the pack's Jump_Loop, one
            // frozen tuck for the whole of every jump; that clip is the rope's hang now (RopeHang, below).
            AnimatorState air = State(sm, "Air", clips["Jump_Arc"], 1f);
            air.timeParameter = AirPhaseParam;
            air.timeParameterActive = true;
            AnimatorState fall = State(sm, "Fall", clips["Fall"], 1f);
            AnimatorState land = State(sm, "Land", clips["Jump_End"], LandPlayback);

            foreach (AnimatorState ground in new[] { loco, crouch, land })
            {
                Go(ground, air, ToAir).Conditions((AnimatorConditionMode.IfNot, 0f, "Grounded"), (AnimatorConditionMode.Greater, JumpAbove, "VerticalVelocity"));
                Go(ground, fall, ToFall).Conditions((AnimatorConditionMode.IfNot, 0f, "Grounded"), (AnimatorConditionMode.Less, JumpAbove, "VerticalVelocity"));
            }
            Go(air, fall, AirToFall).Conditions((AnimatorConditionMode.IfNot, 0f, "Grounded"), (AnimatorConditionMode.Less, LongFall, "VerticalVelocity"));
            foreach (AnimatorState up in new[] { air, fall })
            {
                AnimatorStateTransition run = Go(up, loco, ToGroundMoving);
                run.Conditions((AnimatorConditionMode.If, 0f, "Grounded"), (AnimatorConditionMode.Greater, MovingAbove, "Speed"));
                // Out of a jump the next jump may cut this blend short: chained hops keep their take-off (D-0169).
                if (up == air) run.interruptionSource = TransitionInterruptionSource.Destination;
                AnimatorStateTransition touch = Go(up, land, ToLand);
                touch.offset = LandFrom;
                touch.Conditions((AnimatorConditionMode.If, 0f, "Grounded"), (AnimatorConditionMode.Less, MovingAbove, "Speed"));
            }
            Go(land, loco, ToGroundMoving).Conditions((AnimatorConditionMode.Greater, MovingAbove, "Speed"));
            AnimatorStateTransition stoodUp = Go(land, loco, LandOut);
            stoodUp.hasExitTime = true; stoodUp.exitTime = LandExit;
            Go(loco, crouch, ToCrouch).Conditions((AnimatorConditionMode.If, 0f, "Crouched"));
            Go(crouch, loco, ToCrouch).Conditions((AnimatorConditionMode.IfNot, 0f, "Crouched"));

            // The moves: started and ended by PlayerAnimation, at the playback the settings ask for (MoveSpeed).
            foreach ((string state, string clip) in new[]
            {
                ("Roll", "Dodge_Roll"), ("Punch", "Attack_Punch"), ("Kick", "Attack_Kick"), ("Block", "Block_With_Hands"),
                ("HitLight", "Hit_Reaction_Light"), ("HitHeavy", "Hit_Reaction_Heavy"), ("Death", "Death_Forward"), ("Fidget", "Idle_Look_Around"),
            })
            {
                AnimatorState s = State(sm, state, clips[clip], 1f);
                s.speedParameter = "MoveSpeed";
                s.speedParameterActive = true;
            }
            // The wake-up: the death backwards from where the body lies still, in WakeUp's own time.
            State(sm, "Wake", clips["Death_Forward"], -(WakeFrom * body.death / WakeSeconds));
            // The rope (D-0170): the tuck held on its first frame (speed 0). RopeRider starts and ends it through
            // PlayerAnimation (Play, ResumeInto), so it has no transitions; the arms are the Carry layer's (both hands
            // on the rope).
            State(sm, "RopeHang", clips["Jump_Loop"], 0f);

            Layer(c, "Arms", ArmMask, HoldPose, "Hold", ik: false);
            Layer(c, "Carry", CarryMask, CarryPoseClip, "Carry", ik: true);

            c.name = Path.GetFileNameWithoutExtension(spec.controller);   // the name the file will have
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();

            // Over the old file if there is one, so the controller keeps its GUID and the scenes keep pointing at it.
            if (File.Exists(spec.controller))
            {
                FileUtil.ReplaceFile(TempController, spec.controller);
                AssetDatabase.DeleteAsset(TempController);
                AssetDatabase.ImportAsset(spec.controller, ImportAssetOptions.ForceUpdate);
            }
            else Check(string.IsNullOrEmpty(AssetDatabase.MoveAsset(TempController, spec.controller)), $"{spec.name}: controller written to {spec.controller}");

            var built = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.controller);
            bool whole = built != null && built.layers.Length == 3 && built.layers[1].name == "Arms" && built.layers[2].name == "Carry" && built.layers[2].iKPass
                         && built.layers[0].stateMachine.states.Length == BaseStates;
            Check(whole, $"{spec.name}: {spec.controller} has Base ({BaseStates} states), Arms and Carry (IK pass)" + (built != null ? $" [{built.layers.Length} layers, {built.layers[0].stateMachine.states.Length} states]" : " [missing]"));
            Check(whole && JumpFollows(built, clips["Jump_Arc"]), $"{spec.name}: Air shows Jump_Arc at the time {AirPhaseParam} names, and the next jump can cut its blend into Locomotion short");
            return built;
        }

        /// <summary>True if the written controller has the jump as D-0169 asks: the AirPhase parameter, the state Air on the
        /// arc clip with its time taken from that parameter, and Air's way back to Locomotion open to the next jump.</summary>
        private static bool JumpFollows(AnimatorController built, AnimationClip arc)
        {
            bool listed = false, scrubbed = false, cut = false;
            foreach (AnimatorControllerParameter p in built.parameters) listed |= p.name == AirPhaseParam && p.type == AnimatorControllerParameterType.Float;
            foreach (ChildAnimatorState child in built.layers[0].stateMachine.states)
            {
                if (child.state.name != "Air") continue;
                scrubbed = child.state.motion == arc && child.state.timeParameterActive && child.state.timeParameter == AirPhaseParam;
                foreach (AnimatorStateTransition t in child.state.transitions)
                    cut |= t.destinationState != null && t.destinationState.name == "Locomotion" && t.interruptionSource == TransitionInterruptionSource.Destination;
            }
            return listed && scrubbed && cut;
        }

        private static AnimatorState Tree(AnimatorController c, string name, out BlendTree tree)
        {
            AnimatorState state = c.CreateBlendTreeInController(name, out tree, 0);
            tree.blendType = BlendTreeType.FreeformCartesian2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            state.speedParameter = "Pace";     // the legs' playback, from PlayerAnimation
            state.speedParameterActive = true;
            state.writeDefaultValues = true;
            return state;
        }

        private static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion, float speed)
        {
            AnimatorState state = sm.AddState(name);
            state.motion = motion;
            state.speed = speed;
            state.writeDefaultValues = true;
            return state;
        }

        private static AnimatorStateTransition Go(AnimatorState from, AnimatorState to, float seconds)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = seconds;
            t.offset = 0f;
            return t;
        }

        private static void Conditions(this AnimatorStateTransition t, params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
        {
            foreach (var c in conditions) t.AddCondition(c.mode, c.threshold, c.parameter);
        }

        /// <summary>An override layer at weight 0 with one pose state under a mask: the old body's Arms and Carry layers.</summary>
        private static void Layer(AnimatorController c, string name, string maskPath, string clipPath, string stateName, bool ik)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Check(mask != null && clip != null, $"the {name} layer's mask and pose are there ({maskPath}, {clipPath})");
            c.AddLayer(name);
            AnimatorControllerLayer[] layers = c.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0f;
            layer.iKPass = ik;
            AnimatorState state = layer.stateMachine.AddState(stateName);
            state.motion = clip;
            state.writeDefaultValues = true;
            layer.stateMachine.defaultState = state;
            c.layers = layers;   // layers come back as copies: write them back
        }
    }
}
#endif
