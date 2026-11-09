using UnityEngine;

namespace ShiftJam
{
    /// <summary>Marks something the Interactor can show but E does nothing on: the prompt shows its words without the key.</summary>
    public interface IHintOnly { }

    /// <summary>
    /// The problem says what it needs (D-0046): a trigger box at a blocker (the shut toy door, the safe over the hole)
    /// that shows a line when the player comes up to it, like "Shut. A switch opens it." It goes quiet once the problem
    /// is solved: its door open, or its blocker moved away. It can speak to one size only (the hole talks to the toy;
    /// the kid gets the safe's own prompt). E does nothing on it. CourseBuilder.PuzzleCues places them.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ProblemHint : MonoBehaviour, IInteractable, IHintOnly
    {
        [SerializeField, Tooltip("What the player is told. Placeholder wording.")] private string message = "";
        [SerializeField] private bool anySize = true;
        [SerializeField, Tooltip("The size it speaks to, unless Any Size is on.")] private ShiftSize onlyFor = ShiftSize.Toy;
        [SerializeField, Tooltip("Solved once this door is open. Optional.")] private Door door;
        [SerializeField, Tooltip("Solved once this has moved away from where it started. Optional.")] private Transform blocker;
        [SerializeField] private float movedBy = 15f;

        private Vector3 _blockerStart;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (blocker != null) _blockerStart = blocker.position;
        }

        private bool Solved =>
            (door != null && door.IsOpen) ||
            (blocker != null && (blocker.position - _blockerStart).sqrMagnitude > movedBy * movedBy);

        public string PromptFor(Interactor who)
        {
            if (Solved) return "";
            if (!anySize && who.Shifter != null && who.Shifter.Size != onlyFor) return "";
            return message;
        }

        public void Interact(Interactor who) { }
    }
}
