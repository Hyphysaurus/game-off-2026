using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A place to come back to. Walking into the trigger stores this spot (and facing) on the player's Respawn,
    /// so a fall or a pin puts you back here, not at the room's start. Optional size gate so a kid stepping
    /// over a toy lane does not claim toy checkpoints. Fires once unless Repeat is on.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [SerializeField] private bool anySize = true;
        [SerializeField] private ShiftSize requiredSize = ShiftSize.Toy;
        [SerializeField, Tooltip("Where to put the player back: this object if empty (its forward = facing).")] private Transform point;
        [SerializeField, Tooltip("Claim again every time it is entered.")] private bool repeat;
        [SerializeField, Tooltip("Shown once claimed: a small glow so the player knows where a reset lands.")] private GameObject litWhenClaimed;
        [SerializeField] private UnityEvent onClaimed;

        public bool Claimed { get; private set; }

        private void Reset() { GetComponent<Collider>().isTrigger = true; }

        private void Awake() { if (litWhenClaimed != null) litWhenClaimed.SetActive(false); }

        private void OnTriggerEnter(Collider other)
        {
            if (Claimed && !repeat) return;
            Shifter shifter = other.GetComponentInParent<Shifter>();
            if (shifter == null || shifter.Shifting) return;
            if (!anySize && shifter.Size != requiredSize) return;
            Respawn respawn = shifter.GetComponent<Respawn>();
            if (respawn == null) return;
            Transform p = point != null ? point : transform;
            respawn.SetPoint(p.position, Quaternion.Euler(0f, p.eulerAngles.y, 0f));
            Claimed = true;
            if (litWhenClaimed != null) litWhenClaimed.SetActive(true);
            onClaimed?.Invoke();
        }
    }
}
