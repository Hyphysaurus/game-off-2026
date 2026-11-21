using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A string strung between two points (D-0109): the Dart Line latches on at its Start anchor and the toy rides it to
    /// End, hanging under it, faster the steeper it runs. Drawn with a slight sag by its LineRenderer (made by the
    /// builder; refreshed here so moving an end in the editor redraws it in play).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ZipLine : MonoBehaviour
    {
        [SerializeField] private Transform start, end;
        [SerializeField, Tooltip("How far the middle sags, as a share of the length.")] private float sag = 0.03f;
        [SerializeField, Range(4, 64)] private int points = 24;

        public Vector3 Start => start != null ? start.position : transform.position;
        public Vector3 End => end != null ? end.position : transform.position;
        public float Length => Vector3.Distance(Start, End);

        /// <summary>The point on the string at <paramref name="t"/> (0 the start, 1 the end).</summary>
        public Vector3 At(float t)
        {
            t = Mathf.Clamp01(t);
            return Vector3.Lerp(Start, End, t) + Vector3.down * (4f * t * (1f - t) * sag * Length);
        }


        private void Awake() => Draw();

        public void Draw()
        {
            var line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = points;
            for (int i = 0; i < points; i++) line.SetPosition(i, At(i / (points - 1f)));
        }
    }
}
