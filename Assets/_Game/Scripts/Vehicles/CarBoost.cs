using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Boost numbers both car kinds share (ArcadeCarSettings for the Cyber cars, ToyCar for the wooden one). Speeds are
    /// shares of the car's own top speed and pushes are times its own acceleration, so one set of numbers fits both.
    /// </summary>
    [Serializable]
    public sealed class BoostTuning
    {
        [Header("Bursts (pads, gates, the launch)")]
        [Tooltip("Push along the nose toward the raised top speed, times the car's own acceleration.")] public float push = 2f;
        [Range(0.05f, 1f), Tooltip("A burst fades over this last share of its time.")] public float fadeShare = 0.4f;

        [Header("Nitro (Shift)")]
        [Tooltip("Top speed times this while nitro burns.")] public float nitroMultiplier = 1.5f;
        [Tooltip("Meter burnt per second (the meter runs 0..1). It burns only with a wheel down.")] public float nitroDrain = 0.33f;
        [Tooltip("Meter filled per second at a full slide.")] public float driftFill = 0.12f;
        [Tooltip("Sideways speed, as a share of top speed, where a slide starts (tyre smoke, nitro).")] public float driftMinSlip = 0.25f;
        [Tooltip("Sideways speed, as a share of top speed, of a full slide.")] public float driftFullSlip = 0.5f;
        [Tooltip("Meter filled per second in the air.")] public float airFill = 0.15f;
        [Tooltip("Seconds with no wheel down before the air starts filling it.")] public float airDelay = 0.3f;

        [Header("Pull-back launch (stopped: hold Jump and forward, let go of Jump)")]
        [Tooltip("Below this share of top speed, Jump + forward winds the spring.")] public float windMaxSpeed = 0.15f;
        [Tooltip("Seconds to wind fully.")] public float windTime = 1.2f;
        [Tooltip("Least wind that launches; less just lets go.")] public float windMin = 0.15f;
        [Tooltip("Launch top speed: 1 + this x wind.")] public float launchBoost = 0.8f;
        [Tooltip("Launch seconds: this + launchTimePerWind x wind.")] public float launchTime = 0.6f;
        public float launchTimePerWind = 0.8f;
        [Tooltip("Degrees per second the wind-up key turns while winding (twice that, backwards, while it launches).")] public float keySpin = 540f;

        [Header("Effects")]
        [Tooltip("Flames and smoke size, against the car's length.")] public float fxScale = 1f;
    }

    /// <summary>
    /// The boost state of one car (D-0037): a timed burst, the nitro meter and the pull-back wind. The car feeds it once
    /// per physics step and applies <see cref="Multiplier"/> (on its top speed) and <see cref="Push"/> (along its nose)
    /// its own way. A plain class: the car owns it.
    /// </summary>
    public sealed class CarBoost
    {
        /// <summary>What the car tells the boost each physics step.</summary>
        public struct Feed
        {
            /// <summary>A driver whose keys are live (not getting out, no menu or console holding the keys).</summary>
            public bool Driven;
            /// <summary>A wheel (or the ball) on the ground.</summary>
            public bool Grounded;
            /// <summary>Sideways speed, as a share of top speed.</summary>
            public float Slip;
            /// <summary>The nitro key (Sprint) held.</summary>
            public bool Sprint;
            /// <summary>Jump held (the handbrake).</summary>
            public bool Brake;
            /// <summary>The stick's forward/back, -1..1.</summary>
            public float Throttle;
            /// <summary>Forward speed, as a share of top speed.</summary>
            public float Speed;
        }

        private static readonly BoostTuning Defaults = new BoostTuning();

        public BoostTuning Tuning;
        private float _mult = 1f, _time, _left, _air;
        private bool _launch;

        public CarBoost(BoostTuning tuning) { Tuning = tuning; }

        public float Nitro { get; private set; }
        /// <summary>How far the spring is wound, 0..1.</summary>
        public float Wind { get; private set; }
        public bool Winding { get; private set; }
        /// <summary>The wind grew this step (the key turns and the spring creaks).</summary>
        public bool WindGrowing { get; private set; }
        public bool NitroOn { get; private set; }
        /// <summary>The running burst is a pull-back launch (the key unwinds).</summary>
        public bool Launching => _launch && _left > 0f;
        public bool Boosting => _left > 0f || NitroOn;
        /// <summary>Top speed is multiplied by this now.</summary>
        public float Multiplier => Mathf.Max(BurstMultiplier, NitroOn ? T.nitroMultiplier : 1f);
        /// <summary>How hard to push along the nose now, 0..1.</summary>
        public float Push => Mathf.Max(BurstStrength, NitroOn ? 1f : 0f);

        private BoostTuning T => Tuning ?? Defaults;
        /// <summary>The running burst: full, then down to nothing over its last fadeShare.</summary>
        private float BurstStrength => _left > 0f ? Mathf.Clamp01(_left / Mathf.Max(1e-4f, T.fadeShare * _time)) : 0f;
        private float BurstMultiplier => 1f + (_mult - 1f) * BurstStrength;

        /// <summary>Starts a burst unless a stronger one pushes now (a weaker burst never cuts a stronger one short). True
        /// when it took.</summary>
        public bool Burst(float multiplier, float seconds, bool launch = false)
        {
            if (multiplier <= 1f || seconds <= 0f || Winding) return false;
            if (_left > 0f && multiplier < BurstMultiplier) return false;
            _mult = multiplier; _time = seconds; _left = seconds; _launch = launch;
            return true;
        }

        public void AddNitro(float amount) => Nitro = Mathf.Clamp01(Nitro + amount);

        /// <summary>Stopped on the ground with Jump and forward held: the spring can wind.</summary>
        public bool CanWind(Feed f) =>
            !Winding && f.Driven && f.Grounded && f.Brake && f.Throttle > 0.5f && Mathf.Abs(f.Speed) < T.windMaxSpeed;

        /// <summary>Starts winding: any burst stops (the car is held still while it winds).</summary>
        public void StartWind()
        {
            Winding = true; Wind = 0f; WindGrowing = false;
            _left = 0f; _launch = false; NitroOn = false;
        }

        /// <summary>Everything stops (held, parked, wrecked); the meter keeps what it has.</summary>
        public void Halt()
        {
            _left = 0f; _launch = false; _air = 0f;
            Winding = false; WindGrowing = false; Wind = 0f; NitroOn = false;
        }

        /// <summary>As new: also empties the meter (a reset or a recovered wreck).</summary>
        public void Reset() { Halt(); Nitro = 0f; }

        /// <summary>One physics step: bursts run down, the meter fills and burns, the wind grows or lets go. Returns the wind
        /// let go this step (0 when nothing launched); the launch burst has already started.</summary>
        public float Step(float dt, Feed f)
        {
            var t = T;
            if (_left > 0f) { _left = Mathf.Max(0f, _left - dt); if (_left <= 0f) _launch = false; }

            // The meter fills from slides on the ground and from air time.
            _air = f.Grounded ? 0f : _air + dt;
            if (f.Driven)
            {
                if (f.Grounded && f.Slip > t.driftMinSlip)
                    AddNitro(t.driftFill * Mathf.InverseLerp(t.driftMinSlip, Mathf.Max(t.driftMinSlip + 0.01f, t.driftFullSlip), f.Slip) * dt);
                if (_air > t.airDelay) AddNitro(t.airFill * dt);
            }

            // The pull-back wind: grows while forward is held, launches when Jump lets go.
            float launched = 0f;
            WindGrowing = false;
            if (Winding)
            {
                if (!f.Driven || !f.Grounded) { Winding = false; Wind = 0f; }   // lost the driver or the ground: nothing launches
                else if (!f.Brake)
                {
                    if (Wind >= t.windMin) launched = Wind;
                    Winding = false; Wind = 0f;
                }
                else if (f.Throttle > 0.5f && Wind < 1f)
                {
                    Wind = Mathf.Min(1f, Wind + dt / Mathf.Max(0.05f, t.windTime));
                    WindGrowing = true;
                }
            }

            // Nitro burns while Shift is held, with a wheel down.
            NitroOn = f.Driven && f.Sprint && Nitro > 0f && f.Grounded && !Winding;
            if (NitroOn) Nitro = Mathf.Max(0f, Nitro - t.nitroDrain * dt);

            if (launched > 0f) Burst(1f + t.launchBoost * launched, t.launchTime + t.launchTimePerWind * launched, true);
            return launched;
        }
    }
}
