using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>One paint decal and its life: when it was laid, when it fades, how it grows.</summary>
    public sealed class PaintSpot
    {
        internal DecalProjector Decal;
        internal float Born, FadeAt, GoneAt;
        internal bool Pushed, Drop;
        // Growing: the full size (across, along, deep), how far its middle sits downrange of its fixed point in lengths,
        // the seconds it takes, the seconds before it starts, whether it is a run (hidden until it starts), and how far
        // past its full size it swells before it settles (a share of that size; 0 = it stops at full).
        internal bool Growing, Run;
        internal Vector3 Full;
        internal float Shift, Seconds, Delay, Centre, Pop;
    }

    /// <summary>
    /// Keeps a scene's paint decals: lays each one stuck to what it landed on (a URP Decal Projector, so the paint wraps
    /// the drawn model and moves with it), spreads the splats and grows the runs, fades old paint out, and holds the two
    /// budgets (paint, and drop spots): past a budget the oldest of that kind fades out early. Decal objects are reused.
    /// A decal looks along its own +Z into the surface; its picture's bottom (downrange) is its -Y.
    /// </summary>
    public sealed class PaintBook
    {
        private readonly PaintSettings _settings;
        private readonly Transform _spare;                       // put-away decals wait under here
        private readonly List<PaintSpot> _spots = new List<PaintSpot>();     // every decal, oldest first
        private readonly List<PaintSpot> _growing = new List<PaintSpot>();   // the ones still spreading or growing
        private readonly Stack<PaintSpot> _pool = new Stack<PaintSpot>();
        private float _clock;       // seconds since this scene's paint began; every decal's times are on it
        private float _lastStep;

        public PaintBook(PaintSettings settings, Transform spare) { _settings = settings; _spare = spare; }

        /// <summary>Paint decals (splats, shell, runs, marks) and drop spots not yet pushed out by their budgets.</summary>
        public int LivePaint { get; private set; }
        public int LiveDrops { get; private set; }
        /// <summary>Every decal in the scene, those fading out included.</summary>
        public int Count => _spots.Count;

        /// <summary>Lays a decal at <paramref name="at"/> on <paramref name="holder"/>, looking into the surface along
        /// -<paramref name="facing"/> with its picture's bottom toward <paramref name="along"/> (downrange).
        /// <paramref name="full"/> is its size across, along and deep; <paramref name="reach"/> how far it reaches above
        /// the surface (x) and into it (y). Starts its life and makes room for it under its budget.</summary>
        public PaintSpot Lay(Material look, Transform holder, Vector3 at, Vector3 facing, Vector3 along, Vector3 full, Vector2 reach, float life, bool drop)
        {
            if (look == null) return null;
            PaintSpot spot = null;
            while (spot == null && _pool.Count > 0) { spot = _pool.Pop(); if (spot.Decal == null) spot = null; }   // its holder took it along when it was destroyed
            if (spot == null)
            {
                var go = new GameObject("Paint");
                go.SetActive(false);
                spot = new PaintSpot { Decal = go.AddComponent<DecalProjector>() };
            }
            DecalProjector decal = spot.Decal;
            Transform t = decal.transform;
            t.SetParent(holder, false);
            t.SetPositionAndRotation(at, Quaternion.LookRotation(-facing, -along));
            spot.Centre = (reach.y - reach.x) * 0.5f;
            decal.material = look;
            decal.scaleMode = DecalScaleMode.ScaleInvariant;   // sizes are world units whatever the holder's scale
            decal.size = full;
            decal.pivot = new Vector3(0f, 0f, spot.Centre);
            decal.fadeFactor = 1f;
            decal.drawDistance = _settings.drawDistance;
            decal.startAngleFade = _settings.angleFade.x;
            decal.endAngleFade = _settings.angleFade.y;
            decal.enabled = true;
            decal.gameObject.SetActive(true);

            float fade = Mathf.Min(_settings.fadeTime, life);
            spot.Born = _clock; spot.FadeAt = _clock + life - fade; spot.GoneAt = _clock + life;
            spot.Pushed = false; spot.Drop = drop; spot.Growing = false; spot.Full = full;
            _spots.Add(spot);
            if (drop) LiveDrops++; else LivePaint++;
            MakeRoom(drop);
            return spot;
        }

        /// <summary>Makes a decal grow: a splat spreading out from where the ball touched, or a run growing down from its
        /// top. <paramref name="shift"/> is how far its middle sits downrange of that point, in lengths. With
        /// <paramref name="pop"/> above 0 a splat swells that share past its full size as it finishes spreading and
        /// settles back over the settings' Spread Settle (runs never pop).</summary>
        public void Grow(PaintSpot spot, bool run, float shift, float seconds, float delay = 0f, float pop = 0f)
        {
            if (spot == null) return;
            spot.Growing = true; spot.Run = run; spot.Shift = shift; spot.Seconds = Mathf.Max(seconds, 1e-4f); spot.Delay = delay;
            spot.Pop = run || _settings.spreadSettle <= 0f ? 0f : Mathf.Max(pop, 0f);
            _growing.Add(spot);
            Shape(spot);
        }

        /// <summary>One frame: spread and grow, fade the old, then move the clock (so new paint is drawn at its starting size first).</summary>
        public void Step(float dt)
        {
            for (int i = _growing.Count - 1; i >= 0; i--)
            {
                PaintSpot spot = _growing[i];
                if (spot.Decal == null || !spot.Growing || !Shape(spot)) { spot.Growing = false; _growing.RemoveAt(i); }
            }
            int at = 0;
            while (at < _spots.Count)
            {
                PaintSpot spot = _spots[at];
                if (spot.Decal == null) { Forget(at); continue; }          // its holder was destroyed: forgotten
                if (_clock >= spot.FadeAt)
                {
                    float left = (spot.GoneAt - _clock) / Mathf.Max(spot.GoneAt - spot.FadeAt, 0.001f);
                    if (left <= 0f) { Forget(at); PutAway(spot); continue; }
                    spot.Decal.fadeFactor = left;
                }
                at++;
            }
            _lastStep = dt;
            _clock += dt;
        }

        /// <summary>Takes every decal away at once.</summary>
        public void Clear()
        {
            foreach (PaintSpot spot in _spots) PutAway(spot);
            _spots.Clear();
            _growing.Clear();
            LivePaint = 0; LiveDrops = 0;
        }

        /// <summary>Sizes a growing decal for its age and keeps its fixed point still. False once it is done.</summary>
        private bool Shape(PaintSpot spot)
        {
            float age = _clock - spot.Born;
            float done = Mathf.Clamp01((age - spot.Delay) / spot.Seconds);
            float share;
            bool settling = false;
            if (!spot.Run)
            {
                // Spreads to its full size and, if it pops, on past it by Pop; then eases back to full.
                share = Mathf.Lerp(_settings.spreadStart, 1f + spot.Pop, EaseOut(done));
                if (spot.Pop > 0f && done >= 1f)
                {
                    float back = Mathf.Clamp01((age - spot.Delay - spot.Seconds) / _settings.spreadSettle);
                    share = 1f + spot.Pop * (1f - Mathf.SmoothStep(0f, 1f, back));
                    settling = back < 1f;
                }
            }
            else
            {
                // A run grows wider as it grows longer, keeping the shape it was drawn at, so its bead stays round.
                bool started = age >= spot.Delay;
                if (spot.Decal.enabled != started) spot.Decal.enabled = started;
                share = Mathf.Max(EaseOut(done), Mathf.Min(_settings.runThinnest / Mathf.Max(spot.Full.x, 1e-6f), 1f));
            }
            float along = spot.Full.y * share;
            spot.Decal.size = new Vector3(spot.Full.x * share, along, spot.Full.z);
            spot.Decal.pivot = new Vector3(0f, -spot.Shift * along, spot.Centre);   // downrange is the decal's -Y
            return done < 1f || settling;
        }

        /// <summary>Keeps a budget, the drops' or the rest's: past it, the oldest of that kind fades out quickly.</summary>
        private void MakeRoom(bool drop)
        {
            int most = drop ? _settings.mostDrops : _settings.mostPaint;
            for (int i = 0; i < _spots.Count && (drop ? LiveDrops : LivePaint) > most; i++)
            {
                PaintSpot spot = _spots[i];
                if (spot.Drop != drop || spot.Pushed) continue;
                spot.Pushed = true;
                if (drop) LiveDrops--; else LivePaint--;
                // Paint already fading at the end of its life just carries on. The fade counts from a frame ago, so the
                // paint pushed out is already fading when the new paint first shows.
                if (spot.FadeAt > _clock)
                {
                    spot.FadeAt = _clock - _lastStep;
                    spot.GoneAt = spot.FadeAt + (drop ? _settings.pushedDropFade : _settings.pushedFade);
                }
            }
        }

        private void Forget(int index)
        {
            PaintSpot spot = _spots[index];
            if (!spot.Pushed) { if (spot.Drop) LiveDrops--; else LivePaint--; }
            _spots.RemoveAt(index);
        }

        private void PutAway(PaintSpot spot)
        {
            spot.Growing = false;
            if (spot.Decal == null) return;
            spot.Decal.gameObject.SetActive(false);
            if (_spare != null) spot.Decal.transform.SetParent(_spare, false);
            _pool.Push(spot);
        }

        private static float EaseOut(float t) { float u = 1f - t; return 1f - u * u * u; }
    }
}
