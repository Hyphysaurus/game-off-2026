using UnityEngine;

namespace ShiftJam
{
    /// <summary>Where one hand goes on a carried thing (D-0042): the palm's touch point and how the hand lies there.</summary>
    public struct HandGoal
    {
        /// <summary>The point the palm touches, world.</summary>
        public Vector3 palm;
        /// <summary>Which way the fingers point, world.</summary>
        public Vector3 fingers;
        /// <summary>Which way the palm faces (into the thing), world.</summary>
        public Vector3 normal;
    }

    /// <summary>
    /// Something that puts both hands on what the player carries (D-0042): the third-person grab (a prop in the arms) and
    /// the Carrier (a plank held in both hands). CarryPose asks the strongest one each frame.
    /// </summary>
    public interface ICarryHands
    {
        /// <summary>0..1: how much the carry pose and the hands on the thing show right now (eases in on a pick-up, out after a let-go).</summary>
        float CarryWeight { get; }

        /// <summary>The two hands' goals this frame. False when there are none.</summary>
        bool CarryGoals(out HandGoal right, out HandGoal left);
    }
}
