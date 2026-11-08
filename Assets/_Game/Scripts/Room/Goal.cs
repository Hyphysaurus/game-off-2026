using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The end of a room: a trigger volume the player walks into. It can demand a size (the toy door only
    /// fits a toy), shows a line of text once reached, and raises an event for Feel or a scene change.
    /// A room ends with one of these; a portal later replaces it, not extends it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Goal : MonoBehaviour
    {
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("The size the player must be to count, unless Any Size is on.")] private ShiftSize requiredSize = ShiftSize.Toy;
        [SerializeField, Tooltip("Optional. Text shown when reached.")] private TMP_Text label;
        [SerializeField, TextArea] private string message = "You made it through.";
        [SerializeField, Tooltip("Hook Feel feedbacks or a scene load here.")] private UnityEvent onReached;

        public bool Reached { get; private set; }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Reached) return;
            Shifter shifter = other.GetComponentInParent<Shifter>();
            if (shifter == null) return;                       // not the player
            if (!anySize && shifter.Size != requiredSize) return;
            if (shifter.Shifting) return;
            Reached = true;
            if (label != null) label.text = message;
            onReached?.Invoke();
            GameEvents.RaiseGoalReached(this);
        }
    }
}
