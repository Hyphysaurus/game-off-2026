using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The claw machine's front (D-0229): a trigger box in front of its control panel, the one thing at the machine that E
    /// works on. It only passes the Interactor's questions on to the machine's ClawGame (the words, and the press). It is
    /// its own object, beside the machine's model and not over it, so the machine's other colliders (the glass, the back,
    /// the sides) have nothing to say: E works at the front only. On the Ignore Raycast layer, so the house's ordinary rays
    /// (light, sight, shots) pass through it; the Interactor and first person's hands look at every layer and find it.
    /// The maker places it (Shift Jam > House > Land Claw Game (D-0229)).
    /// </summary>
    public sealed class ClawGamePlaySpot : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("The machine this is the front of. Found on a parent if empty.")] private ClawGame game;

        private void Awake()
        {
            if (game == null) game = GetComponentInParent<ClawGame>();
        }

        /// <summary>The machine's words for this player now ("Play" for the kid in front of it; nothing for the toy).</summary>
        public string PromptFor(Interactor who) => game != null ? game.PromptFor(who) : "";

        /// <summary>The press: the machine starts a round (from its own Update, outside the player's input callback).</summary>
        public void Interact(Interactor who)
        {
            if (game != null) game.Interact(who);
        }
    }
}
