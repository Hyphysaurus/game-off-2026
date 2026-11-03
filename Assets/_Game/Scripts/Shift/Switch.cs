using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>A switch the player presses. It can demand a size: the shelf switch needs a kid-sized hand.</summary>
    public sealed class Switch : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("The size the player must be, unless Any Size is on.")] private ShiftSize requiredSize = ShiftSize.Kid;
        [SerializeField, Tooltip("Works once, then reads Done.")] private bool once = true;
        [SerializeField] private Door[] doors;
        [SerializeField, Tooltip("Hook Feel feedbacks here.")] private UnityEvent onActivated;

        public bool Used { get; private set; }

        private bool RightSize(Interactor who) => anySize || who.Shifter == null || who.Shifter.Size == requiredSize;

        public string PromptFor(Interactor who)
        {
            if (Used && once) return "Done";
            if (!RightSize(who)) return requiredSize == ShiftSize.Kid ? "Too small to reach" : "Too big to fit";
            return "Press";
        }

        public void Interact(Interactor who)
        {
            if ((Used && once) || !RightSize(who)) return;
            Used = true;
            foreach (Door door in doors) if (door != null) door.Open();
            onActivated?.Invoke();
        }
    }
}
