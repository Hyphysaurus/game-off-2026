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
        [SerializeField, Tooltip("Optional. Found on this object if empty. Whatever it holds is never a target.")] private Carrier carrier;
        [SerializeField, Tooltip("Optional. Found on this object, or added, if empty. On E it goes first: what it holds or aims at.")] private ThirdPersonGrab grab;
        [SerializeField, Tooltip("How far in front of the feet the search reaches, at kid size.")] private float reach = 1.6f;
        [SerializeField, Tooltip("Height of the search centre above the feet, at kid size.")] private float height = 0.9f;
        [SerializeField] private LayerMask mask = ~0;

        // 24 filled up beside the desk (its keyboard is 20 keys) and core B went unseen (D-0081); on the desk top a kid's search
        // holds 72 and core B came 67th, past 64 (walk check 20 Nov, D-0093)
        private readonly Collider[] _hits = new Collider[128];

        public Shifter Shifter => shifter;
        public IInteractable Target { get; private set; }
        /// <summary>Raised when the thing in reach changes, with null when nothing is.</summary>
        public event Action<IInteractable> TargetChanged;

        private void Awake()
        {
            if (shifter == null) shifter = GetComponent<Shifter>();
            if (carrier == null) carrier = GetComponent<Carrier>();
            if (grab == null) grab = GetComponent<ThirdPersonGrab>();
            if (grab == null) grab = gameObject.AddComponent<ThirdPersonGrab>();   // every player gets the grab
        }

        private void OnEnable() { input.Interact += OnInteract; }

        private void OnDisable()
        {
            input.Interact -= OnInteract;
            // Parked (first person): nothing is in reach, or the HUD would show the last prompt the whole time.
            if (Target == null) return;
            Target = null;
            TargetChanged?.Invoke(null);
        }

        private void Update()
        {
            float scale = shifter != null ? shifter.Scale : 1f;
            float radius = reach * scale * 0.5f;
            Vector3 centre = transform.position + Vector3.up * (height * scale) + transform.forward * radius;
            // The search runs from the chest sphere down to just over the feet: a ruler or a torch lying flat on the floor
            // was below a kid's sphere (its bottom is 3.5 above the feet at kid size) and could never be picked up (walk
            // check 19 Nov, D-0081). The nearest to the chest still wins, so nothing at chest height loses to the floor.
            Vector3 low = transform.position + Vector3.up * Mathf.Min(height * scale, radius * 0.8f) + transform.forward * radius;   // its bottom just under the floor
            int count = Physics.OverlapCapsuleNonAlloc(low, centre, radius, _hits, mask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (carrier != null && carrier.Held != null && _hits[i].transform.IsChildOf(carrier.Held.transform)) continue;
                if (grab != null && grab.HeldTransform != null && _hits[i].transform.IsChildOf(grab.HeldTransform)) continue;
                var candidate = _hits[i].GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (string.IsNullOrEmpty(candidate.PromptFor(this))) continue;   // nothing to say = not a target
                var hit = _hits[i];
                bool exact = hit is BoxCollider || hit is SphereCollider || hit is CapsuleCollider || (hit is MeshCollider mc && mc.convex);
                float distance = ((exact ? hit.ClosestPoint(centre) : hit.bounds.ClosestPoint(centre)) - centre).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }

            if (!ReferenceEquals(best, Target))
            {
                Target = best;
                TargetChanged?.Invoke(best);
            }
        }

        /// <summary>The player's third-person grab, or null.</summary>
        public ThirdPersonGrab Grab => grab;

        private void OnInteract()
        {
            // Mid-move (a roll, a punch, a block, a hit) the hands are busy: no grabbing, no cores (no size shift mid-roll).
            var moves = GetComponent<PlayerMoves>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
            if (moves != null && !moves.Free) return;
            if (grab != null && grab.isActiveAndEnabled && grab.TryUse()) return;   // set down what is held, or grab what is aimed at
            Target?.Interact(this);
        }
    }
}
