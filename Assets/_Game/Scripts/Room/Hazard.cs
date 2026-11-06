using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A thing you must not touch: a pin, a drop, a hot lamp. Touching it puts the player back at the last
    /// checkpoint, the same way a bad fall does. Size-gated so the kid can step over toy hazards.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Hazard : MonoBehaviour
    {
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("Who it hurts, unless Any Size is on.")] private ShiftSize hurts = ShiftSize.Toy;
        [SerializeField] private UnityEvent onHit;

        private void Reset() { GetComponent<Collider>().isTrigger = true; }

        private void OnTriggerEnter(Collider other)
        {
            Shifter shifter = PlayerTouch.Find(other, out _);   // on foot or the first-person body
            if (shifter == null || shifter.Shifting) return;
            if (!anySize && shifter.Size != hurts) return;
            Respawn respawn = shifter.GetComponent<Respawn>();
            if (respawn == null) return;
            respawn.Reset();
            onHit?.Invoke();
        }
    }
}
