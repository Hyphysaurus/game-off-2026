using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Pocket Core (a dev tool for now, D-0032): a shift core you call to you, so size tests and core placement can be
    /// tried anywhere. <see cref="Summon"/> (F2 and the console's `core`, editor and dev builds) sets it on the ground a step
    /// in front of the player, at either size; E on it shifts like any core (its ShiftSource toggles, unlimited uses).
    /// One per scene: the first call makes it from the Build Mode catalogue, later calls move it. Build Mode can also place
    /// it like any piece. A warm glow tells it apart from the course's cyan cores.
    /// </summary>
    public sealed class PocketCore : MonoBehaviour
    {
        /// <summary>Its Build Mode catalogue id (the prefab lives in Pieces/Interactables).</summary>
        public const string CatalogId = "Pieces/Interactables/PocketCore";

        [SerializeField, Tooltip("How high the core's centre floats above the ground it is set on.")] private float hover = 1.6f;
        [SerializeField, Tooltip("How far in front of the player it lands, in the player's own units (toy = 1).")] private float ahead = 1.2f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>Brings the scene's Pocket Core (made on first use) to the player. Returns a line for the console.</summary>
        public static string Summon()
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            if (motor == null) return "no player in this scene";
            var core = FindFirstObjectByType<PocketCore>();
            if (core == null)
            {
                var catalog = BuildCatalog.Load();
                var entry = catalog != null ? catalog.Find(CatalogId) : null;
                if (entry == null || entry.prefab == null) return "no Pocket Core in the catalogue: run Shift Jam > Dev > Build Pocket Core, then Shift Jam > Workshop > Build Catalog";
                core = Instantiate(entry.prefab).GetComponent<PocketCore>();
                if (core == null) return "the catalogue's Pocket Core has no PocketCore component";
            }
            var shifter = motor.GetComponent<Shifter>();
            core.PlaceBefore(motor.transform, shifter != null ? shifter.Scale : 1f);
            return "pocket core placed";
        }

        /// <summary>A step ahead of the player on the flat, then down onto whatever is there (the player excluded).</summary>
        private void PlaceBefore(Transform player, float scale)
        {
            Vector3 forward = player.forward; forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            float radius = 0f;
            var sphere = GetComponent<SphereCollider>();
            if (sphere != null) radius = sphere.radius * Mathf.Abs(transform.lossyScale.x);
            Vector3 spot = player.position + forward * (ahead * scale + radius);

            float lift = 3f * scale + hover;
            float ground = player.position.y;
            float nearest = float.MaxValue;
            int n = Physics.RaycastNonAlloc(spot + Vector3.up * lift, Vector3.down, Hits, lift + 6f * scale + hover, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.transform.IsChildOf(player) || h.transform.IsChildOf(transform)) continue;
                if (h.distance < nearest) { nearest = h.distance; ground = h.point.y; }
            }
            transform.SetPositionAndRotation(new Vector3(spot.x, ground + hover, spot.z), Quaternion.identity);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
    }
}
