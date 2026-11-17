using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A quick squash that springs back, for when something gets hit (the test room's Punch.squash): flat and wide at
    /// the hit, then back to its own shape with a little wobble. Nothing needs it on a prefab: Squash.Hit puts one on
    /// the part the first time, and it sleeps between hits. A Damageable with Squash above 0 calls it on every hit.
    /// </summary>
    public sealed class Squash : MonoBehaviour
    {
        private Vector3 _rest;
        private float _amount, _seconds, _at;
        private bool _known;

        /// <summary>Squashes <paramref name="part"/> by <paramref name="amount"/> (a share of its height) and lets it
        /// spring back over <paramref name="seconds"/>.</summary>
        public static void Hit(Transform part, float amount, float seconds)
        {
            if (part == null || amount <= 0f || !part.gameObject.activeInHierarchy) return;
            if (!part.TryGetComponent(out Squash squash)) squash = part.gameObject.AddComponent<Squash>();
            squash.Begin(amount, seconds);
        }

        private void Begin(float amount, float seconds)
        {
            // The true size is kept once, so a second hit in mid-spring cannot leave it stretched.
            if (!_known) { _rest = transform.localScale; _known = true; }
            _amount = amount; _seconds = Mathf.Max(0.01f, seconds); _at = Time.time;
            enabled = true;
            Shape(0f);
        }

        private void Update()
        {
            float t = (Time.time - _at) / _seconds;
            if (t >= 1f) { enabled = false; return; }   // OnDisable puts the true size back
            Shape(t);
        }

        private void OnDisable() { if (_known) transform.localScale = _rest; }

        private void Shape(float t)
        {
            float left = _amount * (1f - Spring(t));
            transform.localScale = Vector3.Scale(_rest, new Vector3(1f + left, 1f - left, 1f + left));
        }

        /// <summary>0 at the start, 1 at the end, overshooting and ringing on the way: Godot's elastic ease-out, which
        /// the test room springs with.</summary>
        private static float Spring(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;
        }
    }
}
