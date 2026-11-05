using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Something the player shoves by walking into it. The motor does the pushing (a velocity change on contact);
    /// this only says who may: Light = anyone, so a toy nudges a pencil or a block; Heavy = the kid only, so a toy
    /// walks into a box and stops. It also sets the body up so loose props settle and do not tunnel.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Pushable : MonoBehaviour
    {
        public enum Weight { Light, Heavy }

        [SerializeField] private Weight weight = Weight.Light;
        [SerializeField, Tooltip("Rigidbody mass. The shove is a velocity change, so mass only matters prop against prop.")]
        private float mass = 1f;

        public Weight Class => weight;
        public bool PushableBy(ShiftSize size) => weight == Weight.Light || size == ShiftSize.Kid;

        private void Awake()
        {
            var body = GetComponent<Rigidbody>();
            body.mass = Mathf.Max(0.01f, mass);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.6f;
            body.angularDamping = 1.2f;
            body.sleepThreshold = 0.05f;
        }
    }
}
