using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A paint ball: a gelatin shell full of paint, one kind of ammo for the blasters. A hard enough hit breaks it and
    /// leaves a real splat (PaintSplats), and gives a grey toy some of its colour back (Colourable); a glancing or slow
    /// one can skip it off whole, leaving only a faint mark, and then the hit does not count. Shell and paint are
    /// different colours, so you see one in flight and the other on the wall. Sized to whoever fires it, like every shot.
    /// Ported from the Godot test room's paint_ball.gd; the numbers are in PaintSettings (its speed, drop, radius and
    /// life are the Projectile's own, on the prefab).
    /// </summary>
    public sealed class PaintBall : Projectile
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("The ball that is seen. Sized and coloured each shot.")] private Renderer ball;
        [SerializeField, Tooltip("The streak behind it. None = no streak.")] private TrailRenderer trail;

        private PaintSettings _settings;
        private MaterialPropertyBlock _block;
        private Paint _paint;
        private float _diameter, _strength, _drag;
        private int _skipped;

        protected override void OnLaunched()
        {
            if (_settings == null) { _settings = PaintSettings.Load(); _block = new MaterialPropertyBlock(); }
            var s = _settings;
            float scale = ShotScale;
            _diameter = s.realDiameter * scale;
            _skipped = 0;
            _strength = s.shellStrength * scale * Random.Range(1f - s.strengthSpread, 1f + s.strengthSpread);
            _drag = s.realDrag ? 3f * s.airDensity * s.dragCoefficient / (4f * s.ballDensity * Mathf.Max(_diameter, 1e-6f)) : 0f;
            ScaleHit(s.damageScale, s.knockScale);

            Color fill = s.fillColours.Length > 0 ? s.fillColours[Random.Range(0, s.fillColours.Length)] : Color.white;
            _paint = new Paint(s) { Fill = fill, Shell = Unlike(fill), BallDiameter = _diameter, Scale = scale, Owner = Owner, OwnerAlt = OwnerAlt };

            if (ball != null)
            {
                ball.transform.localScale = Vector3.one * s.realDiameter;   // the root is already at the shooter's scale
                _block.SetColor(BaseColorId, _paint.Shell);
                ball.SetPropertyBlock(_block);
            }
            if (trail != null)
            {
                float speed = Velocity.magnitude;
                trail.Clear();
                trail.time = speed > 1e-4f ? s.trailLength * scale / speed : 0f;
                trail.widthMultiplier = s.trailWidth * scale;
                var streak = new Color(_paint.Shell.r, _paint.Shell.g, _paint.Shell.b, s.trailAlpha);
                trail.startColor = streak;
                trail.endColor = new Color(streak.r, streak.g, streak.b, 0f);
            }
        }

        /// <summary>Real drag (off unless the settings switch it on): each second the ball slows by drag x speed x speed.</summary>
        protected override void BeforeMove(float dt)
        {
            if (_drag <= 0f) return;
            Vector3 v = Velocity;
            Velocity = v / (1f + _drag * v.magnitude * dt);
        }

        /// <summary>The chance a shell breaks meeting a surface at <paramref name="speedIn"/> along its normal: never under
        /// the low share of its strength, half the time at its strength, every time from the high share of it.</summary>
        public static float BreakChance(float speedIn, float strength, Vector2 range)
        {
            float t = Mathf.InverseLerp(range.x, range.y, speedIn / Mathf.Max(strength, 0.001f));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A shell that meets something too slowly or too glancingly skips off whole: it leaves a faint mark and
        /// flies on, and the hit does not count. One that can skip no more and is too slow to break is spent: it is
        /// gone, and that hit does not count either.</summary>
        protected override bool Deflects(in RaycastHit hit, Vector3 point)
        {
            var s = _settings;
            PaintSplats splats = PaintSplats.InScene();
            Vector3 velocity = Velocity;
            Vector3 facing = PaintSplats.Facing(hit.normal, velocity);
            float speedIn = Mathf.Max(-Vector3.Dot(velocity, facing), 0f);
            float slowest = s.minBreakSpeed * ShotScale;
            if (_skipped >= s.mostSkips || !splats.Skips)
            {
                if (speedIn >= slowest) return false;
                Recycle();   // out of skips and too slow to break: no paint, no sound, no hit
                return true;
            }
            float strength = _strength * (PaintSoft.Is(hit.collider) ? s.softStrength : 1f);
            if (speedIn >= slowest && Random.value < BreakChance(speedIn, strength, s.breakRange)) return false;
            _skipped++;
            splats.Mark(_paint, hit.collider, point, facing, velocity, _diameter);
            Vector3 along = velocity + facing * speedIn;
            Velocity = along * s.skipKeepAlong + facing * (speedIn * s.skipKeepIn);
            transform.position = point + facing * (HitRadius + _diameter * s.skipLift);
            return true;
        }

        protected override void OnLanded(in RaycastHit hit, Vector3 point)
        {
            // Paint gives colour back to whatever it lands on, if that thing has lost its colour.
            Colourable thing = Colourable.Find(hit.collider);
            if (thing != null) thing.TakePaint();
            PaintSplats splats = PaintSplats.InScene();
            splats.PlayHit(point);
            splats.Burst(_paint, point, PaintSplats.Facing(hit.normal, Velocity));
            splats.Splat(_paint, hit.collider, point, hit.normal, Velocity, _diameter);
        }

        /// <summary>A shell colour that is not the paint's hue: a hue well away from it, or hardly any hue at all.</summary>
        private Color Unlike(Color fill)
        {
            var s = _settings;
            Color.RGBToHSV(fill, out float fillHue, out _, out _);
            int choices = 0; Color pick = s.shellFallback;
            foreach (Color shell in s.shellColours)
            {
                Color.RGBToHSV(shell, out float hue, out float saturation, out _);
                float apart = Mathf.Abs(hue - fillHue);
                if (saturation >= s.shellPlainBelow && Mathf.Min(apart, 1f - apart) <= s.shellHueApart) continue;
                choices++;
                if (Random.Range(0, choices) == 0) pick = shell;   // each fitting shell is as likely as the next
            }
            return pick;
        }
    }
}
