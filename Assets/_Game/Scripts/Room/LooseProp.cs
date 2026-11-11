using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A loose thing that is meant to be knocked about and put back: a cone, a tyre, a block of a wall a car drives
    /// through. It remembers where it stood when the scene started; the reset key (WorkshopReset, F9) stands it there
    /// again, still. Nothing else: its weight and its Pushable are whatever the builder gave it.
    /// </summary>
    public sealed class LooseProp : MonoBehaviour, IResettable
    {
        private Vector3 _position;
        private Quaternion _rotation;
        private Rigidbody _body;

        private void Awake()
        {
            _position = transform.position;
            _rotation = transform.rotation;
            TryGetComponent(out _body);
        }

        public void ResetState()
        {
            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.position = _position;
                _body.rotation = _rotation;
            }
            transform.SetPositionAndRotation(_position, _rotation);
            if (_body != null) _body.Sleep();
        }
    }
}
