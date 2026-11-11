#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Makes track pieces the pack does not have out of a straight one it does (D-0048): the same piece bent round a
    /// corner, and the same piece with a side rail pressed flat so two lanes can sit side by side. Since D-0050 it also
    /// sweeps the piece along any path (<see cref="Sweep"/>): banked bends, climbing spirals, crests, dips and loops, and
    /// the walls that run beside them (<see cref="SweepWalls"/>). The straight runs along +Z with its road surface at
    /// y = 0 and a rail down each side; nothing else is assumed (the road's half width and the length are read from the
    /// mesh). Editor only: the results are saved as mesh assets by whoever calls this.
    /// </summary>
    public static class TrackBender
    {
        /// <summary>What a straight piece measures, read from its mesh.</summary>
        public readonly struct Shape
        {
            /// <summary>Half the road's width: where the road ends and a rail begins.</summary>
            public readonly float RoadHalf;
            /// <summary>Half the whole piece's width, rails included.</summary>
            public readonly float OuterHalf;
            /// <summary>Where the piece starts along Z, and how long it is.</summary>
            public readonly float Start, Length;
            /// <summary>How far the piece reaches below the road surface.</summary>
            public readonly float Depth;

            public Shape(float roadHalf, float outerHalf, float start, float length, float depth)
            { RoadHalf = roadHalf; OuterHalf = outerHalf; Start = start; Length = length; Depth = depth; }
        }

        private const float RoadTolerance = 0.005f;

        /// <summary>Reads the straight piece: the road is the part lying at y = 0, the rails are everything wider.</summary>
        public static Shape Measure(Mesh source)
        {
            var v = source.vertices;
            float roadHalf = 0f, outerHalf = 0f, zMin = float.MaxValue, zMax = float.MinValue, yMin = 0f;
            foreach (var p in v)
            {
                if (Mathf.Abs(p.y) <= RoadTolerance) roadHalf = Mathf.Max(roadHalf, Mathf.Abs(p.x));
                outerHalf = Mathf.Max(outerHalf, Mathf.Abs(p.x));
                zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z); yMin = Mathf.Min(yMin, p.y);
            }
            return new Shape(roadHalf, outerHalf, zMin, zMax - zMin, -yMin);
        }

        /// <summary>
        /// A copy of the straight piece, starting at z = 0. A rail that is not kept is pressed flat to the road's height (it
        /// stays as a flush lip, so the piece is still closed and a second lane can butt against it). With
        /// <paramref name="degrees"/> other than zero the piece is bent round a corner of that angle (positive = to the
        /// right) whose centre line has <paramref name="radius"/>, in the mesh's own units; it is first cut into
        /// <paramref name="slices"/> along its length so the bend is round, not folded.
        /// </summary>
        public static Mesh Make(Mesh source, string name, bool keepLeftRail, bool keepRightRail, float degrees, float radius, int slices)
        {
            Shape shape = Measure(source);
            var pos = new List<Vector3>(source.vertices);
            var nrm = new List<Vector3>(source.normals);
            var uvs = new List<Vector2>(source.uv);
            var tris = new List<int>(source.triangles);
            if (nrm.Count != pos.Count) { source.RecalculateNormals(); nrm = new List<Vector3>(source.normals); }
            while (uvs.Count < pos.Count) uvs.Add(Vector2.zero);

            for (int i = 0; i < pos.Count; i++)
            {
                Vector3 p = pos[i];
                p.z -= shape.Start;
                bool rail = Mathf.Abs(p.x) >= shape.RoadHalf - RoadTolerance;
                bool pressed = rail && ((p.x > 0f && !keepRightRail) || (p.x < 0f && !keepLeftRail));
                if (pressed && p.y > 0f) { p.y = 0f; nrm[i] = Vector3.up; }
                pos[i] = p;
            }

            bool bend = Mathf.Abs(degrees) > 0.01f && radius > shape.OuterHalf;
            if (bend)
            {
                slices = Mathf.Max(2, slices);
                for (int k = 1; k < slices; k++) Slice(pos, nrm, uvs, tris, shape.Length * k / slices);
                for (int i = 0; i < pos.Count; i++)
                {
                    float along = Mathf.Clamp01(pos[i].z / shape.Length);
                    pos[i] = Bend(new Vector3(pos[i].x, pos[i].y, 0f), along * degrees, radius);
                    nrm[i] = Quaternion.Euler(0f, along * degrees, 0f) * nrm[i];
                }
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Where the road is, and which way it faces, at a share <c>t</c> (0 to 1) of the way along a piece:
        /// a position and a turn, both measured from the piece's own start (so at 0 they are zero and no turn).</summary>
        public delegate void Path(float t, out Vector3 position, out Quaternion turn);

        /// <summary>
        /// A copy of the straight piece carried along <paramref name="path"/>: each cross-section of the piece is set down
        /// where the path says, facing the way the path says. Rails that are not kept are pressed flat as in
        /// <see cref="Make"/>. The piece is cut into <paramref name="slices"/> first so the result follows the path
        /// smoothly. Positions are in the mesh's own units. <paramref name="widen"/> stretches the road between the rails by
        /// that factor and moves the rails out with it, unchanged in size (see <see cref="Widened"/> for the shape that
        /// results).
        /// </summary>
        public static Mesh Sweep(Mesh source, string name, bool keepLeftRail, bool keepRightRail, int slices, Path path, float widen = 1f)
        {
            Shape shape = Measure(source);
            var pos = new List<Vector3>(source.vertices);
            var nrm = new List<Vector3>(source.normals);
            var uvs = new List<Vector2>(source.uv);
            var tris = new List<int>(source.triangles);
            if (nrm.Count != pos.Count) { source.RecalculateNormals(); nrm = new List<Vector3>(source.normals); }
            while (uvs.Count < pos.Count) uvs.Add(Vector2.zero);
            for (int i = 0; i < pos.Count; i++)
            {
                Vector3 p = pos[i];
                p.z -= shape.Start;
                bool rail = Mathf.Abs(p.x) >= shape.RoadHalf - RoadTolerance;
                bool pressed = rail && ((p.x > 0f && !keepRightRail) || (p.x < 0f && !keepLeftRail));
                if (pressed && p.y > 0f) { p.y = 0f; nrm[i] = Vector3.up; }
                pos[i] = p;
            }
            slices = Mathf.Max(2, slices);
            for (int k = 1; k < slices; k++) Slice(pos, nrm, uvs, tris, shape.Length * k / slices);
            for (int i = 0; i < pos.Count; i++)
            {
                path(Mathf.Clamp01(pos[i].z / shape.Length), out Vector3 at, out Quaternion turn);
                float across = pos[i].x;
                if (widen != 1f)
                {
                    float off = Mathf.Abs(across);   // the road part stretches; whatever lies beyond it (a rail) only moves out
                    across = Mathf.Sign(across) * (Mathf.Min(off, shape.RoadHalf) * widen + Mathf.Max(0f, off - shape.RoadHalf));
                }
                pos[i] = at + turn * new Vector3(across, pos[i].y, 0f);
                nrm[i] = turn * nrm[i];
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The walls beside a swept piece: a thin upright strip over each rail asked for, from just above the road to
        /// <paramref name="height"/>, following the same <paramref name="path"/>. One mesh for both sides: it serves as the
        /// collider that keeps a car on the road and, with a renderer, as a wall you can see. Every corner takes
        /// <paramref name="uv"/> (one colour of the pack's palette).
        /// </summary>
        public static Mesh SweepWalls(Shape shape, string name, float height, bool left, bool right, int slices, Path path, Vector2 uv)
        {
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float inner = shape.RoadHalf + 0.04f, outer = shape.OuterHalf - 0.04f, foot = 0.1f;
            slices = Mathf.Max(2, slices);
            foreach (float side in new[] { -1f, 1f })
            {
                if ((side < 0f && !left) || (side > 0f && !right)) continue;
                // three faces a side (the one toward the road, the top, the one away from it), each with its own corners so it shades flat
                var faces = new (Vector2 a, Vector2 b, Vector3 normal)[]
                {
                    (new Vector2(side * inner, foot), new Vector2(side * inner, height), new Vector3(-side, 0f, 0f)),
                    (new Vector2(side * inner, height), new Vector2(side * outer, height), Vector3.up),
                    (new Vector2(side * outer, height), new Vector2(side * outer, foot), new Vector3(side, 0f, 0f)),
                };
                foreach (var face in faces)
                {
                    int first = pos.Count;
                    for (int k = 0; k <= slices; k++)
                    {
                        path((float)k / slices, out Vector3 at, out Quaternion turn);
                        pos.Add(at + turn * new Vector3(face.a.x, face.a.y, 0f)); pos.Add(at + turn * new Vector3(face.b.x, face.b.y, 0f));
                        nrm.Add(turn * face.normal); nrm.Add(turn * face.normal);
                        uvs.Add(uv); uvs.Add(uv);
                    }
                    for (int k = 0; k < slices; k++)
                    {
                        int a0 = first + k * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                        // wound so the face looks along its normal on either side of the road
                        if (side > 0f) { tris.Add(a0); tris.Add(a1); tris.Add(b0); tris.Add(a1); tris.Add(b1); tris.Add(b0); }
                        else { tris.Add(a0); tris.Add(b0); tris.Add(a1); tris.Add(a1); tris.Add(b0); tris.Add(b1); }
                    }
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>What <paramref name="shape"/> measures after <see cref="Sweep"/> has widened its road by
        /// <paramref name="widen"/>: a wider road, rails of the same size further out.</summary>
        public static Shape Widened(Shape shape, float widen)
        {
            float road = shape.RoadHalf * widen;
            return new Shape(road, road + (shape.OuterHalf - shape.RoadHalf), shape.Start, shape.Length, shape.Depth);
        }

        /// <summary>The palette colour of the source piece's rail: the UV of its highest point beyond the road's edge.</summary>
        public static Vector2 RailUv(Mesh source)
        {
            Shape shape = Measure(source);
            var v = source.vertices; var uv = source.uv;
            int best = -1;
            for (int i = 0; i < v.Length && i < uv.Length; i++)
                if (Mathf.Abs(v[i].x) > shape.RoadHalf + 0.02f && (best < 0 || v[i].y > v[best].y)) best = i;
            return best >= 0 ? uv[best] : Vector2.zero;
        }

        /// <summary>
        /// Where a point of the piece's cross-section (x across, y up, z ignored) ends up after travelling
        /// <paramref name="degrees"/> round the corner: the corner's centre is <paramref name="radius"/> to the right for a
        /// right turn, to the left for a left one. Used for the mesh and by callers placing things along the same curve.
        /// </summary>
        public static Vector3 Bend(Vector3 section, float degrees, float radius)
        {
            float a = Mathf.Abs(degrees) * Mathf.Deg2Rad;
            if (degrees >= 0f)
            {
                float rho = radius - section.x;
                return new Vector3(radius - rho * Mathf.Cos(a), section.y, rho * Mathf.Sin(a));
            }
            float rhoLeft = radius + section.x;
            return new Vector3(-radius + rhoLeft * Mathf.Cos(a), section.y, rhoLeft * Mathf.Sin(a));
        }

        /// <summary>Cuts every triangle that crosses the plane z = <paramref name="planeZ"/> along it, keeping the winding;
        /// the new corners take their normal and UV from the edge they sit on.</summary>
        private static void Slice(List<Vector3> pos, List<Vector3> nrm, List<Vector2> uvs, List<int> tris, float planeZ)
        {
            const float eps = 1e-4f;
            var result = new List<int>(tris.Count + 96);
            for (int t = 0; t < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                int sa = Side(pos[a].z - planeZ, eps), sb = Side(pos[b].z - planeZ, eps), sc = Side(pos[c].z - planeZ, eps);
                if ((sa >= 0 && sb >= 0 && sc >= 0) || (sa <= 0 && sb <= 0 && sc <= 0)) { result.Add(a); result.Add(b); result.Add(c); continue; }

                if (sa == 0 || sb == 0 || sc == 0)
                {
                    // One corner lies on the plane and the other two are on opposite sides: one cut, two triangles.
                    while (sa != 0) { int ti = a; a = b; b = c; c = ti; int ts = sa; sa = sb; sb = sc; sc = ts; }
                    int p = Cut(pos, nrm, uvs, b, c, planeZ);
                    result.Add(a); result.Add(b); result.Add(p);
                    result.Add(a); result.Add(p); result.Add(c);
                    continue;
                }

                // One corner is alone on its side: a small triangle at that corner, and the rest as two more.
                while (sa == sb || sa == sc) { int ti = a; a = b; b = c; c = ti; int ts = sa; sa = sb; sb = sc; sc = ts; }
                int ab = Cut(pos, nrm, uvs, a, b, planeZ), ac = Cut(pos, nrm, uvs, a, c, planeZ);
                result.Add(a); result.Add(ab); result.Add(ac);
                result.Add(ab); result.Add(b); result.Add(c);
                result.Add(ab); result.Add(c); result.Add(ac);
            }
            tris.Clear();
            tris.AddRange(result);
        }

        private static int Side(float d, float eps) => d > eps ? 1 : d < -eps ? -1 : 0;

        private static int Cut(List<Vector3> pos, List<Vector3> nrm, List<Vector2> uvs, int from, int to, float planeZ)
        {
            float t = (planeZ - pos[from].z) / (pos[to].z - pos[from].z);
            Vector3 p = Vector3.Lerp(pos[from], pos[to], t);
            p.z = planeZ;
            pos.Add(p);
            Vector3 n = Vector3.Lerp(nrm[from], nrm[to], t);
            nrm.Add(n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up);
            uvs.Add(Vector2.Lerp(uvs[from], uvs[to], t));
            return pos.Count - 1;
        }
    }
}
#endif
