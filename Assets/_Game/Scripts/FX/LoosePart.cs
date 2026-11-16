using UnityEngine;

namespace ShiftJam
{
    /// <summary>Something knocked off a car (a part: CarDamageFx, D-0059; a piece of a wreck: CarWreck, D-0060): it lies
    /// about for a while, then shrinks away.</summary>
    public sealed class LoosePart : MonoBehaviour
    {
        private const float ShrinkSeconds = 0.4f;

        /// <summary>Knocked-off parts in the scene now (CarDamageFx caps how many there can be). A wreck's pieces are
        /// counted by CarWreck, not here.</summary>
        public static int Alive;

        private float _shrinkAt, _goneAt;
        private Vector3 _scale;
        private bool _counted, _begun;

        /// <summary>Starts the clock. <paramref name="counts"/>: whether it takes one of the knocked-off parts' places.</summary>
        public void Begin(float life, bool counts = true)
        {
            if (counts && !_counted) { Alive++; _counted = true; }
            _begun = true;
            _shrinkAt = Time.time + Mathf.Max(0.1f, life);
            _goneAt = _shrinkAt + ShrinkSeconds;
            _scale = transform.localScale;
        }

        /// <summary>Shrinks away from now (the car it came off is whole again). Never puts a shrink already under way back.</summary>
        public void ShrinkNow()
        {
            if (!_begun || Time.time >= _shrinkAt) return;
            _shrinkAt = Time.time;
            _goneAt = _shrinkAt + ShrinkSeconds;
        }

        private void Update()
        {
            if (!_begun || Time.time < _shrinkAt) return;
            transform.localScale = _scale * Mathf.InverseLerp(_goneAt, _shrinkAt, Time.time);
            if (Time.time >= _goneAt) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_counted) Alive = Mathf.Max(0, Alive - 1);
        }
    }
}
