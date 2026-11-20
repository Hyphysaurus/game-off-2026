using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A doorway between rooms that live in the same scene: the mouse hole, the toy door, the hole under the
    /// wardrobe. Walking in (at the right size) puts the player at the target, facing its forward, turns the
    /// camera the same way, and makes the target the new checkpoint. One component, every room change.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Portal : MonoBehaviour
    {
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("The size that fits through, unless Any Size is on.")] private ShiftSize requiredSize = ShiftSize.Toy;
        [SerializeField, Tooltip("Where you come out: position and facing.")] private Transform target;
        [SerializeField, Tooltip("Seconds before it can fire again (so the arrival point can sit near another portal).")] private float cooldown = 1f;
        [SerializeField, Tooltip("Hook a Feel feedback, a Door.Open, a light.")] private UnityEvent onUsed;

        private float _readyAt;

        private void Reset() { GetComponent<Collider>().isTrigger = true; }

        private void OnTriggerEnter(Collider other) => TryUse(other);

        /// <summary>Also while inside: a player who becomes the right size in here goes through when the shift ends. The
        /// wardrobe's top portal is entered by the kid's body growing into it, mid-shift, and Enter alone turned that away
        /// for good: the kid stayed shut in the wardrobe (walk check 19 Nov, D-0081).</summary>
        private void OnTriggerStay(Collider other) => TryUse(other);

        private void TryUse(Collider other)
        {
            if (Time.time < _readyAt || target == null) return;
            Shifter shifter = other.GetComponentInParent<Shifter>();
            if (shifter == null || shifter.Shifting) return;
            if (!anySize && shifter.Size != requiredSize) return;
            PlayerMotor motor = shifter.GetComponent<PlayerMotor>();
            if (motor == null) return;
            _readyAt = Time.time + cooldown;
            Quaternion facing = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            motor.Warp(target.position, facing);
            Respawn respawn = shifter.GetComponent<Respawn>();
            if (respawn != null) respawn.SetPoint(target.position, facing);
            ShoulderCamera cam = Camera.main != null ? Camera.main.GetComponent<ShoulderCamera>() : null;
            if (cam != null) cam.Warp(target.eulerAngles.y);
            onUsed?.Invoke();
        }
    }
}
