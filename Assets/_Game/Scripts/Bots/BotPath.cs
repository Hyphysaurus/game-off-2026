using UnityEngine;
using UnityEngine.AI;

namespace ShiftJam
{
    /// <summary>
    /// One bot's way over the mapped floor (D-0188), for its BotBrain: it asks for a way to a spot (BotGround.Way), is told
    /// the next corner to walk to, whether it has arrived, and how long it has made no headway. Each bot keeps one of
    /// these, with one NavMeshPath and one corner array for its whole life: asking makes no garbage.
    /// A plain part, not a component: the brain makes it when it wakes (and again after a script reload).
    /// </summary>
    public sealed class BotPath
    {
        /// <summary>The most corners kept of one way. A longer way is walked to its last kept corner and asked again from
        /// there (the Stadium's longest, base to base, has 6).</summary>
        public const int MostCorners = 32;

        private readonly NavMeshPath _path = new NavMeshPath();
        private readonly Vector3[] _corners = new Vector3[MostCorners];
        private int _count, _next;
        private bool _has, _cut;
        private Vector3 _goal;
        private float _noHeadway;

        /// <summary>True while it has a way to walk.</summary>
        public bool Has => _has;
        /// <summary>The spot the way was asked for (it ends on the floor nearest it).</summary>
        public Vector3 Goal => _goal;
        /// <summary>Seconds it has wanted to move without making headway (Track).</summary>
        public float NoHeadway => _noHeadway;

        /// <summary>Asks for the way from <paramref name="from"/> to <paramref name="to"/>. True when one can be walked
        /// whole; false (and no way) with no map, an end off the map, or an end that cannot be reached.</summary>
        public bool Ask(Vector3 from, Vector3 to)
        {
            _noHeadway = 0f;
            if (!Measure(from, to, out _)) { Clear(); return false; }
            _goal = to;
            _has = true;
            _next = _count > 1 ? 1 : 0;   // the first corner is where it stands
            return true;
        }

        /// <summary>Whether a way from <paramref name="from"/> to <paramref name="to"/> can be walked, and how long it is
        /// (<paramref name="length"/>, world units). It uses the one kept path, so the way it had is gone after this
        /// (Has is false): ask again for the one it chooses.</summary>
        public bool Probe(Vector3 from, Vector3 to, out float length)
        {
            bool ok = Measure(from, to, out length);
            Clear();
            return ok;
        }

        private bool Measure(Vector3 from, Vector3 to, out float length)
        {
            length = 0f;
            _has = false;
            if (!BotGround.Way(from, to, _path)) return false;
            int n = _path.GetCornersNonAlloc(_corners);
            _count = Mathf.Min(n, _corners.Length);
            if (_count <= 0) return false;
            _cut = n > _corners.Length;
            for (int i = 1; i < _count; i++) length += Vector3.Distance(_corners[i - 1], _corners[i]);
            return true;
        }

        /// <summary>No way: it has nowhere to walk until it asks again.</summary>
        public void Clear()
        {
            _has = _cut = false;
            _count = _next = 0;
            _noHeadway = 0f;
        }

        /// <summary>The corner to walk toward from <paramref name="at"/>: corners nearer than <paramref name="within"/>
        /// (flat) count as passed. A way cut short at its last kept corner is asked again from there. With no way, where
        /// it stands.</summary>
        public Vector3 Next(Vector3 at, float within)
        {
            if (!_has) return at;
            while (_next < _count - 1 && Flat(at, _corners[_next]) <= within) _next++;
            if (_cut && _next == _count - 1 && Flat(at, _corners[_next]) <= within && !Ask(at, _goal)) return at;
            return _corners[_next];
        }

        /// <summary>True once it stands within <paramref name="within"/> (flat) of the way's end.</summary>
        public bool Arrived(Vector3 at, float within) =>
            _has && !_cut && _count > 0 && _next >= _count - 1 && Flat(at, _corners[_count - 1]) <= within;

        /// <summary>Headway, once a frame: <paramref name="moved"/> is how far it went since the last frame (flat),
        /// <paramref name="wanted"/> the velocity it was told to walk at then. Slower than <paramref name="least"/> of
        /// that speed, along that way, counts toward NoHeadway; standing still on purpose does not.</summary>
        public void Track(Vector3 moved, Vector3 wanted, float dt, float least)
        {
            wanted.y = 0f; moved.y = 0f;
            float want = wanted.magnitude;
            if (dt <= 0f || want < 1e-3f) { _noHeadway = 0f; return; }
            float made = Vector3.Dot(moved, wanted / want) / dt;
            _noHeadway = made < least * want ? _noHeadway + dt : 0f;
        }

        /// <summary>Headway counted afresh (a new way, a sidestep, a gap in the asking).</summary>
        public void ResetHeadway() => _noHeadway = 0f;

        /// <summary>The distance between two points seen from above.</summary>
        public static float Flat(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
