using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Skyrim-style grab and move in third person (D-0033). Look at a prop and press E: it floats where the camera aims,
    /// a little ahead of the player, on the same spring as first person's hands (SpringHold), so it still bumps into
    /// things. The wheel moves it nearer or farther, R or the right mouse button with the mouse turns it, E (or C) sets
    /// it down: levelled, lowered straight onto the footprint shown under it, and left still. A click throws it.
    /// It grabs loose physics props and carryables (pillows, the bear, boxes). The flashlight and the ruler stay
    /// hand-held (Carrier), and anything with its own E (cores, switches, seats, weapons) keeps it. Sizes follow the
    /// Shifter: the kid lifts a pillow, the toy only toy-sized things. The Interactor asks it first on E.
    /// </summary>
    public sealed class ThirdPersonGrab : MonoBehaviour
    {
        [Header("Reach, in units at toy size (scaled with size)")]
        [SerializeField, Tooltip("How far from the chest a thing can be grabbed.")] private float reach = 3.5f;
        [SerializeField, Tooltip("Height of the chest above the feet.")] private float chestHeight = 1.1f;
        [SerializeField, Tooltip("Where a held thing floats along the aim, from the chest.")] private float holdDistance = 2.2f;
        [SerializeField] private float minHoldDistance = 1.2f, maxHoldDistance = 6f;
        [SerializeField, Tooltip("How far one wheel notch moves the held thing.")] private float wheelStep = 0.4f;
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

        /// <summary>What the prompt shows while holding or aiming at something grabbable; null otherwise.</summary>
        public string Hint { get; private set; }
        /// <summary>Why the thing under the aim can't be grabbed ("Too big"), for when nothing else is in reach.</summary>
        public string Why { get; private set; }
        public bool Holding => _hold.Held != null;
        public Transform HeldTransform => _hold.Held != null ? _hold.Held.transform : null;
        /// <summary>True while holding, and on the frame something was thrown: that click is not a weapon's.</summary>
        public bool BusyThisFrame => Holding || _busyFrame == Time.frameCount;

        private float Scale => _shifter != null ? _shifter.Scale : 1f;
        private Quaternion Heading => Quaternion.Euler(0f, _view != null ? _view.eulerAngles.y : transform.eulerAngles.y, 0f);
        /// <summary>May hold at all: playing on foot (not seated, not in first person or Build Mode), hands free.</summary>
        private bool Allowed => GameMode.Current == GameMode.Mode.Play && _interactor != null && _interactor.isActiveAndEnabled
                                && (_carrier == null || _carrier.Held == null);
        /// <summary>Takes keys: allowed, and nothing else has them (the console, a paused menu). Holding carries on meanwhile.</summary>
        private bool Listening => Allowed && _input != null && _input.enabled && Time.timeScale > 0f;

        private void Awake()
        {
            _input = GetComponent<PlayerInput>(); _shifter = GetComponent<Shifter>(); _interactor = GetComponent<Interactor>();
            _carrier = GetComponent<Carrier>(); _capsule = GetComponent<CharacterController>();
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
            LetGo();
            Hint = Why = null; _target = null;
            ShowFootprint(false);
        }

        private void OnDestroy() { if (_footprint != null) Destroy(_footprint.gameObject); }

        private void OnShiftStarted(ShiftSize _) => LetGo();                        // the reach and what fits change
        private void OnModeChanged(GameMode.Mode from, GameMode.Mode to) { if (to != GameMode.Mode.Play) LetGo(); }

        /// <summary>E, asked by the Interactor before its own target: sets down what is held, or grabs what is aimed at.</summary>
        public bool TryUse()
        {
            if (!Listening) return false;
            if (Holding) { SetDown(); return true; }
            if (_target == null) return false;
            Grab(_target);
            return true;
        }

        // ---------- per frame ----------

        private void Update()
        {
            if (_hold.HeldWasDestroyed) { _hold.Release(); _was = null; _heldCarry = null; }
            if (!Allowed || !FindView())
            {
                if (Holding) LetGo();
                Hint = Why = null; _target = null;
                ShowFootprint(false);
                return;
            }
            float s = Scale;
            if (Holding)
            {
                if (Listening)
                {
                    // Turning with the mouse: the look goes to the held thing, so the camera stays put.
                    if (_input.RotateHeld) _hold.RotateBy(_input.ConsumeLook(rotateSensitivity, 90f, Time.unscaledDeltaTime), Heading, _view.right);
                    float wheel = _input.Scroll;
                    if (wheel != 0f) _hold.Distance = Mathf.Clamp(_hold.Distance + wheel * wheelStep * s, minHoldDistance * s, maxHoldDistance * s);
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
            float s = Scale;
            Vector3 chest = transform.position + Vector3.up * (chestHeight * s);
            var t = new SpringHold.Tuning { stiffness = holdStiffness, maxSpeed = maxHoldSpeed, breakDistance = breakDistance };
            if (!_hold.Drive(chest + _view.forward * _hold.Distance, Heading, s, t, Time.fixedDeltaTime)) LetGo();   // slipped: it falls
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
            float s = Scale;
            _hold.Grab(rb, Heading, _capsule);
            Bounds b = SpringHold.BoundsOf(rb);
            float radius = _capsule != null ? _capsule.radius * s : 0.3f * s;
            _hold.Distance = Mathf.Clamp(Mathf.Max(holdDistance * s, b.extents.magnitude + radius + 0.15f * s), minHoldDistance * s, maxHoldDistance * s);
            _heldCarry = root.GetComponent<Carryable>();
            if (_heldCarry != null) _heldCarry.SetCarried(true);
            _target = null;
            _busyFrame = Time.frameCount;
        }

        /// <summary>Levelled and lowered straight onto what is under it, then left still as it was before the grab.</summary>
        private void SetDown()
        {
            var was = _was; var carry = _heldCarry; _was = null; _heldCarry = null;
            var rb = _hold.Release();
            ShowFootprint(false);
            _busyFrame = Time.frameCount;
            if (carry != null) carry.SetCarried(false);
            if (rb == null) return;
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
            if (was != null) was.Restore(rb);
        }

        private void OnSetDown() { if (Holding && Listening) SetDown(); }

        private void OnThrow()
        {
            if (!Holding || !Listening || Cursor.lockState != CursorLockMode.Locked) return;
            var was = _was; var carry = _heldCarry; _was = null; _heldCarry = null;
            var rb = _hold.Release();
            ShowFootprint(false);
            _busyFrame = Time.frameCount;
            if (carry != null) carry.SetCarried(false);
            if (rb == null) return;
            float massFactor = Mathf.Clamp(throwFullSpeedMass / Mathf.Max(0.01f, rb.mass), 0.25f, 1f);
            rb.linearVelocity = _view.forward * (throwSpeed * Scale * massFactor);
            rb.angularVelocity = Random.insideUnitSphere * 4f;
            GrabSettle.After(rb, was);   // still again (or as it was) once it lands
        }

        /// <summary>Lets go where it is (a shift, a mode change, a slip): it falls and settles back as it was.</summary>
        private void LetGo()
        {
            var was = _was; var carry = _heldCarry; _was = null; _heldCarry = null;
            var rb = _hold.Release();
            ShowFootprint(false);
            if (carry != null) carry.SetCarried(false);
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

        private void UpdateFootprint()
        {
            var rb = _hold.Held;
            if (rb == null || !Floor(rb.gameObject, out float floor, out Bounds b)) { ShowFootprint(false); return; }
            if (!MakeFootprint()) return;
            _footprint.position = new Vector3(b.center.x, floor + 0.02f * Scale, b.center.z);
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
    }
}
