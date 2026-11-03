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
        [SerializeField, Tooltip("Optional. Shown when nothing is in reach but something is carried.")] private Carrier carrier;
        [SerializeField] private string heldHint = "LMB  Light     C  Put down";

        private void Update()
        {
            if (interactor == null || label == null) return;
            IInteractable target = interactor.Target;
            string words = target != null ? target.PromptFor(interactor) : "";
            if (string.IsNullOrEmpty(words))
                label.text = carrier != null && carrier.Held != null ? heldHint : "";
            else
                label.text = $"{keyName}  {words}";
        }
    }
}
