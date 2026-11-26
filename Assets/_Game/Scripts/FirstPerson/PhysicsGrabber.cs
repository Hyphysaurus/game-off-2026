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
    /// hands. On the player itself (D-0194, D-0195) the player is not parked: E on a vehicle leaves first person and
    /// gets in, a kid may pick the flashlight up, E lets go of a heavy thing that is held (a turntable), and nothing
    /// is grabbed or used mid-move, as in third person. With nothing E works on under the dot, E uses what third person
    /// would use from where you stand (D-0235). Also draws the crosshair and the E hint, from the same choice as E's.
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

        private bool Riding => _body != null && _body.Riding;

        private void OnInteract()
        {
            if (Held != null) { Release(); _busyFrame = Time.frameCount; return; }
            if (Riding && _body.Owner != null)
            {
                // The third-person Interactor's rules, for the player that is really there (D-0195): mid-move the hands
                // are busy, and E lets go of a heavy thing that is held.
                var moves = _body.Owner.GetComponent<PlayerMoves>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
                if (moves != null && !moves.Free) return;
                if (Grabbable.Release(_body.Owner.gameObject)) { _busyFrame = Time.frameCount; return; }
            }
            Choice c = Choose();
            switch (c.act)
            {
                case Act.Take: c.pickup.Give(_holder); return;
                case Act.Lift: Grab(c.body, c.point); return;
                case Act.Use:
                    // A vehicle seats the player and has its own camera: first person is left first, then E is the on-foot E.
                    if (c.use is CarSeat || c.use is ToyCar) GameMode.Set(GameMode.Mode.Play);
                    c.use.Interact(_interactor);
                    return;
            }
        }

        /// <summary>What E does now (D-0235). One choice draws the hint and answers the press, so they never differ.</summary>
        private enum Act { None, Take, Lift, Use }

        private struct Choice
        {
            public Act act;
            public WeaponPickup pickup;   // Take
            public Rigidbody body;        // Lift
            public Vector3 point;         // Lift: where the dot met it
            public IInteractable use;     // Use: a switch, a core, a door; a hint's words when nothing E works on is near
            public string why;            // the prop under the dot cannot be lifted ("Too heavy", "Too big"), shown when nothing is chosen
        }

        /// <summary>
        /// First what the dot is on, so a thing you point at wins: a weapon to take, a prop to lift, something to use. With
        /// nothing E works on under the dot, what third person would use from where you stand (the Interactor's own search,
        /// D-0235): a small switch low on the skirting board, a core by your feet. His 25 Nov play: first person reached
        /// fewer things than third person, because only the thin probe down the middle of the view counted. Words-only
        /// hints come last, as in third person (D-0185).
        /// </summary>
        private Choice Choose()
        {
            var c = new Choice();
            IInteractable hint = null;
            if (Look(out RaycastHit hit))
            {
                var col = hit.collider;
                var pickup = col.GetComponentInParent<WeaponPickup>();
                if (pickup != null && pickup.Available && _holder != null) { c.act = Act.Take; c.pickup = pickup; return c; }
                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic && CanLift(rb, out c.why)) { c.act = Act.Lift; c.body = rb; c.point = hit.point; return c; }
                var aimed = Usable(col.GetComponentInParent<IInteractable>());
                if (aimed != null && !(aimed is IHintOnly)) { c.act = Act.Use; c.use = aimed; return c; }
                hint = aimed;
            }
            var near = _interactor != null ? Usable(_interactor.FindInReach()) : null;
            if (near is WeaponPickup nearPickup)
            {
                if (nearPickup.Available && _holder != null) { c.act = Act.Take; c.pickup = nearPickup; return c; }
            }
            else if (near != null && !(near is IHintOnly)) { c.act = Act.Use; c.use = near; return c; }
            else if (hint == null) hint = near;
            if (hint != null) { c.act = Act.Use; c.use = hint; }
            return c;
        }

        private Act _hintAct;
        private object _hintOf;
        private string _hintWords, _hintText;

        /// <summary>The words under the dot for a choice: built again only when the choice or its words change, not every frame.</summary>
        private string HintFor(Choice c)
        {
            object of = null; string words = null;
            switch (c.act)
            {
                case Act.None: return c.why;
                case Act.Take: of = c.pickup; words = c.pickup.Weapon.DisplayName; break;
                case Act.Use: of = c.use; words = c.use.PromptFor(_interactor); break;
            }
            if (_hintText == null || c.act != _hintAct || !ReferenceEquals(of, _hintOf) || !ReferenceEquals(words, _hintWords))
            {
                _hintAct = c.act; _hintOf = of; _hintWords = words;
                _hintText = c.act == Act.Lift ? "E  Pick up" : c.act == Act.Take ? "E  Take " + words : c.use is IHintOnly ? words : "E  " + words;
            }
            return _hintText;
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
        private IInteractable Usable(IInteractable thing)
        {
            if (_interactor == null || thing == null || thing is Carried) return null;   // a carried prop is lifted by these hands (the spring), not carried in the arms
            // Parked (the old body): no vehicle and no kid's flashlight. On the player itself both are the player's own (D-0195).
            if (!Riding && (thing is CarSeat || thing is ToyCar)) return null;
            if (!Riding && thing is Flashlight && _shifter != null && _shifter.Size == ShiftSize.Kid) return null;   // a toy's E still switches it
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
            _hint = HintFor(Choose());
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
