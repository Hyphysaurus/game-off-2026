using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A room's arrival (D-0221, "name the room, name the job"): a trigger box the helpers maker places over a room. The
    /// first time this PC's player comes into it on a save slot, the room's card is asked for (its name big and one line
    /// of what to do, words on ObjectiveSet > Arrivals under this Id); ObjectiveHelpers shows it once the way is clear
    /// (no other big card, menu, camera move or Flat Zone). Another player's copy online never asks, and nothing shows
    /// online (as D-0220). On the Ignore Raycast layer, so aim and sight never stop on the box.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArrivalCard : MonoBehaviour
    {
        [SerializeField, Tooltip("Which card: its id on ObjectiveSet > Arrivals.")] private string id = "";

        public string Id => id ?? "";

        /// <summary>The maker's.</summary>
        public void Setup(string cardId) => id = cardId ?? "";

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(id)) return;
            Shifter who = PlayerTouch.Find(other, out _);   // on foot, or the first-person body
            if (who == null) return;
            ShiftCharge charge = who.GetComponent<ShiftCharge>();
            if (charge == null || !charge.HasHud) return;   // this PC's own player only
            ObjectiveHelpers.Arrive(id, who);
        }
    }
}
