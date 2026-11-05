using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A column of wind: while the player is inside this trigger, the motor's velocity eases toward the column's
    /// direction (this object's +Y) at wind speed. A broken fan on its back makes one; a toy rides it up, a kid
    /// steps over it. Speeds are for kid size and scale with the player, like everything else.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Updraft : MonoBehaviour
    {
        [SerializeField, Tooltip("Speed the wind carries you along the column, m/s at kid size. Must beat gravity (25) to lift.")] private float windSpeed = 14f;
        [SerializeField, Tooltip("How fast your velocity bends into the wind, m/s per second at kid size.")] private float windAcceleration = 60f;
        [SerializeField] private bool anySize;
        [SerializeField, Tooltip("The size the wind can carry, unless Any Size is on.")] private ShiftSize requiredSize = ShiftSize.Toy;
        [SerializeField, Tooltip("Optional: the fan's blades, spun for the look.")] private Transform blades;
        [SerializeField] private float spinSpeed = 900f;
        [SerializeField, Tooltip("Fires when a carried player enters the column.")] private UnityEvent onEnter;

        private readonly List<PlayerMotor> _inside = new List<PlayerMotor>();
        private BoxCollider _box;

        private void Awake() { _box = GetComponent<BoxCollider>(); _box.isTrigger = true; }

        private bool Carries(PlayerMotor motor)
        {
            if (anySize) return true;
            var shifter = motor.GetComponent<Shifter>();
            return shifter == null || shifter.Size == requiredSize;
        }

        private void OnTriggerEnter(Collider other)
        {
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor == null || _inside.Contains(motor)) return;
            _inside.Add(motor);
            if (Carries(motor)) onEnter?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            var motor = other.GetComponentInParent<PlayerMotor>();
            if (motor != null) _inside.Remove(motor);
        }

        private void Update()
        {
            if (blades != null) blades.Rotate(transform.up, spinSpeed * Time.deltaTime, Space.World);
            for (int i = _inside.Count - 1; i >= 0; i--)
            {
                var motor = _inside[i];
                if (motor == null || !motor.isActiveAndEnabled) { _inside.RemoveAt(i); continue; }
                if (!Carries(motor)) continue;
                motor.Blow(transform.up * (windSpeed * motor.Scale), windAcceleration * motor.Scale);
            }
        }
    }
}
