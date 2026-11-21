using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A place an online player appears (D-0111, D-0113). Everyone's first landing is the hub's one First point (his
    /// "same spawn instance"), each player a little round from its middle; after a knock-out a player comes back at
    /// whichever point is furthest from the nearest other player (Safest), so nobody comes back in front of a gun.
    /// Team spawns come with the modes.
    /// </summary>
    public sealed class NetSpawnPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("Everyone's first landing. One per scene.")] private bool first;
        [SerializeField, Tooltip("How far from the middle each player lands at the First point, in body heights.")] private float spread = 1.2f;

        public bool First => first;
        public float Spread => spread;

        private static readonly List<NetSpawnPoint> All = new List<NetSpawnPoint>();
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        /// <summary>The scene's First point (or any point when none is marked), null when there are none.</summary>
        public static NetSpawnPoint FirstPoint()
        {
            foreach (var p in All) if (p.first) return p;
            return All.Count > 0 ? All[0] : null;
        }

        /// <summary>The point furthest from the nearest of <paramref name="others"/> (any point when there are none).</summary>
        public static NetSpawnPoint Safest(IReadOnlyList<Vector3> others)
        {
            NetSpawnPoint best = null; float bestGap = -1f;
            foreach (var p in All)
            {
                float gap = float.MaxValue;
                foreach (var o in others) gap = Mathf.Min(gap, (o - p.transform.position).sqrMagnitude);
                if (others.Count == 0) gap = p.first ? 1f : 0f;   // alone: the hub's own point
                if (gap > bestGap) { bestGap = gap; best = p; }
            }
            return best;
        }

        /// <summary>Mark this the First point (the Stadium's builder; the house's online spawns, made at run time: D-0125).</summary>
        public void MarkFirst(bool value) => first = value;

        private void OnDrawGizmos()
        {
            Gizmos.color = first ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.3f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
