using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Looks for the nearest IInteractable within reach in front of the player and uses it on Interact.
    /// Reach scales with the Shifter: a toy-sized player reaches toy-sized distances.
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField] private PlayerInput input;
        [SerializeField, Tooltip("Optional. Found on this object if empty.")] private Shifter shifter;
        [SerializeField, Tooltip("How far in front of the feet the search reaches, at kid size.")] private float reach = 1.6f;
        [SerializeField, Tooltip("Height of the search centre above the feet, at kid size.")] private float height = 0.9f;
        [SerializeField] private LayerMask mask = ~0;

        private readonly Collider[] _hits = new Collider[24];

        public Shifter Shifter => shifter;
        public IInteractable Target { get; private set; }
        /// <summary>Raised when the thing in reach changes, with null when nothing is.</summary>
        public event Action<IInteractable> TargetChanged;

        private void Awake()
        {
            if (shifter == null) shifter = GetComponent<Shifter>();
        }

        private void OnEnable() { input.Interact += OnInteract; }
        private void OnDisable() { input.Interact -= OnInteract; }

        private void Update()
        {
            float scale = shifter != null ? shifter.Scale : 1f;
            float radius = reach * scale * 0.5f;
            Vector3 centre = transform.position + Vector3.up * (height * scale) + transform.forward * radius;
            int count = Physics.OverlapSphereNonAlloc(centre, radius, _hits, mask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                var candidate = _hits[i].GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                float distance = (_hits[i].ClosestPoint(centre) - centre).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }

            if (!ReferenceEquals(best, Target))
            {
                Target = best;
                TargetChanged?.Invoke(best);
            }
        }

        private void OnInteract() => Target?.Interact(this);
    }
}
