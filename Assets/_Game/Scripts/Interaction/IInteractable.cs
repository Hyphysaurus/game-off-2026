namespace ShiftJam
{
    /// <summary>
    /// Anything the player can use with E. The Interactor finds the nearest one in front of the player,
    /// shows its prompt, and calls Interact on the press. The thing decides what the press means.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>The words shown next to the E key for this player, right now. Empty hides the prompt.</summary>
        string PromptFor(Interactor who);
        void Interact(Interactor who);
    }
}
