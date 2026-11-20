using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Grab and carry in third person (D-0033, carried in the arms since D-0042). Look at a prop and press E: the hands reach
    /// for it and bring it in front of the chest, held in both arms (CarryPose puts the palms on its sides), on the same
    /// spring as first person's hands (SpringHold), so it still bumps into things. It turns with the body. The footprint shows
    /// where it will land a short way ahead (the wheel nudges that spot nearer or farther), E (or C) lowers it there,
    /// levelled, and lets go; R or the right mouse button with the mouse turns it; a click throws it where the camera aims.
    /// It grabs loose physics props and carryables (pillows, the bear, boxes). The flashlight and the ruler stay
    /// hand-held (Carrier), and anything with its own E (cores, switches, seats, weapons) keeps it. Sizes follow the
    /// Shifter: the kid lifts a pillow, the toy only toy-sized things. The Interactor asks it first on E. A shift lets go
    /// at once (D-0041).
    /// </summary>
    public sealed class ThirdPersonGrab : MonoBehaviour, ICarryHands
    {
        [Header("Reach, in units at toy size (scaled with size)")]
        [SerializeField, Tooltip("How far from the chest a thing can be grabbed.")] private float reach = 3.5f;
        [SerializeField, Tooltip("Height of the chest above the feet.")] private float chestHeight = 1.1f;
        [Header("Carry in the arms (D-0042), units at toy size (scaled with size)")]
        [SerializeField, Tooltip("Height of a carried prop's middle above the feet (raised when a tall prop would touch the ground).")] private float carryHeight = 0.8f;
        [SerializeField, Tooltip("Gap between the body and a carried prop's near side.")] private float carryGap = 0.1f;
        [SerializeField, Tooltip("Where E sets it down: this far ahead of the body to the prop's near side. The wheel nudges it.")] private float setDownGap = 0.35f;
        [SerializeField, Tooltip("The wheel's range for the set-down spot: from about arm's reach to one body length.")] private float minSetDownGap = 0.05f, maxSetDownGap = 1.8f;
        [SerializeField, Tooltip("How far one wheel notch moves the set-down spot.")] private float setDownStep = 0.15f;
        [SerializeField, Tooltip("Hands on the prop's sides this far back from its middle, as a share of its half depth (1 = its back edge).")] private float handsBack = 0.3f;
        [SerializeField, Tooltip("Seconds a grabbed prop takes from where it lies into the arms.")] private float reachTime = 0.25f;
        [SerializeField, Tooltip("Seconds a set-down prop takes from the arms to the footprint. E again sets it down at once.")] private float lowerTime = 0.25f;
        [SerializeField, Tooltip("Seconds the hands take to come away after letting go.")] private float handsFade = 0.2f;
        [Header("What it lifts")]
        [SerializeField, Tooltip("Largest liftable size (the longest side), units at toy size.")] private float maxLiftSize = 4f;
        [SerializeField, Tooltip("Heaviest liftable Rigidbody mass.")] private float maxLiftMass = 20f;
        [Header("Feel (as first person's hands)")]
        [SerializeField] private float holdStiffness = 0.35f;
        [SerializeField, Tooltip("Units per second at toy size.")] private float maxHoldSpeed = 25f;
        [SerializeField, Tooltip("Jammed further than this from the hold point for a moment, it slips. Units at toy size.")] private float breakDistance = 1.2f;
        [SerializeField, Tooltip("Throw speed for light things, units per second at toy size.")] private float throwSpeed = 12f;
        [SerializeField, Tooltip("Things heavier than this are thrown slower, in proportion.")] private float throwFullSpeedMass = 4f;
        [SerializeField, Tooltip("Degrees per mouse pixel while turning the held thing.")] private float rotateSensitivity = 0.4f;
        [SerializeField] private Color footprintColour = new Color(0.55f, 0.9f, 1f, 0.45f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private PlayerInput _input; private Shifter _shifter; private Interactor _interactor; private Carrier _carrier;
        private CharacterController _capsule; private ShoulderCamera _camera; private Transform _view;
        private readonly SpringHold _hold = new SpringHold();
        private readonly RaycastHit[] _hits = new RaycastHit[24];
        private GrabSettle.Original _was;
        private Carryable _heldCarry;
        private GameObject _target;
        private int _busyFrame = -1;
        private Transform _footprint; private Renderer _footprintRenderer; private MaterialPropertyBlock _block;

        private enum Phase { None, Reaching, Holding, Lowering }
        private Phase _phase;
        private float _t0, _gap;
        private Vector3 _reachFrom, _lowerFrom, _lowerTo;
        private Quaternion _turnFrom, _turnTo;
        private float _lowerFloor;
        private Collider[] _heldColliders = System.Array.Empty<Collider>();
        private HandGoal _lastRight, _lastLeft;   // in the body's space, for the hands coming away after a let-go
        private float _releasedAt = float.NegativeInfinity;

        /// <summary>What the prompt shows while holding or aiming at something grabbable; null otherwise.</summary>
        public string Hint { get; private set; }
        /// <summary>Why the thing under the aim can't be grabbed ("Too big"), for when nothing else is in reach.</summary>
        public string Why { get; private set; }
        public bool Holding => _hold.Held != null;
        public Transform HeldTransform => _hold.Held != null ? _hold.Held.transform : null;
        /// <summary>True while holding, and on the frame something was thrown: that click is not a weapon's.</summary>
        public bool BusyThisFrame => Holding || _busyFrame == Time.frameCount;

        private float Scale => _shifter != null ? _shifter.Scale : 1f;
        /// <summary>The body's heading: a carried prop sits in front of the body and turns with it.</summary>
        private Quaternion Heading => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        private float Radius => _capsule != null ? _capsule.radius * Scale : 0.3f * Scale;
        /// <summary>May hold at all: playing on foot (not seated, not in first person or Build Mode), hands free.</summary>
        private bool Allowed => GameMode.Current == GameMode.Mode.Play && _interactor != null && _interactor.isActiveAndEnabled
                                && (_carrier == null || _carrier.Held == null);
        /// <summary>Takes keys: allowed, and nothing else has them (the console, a paused menu). Holding carries on meanwhile.</summary>
        private bool Listening => Allowed && _input != null && _input.enabled && Time.timeScale > 0f;

        private void Awake()
        {
            _input = GetComponent<PlayerInput>(); _shifter = GetComponent<Shifter>(); _interactor = GetComponent<Interactor>();
            _carrier = GetComponent<Carrier>(); _capsule = GetComponent<CharacterController>();
            var pose = CarryPose.On(GetComponentInChildren<Animator>());   // the arms around what is held (D-0042)
            if (pose != null) pose.Add(this);
        }

        private void OnEnable()
        {
            if (_input != null) { _input.UseHeld += OnThrow; _input.Drop += OnSetDown; }
            if (_shifter != null) _shifter.ShiftStarted += OnShiftStarted;
            GameMode.Changed += OnModeChanged;
        }

        private void OnDisable()
        {
            if (_input != null) { _input.UseHeld -= OnThrow; _input.Drop -= OnSetDown; }
            if (_shifter != null) _shifter.ShiftStarted -= OnShiftStarted;
            GameMode.Changed -= OnModeChanged;
            LetGo(fadeHands: false);
            _releasedAt = float.NegativeInfinity;
            Hint = Why = null; _target = null;
            ShowFootprint(false);
        }

        private void OnDestroy() { if (_footprint != null) Destroy(_footprint.gameObject); }

        private void OnShiftStarted(ShiftSize _) => LetGo(fadeHands: false);      // at once (D-0041): the reach and what fits change
        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to) { if (to != GameMode.Mode.Play) LetGo(fadeHands: true); }

        /// <summary>E, asked by the Interactor before its own target: lowers what is held onto the footprint, or grabs what is aimed at.</summary>
        public bool TryUse()
        {
            if (!Listening) return false;
            if (Holding) { StartSetDown(); return true; }
            if (_target == null) return false;
            Grab(_target);
            return true;
        }

        // ---------- per frame ----------

        private void Update()
        {
            if (_hold.HeldWasDestroyed) { _hold.Release(); _was = null; _heldCarry = null; _phase = Phase.None; _heldColliders = System.Array.Empty<Collider>(); }
            if (!Allowed || !FindView())
            {
                if (Holding) LetGo(fadeHands: true);
                Hint = Why = null; _target = null;
                ShowFootprint(false);
                return;
            }
            if (Holding && _phase == Phase.Lowering && Time.time - _t0 >= lowerTime) SetDown();   // it reached the footprint
            if (Holding && _phase == Phase.Reaching && Time.time - _t0 >= reachTime) _phase = Phase.Holding;
            if (Holding)
            {
                if (Listening && _phase != Phase.Lowering)
                {
                    // Turning with the mouse: the look goes to the held thing, so the camera stays put.
                    if (_input.RotateHeld) _hold.RotateBy(_input.ConsumeLook(rotateSensitivity, 90f, Time.unscaledDeltaTime), Heading, _view.right);
                    float wheel = _input.Scroll;
                    if (wheel != 0f) _gap = Mathf.Clamp(_gap + wheel * setDownStep, minSetDownGap, maxSetDownGap);
                }
                UpdateFootprint();
                Hint = "E  Set down     Click  Throw     Wheel  Nearer / farther     Hold R  Turn";
                Why = null;
                return;
            }
            ShowFootprint(false);
            if (!Listening) { Hint = Why = null; _target = null; return; }
            _target = Aim(out string why);
            Hint = _target != null ? "E  Grab" : null;
            Why = why;
        }

        private void FixedUpdate()
        {
            if (!Holding || _view == null) return;
            Vector3 target;
            if (_phase == Phase.Lowering)
            {
                float e = Ease((Time.time - _t0) / Mathf.Max(0.01f, lowerTime));
                target = Vector3.Lerp(_lowerFrom, _lowerTo, e);
                _hold.Turn = Quaternion.Slerp(_turnFrom, _turnTo, e);
            }
            else
            {
                target = CarrySpot();
                if (_phase == Phase.Reaching) target = Vector3.Lerp(_reachFrom, target, Ease((Time.time - _t0) / Mathf.Max(0.01f, reachTime)));
            }
            var t = new SpringHold.Tuning { stiffness = holdStiffness, maxSpeed = maxHoldSpeed, breakDistance = breakDistance };
            if (!_hold.Drive(target, Heading, Scale, t, Time.fixedDeltaTime)) LetGo(fadeHands: true);   // slipped: it falls
        }

        // ---------- where it is carried and set down ----------

        /// <summary>Where a carried prop's middle goes: in front of the chest at carry height (higher if it would touch the
        /// ground), its near side a gap ahead of the body, allowing for its own depth so it never sits inside the body.</summary>
        private Vector3 CarrySpot()
        {
            float s = Scale;
            Bounds b = SpringHold.BoundsOf(_hold.Held);
            Vector3 fwd = Heading * Vector3.forward;
            float y = Mathf.Max(carryHeight * s, b.extents.y + 0.05f * s);
            return transform.position + Vector3.up * y + fwd * (Radius + carryGap * s + HalfDepth(b, fwd));
        }

        private static float HalfDepth(Bounds b, Vector3 fwd) => Mathf.Abs(fwd.x) * b.extents.x + Mathf.Abs(fwd.z) * b.extents.z;

        /// <summary>Where E would set the held prop down: its middle once resting on whatever is under the spot a gap ahead of
        /// the body. Also gives the floor's height and the prop's box now.</summary>
        private Vector3 SetDownSpot(out float floor, out Bounds b)
        {
            float s = Scale;
            var rb = _hold.Held;
            b = SpringHold.BoundsOf(rb);
            Vector3 fwd = Heading * Vector3.forward;
            Vector3 spot = transform.position + fwd * (Radius + _gap * s + HalfDepth(b, fwd));
            float top = Mathf.Max(b.max.y, transform.position.y + chestHeight * s) + 0.05f * s;
            floor = transform.position.y;
            int n = Physics.RaycastNonAlloc(new Vector3(spot.x, top, spot.z), Vector3.down, _hits, top - transform.position.y + 200f * s, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.transform.IsChildOf(rb.transform) || h.transform.IsChildOf(transform)) continue;
                if (h.distance < nearest) { nearest = h.distance; floor = h.point.y; }
            }
            return new Vector3(spot.x, floor + (b.center.y - b.min.y), spot.z);
        }

        /// <summary>E or C while holding: the prop goes down onto the footprint over a moment, levelling as it goes, then is
        /// set down exactly. Pressed again on the way, it is set down at once.</summary>
        private void StartSetDown()
        {
            if (!Holding) return;
            _busyFrame = Time.frameCount;
            if (_phase == Phase.Lowering || lowerTime <= 0f) { SetDown(); return; }
            _lowerTo = SetDownSpot(out _lowerFloor, out Bounds b);
            _lowerFrom = b.center;
            Quaternion heading = Heading;
            _turnFrom = _hold.Turn;
            _turnTo = Quaternion.Inverse(heading) * Level(heading * _hold.Turn);
            _phase = Phase.Lowering;
            _t0 = Time.time;
        }

        private bool FindView()
        {
            if (_camera == null) _camera = FindFirstObjectByType<ShoulderCamera>();
            _view = _camera != null ? _camera.transform : null;
            return _view != null;
        }

        // ---------- what can be grabbed ----------

        /// <summary>The grabbable thing on the camera's aim, within reach of the hands and ahead of the player.</summary>
        private GameObject Aim(out string why)
        {
            why = null;
            float s = Scale;
            Vector3 chest = transform.position + Vector3.up * (chestHeight * s);
            float far = Vector3.Distance(_view.position, chest) + reach * s;
            int n = Physics.SphereCastNonAlloc(_view.position, 0.12f * s, _view.forward, _hits, far, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; RaycastHit best = default; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.distance <= 0f || h.transform.IsChildOf(transform)) continue;   // started inside it, or ourselves
                if (h.distance < nearest) { nearest = h.distance; best = h; found = true; }
            }
            if (!found) return null;
            Vector3 toHit = best.point - chest;
            if (toHit.magnitude > reach * s) return null;   // out of reach of the hands
            Vector3 ahead = Vector3.ProjectOnPlane(_view.forward, Vector3.up);
            if (Vector3.Dot(toHit, ahead) < -0.25f * s) return null;   // behind the player, between it and the camera
            return Grabbable(best.collider, out why);
        }

        private GameObject Grabbable(Collider col, out string why)
        {
            why = null;
            var use = col.GetComponentInParent<IInteractable>();
            if (use != null && !(use is Carried)) return null;   // it has its own E: a core, a switch, a seat, a weapon
            var carry = col.GetComponentInParent<Carryable>();
            var body = col.attachedRigidbody;
            GameObject root;
            if (carry != null)
            {
                if (carry.KeepInHand || carry.IsCarried) return null;   // the flashlight and the ruler go in the hand
                root = carry.gameObject;
            }
            else if (body != null && !body.isKinematic) root = body.gameObject;
            else return null;   // fixed furniture
            if (root.GetComponentInParent<ArcadeCar>() != null || root.GetComponentInParent<ToyCar>() != null || (body != null && ToyCar.OwnsBall(body))) return null;
            if (root.GetComponentInParent<PlayerMotor>() != null) return null;
            var own = root.GetComponent<Rigidbody>();
            if (own != null && !own.isKinematic && own.mass > maxLiftMass) { why = "Too heavy"; return null; }
            var push = root.GetComponent<Pushable>();
            if (push != null && !push.PushableBy(_shifter != null ? _shifter.Size : ShiftSize.Kid)) { why = "Too heavy"; return null; }
            Bounds b = SolidBounds(root);
            if (Mathf.Max(b.size.x, b.size.y, b.size.z) > maxLiftSize * Scale) { why = "Too big"; return null; }
            return root;
        }

        private static Bounds SolidBounds(GameObject root)
        {
            Bounds b = new Bounds(root.transform.position, Vector3.zero); bool any = false;
            foreach (var c in root.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || !c.enabled) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            return b;
        }

        // ---------- grab, set down, throw ----------

        private void Grab(GameObject root)
        {
            // A thrown thing still flying is caught again: take over the restore it was waiting for.
            var settling = root.GetComponent<GrabSettle>();
            Rigidbody rb;
            if (settling != null) { _was = settling.TakeOver(); rb = root.GetComponent<Rigidbody>(); }
            else rb = GrabSettle.Original.Prepare(root, out _was);
            if (rb == null) { _was = null; return; }
            _hold.Grab(rb, Heading, _capsule);
            _heldColliders = SolidCollidersOf(rb);
            // Reach: it travels from where it lies into the arms while the hands come to it.
            _reachFrom = SpringHold.BoundsOf(rb).center;
            _phase = reachTime > 0f ? Phase.Reaching : Phase.Holding;
            _t0 = Time.time;
            _gap = setDownGap;
            _releasedAt = float.NegativeInfinity;
            _heldCarry = root.GetComponent<Carryable>();
            if (_heldCarry != null) _heldCarry.SetCarried(true);
            _target = null;
            _busyFrame = Time.frameCount;
            GameEvents.RaisePickedUp(rb.transform);
        }

        /// <summary>Levelled and lowered straight onto what is under it, then left still as it was before the grab.</summary>
        private void SetDown()
        {
            var was = _was; var carry = _heldCarry; _was = null; _heldCarry = null;
            _phase = Phase.None;
            var rb = _hold.Release();
            ShowFootprint(false);
            _busyFrame = Time.frameCount;
            if (carry != null) carry.SetCarried(false);
            if (rb == null) { FreezeHands(null); return; }
            GameEvents.RaiseSetDown(rb.transform);
            Quaternion level = Level(rb.rotation);
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.rotation = level; rb.transform.rotation = level;
            Physics.SyncTransforms();
            if (Floor(rb.gameObject, out float floor, out Bounds b))
            {
                Vector3 p = rb.transform.position + Vector3.up * (floor - b.min.y);
                rb.position = p; rb.transform.position = p;
                Physics.SyncTransforms();
            }
            FreezeHands(rb);   // before the restore: its colliders are still the convex ones the hands were on
            if (was != null) was.Restore(rb);
        }

        private void OnSetDown() { if (Holding && Listening) StartSetDown(); }

        private void OnThrow()
        {
            if (!Holding || !Listening || Cursor.lockState != CursorLockMode.Locked) return;
            var was = _was; var carry = _heldCarry; _was = null; _heldCarry = null;
            _phase = Phase.None;
            var rb = _hold.Release();
            ShowFootprint(false);
            _busyFrame = Time.frameCount;
            if (carry != null) carry.SetCarried(false);
            if (rb == null) { FreezeHands(null); return; }
            FreezeHands(rb);
            float massFactor = Mathf.Clamp(throwFullSpeedMass / Mathf.Max(0.01f, rb.mass), 0.25f, 1f);
            rb.linearVelocity = _view.forward * (throwSpeed * Scale * massFactor);
            rb.angularVelocity = Random.insideUnitSphere * 4f;
            GrabSettle.After(rb, was);   // still again (or as it was) once it lands
        }

        /// <summary>Lets go where it is (a shift, a mode change, a slip, a seat): it falls and settles back as it was. The hands
        /// come away over a moment, or at once (<paramref name="fadeHands"/> false: a shift resizes the body under them).</summary>
        private void LetGo(bool fadeHands)
        {
            var was = _was; var carry = _heldCarry; _was = null; _heldCarry = null;
            _phase = Phase.None;
            var rb = _hold.Release();
            ShowFootprint(false);
            if (carry != null) carry.SetCarried(false);
            if (rb != null && fadeHands) FreezeHands(rb); else { _releasedAt = float.NegativeInfinity; _heldColliders = System.Array.Empty<Collider>(); }
            if (rb != null) GrabSettle.After(rb, was);
        }

        /// <summary>Its heading kept, its tilt dropped: a set-down thing sits upright.</summary>
        private static Quaternion Level(Quaternion r)
        {
            Vector3 forward = Vector3.ProjectOnPlane(r * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(r * Vector3.up, Vector3.up);
            return forward.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        /// <summary>The top of whatever is straight under the thing (not itself, not the player).</summary>
        private bool Floor(GameObject go, out float floor, out Bounds b)
        {
            b = SolidBounds(go);
            floor = 0f;
            Vector3 from = new Vector3(b.center.x, b.max.y + 0.05f * Scale, b.center.z);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, _hits, b.size.y + 200f * Scale, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (h.transform.IsChildOf(go.transform) || h.transform.IsChildOf(transform)) continue;
                if (h.distance < nearest) { nearest = h.distance; floor = h.point.y; found = true; }
            }
            return found;
        }

        // ---------- the footprint ----------

        /// <summary>The footprint marks where E sets the prop down: a short way ahead of the body (or, while it goes down, the
        /// spot it is going to).</summary>
        private void UpdateFootprint()
        {
            var rb = _hold.Held;
            if (rb == null) { ShowFootprint(false); return; }
            if (!MakeFootprint()) return;
            Bounds b = SpringHold.BoundsOf(rb);
            Vector3 spot; float floor;
            if (_phase == Phase.Lowering) { spot = _lowerTo; floor = _lowerFloor; }
            else spot = SetDownSpot(out floor, out b);
            _footprint.position = new Vector3(spot.x, floor + 0.02f * Scale, spot.z);
            _footprint.rotation = Quaternion.Euler(90f, 0f, 0f);
            _footprint.localScale = new Vector3(Mathf.Max(0.05f, b.size.x * 1.05f), Mathf.Max(0.05f, b.size.z * 1.05f), 1f);
            _footprintRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, footprintColour);
            _footprintRenderer.SetPropertyBlock(_block);
            ShowFootprint(true);
        }

        /// <summary>A flat quad in Build Mode's see-through ghost material (from the catalogue, loaded at run time).</summary>
        private bool MakeFootprint()
        {
            if (_footprint != null) return true;
            var catalog = BuildCatalog.Load();
            var mat = catalog != null ? catalog.GhostMaterial : null;
            if (mat == null) return false;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "GrabFootprint";
            Destroy(quad.GetComponent<Collider>());
            _footprintRenderer = quad.GetComponent<Renderer>();
            _footprintRenderer.sharedMaterial = mat;
            _footprintRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _footprintRenderer.receiveShadows = false;
            _block = new MaterialPropertyBlock();
            _footprint = quad.transform;
            quad.SetActive(false);
            return true;
        }

        private void ShowFootprint(bool on)
        {
            if (_footprint != null && _footprint.gameObject.activeSelf != on) _footprint.gameObject.SetActive(on);
        }

        // ---------- the hands (CarryPose asks) ----------

        /// <summary>How much the arms are around the prop: easing in with the reach, full while held and going down, easing out after.</summary>
        public float CarryWeight
        {
            get
            {
                if (!isActiveAndEnabled) return 0f;
                if (Holding) return _phase == Phase.Reaching ? Ease((Time.time - _t0) / Mathf.Max(0.01f, reachTime)) : 1f;
                float k = (Time.time - _releasedAt) / Mathf.Max(0.01f, handsFade);
                return k < 1f ? 1f - Ease(k) : 0f;
            }
        }

        public bool CarryGoals(out HandGoal right, out HandGoal left)
        {
            if (Holding) { HandsOn(_hold.Held, out right, out left); return true; }
            if (Time.time - _releasedAt < handsFade)
            {
                right = FromBody(_lastRight); left = FromBody(_lastLeft);
                return true;
            }
            right = left = default;
            return false;
        }

        /// <summary>Palms on the prop's two sides (as the body sees them), a little back from its middle, fingers ahead.</summary>
        private void HandsOn(Rigidbody rb, out HandGoal right, out HandGoal left)
        {
            Bounds b = SpringHold.BoundsOf(rb);
            Vector3 fwd = Heading * Vector3.forward, side = Heading * Vector3.right;
            float far = b.extents.magnitude + Scale;
            Vector3 back = -fwd * (handsBack * HalfDepth(b, fwd));
            right = new HandGoal { palm = Closest(b.center + side * far + back, b), fingers = fwd, normal = -side };
            left = new HandGoal { palm = Closest(b.center - side * far + back, b), fingers = fwd, normal = side };
        }

        /// <summary>The nearest point of the held prop's solid colliders to <paramref name="probe"/> (a box for a mesh that is not convex).</summary>
        private Vector3 Closest(Vector3 probe, Bounds fallback)
        {
            Vector3 best = fallback.ClosestPoint(probe);
            float bestSq = float.MaxValue;
            foreach (var c in _heldColliders)
            {
                if (c == null || !c.enabled) continue;
                Vector3 p = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(probe) : c.ClosestPoint(probe);
                float d = (p - probe).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            return best;
        }

        private static Collider[] SolidCollidersOf(Rigidbody rb)
        {
            var list = new System.Collections.Generic.List<Collider>();
            foreach (var c in rb.GetComponentsInChildren<Collider>())
                if (!c.isTrigger && c.attachedRigidbody == rb) list.Add(c);
            return list.ToArray();
        }

        /// <summary>Remembers where the hands were on <paramref name="rb"/> (in the body's space) so they come away from there.</summary>
        private void FreezeHands(Rigidbody rb)
        {
            if (handsFade <= 0f || rb == null) { _releasedAt = float.NegativeInfinity; _heldColliders = System.Array.Empty<Collider>(); return; }
            HandsOn(rb, out HandGoal right, out HandGoal left);
            _lastRight = ToBody(right); _lastLeft = ToBody(left);
            _releasedAt = Time.time;
            _heldColliders = System.Array.Empty<Collider>();
        }

        private HandGoal ToBody(HandGoal g) => new HandGoal
        {
            palm = transform.InverseTransformPoint(g.palm), fingers = transform.InverseTransformDirection(g.fingers), normal = transform.InverseTransformDirection(g.normal)
        };

        private HandGoal FromBody(HandGoal g) => new HandGoal
        {
            palm = transform.TransformPoint(g.palm), fingers = transform.TransformDirection(g.fingers), normal = transform.TransformDirection(g.normal)
        };

        private static float Ease(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
    }
}
