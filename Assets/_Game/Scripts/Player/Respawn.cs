using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The safety net: if the player falls below a height, put them back where they started (or at the last
    /// checkpoint set by code) with no momentum. Lives on the Player beside the motor.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class Respawn : MonoBehaviour
    {
        [SerializeField, Tooltip("Below this world height the player is put back. Pack units.")] private float killHeight = -20f;

        private PlayerMotor _motor;
        private Vector3 _point;
        private Quaternion _facing;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            SetPoint(transform.position, transform.rotation);
        }

        /// <summary>Where the next respawn lands. Checkpoints call this.</summary>
        public void SetPoint(Vector3 position, Quaternion rotation)
        {
            _point = position;
            _facing = rotation;
        }

        private void Update()
        {
            if (transform.position.y < killHeight) _motor.Warp(_point, _facing);
        }
    }
}
