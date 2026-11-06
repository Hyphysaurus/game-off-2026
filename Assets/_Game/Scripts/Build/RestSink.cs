using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Marks a piece that stands lower than its drawn bottom. The pack ramp ends in a small raised lip at its toe, and the
    /// builder sinks that lip under the floor so there is no step onto the ramp (D-0029). Build Mode rests and
    /// overlap-tests pieces by what is drawn, so it reads this to keep the sink when a ramp is placed, moved or scaled.
    /// It holds no behaviour, so placement ghosts keep it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestSink : MonoBehaviour
    {
        [SerializeField, Tooltip("How far below its drawn bottom this stands, in this object's own (mesh) units.")]
        private float depth;

        private static readonly List<RestSink> Found = new List<RestSink>();

        /// <summary>The sink in world units at the current scale.</summary>
        public float WorldDepth => depth * Mathf.Abs(transform.lossyScale.y);

        public void Set(float meshDepth) { depth = Mathf.Max(0f, meshDepth); }

        /// <summary>The deepest sink under <paramref name="root"/>, in world units (0 when nothing there is sunk).</summary>
        public static float Under(Transform root)
        {
            if (root == null) return 0f;
            root.GetComponentsInChildren(Found);
            float d = 0f;
            foreach (var s in Found) d = Mathf.Max(d, s.WorldDepth);
            Found.Clear();
            return d;
        }
    }
}
