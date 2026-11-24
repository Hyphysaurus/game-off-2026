using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Where a bot sends each dart and when (D-0188), for its BotBrain. It watches the quarry the soldier is after and
    /// measures how fast it moves (from where its root was a frame ago, smoothed; a new quarry, or a jump faster than
    /// anything runs, starts the measure over). A dart is aimed at the quarry's point, plus the level's Lead share of the
    /// way the quarry moves while the dart flies, plus an error: Aim Error First on first sight, settling to Aim Error
    /// Steady over Aim Settle Seconds of unbroken sight of the same quarry, more the faster the quarry moves. The error's
    /// direction drifts smoothly (a slow noise, not a new roll each dart), so a burst walks across its mark rather than
    /// spraying. Darts come in bursts: Burst Shots of them Burst Gap apart, then Burst Rest.
    /// A plain part, not a component: the brain makes it when it wakes.
    /// </summary>
    public sealed class BotAim
    {
        private Transform _quarry;
        private Vector3 _last, _velocity;
        private bool _have, _sighted;
        private float _sightSince, _readyAt;
        private int _burstLeft;
        private readonly float _seed;

        // The error's wander: the dart misses by between LeastShare and all of the error, in a direction that turns
        // through two full circles over the noise's range; the size's noise is read this far along from the turn's.
        private const float LeastShare = 0.6f;
        private const float Turns = 2f;
        private const float SizeNoiseOffset = 31.7f;
        private const float MostDegrees = 60f;   // never more than this off, whatever the numbers say

        /// <summary>A new aim; <paramref name="seed"/> sets where its error's drift starts (one per bot, so two bots do
        /// not miss alike).</summary>
        public BotAim(float seed) => _seed = seed;

        /// <summary>The quarry's measured velocity, world units a second, flat.</summary>
        public Vector3 Velocity => _velocity;

        /// <summary>Once a frame: the quarry now (null: none), whether it is seen, and the frame's time.
        /// <paramref name="smoothing"/>: seconds the speed is smoothed over; <paramref name="fastest"/>: a speed above
        /// this (world units a second) is a jump, not a run.</summary>
        public void Watch(Transform quarry, bool seen, float now, float dt, float smoothing, float fastest)
        {
            if (quarry != _quarry) { _quarry = quarry; _have = false; _velocity = Vector3.zero; _sighted = false; }
            if (quarry == null) { _sighted = false; return; }
            Vector3 at = quarry.position;
            if (_have && dt > 0f)
            {
                Vector3 v = (at - _last) / dt;
                v.y = 0f;
                if (v.sqrMagnitude > fastest * fastest) _velocity = Vector3.zero;   // a warp or a comeback
                else _velocity = Vector3.Lerp(_velocity, v, 1f - Mathf.Exp(-dt / Mathf.Max(1e-3f, smoothing)));
            }
            _last = at; _have = true;
            if (seen && !_sighted) { _sighted = true; _sightSince = now; }
            else if (!seen) _sighted = false;
        }

        /// <summary>Sight was broken (a gap in the asking): the error starts again from First.</summary>
        public void BreakSight() { _sighted = false; _have = false; _velocity = Vector3.zero; }

        /// <summary>Everything forgotten: no quarry, a fresh burst.</summary>
        public void Forget()
        {
            _quarry = null; _have = _sighted = false; _velocity = Vector3.zero;
            _burstLeft = 0; _readyAt = 0f;
        }

        /// <summary>The error now, degrees (see the class notes).</summary>
        public float ErrorDegrees(BotSkill skill, float now, float scale)
        {
            float settled = !_sighted ? 0f : skill.aimSettleSeconds > 0f ? Mathf.Clamp01((now - _sightSince) / skill.aimSettleSeconds) : 1f;
            return Mathf.Lerp(skill.aimErrorFirst, skill.aimErrorSteady, settled) + skill.aimErrorPerSpeed * (_velocity.magnitude / Mathf.Max(0.001f, scale));
        }

        /// <summary>The point to send a dart at from <paramref name="from"/>, at the quarry's <paramref name="aim"/>, for a
        /// dart flying <paramref name="dartSpeed"/> (world units a second): lead and error put in. <paramref name="drift"/>
        /// is how fast the error's direction wanders, roughly turns a second.</summary>
        public Vector3 ShotPoint(Vector3 from, Vector3 aim, float dartSpeed, BotSkill skill, float now, float scale, float drift)
        {
            float far = Vector3.Distance(from, aim);
            float flight = dartSpeed > 0.01f ? far / dartSpeed : 0f;
            Vector3 led = aim + _velocity * (flight * skill.lead);
            Vector3 line = led - from;
            float length = line.magnitude;
            if (length < 1e-4f) return led;
            Vector3 dir = line / length;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(dir, side);
            // The error's direction and size wander smoothly with time: two slow noises.
            float t = now * drift;
            float turn = Mathf.PerlinNoise(_seed, t) * Turns * 2f * Mathf.PI;
            float size = Mathf.Lerp(LeastShare, 1f, Mathf.PerlinNoise(_seed + SizeNoiseOffset, t));
            float off = Mathf.Tan(Mathf.Clamp(ErrorDegrees(skill, now, scale) * size, 0f, MostDegrees) * Mathf.Deg2Rad);
            Vector3 bent = dir + (side * Mathf.Cos(turn) + up * Mathf.Sin(turn)) * off;
            return from + bent.normalized * length;
        }

        /// <summary>True when the burst allows a dart now.</summary>
        public bool Ready(float now) => now >= _readyAt;

        /// <summary>Before a dart: the seconds the next one waits (Burst Gap, or Burst Rest after a burst's last). A new
        /// burst's size is picked here. Pass the answer to the soldier's Fire and, if a dart left, to Fired.</summary>
        public float NextWait(BotSkill skill, System.Random dice)
        {
            if (_burstLeft <= 0)
            {
                int least = Mathf.Max(1, skill.burstShots.x), most = Mathf.Max(least, skill.burstShots.y);
                _burstLeft = dice.Next(least, most + 1);
            }
            if (_burstLeft > 1) return skill.burstGap;
            return Mathf.Lerp(skill.burstRest.x, skill.burstRest.y, (float)dice.NextDouble());
        }

        /// <summary>A dart left at <paramref name="now"/>; the next waits <paramref name="wait"/> (NextWait's answer).</summary>
        public void Fired(float now, float wait)
        {
            _burstLeft = Mathf.Max(0, _burstLeft - 1);
            _readyAt = now + wait;
        }
    }
}
