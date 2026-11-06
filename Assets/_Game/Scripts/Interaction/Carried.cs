using UnityEngine;

namespace ShiftJam
{
    /// <summary>Kid-only pick-up prompt for a plain Carryable (a ruler, a box, the safe): E picks it up, nothing else.</summary>
    public sealed class Carried : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Pick up";
        [SerializeField] private string tooSmall = "Too heavy";
        public string PromptFor(Interactor who)
        {
            if (GrabbedInstead(who)) return "";   // the third-person grab shows its own prompt for it
            return who.Shifter != null && who.Shifter.Size == ShiftSize.Kid ? prompt : tooSmall;
        }

        public void Interact(Interactor who)
        {
            if (GrabbedInstead(who)) return;
            if (who.Shifter == null || who.Shifter.Size != ShiftSize.Kid) return;
            var hands = who.GetComponent<Carrier>(); var thing = GetComponent<Carryable>();
            if (hands != null && thing != null && hands.Held == null) hands.PickUp(thing);
        }

        /// <summary>True when this goes to the player's grab, not the hand: it is not a hand tool and the grab is on.</summary>
        private bool GrabbedInstead(Interactor who)
        {
            var thing = GetComponent<Carryable>();
            if (thing == null || thing.KeepInHand) return false;
            var grab = who != null ? who.Grab : null;
            return grab != null && grab.isActiveAndEnabled;
        }
    }
}
