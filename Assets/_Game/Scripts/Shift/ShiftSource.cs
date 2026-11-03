using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>The glowing thing in the room that changes the player's size when used.</summary>
    public sealed class ShiftSource : MonoBehaviour, IInteractable
    {
        public enum Mode { Toggle, GrowOnly, ShrinkOnly }

        [SerializeField] private Mode mode = Mode.Toggle;
        [SerializeField, Tooltip("Shown when the player is the wrong size for this source (\"Too big to hold\"). Empty hides the prompt.")]
        private string wrongSizePrompt = "";
        [SerializeField, Tooltip("Hook Feel feedbacks here: the flash, the punch, the sound.")]
        private UnityEvent onUsed;

        public string PromptFor(Interactor who)
        {
            Shifter shifter = who.Shifter;
            if (shifter == null) return "";
            if (shifter.Shifting) return "";
            bool toy = shifter.Size == ShiftSize.Toy;
            switch (mode)
            {
                case Mode.GrowOnly: return toy ? "Shift: grow" : wrongSizePrompt;
                case Mode.ShrinkOnly: return toy ? wrongSizePrompt : "Shift: shrink";
                default: return toy ? "Shift: grow" : "Shift: shrink";
            }
        }

        public void Interact(Interactor who)
        {
            Shifter shifter = who.Shifter;
            if (shifter == null || shifter.Shifting) return;
            ShiftSize before = shifter.Size;
            switch (mode)
            {
                case Mode.GrowOnly: shifter.ShiftTo(ShiftSize.Kid); break;
                case Mode.ShrinkOnly: shifter.ShiftTo(ShiftSize.Toy); break;
                default: shifter.Toggle(); break;
            }
            if (shifter.Size != before) onUsed?.Invoke();
        }
    }
}
