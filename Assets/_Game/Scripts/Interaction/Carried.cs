using UnityEngine;

namespace ShiftJam
{
    /// <summary>Kid-only pick-up prompt for a plain Carryable (a ruler, a box, the safe): E picks it up, nothing else.</summary>
    public sealed class Carried : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Pick up";
        [SerializeField] private string tooSmall = "Too heavy";
        public string PromptFor(Interactor who) => who.Shifter != null && who.Shifter.Size == ShiftSize.Kid ? prompt : tooSmall;
        public void Interact(Interactor who)
        {
            if (who.Shifter == null || who.Shifter.Size != ShiftSize.Kid) return;
            var hands = who.GetComponent<Carrier>(); var thing = GetComponent<Carryable>();
            if (hands != null && thing != null && hands.Held == null) hands.PickUp(thing);
        }
    }
}
