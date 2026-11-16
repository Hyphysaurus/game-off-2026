using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The way round a circuit, as data (D-0061): the middle of the road as evenly spaced points, each with the road's
    /// own up (so a loop, a spiral and a banked bend are followed the right way up), half the road's width and a
    /// suggested speed. The raceway's builder bakes it from the same segment table it lays the track from; rivals drive
    /// along it, the race measures every car's progress on it, and a car in trouble is put back on it. Nothing here
    /// moves anything: it only answers questions about the road. Distances run from the first point, round the lap.
    /// </summary>
    public sealed class RacePath : MonoBehaviour
    {
        private const int LookBack = 3, LookOn = 8;   // points searched behind and ahead of where a car last was

        [SerializeField, Tooltip("The middle of the road, on its surface, evenly spaced round the lap.")] private Vector3[] points = new Vector3[0];
        [SerializeField, Tooltip("The road's own up at each point.")] private Vector3[] ups = new Vector3[0];
        [SerializeField, Tooltip("Half the road's width between the walls at each point.")] private float[] halfWidths = new float[0];
        [SerializeField, Tooltip("A speed the road suggests at each point, units per second at car scale 1 (from how tight it bends, its lean, and its humps and dips).")] private float[] speeds = new float[0];
        [SerializeField, Tooltip("The distance between two neighbouring points.")] private float spacing = 2f;
        [SerializeField, Tooltip("Where the finish line crosses the road, as a distance from the first point.")] private float lineDistance;

        public int Count => points.Length;
        public float Spacing => spacing;
        /// <summary>Once round.</summary>
        public float Length => spacing * points.Length;
        public float LineDistance => lineDistance;
        /// <summary>There is a road to follow.</summary>
        public bool Ready => points.Length >= 8 && spacing > 0.01f && ups.Length == points.Length && halfWidths.Length == points.Length && speeds.Length == points.Length;

        /// <summary>The builder's way in: the whole lap at once.</summary>
        public void Bake(Vector3[] roadPoints, Vector3[] roadUps, float[] roadHalfWidths, float[] roadSpeeds, float step, float finishLine)
        {
            points = roadPoints; ups = roadUps; halfWidths = roadHalfWidths; speeds = roadSpeeds;
            spacing = step; lineDistance = finishLine;
        }

        /// <summary>The shortest way along the lap from one distance to another: positive is onward, negative is back.</summary>
        public float Delta(float from, float to)
        {
            float length = Length;
            float d = Mathf.Repeat(to - from, length);
            return d > length * 0.5f ? d - length : d;
        }

        /// <summary>The road at a distance: the middle of its surface, its up, and the way it runs.</summary>
        public void Sample(float distance, out Vector3 position, out Vector3 up, out Vector3 forward)
        {
            Split(distance, out int i, out int j, out float t);
            position = Vector3.LerpUnclamped(points[i], points[j], t);
            up = Vector3.LerpUnclamped(ups[i], ups[j], t).normalized;
            forward = Vector3.LerpUnclamped(Forward(i), Forward(j), t).normalized;
        }

        public float SpeedAt(float distance)
        {
            Split(distance, out int i, out int j, out float t);
            return Mathf.LerpUnclamped(speeds[i], speeds[j], t);
        }

        public float HalfWidthAt(float distance)
        {
            Split(distance, out int i, out int j, out float t);
            return Mathf.LerpUnclamped(halfWidths[i], halfWidths[j], t);
        }

        /// <summary>The distance of the road nearest to a point anywhere in the room. Looks at the whole lap: for a car
        /// just set down. A car on the move is followed with <see cref="Track"/> (the spiral passes over other road).</summary>
        public float Nearest(Vector3 position) => Closest(position, 0, points.Length);

        /// <summary>
        /// Follows a moving car: the distance of the road nearest to <paramref name="position"/>, looked for only close to
        /// <paramref name="last"/> (where it was a moment ago), with the way the road runs there, how far the car is to
        /// the right of the middle (negative: left) and how far above the road.
        /// </summary>
        public float Track(Vector3 position, float last, out Vector3 forward, out float lateral, out float height)
        {
            int centre = Mathf.FloorToInt(Mathf.Repeat(last, Length) / spacing);
            float distance = Closest(position, centre - LookBack, LookBack + LookOn);
            Sample(distance, out Vector3 road, out Vector3 up, out forward);
            Vector3 off = position - road;
            lateral = Vector3.Dot(off, Vector3.Cross(up, forward));
            height = Vector3.Dot(off, up);
            return distance;
        }

        /// <summary>The nearest spot on the <paramref name="count"/> stretches from point <paramref name="first"/> on.</summary>
        private float Closest(Vector3 position, int first, int count)
        {
            int n = points.Length;
            float best = float.MaxValue, at = 0f;
            for (int k = 0; k < count; k++)
            {
                int i = ((first + k) % n + n) % n, j = (i + 1) % n;
                Vector3 a = points[i], ab = points[j] - a;
                float t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (position - (a + ab * t)).sqrMagnitude;
                if (d < best) { best = d; at = (i + t) * spacing; }
            }
            return at;
        }

        private void Split(float distance, out int i, out int j, out float t)
        {
            int n = points.Length;
            float f = Mathf.Repeat(distance, n * spacing) / spacing;
            i = Mathf.Min(n - 1, Mathf.FloorToInt(f));
            j = (i + 1) % n;
            t = f - i;
        }

        /// <summary>The way the road runs at a point: from the point before it to the one after.</summary>
        private Vector3 Forward(int i)
        {
            int n = points.Length;
            return (points[(i + 1) % n] - points[(i - 1 + n) % n]).normalized;
        }
    }
}
