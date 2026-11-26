using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Sorts a model's points for a cloth (D-0233). First the model's separate pieces: points joined by its triangles, or
    /// sitting on the very same spot (a seam splits one corner into several points), are one piece. Then each point: a
    /// point within Hold Distance of the model's top (a curtain on its rod) or of its pole (a flag: the upright line through
    /// the model's own pivot) is held. A piece with points on both sides of that line hangs from its held points and its
    /// other points move; a piece wholly inside stays as modelled; a loose piece wholly outside (it touches nothing held,
    /// so it would fall away) stays as modelled too, unless Join Pieces is on (Magica may then join it to its neighbours).
    /// The curtain (Playroom Curtain_01, a raised blind): 31 pieces; with the hold at 16.5 only the pull cord moves,
    /// hanging from its top ring. The flag (Army_Flag_01): 3 pieces (the pole, its knob, the flag); with 0.6 the flag flies
    /// from its edge along the pole.
    /// It reads the model once (its mesh must be readable) into lists that are kept and reused: a build allocates little,
    /// and nothing here runs every frame. Also: a line against a box (the shots' poke).
    /// </summary>
    public static class ClothShape
    {
        /// <summary>What one point of the model does: stays as modelled, is held (an anchor of a moving piece), or moves.</summary>
        public enum Part : byte { Still = 0, Held = 1, Moves = 2 }

        /// <summary>What a sort found.</summary>
        public struct Counts
        {
            /// <summary>Points in the model, pieces in it, pieces that move, loose pieces (nothing in them is held).</summary>
            public int points, pieces, movingPieces, loosePieces;
            /// <summary>Points that move, points held, points that stay as modelled.</summary>
            public int moving, held, still;
            /// <summary>Every point of the moving pieces (their held points too), in the model's own space; empty when
            /// nothing moves.</summary>
            public Bounds movingBounds;
            /// <summary>The held points only (Magica's cloth centre is their middle), in the model's own space; empty when
            /// nothing is held.</summary>
            public Bounds heldBounds;
        }

        private static readonly List<Vector3> Points = new List<Vector3>();
        private static readonly List<int> Corners = new List<int>();
        private static readonly Dictionary<Vector3, int> Spots = new Dictionary<Vector3, int>();
        private static int[] _root = new int[0];
        private static int[] _pieceOfRoot = new int[0];
        private static bool[] _heldPoint = new bool[0];
        private static bool[] _hasIn = new bool[0], _hasOut = new bool[0];

        /// <summary>
        /// Sorts <paramref name="mesh"/>'s points into <paramref name="parts"/> (one a point, at least the mesh's point
        /// count long) and, when given, each point's piece number into <paramref name="pieceOf"/>. <paramref name="hold"/>
        /// and <paramref name="holdDistance"/> (the model's own units) say which points are held;
        /// <paramref name="joinLoose"/>: a loose piece moves instead of staying (Join Pieces). The mesh must be readable.
        /// </summary>
        public static Counts Sort(Mesh mesh, ClothSettings.HoldFrom hold, float holdDistance, bool joinLoose, Part[] parts, int[] pieceOf)
        {
            var c = new Counts();
            if (mesh == null || parts == null) return c;
            int n = mesh.vertexCount;
            if (n == 0 || parts.Length < n) return c;
            c.points = n;
            mesh.GetVertices(Points);
            Grow(ref _root, n);
            Grow(ref _pieceOfRoot, n);
            Grow(ref _heldPoint, n);
            for (int i = 0; i < n; i++) { _root[i] = i; _pieceOfRoot[i] = -1; }

            // Points joined by a triangle are one piece.
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                mesh.GetTriangles(Corners, sub);
                for (int t = 0; t + 2 < Corners.Count; t += 3)
                {
                    Join(Corners[t], Corners[t + 1]);
                    Join(Corners[t], Corners[t + 2]);
                }
            }
            // So are points on the same spot (Magica joins those too when it makes its cloth).
            Spots.Clear();
            for (int i = 0; i < n; i++)
            {
                if (Spots.TryGetValue(Points[i], out int first)) Join(first, i);
                else Spots.Add(Points[i], i);
            }
            Spots.Clear();

            // Number the pieces.
            int pieces = 0;
            for (int i = 0; i < n; i++)
            {
                int r = Find(i);
                if (_pieceOfRoot[r] < 0) _pieceOfRoot[r] = pieces++;
            }
            c.pieces = pieces;

            // Which points are held, and which pieces have points on each side of the line.
            float top = float.NegativeInfinity;
            if (hold == ClothSettings.HoldFrom.Top)
                for (int i = 0; i < n; i++) top = Mathf.Max(top, Points[i].y);
            Grow(ref _hasIn, pieces);
            Grow(ref _hasOut, pieces);
            for (int p = 0; p < pieces; p++) { _hasIn[p] = false; _hasOut[p] = false; }
            for (int i = 0; i < n; i++)
            {
                Vector3 v = Points[i];
                float away = hold == ClothSettings.HoldFrom.Top ? top - v.y : Mathf.Sqrt(v.x * v.x + v.z * v.z);
                bool held = away <= holdDistance;
                _heldPoint[i] = held;
                int p = _pieceOfRoot[Find(i)];
                if (held) _hasIn[p] = true; else _hasOut[p] = true;
            }
            for (int p = 0; p < pieces; p++)
            {
                if (_hasIn[p] && _hasOut[p]) c.movingPieces++;
                else if (!_hasIn[p]) { c.loosePieces++; if (joinLoose) c.movingPieces++; }
            }

            // Each point's part, and the boxes round the moving pieces and round the held points.
            bool any = false, anyHeld = false;
            Vector3 lo = Vector3.zero, hi = Vector3.zero, heldLo = Vector3.zero, heldHi = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                int p = _pieceOfRoot[Find(i)];
                Part part;
                if (_hasIn[p] && _hasOut[p]) part = _heldPoint[i] ? Part.Held : Part.Moves;
                else if (!_hasIn[p] && joinLoose) part = Part.Moves;
                else part = Part.Still;
                parts[i] = part;
                if (pieceOf != null && i < pieceOf.Length) pieceOf[i] = p;
                if (part == Part.Moves) c.moving++;
                else if (part == Part.Held) c.held++;
                else c.still++;
                if (part == Part.Still) continue;
                Vector3 v = Points[i];
                if (!any) { lo = hi = v; any = true; }
                else { lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
                if (part != Part.Held) continue;
                if (!anyHeld) { heldLo = heldHi = v; anyHeld = true; }
                else { heldLo = Vector3.Min(heldLo, v); heldHi = Vector3.Max(heldHi, v); }
            }
            c.movingBounds = any ? new Bounds((lo + hi) * 0.5f, hi - lo) : new Bounds();
            c.heldBounds = anyHeld ? new Bounds((heldLo + heldHi) * 0.5f, heldHi - heldLo) : new Bounds();
            return c;
        }

        /// <summary>
        /// Where a line from <paramref name="from"/> going <paramref name="way"/> first meets the box from
        /// <paramref name="min"/> to <paramref name="max"/>: how far along the line, in the line's own units (0 when it
        /// starts inside). False when it misses, or the box is behind it.
        /// </summary>
        public static bool LineMeetsBox(Vector3 from, Vector3 way, Vector3 min, Vector3 max, out float along)
        {
            float enter = 0f, exit = float.PositiveInfinity;
            along = 0f;
            for (int a = 0; a < 3; a++)
            {
                float o = from[a], d = way[a];
                if (Mathf.Abs(d) < 1e-8f)
                {
                    if (o < min[a] || o > max[a]) return false;   // alongside the box, outside it
                    continue;
                }
                float t1 = (min[a] - o) / d, t2 = (max[a] - o) / d;
                if (t1 > t2) { float swap = t1; t1 = t2; t2 = swap; }
                if (t1 > enter) enter = t1;
                if (t2 < exit) exit = t2;
                if (enter > exit) return false;
            }
            along = enter;
            return true;
        }

        private static int Find(int i)
        {
            while (_root[i] != i)
            {
                _root[i] = _root[_root[i]];
                i = _root[i];
            }
            return i;
        }

        private static void Join(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra != rb) _root[ra] = rb;
        }

        private static void Grow(ref int[] list, int n) { if (list.Length < n) list = new int[Mathf.NextPowerOfTwo(n)]; }

        private static void Grow(ref bool[] list, int n) { if (list.Length < n) list = new bool[Mathf.NextPowerOfTwo(n)]; }
    }
}
