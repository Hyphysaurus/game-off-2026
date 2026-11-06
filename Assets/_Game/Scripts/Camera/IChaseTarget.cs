using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What a driven vehicle tells the chase camera (D-0036): how fast it is going, its top speed, and whether it is on the
    /// ground. The camera follows the way the car travels at speed, widens a little with speed, and turns slower in the air.
    /// A vehicle without it is still followed (its speed is measured from how it moves).
    /// </summary>
    public interface IChaseTarget
    {
        /// <summary>World units per second.</summary>
        Vector3 ChaseVelocity { get; }
        /// <summary>World units per second at its current size.</summary>
        float ChaseTopSpeed { get; }
        bool ChaseGrounded { get; }
    }
}
