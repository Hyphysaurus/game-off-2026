#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Makes track pieces the pack does not have out of a straight one it does (D-0048): the same piece bent round a
    /// corner, and the same piece with a side rail pressed flat so two lanes can sit side by side. The straight runs along
    /// +Z with its road surface at y = 0 and a rail down each side; nothing else is assumed (the road's half width and the
    /// length are read from the mesh). Editor only: the results are saved as mesh assets by whoever calls this.
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
