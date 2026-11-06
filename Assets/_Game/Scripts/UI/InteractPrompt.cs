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
            // The grab first: what it holds, or the prop under the aim. Then whatever is in reach. Then why the aimed-at
            // prop can't be grabbed ("Too big"), then the hand-carry hint.
            var grab = interactor.Grab;
            bool grabOn = grab != null && grab.isActiveAndEnabled;
            if (grabOn && !string.IsNullOrEmpty(grab.Hint)) { label.text = grab.Hint; return; }
            IInteractable target = interactor.Target;
            string words = target != null ? target.PromptFor(interactor) : "";
            if (!string.IsNullOrEmpty(words)) label.text = $"{keyName}  {words}";
            else if (grabOn && !string.IsNullOrEmpty(grab.Why)) label.text = grab.Why;
            else label.text = carrier != null && carrier.Held != null ? heldHint : "";
        }
    }
}
