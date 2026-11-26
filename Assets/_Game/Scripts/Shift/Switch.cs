using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>A switch the player presses. It can demand a size: the shelf switch needs a kid-sized hand. With a
    /// Damageable beside it a shot presses it too, at any size: a toy with a dart pistol hits it from the floor (D-0085).
    /// Its words are its own (Prompt): the bedroom's light switch reads "Lights" (D-0214).
    /// D-0241: a lock may hold it shut for now (Held Shut By: the bedroom gate on the shelf switch and the wall switch).
    /// While it holds, the words beside E are the lock's, E is refused (the refusal shake and sound, and the lock's toast)
    /// and a shot presses nothing. No lock set (every other switch): exactly as before.</summary>
    public sealed class Switch : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("The size the player must be, unless Any Size is on.")] private ShiftSize requiredSize = ShiftSize.Kid;
        [SerializeField, Tooltip("Works once, then reads Done.")] private bool once = true;
        [SerializeField, Tooltip("What E does here, shown after the key (\"Press\"; the bedroom's light switch: \"Lights\").")] private string prompt = "Press";
        [SerializeField, Tooltip("The doors it opens. Empty: none (a switch that only fires On Activated).")] private Door[] doors;
        [SerializeField, Tooltip("Hook Feel feedbacks here.")] private UnityEvent onActivated;
        [SerializeField, Tooltip("D-0241: a lock that may hold this switch shut for now (a component that is an IShutLock: the bedroom gate, set by Land Bedroom Gate). While it holds: its words beside E, E refused, a shot presses nothing. Empty: never held.")]
        private MonoBehaviour heldShutBy;

        private IShutLock _lock;

        public bool Used { get; private set; }
        /// <summary>The doors this switch opens (CauseCamera turns to the first).</summary>
        public System.Collections.Generic.IReadOnlyList<Door> Doors => doors ?? System.Array.Empty<Door>();

        private bool RightSize(Interactor who) => anySize || who.Shifter == null || who.Shifter.Size == requiredSize;

        /// <summary>D-0241: true while its lock holds it shut (<paramref name="shut"/>: the lock's words, or null).</summary>
        private bool Held(out string shut)
        {
            shut = null;
            return heldShutBy != null && _lock != null && _lock.Holds(this, out shut);
        }

        public string PromptFor(Interactor who)
        {
            if (Used && once) return "Done";
            if (Held(out string shut)) return string.IsNullOrEmpty(shut) ? (string.IsNullOrEmpty(prompt) ? "Press" : prompt) : shut;   // D-0241
            if (!RightSize(who)) return requiredSize == ShiftSize.Kid ? "Too small to reach" : "Too big to fit";
            return string.IsNullOrEmpty(prompt) ? "Press" : prompt;
        }

        private void Awake()
        {
            _lock = heldShutBy as IShutLock;   // D-0241: anything else in the field is no lock
            var health = GetComponent<Damageable>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
            if (health != null) health.Damaged += _ => Press();
        }

        public void Interact(Interactor who)
        {
            if (Held(out _) && !(Used && once))   // D-0241: held shut: refused with the lock's words
            {
                _lock.Refuse(this, who);
                GameEvents.RaiseUseRefused(who, PromptFor(who));
                return;
            }
            if (RightSize(who)) Press();
            else GameEvents.RaiseUseRefused(who, PromptFor(who));
        }

        /// <summary>Presses it: the doors open. A hand (Interact, sized) or a shot (its Damageable) calls this. D-0241: held
        /// shut, a shot presses nothing (the lock's toast says why).</summary>
        public void Press()
        {
            if (Used && once) return;
            if (Held(out _)) { _lock.Refuse(this, null); return; }
            Used = true;
            if (doors != null) foreach (Door door in doors) if (door != null) door.Open();
            onActivated?.Invoke();
            GameEvents.RaiseSwitchPressed(this);
        }

        /// <summary>Sets whether it has been pressed without pressing it: no doors, no events, no sound. What it switches was
        /// put back from a save (the bedroom's lights on Continue, D-0214) or made new again (New Game): it reads Done, or
        /// Press, to match.</summary>
        public void SetUsed(bool used) => Used = used;
    }
}
