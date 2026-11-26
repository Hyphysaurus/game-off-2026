using System;
using System.Collections.Generic;
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
        private readonly List<IInteractable> _parts = new List<IInteractable>();   // one thing's parts, reused (no new array a hit)

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
            IInteractable best = FindInReach();
            if (!ReferenceEquals(best, Target))
            {
                Target = best;
                TargetChanged?.Invoke(best);
            }
        }

        /// <summary>
        /// What E would use from where the player stands: the nearest thing in reach in front of the feet with something to
        /// say, or a hint (words, no key) when nothing E works on is in reach; null when nothing is. Third person asks every
        /// frame; first person asks when nothing usable is under its dot (D-0235), so it reaches whatever third person does.
        /// </summary>
        public IInteractable FindInReach()
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
            // A hint (words, no key) is the target only when nothing E works on is in reach (D-0185): Gap, a talking toy,
            // stands inside the gap's hint box, and with the search's centre inside both the hint won as often as not, so
            // E did nothing.
            IInteractable bestHint = null;
            float bestHintDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (carrier != null && carrier.Held != null && _hits[i].transform.IsChildOf(carrier.Held.transform)) continue;
                if (grab != null && grab.HeldTransform != null && _hits[i].transform.IsChildOf(grab.HeldTransform)) continue;
                // The first part on it with something to say (a toy finds a giant flashlight's Grab past its silent pick-up, D-0156).
                IInteractable candidate = null;
                _hits[i].GetComponentsInParent(false, _parts);
                foreach (var part in _parts)
                    if (!string.IsNullOrEmpty(part.PromptFor(this))) { candidate = part; break; }
                if (candidate == null) continue;   // nothing to say = not a target
                var hit = _hits[i];
                bool exact = hit is BoxCollider || hit is SphereCollider || hit is CapsuleCollider || (hit is MeshCollider mc && mc.convex);
                float distance = ((exact ? hit.ClosestPoint(centre) : hit.bounds.ClosestPoint(centre)) - centre).sqrMagnitude;
                if (candidate is IHintOnly) { if (distance < bestHintDistance) { bestHintDistance = distance; bestHint = candidate; } }
                else if (distance < bestDistance) { bestDistance = distance; best = candidate; }
            }
            if (best == null) best = bestHint;
            return best;
        }

        /// <summary>The player's third-person grab, or null.</summary>
        public ThirdPersonGrab Grab => grab;

        private void OnInteract()
        {
            // Mid-move (a roll, a punch, a block, a hit) the hands are busy: no grabbing, no cores (no size shift mid-roll).
            var moves = GetComponent<PlayerMoves>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
            if (moves != null && !moves.Free) return;
            if (grab != null && grab.isActiveAndEnabled && grab.TryUse()) return;   // set down what is held, or grab what is aimed at
            if (Grabbable.Release(gameObject)) return;   // holding a heavy thing: E lets go (D-0156)
            Target?.Interact(this);
        }
    }
}
