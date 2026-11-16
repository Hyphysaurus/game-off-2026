using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Whoever drives an ArcadeCar (D-0061): the player's PlayerInput, handed over by the CarSeat, or a rival's
    /// RivalDriver. The car reads its driver only through this, so a rival drives the same car through the same physics
    /// as the player. The names are the ones PlayerInput already had (and the two every Unity component has), so the
    /// player's input satisfies it as it stands and the car's code did not change.
    /// </summary>
    public interface ICarDriver
    {
        /// <summary>Steering (x: right is positive) and throttle (y: forward is positive), each -1..1.</summary>
        Vector2 Move { get; }
        /// <summary>The handbrake: drifts, and stopped with forward held it winds the pull-back launch.</summary>
        bool JumpHeld { get; }
        /// <summary>Nitro.</summary>
        bool Sprint { get; }
        /// <summary>The driver's controls are live (no menu or console has taken them): nitro and the launch need it.</summary>
        bool isActiveAndEnabled { get; }
        /// <summary>The driver's own object: a car never rams the one who drives it.</summary>
        Transform transform { get; }
    }
}
