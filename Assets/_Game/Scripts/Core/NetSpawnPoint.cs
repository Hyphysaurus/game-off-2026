using UnityEngine;

namespace ShiftJam
{
    /// <summary>The online hub's one spawn (D-0111): every player lands here, each a little round from the middle
    /// (NetPlayer). Team spawns come with the modes.</summary>
    public sealed class NetSpawnPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("How far from the middle each player lands, in body heights.")] private float spread = 1.2f;
        public float Spread => spread;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
