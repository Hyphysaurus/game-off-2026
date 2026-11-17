using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Closes a hand around what it holds by curling the finger bones, after the test room's HandGrip. Each bone bends
    /// from its rest pose towards the palm, so the grip is the same over every clip; a weight fades it (0 leaves the
    /// animation's open hand, 1 is the full grip). Which way "towards the palm" is, is read from the hand's own bones
    /// (the knuckles and the thumb), so it fits either hand and any humanoid rig.
    /// Not a component: the WeaponHolder owns one per hand and calls it from its LateUpdate, after the arms are posed.
    /// </summary>
    public sealed class FingerCurl
    {
        /// <summary>One finger bone: how it sits at rest, and the axis (in its own space) a bend towards the palm turns about.</summary>
        private struct Joint { public Transform bone; public Quaternion rest; public Vector3 axis; }

        private readonly List<Joint> _fingers = new List<Joint>(), _trigger = new List<Joint>(), _thumb = new List<Joint>();

        /// <summary>False when the body has no humanoid finger bones on this hand: nothing is curled.</summary>
        public bool Ready => _fingers.Count > 0 || _trigger.Count > 0;

        /// <summary>Reads the hand as it stands (call before the Animator has posed it: the rest pose is the open hand).</summary>
        public FingerCurl(Animator animator, bool right)
        {
            if (animator == null || !animator.isHuman) return;
            Transform hand = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform knuckle = animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform thumb = animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            if (hand == null || knuckle == null || thumb == null) return;

            // The hand's own directions, in the world as it stands: the fingers, the thumb's side, and the way the palm faces.
            Vector3 fingers = (knuckle.position - hand.position).normalized;
            Vector3 side = thumb.position - hand.position;
            side = (side - fingers * Vector3.Dot(side, fingers)).normalized;
            Vector3 palm = right ? Vector3.Cross(fingers, side) : Vector3.Cross(side, fingers);
            // A turn about `hinge` takes the fingers towards the palm; a turn about `fold` takes the thumb across it.
            Vector3 hinge = Vector3.Cross(fingers, palm), fold = Vector3.Cross(side, palm);

            HumanBodyBones first = right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal;
            // The humanoid finger bones run thumb, index, middle, ring, little, three names each. Only the first of each
            // (the knuckle) is asked for by name: the second bone to bend is the next one down the chain, so a rig with
            // two bones a finger (the Playroom's) and one with three curl the same way.
            Add(_thumb, animator, first + 0, fold);
            Add(_trigger, animator, first + 3, hinge);
            for (int finger = 2; finger < 5; finger++) Add(_fingers, animator, first + finger * 3, hinge);
        }

        /// <summary>The finger's knuckle bone and the next bone down its chain.</summary>
        private static void Add(List<Joint> list, Animator animator, HumanBodyBones knuckle, Vector3 axisInWorld)
        {
            Transform bone = animator.GetBoneTransform(knuckle);
            for (int i = 0; i < 2 && bone != null; i++)
            {
                list.Add(new Joint { bone = bone, rest = bone.localRotation, axis = (Quaternion.Inverse(bone.rotation) * axisInWorld).normalized });
                bone = bone.childCount > 0 ? bone.GetChild(0) : null;
            }
        }

        /// <summary>Curls the hand as far as <paramref name="weight"/>: the three gripping fingers by
        /// <paramref name="fingerDegrees"/> a bone, the trigger finger by <paramref name="triggerDegrees"/> (less, so it
        /// rests on the trigger instead of wrapping the grip), the thumb folded across by <paramref name="thumbDegrees"/>.</summary>
        public void Close(float weight, float fingerDegrees, float triggerDegrees, float thumbDegrees)
        {
            if (weight <= 0f) return;
            Bend(_fingers, fingerDegrees, weight);
            Bend(_trigger, triggerDegrees, weight);
            Bend(_thumb, thumbDegrees, weight);
        }

        private static void Bend(List<Joint> joints, float degrees, float weight)
        {
            for (int i = 0; i < joints.Count; i++)
            {
                Joint j = joints[i];
                if (j.bone == null) continue;
                Quaternion closed = j.rest * Quaternion.AngleAxis(degrees, j.axis);
                j.bone.localRotation = weight >= 1f ? closed : Quaternion.Slerp(j.bone.localRotation, closed, weight);
            }
        }
    }
}
