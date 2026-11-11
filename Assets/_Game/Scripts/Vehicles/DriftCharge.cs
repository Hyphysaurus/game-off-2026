using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The drift boost's numbers (ArcadeCarSettings.drift). Slip and speed are shares of the car's own top speed, bursts
    /// are top speed multipliers like every other boost (D-0037), so one set fits every car size.
    /// </summary>
    [Serializable]
    public sealed class DriftBoostTuning
    {
        [Header("Charging (handbrake held, sliding, on the ground)")]
        [Tooltip("Sideways speed, as a share of top speed, a slide needs to charge. Lower = easier to charge.")] public float minSlip = 0.2f;
        [Tooltip("Forward speed, as a share of top speed, a slide needs to charge (no charging while spinning on the spot).")] public float minSpeed = 0.3f;
        [Tooltip("Seconds of sliding to reach level one.")] public float levelOneTime = 0.6f;
        [Tooltip("Seconds of sliding to reach level two.")] public float levelTwoTime = 1.5f;
        [Tooltip("Seconds the slide may dip under the thresholds (a bump, a straightened wheel) before the charge is lost.")] public float grace = 0.3f;

        [Header("The burst (handbrake let go)")]
        [Tooltip("Level one: top speed times this. 1 or less = no burst.")] public float levelOneBoost = 1.2f;
        [Tooltip("Level one: seconds.")] public float levelOneSeconds = 0.7f;
        [Tooltip("Level two: top speed times this. 1 or less = no burst.")] public float levelTwoBoost = 1.35f;
        [Tooltip("Level two: seconds.")] public float levelTwoSeconds = 1.1f;
    }

    /// <summary>
    /// What a car tells the HUD and the effects about its drift charge. ArcadeCar implements it; a vehicle without it
    /// simply shows no charge.
    /// </summary>
    public interface IDriftCharge
    {
        /// <summary>0 = nothing banked, 1 or 2 = the burst letting go of the handbrake would fire now.</summary>
        int DriftLevel { get; }
        /// <summary>The charge as a number from 0 to 2: the level reached plus the share of the way to the next.</summary>
        float DriftMeter { get; }
    }

    /// <summary>
    /// The drift charge of one car: time spent in a handbrake slide banks level one, then level two; letting go of the
    /// handbrake pays the level out (the car turns it into a burst through its usual Boost path). Losing the slide for
    /// longer than the grace time loses the charge. A plain class: the car owns it and steps it once per physics step.
    /// </summary>
    public sealed class DriftCharge
    {
        private float _time, _lapse;

        /// <summary>0, 1 or 2: what letting go would pay out now.</summary>
        public int Level { get; private set; }
        /// <summary>0..2: the level plus the share of the way to the next one.</summary>
        public float Charge { get; private set; }

        /// <summary>
        /// One physics step. <paramref name="held"/>: the handbrake is down and the car can drift (driven, not winding).
        /// <paramref name="sliding"/>: on the ground, sideways and forward speed above the thresholds. Returns the level
        /// paid out this step: 0 while charging, 1 or 2 on the step the handbrake lets go with that level banked.
        /// </summary>
        public int Step(float dt, DriftBoostTuning t, bool held, bool sliding)
        {
            if (!held)
            {
                int paid = Level;
                Cancel();
                return paid;
            }
            if (sliding) { _time += dt; _lapse = 0f; }
            else
            {
                _lapse += dt;
                if (_lapse > t.grace) { Cancel(); return 0; }
            }
            float one = Mathf.Max(0.01f, t.levelOneTime), two = Mathf.Max(one + 0.01f, t.levelTwoTime);
            Level = _time >= two ? 2 : _time >= one ? 1 : 0;
            Charge = _time >= two ? 2f : _time >= one ? 1f + (_time - one) / (two - one) : _time / one;
            return 0;
        }

        /// <summary>Drops the charge with nothing paid (the driver left, the car was wrecked, held or parked, the spring winds).</summary>
        public void Cancel()
        {
            _time = 0f; _lapse = 0f; Level = 0; Charge = 0f;
        }
    }
}
