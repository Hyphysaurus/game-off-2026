using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What boosts can do to a car (D-0037). Boost pads and gates, jump pads, nitro pickups and the cannon talk to a car
    /// only through this; ArcadeCar and ToyCar implement it.
    /// </summary>
    public interface IBoostable
    {
        /// <summary>A burst: for <paramref name="seconds"/> the car may reach <paramref name="topSpeedMultiplier"/> times its
        /// top speed and is pushed toward it along its nose, fading over the last part. A stronger burst replaces a weaker
        /// one; a weaker one never cuts a stronger one short.</summary>
        void Boost(float topSpeedMultiplier, float seconds);
        /// <summary>Adds to the nitro meter, as a share of full (the meter runs 0..1).</summary>
        void AddNitro(float amount);
        /// <summary>The nitro meter, 0..1.</summary>
        float Nitro { get; }
        /// <summary>True while a burst or nitro is pushing the car (flames, the HUD's BOOST, the camera).</summary>
        bool Boosting { get; }
        /// <summary>How far the pull-back spring is wound, 0..1 (0 when not winding).</summary>
        float Wind { get; }
        /// <summary>Sets the car flying at <paramref name="velocity"/>, world units per second (jump pads, the cannon).</summary>
        void Launch(Vector3 velocity);
        /// <summary>Holds the car still where it is (true), or lets it go (false): the cannon loads and aims it.</summary>
        void Hold(bool held);
        /// <summary>True while someone drives it.</summary>
        bool Driven { get; }
    }
}
