using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>Shows "E  Shift: grow" style text for whatever the Interactor has in reach. Blank when nothing.</summary>
    public sealed class InteractPrompt : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string keyName = "E";

        private void Update()
        {
            if (interactor == null || label == null) return;
            IInteractable target = interactor.Target;
            string words = target != null ? target.PromptFor(interactor) : "";
            label.text = string.IsNullOrEmpty(words) ? "" : $"{keyName}  {words}";
        }
    }
}
