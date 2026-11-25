using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A zone's box in the house with no card (D-0224): the bedroom and the window sill, placed by the chart maker
    /// (Shift Jam > Objectives > Land Sticker Chart). The rooms with an arrival card (ArrivalCard, D-0221) are zone boxes
    /// too, under their card's id. Every box is kept in one list while it is switched on; Objectives asks it which
    /// zone holds the player (the smallest box that does: the window's box sits inside the playroom's). Nothing touches
    /// the box itself in play, so a box costs nothing per frame. On the Ignore Raycast layer, so aim and sight never
    /// stop on it. The box must not be turned (the maker places them square to the world); a turned one is read by the
    /// square box round it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ZoneBox : MonoBehaviour
    {
        [SerializeField, Tooltip("Which zone: its id on ObjectiveSet > Zones.")] private string id = "";

        /// <summary>One box in the list: who keeps it, its zone's id, its box in the world, and its zone's row (-2 until asked).</summary>
        private struct Box
        {
            public Component owner;
            public string id;
            public Bounds bounds;
            public int scene, zone;
        }

        private static readonly List<Box> Boxes = new List<Box>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Boxes.Clear();

        public string Id => id ?? "";

        /// <summary>The maker's.</summary>
        public void Setup(string zoneId) => id = zoneId ?? "";

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;
        private void OnEnable() => Track(this, id, GetComponent<BoxCollider>());
        private void OnDisable() => Untrack(this);

        /// <summary>Puts <paramref name="owner"/>'s box in the list under the zone <paramref name="zoneId"/> (its box read
        /// once, now). An ArrivalCard calls it too. Empty ids and missing boxes are left out.</summary>
        public static void Track(Component owner, string zoneId, BoxCollider box)
        {
            if (owner == null || box == null || string.IsNullOrEmpty(zoneId)) return;
            Untrack(owner);
            Transform t = box.transform;
            Vector3 half = Vector3.Scale(box.size * 0.5f, t.lossyScale);
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            // The square box round a turned one (each world axis takes its share of the three turned half-sizes).
            Matrix4x4 turn = Matrix4x4.Rotate(t.rotation);
            var extents = new Vector3(
                Mathf.Abs(turn.m00) * half.x + Mathf.Abs(turn.m01) * half.y + Mathf.Abs(turn.m02) * half.z,
                Mathf.Abs(turn.m10) * half.x + Mathf.Abs(turn.m11) * half.y + Mathf.Abs(turn.m12) * half.z,
                Mathf.Abs(turn.m20) * half.x + Mathf.Abs(turn.m21) * half.y + Mathf.Abs(turn.m22) * half.z);
            Boxes.Add(new Box { owner = owner, id = zoneId, bounds = new Bounds(t.TransformPoint(box.center), extents * 2f), scene = owner.gameObject.scene.handle, zone = -2 });
        }

        /// <summary>Takes <paramref name="owner"/>'s box out of the list.</summary>
        public static void Untrack(Component owner)
        {
            for (int i = Boxes.Count - 1; i >= 0; i--) if (Boxes[i].owner == owner || Boxes[i].owner == null) Boxes.RemoveAt(i);
        }

        /// <summary>The zone row (an index into <paramref name="zones"/>) of the SMALLEST box in the scene
        /// <paramref name="sceneHandle"/> that holds <paramref name="point"/>, or -1. Each box's row is found once.
        /// Nothing is allocated.</summary>
        public static int ZoneAt(Vector3 point, int sceneHandle, ChartZone[] zones)
        {
            int best = -1;
            float bestSize = float.MaxValue;
            for (int i = 0; i < Boxes.Count; i++)
            {
                Box b = Boxes[i];
                if (b.scene != sceneHandle || b.owner == null || !b.bounds.Contains(point)) continue;
                if (b.zone == -2) { b.zone = RowOf(b.id, zones); Boxes[i] = b; }
                if (b.zone < 0) continue;
                Vector3 s = b.bounds.size;
                float size = s.x * s.y * s.z;
                if (size < bestSize) { bestSize = size; best = b.zone; }
            }
            return best;
        }

        /// <summary>The zone row of the box in the scene <paramref name="sceneHandle"/> nearest to <paramref name="point"/>
        /// (a save point between two boxes), or -1 when the scene has none. Nothing is allocated.</summary>
        public static int Nearest(Vector3 point, int sceneHandle, ChartZone[] zones)
        {
            int best = -1;
            float bestGap = float.MaxValue;
            for (int i = 0; i < Boxes.Count; i++)
            {
                Box b = Boxes[i];
                if (b.scene != sceneHandle || b.owner == null) continue;
                if (b.zone == -2) { b.zone = RowOf(b.id, zones); Boxes[i] = b; }
                if (b.zone < 0) continue;
                float gap = b.bounds.SqrDistance(point);
                if (gap < bestGap) { bestGap = gap; best = b.zone; }
            }
            return best;
        }

        private static int RowOf(string zoneId, ChartZone[] zones)
        {
            if (zones == null) return -1;
            for (int z = 0; z < zones.Length; z++)
                if (zones[z] != null && !zones[z].wholeScene && zones[z].id == zoneId) return z;
            return -1;
        }

        /// <summary>The zone rows are read again (a new asset in the editor): each box finds its row afresh.</summary>
        public static void Forget()
        {
            for (int i = 0; i < Boxes.Count; i++) { Box b = Boxes[i]; b.zone = -2; Boxes[i] = b; }
        }

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(1f, 0.82f, 0.4f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
