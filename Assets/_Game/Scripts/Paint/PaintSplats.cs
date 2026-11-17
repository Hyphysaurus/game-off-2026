using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The paint a paint ball leaves: the splat, the broken shell on it, the spatter ring round it, the drops it throws,
    /// the runs down a steep face, and the faint mark of a ball that skipped off whole. Each is a decal stuck to whatever
    /// it landed on, so it wraps the drawn model and moves with it. One of these sits in each scene (the first paint ball
    /// makes it; a level can hold its own to switch skips off): it works out what a hit leaves, and hands the decals to its
    /// PaintBook, the drops and bits to its PaintDrops and the materials to its PaintLooks. The reset key (F9) and
    /// Clear take all the paint away, balls in flight too.
    /// Ported from the Godot test room's paint_splat.gd; the numbers are in PaintSettings.
    ///
    /// In a splat picture the ball first touched at the middle across and at the family's anchor down it; the dense core
    /// is coreShare of the picture's width, and spines and tails run down the picture (downrange).
    /// A decal paints what lies inside its box. A drawn model can sit a little off its hit shape, so each box runs from
    /// reachUp above the hit down to the far side of the shape that was hit.
    /// </summary>
    public sealed class PaintSplats : MonoBehaviour, IResettable
    {
        [SerializeField, Tooltip("Off: every ball breaks here, whatever its angle and speed (a skipped shot would colour nothing in the Colour Corner).")]
        private bool skips = true;

        private static PaintSplats _inScene;
        private static readonly List<Renderer> Drawn = new List<Renderer>();

        private PaintSettings _settings;
        private PaintBook _book;
        private PaintDrops _drops;
        private PaintLooks _looks;
        private AudioSource _audio;
        private bool _warned;

        /// <summary>Balls may skip off whole in this scene (the settings must allow it too).</summary>
        public bool Skips { get => skips && _settings.skips; set => skips = value; }
        /// <summary>Paint decals (splats, shell, runs, marks) and drop spots not yet pushed out by their budgets.</summary>
        public int LivePaint => _book.LivePaint;
        public int LiveDrops => _book.LiveDrops;
        /// <summary>Every paint decal in the scene, those fading out included.</summary>
        public int Decals => _book.Count;
        public int DropsInAir => _drops.InAir;
        /// <summary>Bits in the air now: paint bursts, a dart's bits, the puff at a barrel (for checks).</summary>
        public int BitsInAir => _drops.BitsInAir;
        /// <summary>Placed sounds played since the scene began (for checks).</summary>
        public int Sounds { get; private set; }
        /// <summary>Balls that broke, and skips that left a mark, since the scene began (Clear does not zero them).</summary>
        public int Breaks { get; private set; }
        public int Marks { get; private set; }
        /// <summary>Where the last ball broke, and the side of the surface it came from (for checks and stills).</summary>
        public Vector3 LastBreakPoint { get; private set; }
        public Vector3 LastBreakFacing { get; private set; }
        /// <summary>Decal materials made so far (one for each picture in each colour).</summary>
        public int Materials => _looks.Count;

        /// <summary>The scene's paint keeper. The first paint ball to need it makes it.</summary>
        public static PaintSplats InScene()
        {
            if (_inScene == null) _inScene = FindFirstObjectByType<PaintSplats>();
            if (_inScene == null) _inScene = new GameObject("PaintSplats").AddComponent<PaintSplats>();
            return _inScene;
        }

        private void Awake()
        {
            _settings = PaintSettings.Load();
            _book = new PaintBook(_settings, transform);
            _drops = new PaintDrops(_settings, transform);
            _looks = new PaintLooks(_settings);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = _settings.soundInRoom;
            _inScene = this;
        }

        private void OnDestroy()
        {
            _looks.Dispose();
            if (_inScene == this) _inScene = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _drops.Fly(dt, this);
            _book.Step(dt);
        }

        /// <summary>The reset key: all paint goes.</summary>
        public void ResetState() => Clear();

        /// <summary>Takes all the paint away at once, and the drops in the air: the level started over in place.
        /// Shots still flying go too, or they would land on the fresh level.</summary>
        public void Clear()
        {
            Projectile.ClearAll();
            _book.Clear();
            _drops.Clear();
        }

        // ---------- what a hit leaves ----------

        /// <summary>A ball broke here: the splat, the broken shell on it, runs down a steep hard face, the spatter ring
        /// and the drops thrown off. <paramref name="velocity"/> is the ball's at the moment it hit;
        /// <paramref name="diameter"/> is its width.</summary>
        public void Splat(Paint paint, Collider struck, Vector3 point, Vector3 normal, Vector3 velocity, float diameter)
        {
            var s = _settings;
            Breaks++;
            Vector3 facing = Facing(normal, velocity);
            LastBreakPoint = point; LastBreakFacing = facing;
            float speedIn = Mathf.Max(-Vector3.Dot(velocity, facing), 0f);
            float angle = ImpactAngle(speedIn, velocity.magnitude);
            Vector3 along = Along(facing, velocity, angle);
            float stretch = Stretch(angle);
            bool soft = PaintSoft.Is(struck);
            Transform holder = HolderOf(struck, point);
            float core = paint.CoreWidth(speedIn, diameter) * (soft ? s.softScale : 1f);
            Vector2 reach = Reach(struck, point, facing, diameter, Mathf.Max(core * s.depth, s.leastReach.x * paint.Scale) * 0.5f);
            float deep = reach.x + reach.y;

            // The splat. Its picture's core is coreShare of its width; it stretches downrange by the angle.
            PaintSettings.Family family = soft ? s.soft : FamilyFor(angle);
            if (family == null || family.looks.Length == 0) { WarnNoPictures(); return; }
            float width = core / s.coreShare;
            PaintSpot spot = _book.Lay(_looks.For(family.looks[Variant(family.looks.Length, speedIn / paint.Scale)], paint.Fill),
                holder, point, facing, along, new Vector3(width, width * stretch, deep), reach, paint.Life, false);
            _book.Grow(spot, false, 0.5f - family.anchor, s.spreadTime);

            // The broken shell, lying on the paint (drawn after it).
            if (s.flakes.Length > 0)
            {
                PaintSpot shell = _book.Lay(_looks.For(s.flakes[Random.Range(0, s.flakes.Length)], paint.Shell, s.flakesDrawOrder),
                    holder, point, facing, along, new Vector3(core * s.flakesShare, core * s.flakesShare * stretch, deep), reach, paint.Life, false);
                _book.Grow(shell, false, 0f, s.spreadTime);
            }

            if (!soft && facing.y < s.runSteepness) AddRuns(paint, holder, point, facing, along, core, stretch, reach);
            AddSpatter(paint, holder, point, facing, along, core, width, stretch, reach, speedIn, soft);
            ThrowDrops(paint, point, facing, velocity, speedIn, diameter, soft);
        }

        /// <summary>The faint smear a ball leaves when it skips off whole. <paramref name="facing"/> is the surface's side
        /// the ball came from; <paramref name="velocity"/> is the ball's as it met it.</summary>
        public void Mark(Paint paint, Collider struck, Vector3 point, Vector3 facing, Vector3 velocity, float diameter)
        {
            var s = _settings;
            Marks++;
            float speedIn = Mathf.Max(-Vector3.Dot(velocity, facing), 0f);
            float angle = ImpactAngle(speedIn, velocity.magnitude);
            Vector3 along = Along(facing, velocity, angle);
            float width = diameter * s.markWidth;
            Vector2 reach = Reach(struck, point, facing, diameter, Mathf.Max(width * 0.5f, s.leastReach.y * paint.Scale) * 0.5f);
            _book.Lay(_looks.For(s.skipMark, paint.Shell), HolderOf(struck, point), point, facing, along,
                new Vector3(width, width * Mathf.Min(Stretch(angle), s.markLongest), reach.x + reach.y), reach, paint.Life, false);
        }

        /// <summary>A drop met something: it leaves a small spot, sized and stretched like a ball's splat.</summary>
        public void LandDrop(Paint paint, Collider struck, Vector3 point, Vector3 normal, Vector3 velocity, float dropWidth)
        {
            var s = _settings;
            if (s.drops.Length == 0) return;
            Vector3 facing = Facing(normal, velocity);
            float speedIn = Mathf.Max(-Vector3.Dot(velocity, facing), 0f);
            float angle = ImpactAngle(speedIn, velocity.magnitude);
            Vector3 along = Along(facing, velocity, angle);
            float core = paint.CoreWidth(speedIn, dropWidth) * (PaintSoft.Is(struck) ? s.softScale : 1f);
            float width = core / s.dropCoreShare;
            Vector2 reach = Reach(struck, point, facing, paint.BallDiameter, Mathf.Max(core * s.depth, s.leastReach.z * paint.Scale) * 0.5f);
            _book.Lay(_looks.For(s.drops[Random.Range(0, s.drops.Length)], paint.Fill), HolderOf(struck, point), point, facing, along,
                new Vector3(width, width * Stretch(angle), reach.x + reach.y), reach, paint.Life, true);
        }

        /// <summary>Bits of broken shell, sized to the ball, and a mist of the paint itself flung out wider.</summary>
        public void Burst(Paint paint, Vector3 point, Vector3 normal)
        {
            var s = _settings;
            float scale = paint.Scale;
            float size = s.burstSize * paint.BallDiameter / Mathf.Max(s.burstBall, 1e-6f);   // the bits shrink with the ball
            Vector3 from = point + normal * (0.03f * scale);
            _drops.Puff(from, normal, paint.Shell, s.burstAmount, s.burstSpeed * scale, size, s.burstGravity * scale, s.burstSeconds);
            _drops.Puff(from, normal, paint.Fill, Mathf.RoundToInt(s.burstAmount * s.mistShare), s.burstSpeed * s.mistSpeed * scale, size * s.mistSize, s.burstGravity * scale, s.burstSeconds);
        }

        /// <summary>A quick burst of small round bits (the Colour Corner's confetti uses the paint's own).
        /// <paramref name="size"/> is a bit's radius.</summary>
        public void Puff(Vector3 point, Vector3 way, Color colour, int amount, float speed, float size, float gravity, float seconds)
            => _drops.Puff(point, way, colour, amount, speed, size, gravity, seconds);

        /// <summary>The wet hit sound of a break.</summary>
        public void PlayHit(Vector3 point)
        {
            var clips = _settings.hitClips;
            if (clips.Length > 0) Play(clips[Random.Range(0, clips.Length)], point, _settings.hitVolume);
        }

        /// <summary>Plays a sound at a place, a little higher or lower each time.</summary>
        public void Play(AudioClip clip, Vector3 point, float volume = 1f)
        {
            if (clip == null) return;
            Sounds++;
            float spread = Mathf.Max(_settings.pitchSpread, 1f);
            _audio.transform.position = point;
            _audio.pitch = Mathf.Pow(spread, Random.Range(-1f, 1f));
            _audio.PlayOneShot(clip, volume);
        }

        // ---------- the parts of a splat ----------

        /// <summary>Paint running down a steep face: one to three runs leave the core's lower rim and grow down the face
        /// along the way gravity pulls on it, each a trickle ending in a bead.</summary>
        private void AddRuns(Paint paint, Transform holder, Vector3 point, Vector3 facing, Vector3 along, float core, float stretch, Vector2 reach)
        {
            var s = _settings;
            if (s.runs.Length == 0) return;
            Vector3 down = Vector3.down - facing * Vector3.Dot(Vector3.down, facing);
            if (down.magnitude < s.runOverhang) return;   // under an overhang this steep paint drips off instead of running
            down.Normalize();
            Vector3 across = Vector3.Cross(facing, along).normalized;
            float halfAcross = core * 0.5f, halfAlong = core * 0.5f * stretch;
            int count = Random.Range(s.runCount.x, s.runCount.y + 1);
            for (int k = 0; k < count; k++)
            {
                // Spread the runs over the lower rim, each a little off even.
                float fan = count == 1 ? 0f : Mathf.Lerp(-s.runFan, s.runFan, k / (float)(count - 1));
                Vector3 way = Quaternion.AngleAxis(fan + Random.Range(-s.runJitter, s.runJitter), facing) * down;
                // Where that way leaves the core's ellipse (it is stretched along the path like the splat).
                float a = Vector3.Dot(way, across) / halfAcross, b = Vector3.Dot(way, along) / halfAlong;
                Vector3 top = point + way * (s.runStart / Mathf.Sqrt(a * a + b * b));
                float length = core * Random.Range(s.runLength.x, s.runLength.y);
                PaintSpot run = _book.Lay(_looks.For(s.runs[Random.Range(0, s.runs.Length)], paint.Fill), holder, top, facing, down,
                    new Vector3(length / s.runShape, length, reach.x + reach.y), reach, paint.Life, false);
                _book.Grow(run, true, 0.5f, Random.Range(s.runTime.x, s.runTime.y), s.spreadTime + Random.Range(0f, s.runDelay));
            }
        }

        /// <summary>The spatter ring: small spots round the splat, flung outward, leaning downrange on an angled hit. Each
        /// is laid on the same surface as the splat (a spot that falls past its edge shows nothing). They count as drops,
        /// so they never push a splat out.</summary>
        private void AddSpatter(Paint paint, Transform holder, Vector3 point, Vector3 facing, Vector3 along, float core, float width, float stretch, Vector2 reach, float speedIn, bool soft)
        {
            var s = _settings;
            if (s.drops.Length == 0) return;
            float spray = Mathf.Clamp01(speedIn / paint.Scale / s.fullSpraySpeed) * (soft ? s.softSpatter : 1f);
            int count = Mathf.RoundToInt(Mathf.Lerp(s.spatterCount.x, s.spatterCount.y, spray));
            Vector3 across = Vector3.Cross(facing, along).normalized;
            float half = width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float turn = Random.Range(0f, Mathf.PI * 2f);
                Vector3 outward = (across * Mathf.Cos(turn) + along * Mathf.Sin(turn)).normalized;
                Vector3 at = point + outward * (half * Random.Range(s.spatterOut.x, s.spatterOut.y)) + along * (half * s.spatterLean * (stretch - 1f));
                float size = core * Random.Range(s.spatterSize.x, s.spatterSize.y);
                PaintSpot spot = _book.Lay(_looks.For(s.drops[Random.Range(0, s.drops.Length)], paint.Fill), holder, at, facing, outward,
                    new Vector3(size, size * Random.Range(s.spatterStretch.x, s.spatterStretch.y), reach.x + reach.y), reach, paint.Life, true);
                _book.Grow(spot, false, 0f, s.spreadTime * s.spatterSpreadSlow, Random.Range(0f, s.spatterDelay));
            }
        }

        /// <summary>Throws drops off a break: each keeps part of the ball's speed along the surface and gets a small kick
        /// out across it and up off it. Faster hits throw more; soft things fewer.</summary>
        private void ThrowDrops(Paint paint, Vector3 point, Vector3 facing, Vector3 velocity, float speedIn, float diameter, bool soft)
        {
            var s = _settings;
            float spray = Mathf.Clamp01(speedIn / paint.Scale / s.fullSpraySpeed) * (soft ? s.softDrops : 1f);
            int count = Mathf.RoundToInt(Mathf.Lerp(s.dropCount.x, s.dropCount.y, spray));
            Vector3 flat = velocity + facing * speedIn;
            Vector3 side = Vector3.Cross(facing, Mathf.Abs(facing.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 outward = Quaternion.AngleAxis(Random.Range(0f, 360f), facing) * side;
                float width = diameter * Random.Range(s.dropSize.x, s.dropSize.y);
                Vector3 kick = flat * Random.Range(s.dropCarry.x, s.dropCarry.y)
                             + outward * (speedIn * Random.Range(s.dropOut.x, s.dropOut.y))
                             + facing * (speedIn * Random.Range(s.dropUp.x, s.dropUp.y));
                if (!_drops.Throw(paint, point + facing * (width + s.dropLift * paint.Scale), kick, width)) return;
            }
        }

        // ---------- where paint sticks, and how deep it reaches ----------

        /// <summary>The part paint sticks to: the drawn part of what was hit that is nearest the hit (a target's rocking
        /// pivot, a soldier's tipping body), so the paint moves with what is seen. With nothing drawn, the thing itself.</summary>
        public Transform HolderOf(Collider struck, Vector3 point)
        {
            if (struck == null) return transform;
            Transform root = struck.attachedRigidbody != null ? struck.attachedRigidbody.transform : struck.transform;
            root.GetComponentsInChildren(false, Drawn);
            Transform best = root; float nearest = float.MaxValue;
            foreach (Renderer drawn in Drawn)
            {
                if (!(drawn is MeshRenderer) && !(drawn is SkinnedMeshRenderer)) continue;
                float gap = drawn.bounds.SqrDistance(point);
                if (gap < nearest) { nearest = gap; best = drawn.transform; }
            }
            Drawn.Clear();
            return best;
        }

        /// <summary>How far paint reaches above a hit and into what was hit (x up, y in): up reachUp, and in to the far
        /// side of the shape that was hit, reachIn at most, both for a reachBall ball and scaled to this one
        /// (<paramref name="ball"/> wide). Never less than <paramref name="least"/> either way.</summary>
        private Vector2 Reach(Collider struck, Vector3 point, Vector3 facing, float ball, float least)
        {
            float sizeUp = ball / Mathf.Max(_settings.reachBall, 1e-6f);
            float deepest = Mathf.Max(_settings.reachIn * sizeUp, least);
            float inward = Mathf.Clamp(FarSide(struck, point, facing, deepest), least, deepest);
            return new Vector2(Mathf.Max(_settings.reachUp * sizeUp, least), inward);
        }

        /// <summary>How deep the shape that was hit goes behind <paramref name="point"/>, straight in along
        /// -<paramref name="facing"/>, found by looking from <paramref name="most"/> deep back out toward the hit: the
        /// nearest face of it seen from there is its far face. A solid deeper than <paramref name="most"/> shows none
        /// (most), and so does a mesh with nothing behind its face (0: a sheet).</summary>
        private float FarSide(Collider struck, Vector3 point, Vector3 facing, float most)
        {
            if (struck == null) return most;
            Vector3 from = point - facing * most;
            float nearest = float.PositiveInfinity;
            float step = _settings.farSideStep * most / Mathf.Max(_settings.reachIn, 1e-6f);
            for (int ray = 0; ray < _settings.farSideRays; ray++)
            {
                float left = Vector3.Dot(point - from, facing);
                if (left <= 0f || !struck.Raycast(new Ray(from, facing), out RaycastHit hit, left)) break;
                nearest = Mathf.Min(nearest, Vector3.Dot(point - hit.point, facing));
                from = hit.point + facing * step;   // on past that face toward the hit, for a nearer one
            }
            if (nearest < float.PositiveInfinity) return Mathf.Max(nearest, 0f);
            return IsSolid(struck) ? most : 0f;
        }

        /// <summary>True for a solid shape (a box, ball, capsule or convex hull), false for a mesh or a terrain: only a
        /// skin, with nothing behind its faces.</summary>
        private static bool IsSolid(Collider struck) => !(struck is MeshCollider mesh && !mesh.convex) && !(struck is TerrainCollider);

        // ---------- the geometry of a hit ----------

        /// <summary>The side of the surface the shot came from (a ray can meet the back of a face).</summary>
        public static Vector3 Facing(Vector3 normal, Vector3 velocity) => Vector3.Dot(velocity, normal) <= 0f ? normal : -normal;

        /// <summary>Degrees between the path and the surface.</summary>
        public static float ImpactAngle(float speedIn, float speed) => Mathf.Asin(Mathf.Clamp01(speedIn / Mathf.Max(speed, 0.0001f))) * Mathf.Rad2Deg;

        /// <summary>How many times longer than wide the paint lands at this angle.</summary>
        private float Stretch(float angle) => 1f / Mathf.Sin(Mathf.Max(angle, _settings.flattest) * Mathf.Deg2Rad);

        /// <summary>The way along the surface the path was going (downrange). A square-on hit has none, so any way along
        /// the surface will do.</summary>
        private Vector3 Along(Vector3 facing, Vector3 velocity, float angle)
        {
            Vector3 flat = velocity - facing * Vector3.Dot(velocity, facing);
            if (angle < _settings.squareOn && flat.sqrMagnitude > 1e-8f) return flat.normalized;
            Vector3 across = Vector3.Cross(facing, Mathf.Abs(facing.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            return Quaternion.AngleAxis(Random.Range(0f, 360f), facing) * across;
        }

        /// <summary>The splat family drawn nearest this impact angle.</summary>
        private PaintSettings.Family FamilyFor(float angle)
        {
            PaintSettings.Family best = null;
            foreach (PaintSettings.Family family in _settings.families)
                if (best == null || Mathf.Abs(angle - family.drawnAt) < Mathf.Abs(angle - best.drawnAt)) best = family;
            return best;
        }

        /// <summary>Calm pictures (the first half) for slow hits, rich ones (the second half) for fast.
        /// <paramref name="speedIn"/> at toy size.</summary>
        private int Variant(int pictures, float speedIn)
        {
            int half = Mathf.Max(pictures / 2, 1);
            bool rich = pictures > 1 && Random.value < Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_settings.richSpeed * _settings.richRange.x, _settings.richSpeed * _settings.richRange.y, speedIn));
            return Mathf.Min((rich ? half : 0) + Random.Range(0, half), pictures - 1);
        }

        private void WarnNoPictures()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("PAINT no splat pictures in Resources/" + PaintSettings.ResourceName + ": run Shift Jam > Paint > Build Paint. Balls break but leave nothing.");
        }
    }
}
