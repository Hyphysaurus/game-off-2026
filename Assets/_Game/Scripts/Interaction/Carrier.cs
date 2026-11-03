using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's hands. Holds one Carryable at a hold point, aims it where the camera looks (so you aim a
    /// flashlight by looking), passes the Attack button to it, and puts it down on whatever is under the hand
    /// when Crouch is pressed. Never parents the thing, so the player's 35x scale does not touch it.
    /// </summary>
    public sealed class Carrier : MonoBehaviour
    {
        [SerializeField, Tooltip("Found on this object if empty.")] private PlayerInput input;
        [SerializeField, Tooltip("Where the held thing sits: a child of the Player at kid-size hand height.")] private Transform holdPoint;
        [SerializeField, Tooltip("What the held thing points along. The main camera if empty.")] private Transform aim;
        [SerializeField, Tooltip("What a put-down thing may rest on.")] private LayerMask groundMask = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public Carryable Held { get; private set; }

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (holdPoint == null) holdPoint = transform;
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

            Vector3 from = holdPoint.position;
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

        private void LateUpdate()
        {
            if (Held != null) Follow();
        }

        private void Follow()
        {
            Transform a = aim != null ? aim : (Camera.main != null ? Camera.main.transform : transform);
            Held.transform.SetPositionAndRotation(holdPoint.position, Quaternion.LookRotation(a.forward, Vector3.up) * Held.HoldRotation);
        }
    }
}
