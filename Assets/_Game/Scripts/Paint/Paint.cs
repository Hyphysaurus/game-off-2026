using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What one ball's paint is: its two colours, how big its ball was and who fired it. The ball fills one in; its drops
    /// keep it after the ball is gone. It also knows how wide this paint spreads when it lands.
    /// </summary>
    public sealed class Paint
    {
        private readonly PaintSettings _settings;

        /// <summary>The paint inside (the splat) and the shell (the ball in flight, the flakes, the skip mark).</summary>
        public Color Fill, Shell;
        /// <summary>The ball's width, world units. How far its paint reaches, its drops' too, goes with it.</summary>
        public float BallDiameter;
        /// <summary>The shooter's scale: 1 the toy. Every length and speed of this paint is that many times the toy's.</summary>
        public float Scale = 1f;
        /// <summary>Whoever fired the ball, and the body or car they were in: the drops fly through them.</summary>
        public Transform Owner, OwnerAlt;

        public Paint(PaintSettings settings) { _settings = settings; }

        /// <summary>Seconds this paint stays.</summary>
        public float Life => _settings.splatLife;

        /// <summary>True for a part of whoever fired the ball.</summary>
        public bool IsOwner(Transform t) => (Owner != null && t.IsChildOf(Owner)) || (OwnerAlt != null && t.IsChildOf(OwnerAlt));

        /// <summary>How wide the dense core spreads when a drop <paramref name="diameter"/> wide meets a surface at
        /// <paramref name="speedIn"/> along its normal (both in world units, at this paint's scale). Pasandideh-Fard's
        /// maximum spread: beta = sqrt((We + 12) / (3 (1 - cos theta) + 4 We / sqrt(Re))), and the core is beta x diameter.
        /// The formula is worked at toy size and the answer scaled back up, so a kid's splat is the toy's, bigger.</summary>
        public float CoreWidth(float speedIn, float diameter)
        {
            var s = _settings;
            float scale = Mathf.Max(Scale, 1e-4f);
            // Under the slowest speed the formula's resting term takes over and would make a slower drop spread wider again.
            float v = Mathf.Max(speedIn / scale, s.slowestSpread);
            float d = Mathf.Max(diameter / scale, 1e-6f);
            float weber = s.fillDensity * v * v * d / s.surfaceTension;
            float reynolds = s.fillDensity * v * d / s.fillViscosity;
            float wetting = 3f * (1f - Mathf.Cos(s.contactAngle * Mathf.Deg2Rad));
            float beta = Mathf.Sqrt((weber + 12f) / (wetting + 4f * weber / Mathf.Sqrt(reynolds)));
            return beta * d * s.spreadScale * scale;
        }
    }
}
