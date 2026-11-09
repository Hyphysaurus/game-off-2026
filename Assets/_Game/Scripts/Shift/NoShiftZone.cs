using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A box where the carried shift does not work (D-0045): a room whose puzzle needs its fixed core (the wardrobe:
    /// grown on its floor, a kid would be shut inside). Cores inside still work. Needs a BoxCollider (made a trigger
    /// here, axis-aligned: the zone is tested by its bounds). CourseBuilder places them.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NoShiftZone : MonoBehaviour
    {
        private static readonly List<NoShiftZone> Zones = new List<NoShiftZone>();

        [SerializeField, Tooltip("Shown when the Shift key is pressed in here. Placeholder wording.")] private string message = "Can't shift in here";

        private BoxCollider _box;

        public string Message => message;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        private void OnEnable() => Zones.Add(this);
        private void OnDisable() => Zones.Remove(this);

        /// <summary>The zone that holds this point, or null.</summary>
        public static NoShiftZone Covering(Vector3 point)
        {
            foreach (var zone in Zones)
                if (zone != null && zone._box != null && zone._box.bounds.Contains(point)) return zone;
            return null;
        }
    }
}
