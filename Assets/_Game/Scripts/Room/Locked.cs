using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A thing that answers E with a refusal: the real bedroom door. It shows one line and raises an event
    /// (a rattle, a Feel shake) so the player learns that this way is shut without a cutscene.
    /// </summary>
    public sealed class Locked : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Locked";
        [SerializeField, Tooltip("Hook a rattle sound or a shake here.")] private UnityEvent onTried;

        public string PromptFor(Interactor who) => prompt;

        public void Interact(Interactor who) => onTried?.Invoke();
    }
}
