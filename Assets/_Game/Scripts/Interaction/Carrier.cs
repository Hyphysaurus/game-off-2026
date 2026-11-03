using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's hands. Holds one Carryable in the right hand (the Humanoid hand bone, with the thing's own
    /// grip offsets), fades the animator's Arms layer in so the arm reaches out and the fingers close, passes
    /// the Attack button to it, and puts it down on whatever is under the hand when Crouch is pressed.
    /// Never parents the thing, so the player's 35x scale does not touch it.
    /// </summary>
    public sealed class Carrier : MonoBehaviour
    {
        [SerializeField, Tooltip("Found on this object if empty.")] private PlayerInput input;
        [SerializeField, Tooltip("Fallback hold point if the rig has no right hand bone.")] private Transform holdPoint;
        [SerializeField, Tooltip("Found in children if empty.")] private Animator animator;
        [SerializeField, Tooltip("Animator layer that poses the arm while holding; faded in and out.")] private string armsLayer = "Arms";
        [SerializeField, Tooltip("Seconds to fade the arm pose in or out.")] private float armFade = 0.15f;
        [SerializeField, Tooltip("What a put-down thing may rest on.")] private LayerMask groundMask = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Transform _hand, _shoulder;
        private int _layer = -1;
        private float _armWeight;

        public Carryable Held { get; private set; }

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (holdPoint == null) holdPoint = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (animator.isHuman)
                {
                    _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    _shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                }
                _layer = animator.GetLayerIndex(armsLayer);
            }
        }

        private void OnEnable()
        {
            if (input != null) { input.UseHeld += OnUse; input.Drop += PutDown; }
        }

        private void OnDisable()
        {
            if (input != null) { input.UseHeld -= OnUse; input.Drop -= PutDown; }
        }

        public void PickUp(Carryable thing)
        {
            if (thing == null || Held != null) return;
            Held = thing;
            foreach (var col in Held.GetComponentsInChildren<Collider>()) col.enabled = false;   // out of the physics while in hand
            Held.SetCarried(true);
            Follow();
        }

        /// <summary>Rest the held thing on whatever is under the hand, facing the way the player faces, and let go.</summary>
        public void PutDown()
        {
            if (Held == null) return;
            Carryable thing = Held;
            Held = null;
            foreach (var col in thing.GetComponentsInChildren<Collider>()) col.enabled = true;

            Vector3 from = _hand != null ? _hand.position : holdPoint.position;
            float yaw = transform.eulerAngles.y;
            thing.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * thing.HoldRotation;

            float floorY = transform.position.y;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, _hits, 1000f, groundMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform) || _hits[i].transform.IsChildOf(thing.transform)) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; floorY = _hits[i].point.y; }
            }

            // Rest it by its renderer bounds, so the pivot does not matter.
            thing.transform.position = new Vector3(from.x, floorY, from.z);
            var rs = thing.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                thing.transform.position += Vector3.up * (floorY - b.min.y);
            }
            thing.SetCarried(false);
        }

        private void OnUse()
        {
            if (Held != null) Held.Use();
        }

        private void Update()
        {
            // The arm pose fades with the hold; Update so it is in before the animator evaluates this frame.
            if (animator == null || _layer < 0) return;
            float target = Held != null ? 1f : 0f;
            _armWeight = Mathf.MoveTowards(_armWeight, target, Time.deltaTime / Mathf.Max(0.01f, armFade));
            animator.SetLayerWeight(_layer, _armWeight);
        }

        private void LateUpdate()
        {
            if (Held != null) Follow();
        }

        private void Follow()
        {
            if (_hand != null)
            {
                // The thing points the way the whole arm reaches (shoulder -> hand), whatever the elbow and
                // wrist are doing, so a torch reads as aimed by the arm rather than held like a microphone.
                Vector3 forward = _shoulder != null ? (_hand.position - _shoulder.position) : _hand.up;
                if (forward.sqrMagnitude < 1e-6f) forward = transform.forward;
                Quaternion rot = Quaternion.LookRotation(forward.normalized, Vector3.up) * Held.GripRotation;
                Vector3 pos = _hand.TransformPoint(Held.GripPalm) + rot * Held.GripOffset;
                Held.transform.SetPositionAndRotation(pos, rot);
            }
            else
            {
                Held.transform.SetPositionAndRotation(holdPoint.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Held.HoldRotation);
            }
        }
    }
}
