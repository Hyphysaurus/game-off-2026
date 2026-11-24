using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A place an online player appears (D-0111, D-0113). Everyone's first landing is the hub's one First point (his
    /// "same spawn instance"), each player a little round from its middle; after a knock-out a player comes back at
    /// whichever point is furthest from the nearest other player (Safest), so nobody comes back in front of a gun.
    /// Team spawns (D-0182): a point can belong to a team (1 Blue, 2 Orange; 0 = anyone). While a team match runs, a
    /// team's comebacks use only its own points (Safest(team, enemies)), and each team has one Landing point at its base,
    /// where the whole team stands at GO. Outside a match everything is as before: a team's ring points are ordinary
    /// comeback points; the landing points are left out.
    /// </summary>
    public sealed class NetSpawnPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("Everyone's first landing. One per scene.")] private bool first;
        [SerializeField, Tooltip("How far from the middle each player lands at the First point, in body heights.")] private float spread = 1.2f;
        [SerializeField, Tooltip("Whose comeback point this is in a team match: 0 anyone, 1 Blue, 2 Orange (D-0182).")] private int team;
        [SerializeField, Tooltip("The team's landing at its base: where the whole team stands at GO. Never a comeback point, and unused outside a match.")] private bool landing;

        public bool First => first;
        public float Spread => spread;
        /// <summary>Whose point this is in a team match: 0 anyone, 1 Blue, 2 Orange.</summary>
        public int Team => team;
        /// <summary>True for a team's landing point at its base (where it stands at GO).</summary>
        public bool IsLanding => landing;

        private static readonly List<NetSpawnPoint> All = new List<NetSpawnPoint>();
        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        /// <summary>The scene's First point (or any point when none is marked), null when there are none.</summary>
        public static NetSpawnPoint FirstPoint()
        {
            foreach (var p in All) if (p.first) return p;
            foreach (var p in All) if (!p.landing) return p;
            return null;
        }

        /// <summary>The point furthest from the nearest of <paramref name="others"/> (any point when there are none).
        /// A team's landing point is never one of them.</summary>
        public static NetSpawnPoint Safest(IReadOnlyList<Vector3> others)
        {
            NetSpawnPoint best = null; float bestGap = -1f;
            foreach (var p in All)
            {
                if (p.landing) continue;
                float gap = float.MaxValue;
                foreach (var o in others) gap = Mathf.Min(gap, (o - p.transform.position).sqrMagnitude);
                if (others.Count == 0) gap = p.first ? 1f : 0f;   // alone: the hub's own point
                if (gap > bestGap) { bestGap = gap; best = p; }
            }
            return best;
        }

        /// <summary>A team's landing point at its base (D-0182), null when the scene has none for it.</summary>
        public static NetSpawnPoint Landing(int team)
        {
            if (team == 0) return null;
            foreach (var p in All) if (p.landing && p.team == team) return p;
            return null;
        }

        /// <summary>In a team match (D-0182): the team's own comeback point furthest from the nearest of its
        /// <paramref name="enemies"/> (the first of them when there are none). Only that team's points: never its landing
        /// point, never a point for anyone (team 0, such as a door's arrival point). Null when the team has none.</summary>
        public static NetSpawnPoint Safest(int team, IReadOnlyList<Vector3> enemies)
        {
            if (team == 0) return null;
            NetSpawnPoint best = null; float bestGap = -1f;
            foreach (var p in All)
            {
                if (p.team != team || p.landing) continue;
                float gap = float.MaxValue;
                if (enemies != null) foreach (var e in enemies) gap = Mathf.Min(gap, (e - p.transform.position).sqrMagnitude);
                if (gap > bestGap) { bestGap = gap; best = p; }
            }
            return best;
        }

        /// <summary>Mark this the First point (the Stadium's builder; the house's online spawns, made at run time: D-0125).</summary>
        public void MarkFirst(bool value) => first = value;

        /// <summary>Give this point to a team (0 anyone, 1 Blue, 2 Orange), and say whether it is that team's landing at its
        /// base (the Stadium's builder, D-0182).</summary>
        public void SetTeam(int value, bool isLanding)
        {
            team = Mathf.Clamp(value, TeamMatch.NoTeam, TeamMatch.Orange);
            landing = isLanding && team != 0;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = first ? new Color(1f, 0.85f, 0.3f, 0.9f) : team != 0 ? TeamMatch.Colour(team) : new Color(0.3f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, landing ? 1f : 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
