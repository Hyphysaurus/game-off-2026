using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Builds the meshes of the shape pieces. Every shape has its pivot at the middle of its bottom face, so it rests on
    /// whatever it is put on. Size is width (x), height (y), depth (z) in world units. Ramps and stairs climb toward +z.
    /// UVs are in world units / 10, so a textured material would tile evenly at any size.
    /// </summary>
    public static class ShapeMesh
    {
        private const int PillarSegments = 24;
        private const float UvScale = 0.1f;

        private static readonly List<Vector3> V = new List<Vector3>();
        private static readonly List<Vector3> N = new List<Vector3>();
        private static readonly List<Vector2> U = new List<Vector2>();
        private static readonly List<int> T = new List<int>();

        public static void Fill(Mesh mesh, ShapeKind kind, Vector3 size, int steps)
        {
            V.Clear(); N.Clear(); U.Clear(); T.Clear();
            float w = Mathf.Max(0.01f, size.x), h = Mathf.Max(0.01f, size.y), d = Mathf.Max(0.01f, size.z);
            switch (kind)
            {
                case ShapeKind.Ramp: Ramp(w, h, d); break;
                case ShapeKind.Stairs: Stairs(w, h, d, Mathf.Max(1, steps)); break;
                case ShapeKind.Pillar: Pillar(w, h, d); break;
                default: Box(new Vector3(-w / 2f, 0f, -d / 2f), new Vector3(w / 2f, h, d / 2f)); break;   // Box, Wall, None
            }
            mesh.Clear();
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, U); mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        private static void Box(Vector3 a, Vector3 b)
        {
            Quad(new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), Vector3.up);       // top
            Quad(new Vector3(a.x, a.y, b.z), new Vector3(a.x, a.y, a.z), new Vector3(b.x, a.y, a.z), new Vector3(b.x, a.y, b.z), Vector3.down);     // bottom
            Quad(new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z), Vector3.back);     // front (-z)
            Quad(new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, a.y, b.z), Vector3.forward);  // back (+z)
            Quad(new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z), Vector3.left);     // -x
            Quad(new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), Vector3.right);    // +x
        }

        private static void Ramp(float w, float h, float d)
        {
            float x0 = -w / 2f, x1 = w / 2f, z0 = -d / 2f, z1 = d / 2f;
            Vector3 slope = new Vector3(0f, d, -h).normalized;
            Quad(new Vector3(x0, 0f, z0), new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, 0f, z0), slope);                       // slope
            Quad(new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), Vector3.down);              // bottom
            Quad(new Vector3(x1, 0f, z1), new Vector3(x1, h, z1), new Vector3(x0, h, z1), new Vector3(x0, 0f, z1), Vector3.forward);              // back wall
            Tri(new Vector3(x0, 0f, z1), new Vector3(x0, h, z1), new Vector3(x0, 0f, z0), Vector3.left);                                         // sides
            Tri(new Vector3(x1, 0f, z0), new Vector3(x1, h, z1), new Vector3(x1, 0f, z1), Vector3.right);
        }

        private static void Stairs(float w, float h, float d, int steps)
        {
            float run = d / steps, rise = h / steps;
            for (int i = 0; i < steps; i++)
            {
                float z0 = -d / 2f + i * run;
                Box(new Vector3(-w / 2f, 0f, z0), new Vector3(w / 2f, rise * (i + 1), z0 + run));
            }
        }

        private static void Pillar(float w, float h, float d)
        {
            float rx = w / 2f, rz = d / 2f;
            int start = V.Count;
            for (int i = 0; i <= PillarSegments; i++)
            {
                float a = i / (float)PillarSegments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz);
                var n = new Vector3(Mathf.Cos(a) / Mathf.Max(rx, 1e-4f), 0f, Mathf.Sin(a) / Mathf.Max(rz, 1e-4f)).normalized;
                float u = i / (float)PillarSegments * Mathf.PI * (rx + rz) * UvScale;
                Add(p, n, new Vector2(u, 0f));
                Add(p + Vector3.up * h, n, new Vector2(u, h * UvScale));
            }
            for (int i = 0; i < PillarSegments; i++)
            {
                int a = start + i * 2, b = a + 2;
                T.Add(a); T.Add(a + 1); T.Add(b + 1);
                T.Add(a); T.Add(b + 1); T.Add(b);
            }
            Cap(rx, rz, h, Vector3.up);
            Cap(rx, rz, 0f, Vector3.down);
        }

        private static void Cap(float rx, float rz, float y, Vector3 n)
        {
            int centre = V.Count;
            Add(new Vector3(0f, y, 0f), n, Vector2.zero);
            for (int i = 0; i <= PillarSegments; i++)
            {
                float a = i / (float)PillarSegments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * rx, y, Mathf.Sin(a) * rz);
                Add(p, n, new Vector2(p.x, p.z) * UvScale);
            }
            for (int i = 0; i < PillarSegments; i++)
            {
                int a = centre + 1 + i, b = a + 1;
                if (n.y > 0f) { T.Add(centre); T.Add(b); T.Add(a); }
                else { T.Add(centre); T.Add(a); T.Add(b); }
            }
        }

        // a b c d run clockwise seen from outside (Unity's front face)
        private static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int s = V.Count;
            Add(a, n, Uv(a, n)); Add(b, n, Uv(b, n)); Add(c, n, Uv(c, n)); Add(d, n, Uv(d, n));
            T.Add(s); T.Add(s + 1); T.Add(s + 2);
            T.Add(s); T.Add(s + 2); T.Add(s + 3);
        }

        private static void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
        {
            int s = V.Count;
            Add(a, n, Uv(a, n)); Add(b, n, Uv(b, n)); Add(c, n, Uv(c, n));
            T.Add(s); T.Add(s + 1); T.Add(s + 2);
        }

        private static void Add(Vector3 p, Vector3 n, Vector2 uv) { V.Add(p); N.Add(n); U.Add(uv); }

        // box projection: the axis the normal mostly points along is dropped
        private static Vector2 Uv(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z) * UvScale;
            if (ax >= az) return new Vector2(p.z, p.y) * UvScale;
            return new Vector2(p.x, p.y) * UvScale;
        }
    }
}
