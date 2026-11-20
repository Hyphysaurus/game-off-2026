using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>A switch the player presses. It can demand a size: the shelf switch needs a kid-sized hand. With a
    /// Damageable beside it a shot presses it too, at any size: a toy with a dart pistol hits it from the floor (D-0085).</summary>
    public sealed class Switch : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("The size the player must be, unless Any Size is on.")] private ShiftSize requiredSize = ShiftSize.Kid;
        [SerializeField, Tooltip("Works once, then reads Done.")] private bool once = true;
        [SerializeField] private Door[] doors;
        [SerializeField, Tooltip("Hook Feel feedbacks here.")] private UnityEvent onActivated;

        public bool Used { get; private set; }
        /// <summary>The doors this switch opens (CauseCamera turns to the first).</summary>
        public System.Collections.Generic.IReadOnlyList<Door> Doors => doors ?? System.Array.Empty<Door>();

        private bool RightSize(Interactor who) => anySize || who.Shifter == null || who.Shifter.Size == requiredSize;

        public string PromptFor(Interactor who)
        {
            if (Used && once) return "Done";
            if (!RightSize(who)) return requiredSize == ShiftSize.Kid ? "Too small to reach" : "Too big to fit";
            return "Press";
        }

        private void Awake()
        {
            var health = GetComponent<Damageable>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
            if (health != null) health.Damaged += _ => Press();
        }

        public void Interact(Interactor who)
        {
            if (RightSize(who)) Press();
            else GameEvents.RaiseUseRefused(who, PromptFor(who));
        }

        /// <summary>Presses it: the doors open. A hand (Interact, sized) or a shot (its Damageable) calls this.</summary>
        public void Press()
        {
            if (Used && once) return;
            Used = true;
            foreach (Door door in doors) if (door != null) door.Open();
            onActivated?.Invoke();
            GameEvents.RaiseSwitchPressed(this);
        }
    }
}
