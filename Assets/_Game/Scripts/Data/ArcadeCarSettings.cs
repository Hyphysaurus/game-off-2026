using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How an ArcadeCar drives, as numbers shared by every car that points at the asset. Lengths and speeds are for a car
    /// at transform scale 1 (a toy's car); a car scaled up drives proportionally faster and sits on longer springs.
    /// Forces are written as accelerations, so the car feels the same under the project's strong prop gravity.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Arcade Car Settings", fileName = "ArcadeCarSettings")]
    public sealed class ArcadeCarSettings : ScriptableObject
    {
        [Header("Engine (units at car scale 1)")]
        public float maxSpeed = 24f;
        public float reverseSpeed = 9f;
        [Tooltip("Speeding up, units per second squared.")] public float acceleration = 22f;
        [Tooltip("Pressing against the way you roll, units per second squared.")] public float braking = 45f;
        [Tooltip("Rolling with no throttle, units per second squared.")] public float coastDrag = 6f;

        [Header("Steering and grip")]
        [Tooltip("Turn rate at full lock, degrees per second.")] public float steerRate = 120f;
        [Tooltip("Speed (units per second at scale 1) at which steering reaches full rate; slower turns slower, like real wheels.")]
        public float steerFullSpeed = 6f;
        [Tooltip("How fast sideways sliding is cancelled, per second. High = on rails.")] public float grip = 9f;
        [Tooltip("Grip while the handbrake (Jump) is held: low = drift.")] public float handbrakeGrip = 1.5f;
        [Tooltip("Wheel turn shown on the front wheels at full lock, degrees.")] public float visualSteerAngle = 28f;

        [Header("Suspension (units at car scale 1)")]
        [Tooltip("Spring travel each way from rest.")] public float suspensionTravel = 0.18f;
        [Tooltip("Wheel radius if it cannot be measured from the wheel mesh.")] public float wheelRadius = 0.45f;
        [Tooltip("Spring damping, per second.")] public float damping = 7f;
        [Tooltip("Turns a car upright in the air, so it lands on its wheels.")] public float airUpright = 4f;
        [Tooltip("Extra pull down at speed, as a share of gravity at top speed: keeps it on the track over crests.")] public float downforce = 0.4f;

        [Header("Damage")]
        public float health = 150f;
        [Tooltip("Impact speed (units per second at scale 1) below which a crash does nothing.")] public float crashMinSpeed = 14f;
        public float crashDamagePerSpeed = 3f;
        [Tooltip("Speed above which hitting something with health hurts it.")] public float ramMinSpeed = 5f;
        public float ramDamagePerSpeed = 6f;
        public float ramMaxDamage = 80f;
        [Tooltip("Shove given to whatever is rammed, units per second at scale 1.")] public float ramKnockback = 9f;
        [Tooltip("Seconds a wreck sits smoking before it is put back at its start, repaired. 0 = never.")] public float wreckRespawn = 5f;

        [Header("Seat")]
        [Tooltip("Above this speed (units per second at scale 1) E brakes first and lets you out once slow.")] public float maxExitSpeed = 5f;
        [Tooltip("Largest driver scale relative to the car's scale (a kid cannot fold into a toy car).")] public float maxDriverScale = 1.5f;
        [Tooltip("Chase camera pull-back, times the on-foot distance.")] public float chaseDistance = 1.8f;
    }
}
