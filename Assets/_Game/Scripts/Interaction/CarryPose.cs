using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Both hands on what the player carries (D-0042). Lives on the body's Animator (added at run time by the Carrier and the
    /// third-person grab, or placed there by hand to keep tuned values). Each frame it takes the strongest ICarryHands, fades
    /// the animator's Carry layer (a two-arm carry pose, with the IK pass on) by its weight, and in that layer's IK pass puts
    /// each wrist where its palm lands on the thing, with the elbows steered out and down. After the animator has posed the
    /// body it turns each hand so the palm lies on the thing and the fingers point the way asked. The hand's own axes are
    /// read from the rig (wrist to middle knuckle, wrist to thumb), so nothing assumes how a bone is oriented.
    /// Without the Carry layer (Shift Jam/Animation/Build Carry Pose not run) it does nothing and the carry still works.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CarryPose : MonoBehaviour
    {
        [SerializeField, Tooltip("Animator layer with the two-arm carry pose and the IK pass (made by Shift Jam/Animation/Build Carry Pose).")]
        private string carryLayer = "Carry";
        [SerializeField, Range(0f, 1f), Tooltip("How firmly each wrist goes to its mark on the thing.")]
        private float handWeight = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("How far each hand turns to lie on the thing (palm on it, fingers forward). 0 keeps the pose's own wrists.")]
        private float turnWeight = 0.85f;
        [SerializeField, Range(0f, 1f), Tooltip("How strongly the elbows are steered out and down.")]
        private float elbowWeight = 0.5f;
        [SerializeField, Tooltip("Elbows out to the side, as a share of the shoulder-to-wrist distance.")]
        private float elbowOut = 0.35f;
        [SerializeField, Tooltip("Elbows down, as a share of the shoulder-to-wrist distance.")]
        private float elbowDown = 0.35f;
        [SerializeField, Tooltip("How far the palm surface sits off the hand bone, as a share of the wrist-to-knuckle length.")]
        private float palmThickness = 0.25f;

        /// <summary>One arm: its bones, the hand's own frame read from the rig, and this frame's aim.</summary>
        private sealed class Arm
        {
            public AvatarIKGoal goal;
            public AvatarIKHint hint;
            public Transform hand, shoulder;
            /// <summary>The middle knuckle in the hand bone's space.</summary>
            public Vector3 knuckle;
            /// <summary>The hand's frame in its own space: forward = fingers, up = the way the palm faces.</summary>
            public Quaternion frame;
            public bool turnable;
            public Vector3 target;
            public Quaternion turn;
        }

        private readonly List<ICarryHands> _sources = new List<ICarryHands>();
        private Animator _animator;
        private int _layer = -1;
        private Arm _right, _left;
        private ICarryHands _active;
        private int _goalFrame = -1;

        /// <summary>How much the carry shows this frame, 0..1.</summary>
        public float Weight { get; private set; }
        /// <summary>True when the controller has the Carry layer, so the hands can be placed.</summary>
        public bool HasCarryLayer => _layer >= 0;
        /// <summary>True when the hands were given marks in the last IK pass.</summary>
        public bool HasGoals { get; private set; }
        /// <summary>Where the right wrist was sent in the last IK pass (for checks).</summary>
        public Vector3 RightTarget => _right != null ? _right.target : Vector3.zero;
        /// <summary>Where the left wrist was sent in the last IK pass (for checks).</summary>
        public Vector3 LeftTarget => _left != null ? _left.target : Vector3.zero;

        /// <summary>The CarryPose on <paramref name="animator"/>'s object, added if missing; null for a rig that is not humanoid.</summary>
        public static CarryPose On(Animator animator)
        {
            if (animator == null || !animator.isHuman) return null;
            if (!animator.TryGetComponent(out CarryPose pose)) pose = animator.gameObject.AddComponent<CarryPose>();
            return pose;
        }

        public void Add(ICarryHands source)
        {
            if (source != null && !_sources.Contains(source)) _sources.Add(source);
        }

        public void Remove(ICarryHands source) => _sources.Remove(source);

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            // The pack's body culls its animation when no camera sees it, and a culled Animator skips the IK pass: the hands
            // would let go of what they hold whenever the camera pushes into the body or a wall. The one body always animates.
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _layer = _animator.GetLayerIndex(carryLayer);
            if (!_animator.isHuman) return;
            _right = MakeArm(true);
            _left = MakeArm(false);
        }

        private Arm MakeArm(bool right)
        {
            var arm = new Arm
            {
                goal = right ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand,
                hint = right ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow,
                hand = _animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand),
                shoulder = _animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm),
            };
            if (arm.hand == null) return null;
            Transform middle = _animator.GetBoneTransform(right ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform thumb = _animator.GetBoneTransform(right ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            if (middle == null || thumb == null) return arm;   // no fingers mapped: the wrist is placed, the hand is not turned
            // Where the knuckles and the thumb sit from the wrist never changes with the pose: read once.
            arm.knuckle = arm.hand.InverseTransformPoint(middle.position);
            Vector3 fingers = arm.knuckle.normalized;
            Vector3 thumbSide = Vector3.ProjectOnPlane(arm.hand.InverseTransformPoint(thumb.position), fingers).normalized;
            // Fingers ahead and thumb up, a right palm faces left and a left palm faces right.
            Vector3 palm = right ? Vector3.Cross(fingers, thumbSide) : Vector3.Cross(thumbSide, fingers);
            arm.turnable = fingers.sqrMagnitude > 0.5f && palm.sqrMagnitude > 0.5f;
            if (arm.turnable) arm.frame = Quaternion.LookRotation(fingers, palm);
            return arm;
        }

        private void Update()
        {
            // The strongest carrier wins; its weight fades the Carry layer before the animator runs this frame.
            float best = 0f;
            _active = null;
            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                if (Gone(source)) { _sources.RemoveAt(i); continue; }
                float w = source.CarryWeight;
                if (w > best) { best = w; _active = source; }
            }
            Weight = best;
            if (_layer >= 0) _animator.SetLayerWeight(_layer, best);
        }

        /// <summary>True for a carrier that was destroyed (a Unity object compares equal to null then).</summary>
        private static bool Gone(ICarryHands source)
        {
            if (source == null) return true;
            var unity = source as Object;
            return !ReferenceEquals(unity, null) && unity == null;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != _layer) return;
            HasGoals = false;
            if (_right == null || _left == null || _active == null || Weight <= 0f || !_active.CarryGoals(out HandGoal r, out HandGoal l))
            {
                Clear(_right); Clear(_left);
                return;
            }
            // Out to the side for each elbow: along the shoulders, whatever way the model's own axes run.
            Vector3 across = _right.shoulder != null && _left.shoulder != null ? _right.shoulder.position - _left.shoulder.position : transform.right;
            across = across.sqrMagnitude > 1e-8f ? across.normalized : transform.right;
            Aim(_right, r, across);
            Aim(_left, l, -across);
            HasGoals = true;
            _goalFrame = Time.frameCount;
        }

        private void Aim(Arm arm, HandGoal g, Vector3 outward)
        {
            Vector3 fingers = g.fingers.sqrMagnitude > 1e-8f ? g.fingers.normalized : transform.forward;
            Vector3 normal = Vector3.ProjectOnPlane(g.normal, fingers);
            bool turn = arm.turnable && normal.sqrMagnitude > 1e-8f;
            arm.turn = turn ? Quaternion.LookRotation(fingers, normal.normalized) * Quaternion.Inverse(arm.frame) : arm.hand.rotation;
            // The palm's surface: halfway to the knuckles, a little off the bone on the palm side.
            Vector3 palmLocal = turn ? arm.knuckle * 0.5f + arm.frame * Vector3.up * (palmThickness * arm.knuckle.magnitude) : Vector3.zero;
            arm.target = g.palm - arm.turn * Vector3.Scale(palmLocal, arm.hand.lossyScale);
            _animator.SetIKPositionWeight(arm.goal, handWeight);
            _animator.SetIKPosition(arm.goal, arm.target);
            _animator.SetIKRotationWeight(arm.goal, 0f);   // the hand is turned after the pose, in LateUpdate
            if (arm.shoulder == null) { _animator.SetIKHintPositionWeight(arm.hint, 0f); return; }
            Vector3 shoulder = arm.shoulder.position;
            float reach = Vector3.Distance(shoulder, arm.target);
            Vector3 elbow = Vector3.Lerp(shoulder, arm.target, 0.5f) + outward * (elbowOut * reach) - Vector3.up * (elbowDown * reach);
            _animator.SetIKHintPositionWeight(arm.hint, elbowWeight);
            _animator.SetIKHintPosition(arm.hint, elbow);
        }

        private void Clear(Arm arm)
        {
            if (arm == null) return;
            _animator.SetIKPositionWeight(arm.goal, 0f);
            _animator.SetIKRotationWeight(arm.goal, 0f);
            _animator.SetIKHintPositionWeight(arm.hint, 0f);
        }

        private void LateUpdate()
        {
            // After the animator: lay each palm on the thing. Only on a frame whose IK pass placed the wrists.
            if (!HasGoals || _goalFrame != Time.frameCount) return;
            float k = Weight * turnWeight;
            if (k <= 0f) return;
            if (_right.turnable) _right.hand.rotation = Quaternion.Slerp(_right.hand.rotation, _right.turn, k);
            if (_left.turnable) _left.hand.rotation = Quaternion.Slerp(_left.hand.rotation, _left.turn, k);
        }
    }
}
