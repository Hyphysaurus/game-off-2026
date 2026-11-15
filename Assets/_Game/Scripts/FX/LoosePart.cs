using UnityEngine;

namespace ShiftJam
{
    /// <summary>A part knocked off a car (CarDamageFx, D-0059): it lies about for a while, then shrinks away.</summary>
    public sealed class LoosePart : MonoBehaviour
    {
        /// <summary>Loose parts in the scene now (CarDamageFx caps how many there can be).</summary>
        public static int Alive;

        private float _shrinkAt, _goneAt;
        private Vector3 _scale;
        private bool _counted;

        public void Begin(float life)
        {
            Alive++;
            _counted = true;
            _shrinkAt = Time.time + Mathf.Max(0.1f, life);
            _goneAt = _shrinkAt + 0.4f;
            _scale = transform.localScale;
        }

        private void Update()
        {
            if (Time.time < _shrinkAt) return;
            transform.localScale = _scale * Mathf.InverseLerp(_goneAt, _shrinkAt, Time.time);
            if (Time.time >= _goneAt) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_counted) Alive = Mathf.Max(0, Alive - 1);
        }
    }
}
