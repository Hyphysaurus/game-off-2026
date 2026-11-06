using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Half-Life 2's hands. E on a loose prop picks it up (if it is light and small enough for your size; Heavy props need
    /// kid size) and holds it floating in front of the eyes on a spring: the prop is driven by velocity, never parented,
    /// so it still bumps into walls and other props. Attack throws it, E again lets go, the mouse wheel turns it, and
    /// holding R (or the right mouse button) turns it with the mouse. Jammed behind a wall, it slips out of your hands.
    /// E on anything else does what E does in third person (a switch, a weapon pickup); vehicles are left to on-foot play.
    /// Also draws the crosshair and the E hint.
    /// </summary>
    public sealed class PhysicsGrabber : MonoBehaviour
    {
        private FirstPersonSettings _s;
        private PlayerInput _input;
        private FirstPersonBody _body;
        private Transform _eye;
        private WeaponHolder _holder;
        private Interactor _interactor;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly List<Collider> _heldColliders = new List<Collider>();
        private Quaternion _turn;            // the held prop's rotation relative to the body's heading
        private Vector3 _centreLocal;        // bounds centre in the prop's own space
        private float _distance, _strain, _savedMaxAngular;
        private bool _savedGravity;
        private RigidbodyInterpolation _savedInterp;
        private int _busyFrame = -1;
        private string _hint;

        public Rigidbody Held { get; private set; }
        /// <summary>True while holding, and on the frame something was thrown: that click is not a weapon's.</summary>
        public bool BusyThisFrame => Held != null || _busyFrame == Time.frameCount;
        public bool RotatingHeld => Held != null && _input != null && _input.RotateHeld;

        public void Setup(FirstPersonSettings settings, PlayerInput input, FirstPersonBody body, Transform eye, WeaponHolder holder, Interactor interactor)
        {
            _s = settings; _input = input; _body = body; _eye = eye; _holder = holder; _interactor = interactor;
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.Interact += OnInteract;
            _input.UseHeld += OnAttack;
        }

        private void OnDisable()
        {
            if (_input != null) { _input.Interact -= OnInteract; _input.UseHeld -= OnAttack; }
            Release();
        }

        private float Scale => _body != null ? _body.Scale : 1f;

        private void OnInteract()
        {
            if (Held != null) { Release(); _busyFrame = Time.frameCount; return; }
            if (!Look(out RaycastHit hit)) return;
            var col = hit.collider;
            var pickup = col.GetComponentInParent<WeaponPickup>();
            if (pickup != null && pickup.Available && _holder != null && pickup.Give(_holder)) return;
            var rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic && CanLift(rb, out _)) { Grab(rb, hit.point); return; }
            var use = Usable(col);
            if (use != null) use.Interact(_interactor);
        }

        private void OnAttack()
        {
            if (Held == null || Cursor.lockState != CursorLockMode.Locked || Time.timeScale <= 0f) return;
            Throw();
        }

        /// <summary>The first thing on the centre ray within reach, not us.</summary>
        private bool Look(out RaycastHit best)
        {
            best = default;
            if (_eye == null) return false;
            float scale = Scale;
            int n = Physics.SphereCastNonAlloc(_eye.position, 0.1f * scale, _eye.forward, _hits, _s.grabReach * scale, ~0, QueryTriggerInteraction.Collide);
            float nearest = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.transform.IsChildOf(transform) || (_body.Owner != null && h.transform.IsChildOf(_body.Owner))) continue;
                if (h.collider.isTrigger && h.collider.GetComponentInParent<IInteractable>() == null) continue;   // wind, checkpoints
                if (h.distance <= 0f) continue;   // started inside it
                if (h.distance < nearest) { nearest = h.distance; best = h; found = true; }
            }
            return found;
        }

        /// <summary>An IInteractable that makes sense in first person: not a vehicle (they seat the parked player).</summary>
        private IInteractable Usable(Collider col)
        {
            if (_interactor == null) return null;
            var thing = col.GetComponentInParent<IInteractable>();
            if (thing == null || thing is CarSeat || thing is ToyCar) return null;
            return string.IsNullOrEmpty(thing.PromptFor(_interactor)) ? null : thing;
        }

        private bool CanLift(Rigidbody rb, out string why)
        {
            why = null;
            if (rb.GetComponentInParent<ArcadeCar>() != null || rb.GetComponentInParent<ToyCar>() != null) { why = "Too heavy"; return false; }
            if (rb.mass > _s.maxLiftMass) { why = "Too heavy"; return false; }
            var shifter = _body.Owner != null ? _body.Owner.GetComponent<Shifter>() : null;
            if (rb.TryGetComponent(out Pushable push) && !push.PushableBy(shifter != null ? shifter.Size : ShiftSize.Kid)) { why = "Too heavy"; return false; }
            Bounds b = BoundsOf(rb);
            if (Mathf.Max(b.size.x, b.size.y, b.size.z) > _s.maxLiftSize * Scale) { why = "Too big"; return false; }
            return true;
        }

        private static Bounds BoundsOf(Rigidbody rb)
        {
            var cols = rb.GetComponentsInChildren<Collider>();
            Bounds b = new Bounds(rb.position, Vector3.zero);
            bool any = false;
            foreach (var c in cols)
            {
                if (c.isTrigger || c.attachedRigidbody != rb) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            return b;
        }

        private void Grab(Rigidbody rb, Vector3 at)
        {
            Held = rb;
            _savedGravity = rb.useGravity; _savedInterp = rb.interpolation; _savedMaxAngular = rb.maxAngularVelocity;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxAngularVelocity = 30f;
            Bounds b = BoundsOf(rb);
            _centreLocal = Quaternion.Inverse(rb.rotation) * (b.center - rb.position);
            float reach = b.extents.magnitude + _body.Capsule.radius + 0.15f * Scale;
            _distance = Mathf.Clamp(Mathf.Max(_s.holdDistance * Scale, reach), 0.1f, _s.grabReach * Scale * 1.25f);
            _turn = Quaternion.Inverse(Quaternion.Euler(0f, _eye.eulerAngles.y, 0f)) * rb.rotation;
            _strain = 0f;
            // We never stand on, or get shoved by, what we hold.
            _heldColliders.Clear();
            foreach (var c in rb.GetComponentsInChildren<Collider>())
                if (c.attachedRigidbody == rb) { _heldColliders.Add(c); Physics.IgnoreCollision(c, _body.Capsule, true); }
            _busyFrame = Time.frameCount;
        }

        /// <summary>Lets go gently: the prop keeps its current motion and falls.</summary>
        public void Release()
        {
            var rb = Held;
            if (rb == null) { Held = null; return; }
            Held = null;
            rb.useGravity = _savedGravity;
            rb.interpolation = _savedInterp;
            rb.maxAngularVelocity = _savedMaxAngular;
            if (_body != null && _body.Capsule != null)
                foreach (var c in _heldColliders) if (c != null) Physics.IgnoreCollision(c, _body.Capsule, false);
            _heldColliders.Clear();
        }

        private void Throw()
        {
            var rb = Held;
            Release();
            if (rb == null) return;
            float massFactor = Mathf.Clamp(_s.throwFullSpeedMass / Mathf.Max(0.01f, rb.mass), 0.25f, 1f);
            rb.linearVelocity = _eye.forward * (_s.throwSpeed * Scale * massFactor) + _body.Velocity * 0.5f;
            rb.angularVelocity = Random.insideUnitSphere * 4f;
            _busyFrame = Time.frameCount;
        }

        /// <summary>Turns the held prop with a mouse delta (degrees), about the view's up and right.</summary>
        public void RotateBy(Vector2 delta)
        {
            if (Held == null) return;
            float k = _s.mouseRotateSensitivity / Mathf.Max(0.0001f, _s.mouseSensitivity);   // look comes in degrees already
            Quaternion yaw = Quaternion.Euler(0f, _eye.eulerAngles.y, 0f);
            Quaternion world = Quaternion.AngleAxis(-delta.x * k, Vector3.up) * Quaternion.AngleAxis(delta.y * k, _eye.right);
            _turn = Quaternion.Inverse(yaw) * world * yaw * _turn;
        }

        private void Update()
        {
            _hint = null;
            if (Held == null && Held is object) Release();   // destroyed while held
            if (Held != null)
            {
                float wheel = _input != null ? _input.Scroll : 0f;
                if (wheel != 0f) _turn = Quaternion.Euler(0f, wheel * _s.wheelRotateStep, 0f) * _turn;
                _hint = "Click  Throw     E  Drop     Wheel / hold R  Turn";
                return;
            }
            if (!Look(out RaycastHit hit)) return;
            var col = hit.collider;
            var pickup = col.GetComponentInParent<WeaponPickup>();
            if (pickup != null && pickup.Available && _holder != null) { _hint = "E  Take " + pickup.Weapon.DisplayName; return; }
            var rb = col.attachedRigidbody;
            string why = null;
            if (rb != null && !rb.isKinematic) { if (CanLift(rb, out why)) { _hint = "E  Pick up"; return; } }
            var use = Usable(col);
            _hint = use != null ? "E  " + use.PromptFor(_interactor) : why;
        }

        private void FixedUpdate()
        {
            var rb = Held;
            if (rb == null || _eye == null) return;
            float dt = Time.fixedDeltaTime, scale = Scale;
            Quaternion targetRot = Quaternion.Euler(0f, _eye.eulerAngles.y, 0f) * _turn;
            Vector3 targetCentre = _eye.position + _eye.forward * _distance;
            Vector3 targetPos = targetCentre - targetRot * _centreLocal;

            Vector3 gap = targetPos - rb.position;
            Vector3 want = Vector3.ClampMagnitude(gap * (_s.holdStiffness / dt), _s.maxHoldSpeed * scale);
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, want, 0.8f);

            Quaternion delta = targetRot * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 1e-6f)
                rb.angularVelocity = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * _s.holdStiffness / dt), 25f);

            // Stuck behind something for a moment: it slips from the hands.
            _strain = gap.magnitude > _s.breakDistance * scale ? _strain + dt : 0f;
            if (_strain > 0.25f) Release();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);
            if (!string.IsNullOrEmpty(_hint))
            {
                var style = GUI.skin.label;
                var content = new GUIContent(_hint);
                Vector2 size = style.CalcSize(content);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.Label(new Rect(cx - size.x * 0.5f + 1f, cy + 24f + 1f, size.x, size.y), content, style);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - size.x * 0.5f, cy + 24f, size.x, size.y), content, style);
            }
            GUI.color = old;
        }
    }
}
