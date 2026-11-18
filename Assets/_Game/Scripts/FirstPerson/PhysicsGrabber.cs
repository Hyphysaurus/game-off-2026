using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Half-Life 2's hands. E on a loose prop picks it up (if it is light and small enough for your size; Heavy props need
    /// kid size) and holds it floating in front of the eyes on a spring: the prop is driven by velocity, never parented,
    /// so it still bumps into walls and other props. Attack throws it, E again lets go, the mouse wheel turns it, and
    /// holding R (or the right mouse button) turns it with the mouse. Jammed behind a wall, or when a shift starts, it
    /// drops. E on anything else does what E does in third person (a switch, a weapon pickup); vehicles and the kid's
    /// carried things (a carried prop, the flashlight) are left to on-foot play, as they seat or fill the parked player's
    /// hands. Also draws the crosshair and the E hint.
    /// </summary>
    public sealed class PhysicsGrabber : MonoBehaviour
    {
        private FirstPersonSettings _s;
        private PlayerInput _input;
        private FirstPersonBody _body;
        private Transform _eye;
        private WeaponHolder _holder;
        private Interactor _interactor;
        private Shifter _shifter;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly SpringHold _hold = new SpringHold();   // the float-in-front spring, shared with the third-person grab
        private int _busyFrame = -1;
        private string _hint;

        public Rigidbody Held => _hold.Held;
        /// <summary>True while holding, and on the frame something was thrown: that click is not a weapon's.</summary>
        public bool BusyThisFrame => Held != null || _busyFrame == Time.frameCount;
        public bool RotatingHeld => Held != null && _input != null && _input.RotateHeld;

        public void Setup(FirstPersonSettings settings, PlayerInput input, FirstPersonBody body, Transform eye, WeaponHolder holder, Interactor interactor)
        {
            _s = settings; _input = input; _body = body; _eye = eye; _holder = holder; _interactor = interactor;
            _shifter = body != null && body.Owner != null ? body.Owner.GetComponent<Shifter>() : null;
        }

        private void OnEnable()
        {
            if (_shifter != null) _shifter.ShiftStarted += OnShiftStarted;
            if (_input == null) return;
            _input.Interact += OnInteract;
            _input.UseHeld += OnAttack;
        }

        private void OnDisable()
        {
            if (_shifter != null) _shifter.ShiftStarted -= OnShiftStarted;
            if (_input != null) { _input.Interact -= OnInteract; _input.UseHeld -= OnAttack; }
            Release();
        }

        /// <summary>A shift changes the hold distance, the reach, what can be lifted and the near plane: let go.</summary>
        private void OnShiftStarted(ShiftSize _) => Release();

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

        /// <summary>An IInteractable that makes sense in first person: not a vehicle (they seat the parked player), and not
        /// the kid's carry (a Carried prop, a flashlight picked up by a kid): those go into the parked player's hands, which
        /// never follow while parked, so the thing would hang in mid-air and the weapon would be put away.</summary>
        private IInteractable Usable(Collider col)
        {
            if (_interactor == null) return null;
            var thing = col.GetComponentInParent<IInteractable>();
            if (thing == null || thing is CarSeat || thing is ToyCar || thing is Carried) return null;
            if (thing is Flashlight && _shifter != null && _shifter.Size == ShiftSize.Kid) return null;   // a toy's E still switches it
            return string.IsNullOrEmpty(thing.PromptFor(_interactor)) ? null : thing;
        }

        private bool CanLift(Rigidbody rb, out string why)
        {
            why = null;
            if (rb.GetComponentInParent<ArcadeCar>() != null || rb.GetComponentInParent<ToyCar>() != null || ToyCar.OwnsBall(rb)) { why = "Too heavy"; return false; }
            if (rb.mass > _s.maxLiftMass) { why = "Too heavy"; return false; }
            if (rb.TryGetComponent(out Pushable push) && !push.PushableBy(_shifter != null ? _shifter.Size : ShiftSize.Kid)) { why = "Too heavy"; return false; }
            Bounds b = BoundsOf(rb);
            if (Mathf.Max(b.size.x, b.size.y, b.size.z) > _s.maxLiftSize * Scale) { why = "Too big"; return false; }
            return true;
        }

        private static Bounds BoundsOf(Rigidbody rb) => SpringHold.BoundsOf(rb);

        private void Grab(Rigidbody rb, Vector3 at)
        {
            // We never stand on, or get shoved by, what we hold (the hold ignores the body's capsule).
            _hold.Grab(rb, Quaternion.Euler(0f, _eye.eulerAngles.y, 0f), _body.Capsule);
            Bounds b = BoundsOf(rb);
            float reach = b.extents.magnitude + _body.Capsule.radius + 0.15f * Scale;
            _hold.Distance = Mathf.Clamp(Mathf.Max(_s.holdDistance * Scale, reach), 0.1f, _s.grabReach * Scale * 1.25f);
            _busyFrame = Time.frameCount;
        }

        /// <summary>Lets go gently: the prop keeps its current motion and falls.</summary>
        public void Release() => _hold.Release();

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
            _hold.RotateBy(delta * k, Quaternion.Euler(0f, _eye.eulerAngles.y, 0f), _eye.right);
        }

        private void Update()
        {
            _hint = null;
            if (Held == null && Held is object) Release();   // destroyed while held
            if (Held != null)
            {
                float wheel = _input != null ? _input.Scroll : 0f;
                if (wheel != 0f) _hold.Spin(wheel * _s.wheelRotateStep);
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
            if (Held == null || _eye == null) return;
            var t = new SpringHold.Tuning { stiffness = _s.holdStiffness, maxSpeed = _s.maxHoldSpeed, breakDistance = _s.breakDistance };
            _hold.Drive(_eye.position + _eye.forward * _hold.Distance, Quaternion.Euler(0f, _eye.eulerAngles.y, 0f), Scale, t, Time.fixedDeltaTime);
        }

        /// <summary>True while the dot in the middle of the view is drawn: not while a gun's own sight is up at the
        /// eye (the test room's rule).</summary>
        public bool DotShowing => _holder == null || !_holder.DownTheSight;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            if (DotShowing) GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, 4f, 4f), Texture2D.whiteTexture);
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
