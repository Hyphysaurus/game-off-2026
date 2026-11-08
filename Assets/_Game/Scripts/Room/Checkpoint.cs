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
        [SerializeField, Tooltip("Also writes the active save slot: Continue starts here (SaveGame).")] private bool savePoint;
        [SerializeField, Tooltip("The room's name shown on the slot. Parent's name if empty.")] private string saveLabel = "";
        [SerializeField] private UnityEvent onClaimed;

        public bool Claimed { get; private set; }

        private void Reset() { GetComponent<Collider>().isTrigger = true; }

        private void Awake() { if (litWhenClaimed != null) litWhenClaimed.SetActive(false); }

        private void OnTriggerEnter(Collider other)
        {
            if (Claimed && !repeat) return;
            Shifter shifter = PlayerTouch.Find(other, out _);   // on foot or the first-person body
            if (shifter == null || shifter.Shifting) return;
            if (!anySize && shifter.Size != requiredSize) return;
            Respawn respawn = shifter.GetComponent<Respawn>();
            if (respawn == null) return;
            Transform p = point != null ? point : transform;
            respawn.SetPoint(p.position, Quaternion.Euler(0f, p.eulerAngles.y, 0f));
            if (savePoint) { SaveGame.Save(p.position, p.eulerAngles.y, shifter.Size, saveLabel.Length > 0 ? saveLabel : transform.parent != null ? transform.parent.name : name); Debug.Log("Saved to slot " + (SaveGame.Slot + 1) + " at " + name); }
            Claimed = true;
            if (litWhenClaimed != null) litWhenClaimed.SetActive(true);
            onClaimed?.Invoke();
            GameEvents.RaiseCheckpointClaimed(this);
        }
    }
}
