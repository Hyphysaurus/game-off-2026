using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A heavy thing taken hold of and steered, as in Zelda (D-0156, his "a Zelda-like grab onto the item and we can
    /// rotate it"): E takes hold where the player stands; it turns to face where the player looks (Steer View, D-0157:
    /// left / right on the stick did not get through and were hard to aim with) or with left / right (Steer Stick),
    /// forward / back push and pull it; E or Jump lets go. The player is held at their grip, facing it, and walks round
    /// with it. Its own physics body moves it (by speed, so walls stop it). The giant flashlight in the toy box has one:
    /// steer its beam onto the core. Who may grab it, how fast it turns and slides, and its pivot are set on it.
    /// Both palms go on it through CarryPose (D-0164: on a rim's top edge, else on its face), a tool in hand is put
    /// away and the gun hidden while held. Anchored (a turntable): the grabber walks round with its rim, facing the hub,
    /// and it stops turning when they are blocked (a wall, a block) instead of dragging them off it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Grabbable : MonoBehaviour, IInteractable, ICarryHands
    {
        public enum Pivot { Middle, FarEnd }
        public enum Steer { View, Stick }

        [SerializeField] private string prompt = "Grab";
        [SerializeField, Tooltip("A toy may grab it.")] private bool toyCan = true;
        [SerializeField, Tooltip("A kid may grab it.")] private bool kidCan;
        [SerializeField, Tooltip("View: it turns to point where the player looks (a giant flashlight: look at the target). Stick: left / right turn it.")] private Steer steer = Steer.View;
        [SerializeField, Tooltip("Optional. The way it points (a flashlight's Beam). Its own forward if empty, or a Flashlight's beam when it has one.")] private Transform aim;
        [SerializeField, Tooltip("It may be turned.")] private bool canTurn = true;
        [SerializeField, Tooltip("It only turns in place about its own pivot, on a fixed hub (a turntable, a valve, D-0158): never pushed, never knocked about; what rides on it (its children) turns with it. Its body stays kinematic.")] private bool anchored;
        [SerializeField, Tooltip("Forward / back push and pull it.")] private bool canPush = true;
        [SerializeField, Min(0f), Tooltip("Degrees a second at full stick.")] private float turnSpeed = 45f;
        [SerializeField, Min(0f), Tooltip("Push / pull speed at full stick, in the grabber's own heights a second.")] private float pushSpeed = 0.8f;
        [SerializeField, Tooltip("Middle: it turns about its own middle, the grabber walking round with its end. Far End: it swings about the end away from the grabber, like a log dragged round.")] private Pivot pivot = Pivot.Middle;
        [SerializeField, Min(0.1f), Tooltip("How quickly it starts and stops (higher = snappier; low feels heavy).")] private float ease = 5f;
        [SerializeField, Min(1f), Tooltip("It lets go if the grabber ends up this many of its own heights from the grip.")] private float slipAt = 2f;
        [Header("Hands on it (D-0164)")]
        [SerializeField, Tooltip("Both palms go on it while held (CarryPose).")] private bool handsOn = true;
        [SerializeField, Range(0.05f, 0.5f), Tooltip("How far apart the palms are, as a share of the grabber's height.")] private float handsApart = 0.2f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the hands take to go on and come off.")] private float handsFade = 0.25f;
        [SerializeField, Range(0.02f, 1f), Tooltip("Anchored: when the grabber trails their grip by more than this share of their height (blocked), it stops turning that way.")] private float blockAt = 0.15f;

        private static readonly Dictionary<GameObject, Grabbable> Held = new Dictionary<GameObject, Grabbable>();

        private Rigidbody _body;
        private GameObject _who;
        private PlayerInput _input;
        private PlayerMotor _motor;
        private Shifter _shifter;
        private Vector3 _grip;          // the grabber's place, in this thing's own space
        private Vector3 _far;           // the far end from the grip, in its own space (the Far End pivot)
        private float _turn, _push;     // eased
        private bool _wasKinematic;
        private Collider[] _mine;
        private CharacterController _cc;
        private CarryPose _pose;
        private bool _hasHands;
        private Vector3 _palmR, _palmL, _normal, _fingers;   // in its own space: the palms ride with it
        private float _grabbedAt, _releasedAt = float.NegativeInfinity;
        private float _lag;             // how far the grabber trails their grip, world units (blocked when large)

        /// <summary>True while someone holds it.</summary>
        public bool Grabbed => _who != null;

        private void Awake() => _body = GetComponent<Rigidbody>();

        /// <summary>True while <paramref name="who"/> holds one (their hands are full: the gun is hidden).</summary>
        public static bool Holds(GameObject who) => who != null && Held.ContainsKey(who);

        /// <summary>Lets go of whatever <paramref name="who"/> holds. False when they hold nothing.</summary>
        public static bool Release(GameObject who)
        {
            if (who == null || !Held.TryGetValue(who, out Grabbable g) || g == null) return false;
            g.LetGo();
            return true;
        }

        public string PromptFor(Interactor who)
        {
            if (Grabbed) return "";
            Shifter s = who.Shifter;
            bool kid = s != null && s.Size == ShiftSize.Kid;
            return (kid ? kidCan : toyCan) ? prompt : "";
        }

        public void Interact(Interactor who)
        {
            if (Grabbed || string.IsNullOrEmpty(PromptFor(who))) return;
            _who = who.gameObject;
            _input = _who.GetComponent<PlayerInput>();
            _motor = _who.GetComponent<PlayerMotor>();
            _shifter = _who.GetComponent<Shifter>();
            Held[_who] = this;
            // Grabber and thing do not push each other while held (they crept apart together, D-0157).
            _cc = _who.GetComponent<CharacterController>();
            _mine = GetComponentsInChildren<Collider>();
            if (_cc != null) foreach (var c in _mine) if (c != null) Physics.IgnoreCollision(_cc, c, true);
            _grip = transform.InverseTransformPoint(_who.transform.position);
            if (aim == null && TryGetComponent(out Flashlight torch) && torch.Beam != null) aim = torch.Beam.transform;
            Vector3 middle = transform.InverseTransformPoint(_body.worldCenterOfMass);
            Vector3 away = middle - _grip; away.y = 0f;
            Bounds local = LocalBox();
            float half = Mathf.Abs(away.x) > Mathf.Abs(away.z) ? local.extents.x : local.extents.z;
            _far = middle + (away.sqrMagnitude > 1e-6f ? away.normalized * half : Vector3.zero);
            _wasKinematic = _body.isKinematic;
            if (!anchored) _body.isKinematic = false;
            _turn = _push = 0f;
            if (_input != null) _input.Jump += LetGo;
            if (_shifter != null) _shifter.ShiftStarted += OnShift;
            _lag = 0f;
            // The hands (D-0164): a tool in hand goes to its slot (the gun hides by itself while held), both palms on it.
            if (_who.TryGetComponent(out Carrier carrier)) carrier.Stow();
            _grabbedAt = Time.time;
            _hasHands = handsOn && FindHands();
            CarryPose pose = CarryPose.On(_who.GetComponentInChildren<Animator>());
            if (pose != _pose && _pose != null) _pose.Remove(this);
            _pose = pose;
            if (_pose != null) _pose.Add(this);
        }

        private void OnShift(ShiftSize _) => LetGo();

        /// <summary>Lets go: it stays where it was left.</summary>
        public void LetGo()
        {
            if (_who == null) return;
            if (_input != null) _input.Jump -= LetGo;
            if (_shifter != null) _shifter.ShiftStarted -= OnShift;
            if (_motor != null) _motor.HoldAt = null;
            if (_cc != null && _mine != null) foreach (var c in _mine) if (c != null) Physics.IgnoreCollision(_cc, c, false);
            Held.Remove(_who);
            _who = null;
            _releasedAt = Time.time;
            _body.isKinematic = _wasKinematic;
            if (!_body.isKinematic) { _body.linearVelocity = Vector3.zero; _body.angularVelocity = Vector3.zero; }
        }

        private void OnDisable() => LetGo();

        private void OnDestroy() { if (_pose != null) _pose.Remove(this); }

        private float GrabberScale => _shifter != null ? _shifter.Scale : (_who != null ? _who.transform.lossyScale.y : 1f);

        private void FixedUpdate()
        {
            if (_who == null) return;
            Vector2 stick = _input != null && Time.timeScale > 0f ? _input.Move : Vector2.zero;
            float k = 1f - Mathf.Exp(-ease * Time.fixedDeltaTime);
            float want = 0f;
            if (canTurn && steer == Steer.Stick) want = stick.x;
            else if (canTurn)
            {
                // Point where the player looks: the turn that brings its aim round to the view, flat, heavier near the end.
                Camera view = PlayerSlot.ViewFor(_who.transform);
                Transform way = aim != null ? aim : transform;
                Vector3 now = Vector3.ProjectOnPlane(way.forward, Vector3.up), to = view != null ? Vector3.ProjectOnPlane(view.transform.forward, Vector3.up) : now;
                if (now.sqrMagnitude > 1e-6f && to.sqrMagnitude > 1e-6f)
                    want = Mathf.Clamp(Vector3.SignedAngle(now, to, Vector3.up) / 30f, -1f, 1f);   // full speed when 30 degrees or more off
            }
            _turn = Mathf.Lerp(_turn, want, k);
            _push = Mathf.Lerp(_push, canPush ? stick.y : 0f, k);

            // Turn about the chosen pivot; push along the grabber's line to it, flat.
            Vector3 grabber = _who.transform.position;
            Vector3 along = _body.worldCenterOfMass - grabber; along.y = 0f;
            along = along.sqrMagnitude > 1e-6f ? along.normalized : _who.transform.forward;
            float spin = _turn * turnSpeed * Mathf.Deg2Rad;   // + = to the right (clockwise seen from above)
            if (anchored)
            {
                // On its hub: only a turn about its own pivot, moved as a kinematic body (riders turn with it). The grabber
                // walks round with its rim; when they are blocked (trailing their grip), it does not turn on away from them.
                float step = spin * Mathf.Rad2Deg * Time.fixedDeltaTime;
                if (_lag > blockAt * Height && step != 0f)
                {
                    Vector3 hub = _body.position, grip = transform.TransformPoint(_grip), next = hub + Quaternion.AngleAxis(step, Vector3.up) * (grip - hub);
                    if (Flat(next - grabber) > Flat(grip - grabber)) { step = 0f; _turn = 0f; }
                }
                if (step != 0f) _body.MoveRotation(Quaternion.AngleAxis(step, Vector3.up) * _body.rotation);
                return;
            }
            Vector3 velocity = along * (_push * pushSpeed * GrabberScale * 1.8f);   // the toy's height is 1.8 of its units
            if (pivot == Pivot.FarEnd)
            {
                Vector3 fromPivot = _body.worldCenterOfMass - transform.TransformPoint(_far);
                velocity += Vector3.Cross(Vector3.up * spin, fromPivot);   // the middle moves round the far end
            }
            velocity.y = _body.linearVelocity.y;   // it still falls and rests on the floor
            _body.linearVelocity = velocity;
            _body.angularVelocity = new Vector3(0f, spin, 0f);
        }

        private void LateUpdate()
        {
            if (_who == null || _motor == null) return;
            // The grabber is held at their grip, facing the thing, and walks round with it. Anchored (a turntable, D-0164):
            // they side-step round with its rim, facing its hub, hands on it, so they are seen turning it (D-0161 had them
            // stand still; before that the camera swung round behind them, which read as it turning by itself: the camera
            // still stays put while holding, D-0160).
            Vector3 target = transform.TransformPoint(_grip);
            Vector3 gap = target - _who.transform.position; gap.y = 0f;
            _lag = gap.magnitude;
            if (_lag > slipAt * Height) { LetGo(); return; }   // pulled off it (a fall)
            _motor.HoldAt = target;
            Vector3 look = (anchored ? _body.position : _body.worldCenterOfMass) - _who.transform.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-6f) _motor.HoldYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
        }

        /// <summary>The grabber's height, world units (the toy's height is 1.8 of its units).</summary>
        private float Height => 1.8f * GrabberScale;

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        // ---------- the hands (CarryPose asks, D-0164) ----------

        /// <summary>Where both palms go: on its top edge in front of the grabber when that is between their knees and head
        /// (a turntable's rim), else on the face in front of them at waist to chest height. Kept in its own space, so the
        /// palms ride with it. False when nothing of it is in reach.</summary>
        private bool FindHands()
        {
            float h = Height;
            Vector3 feet = _who.transform.position;
            Vector3 toward = (anchored ? _body.position : _body.worldCenterOfMass) - feet; toward.y = 0f;
            if (toward.sqrMagnitude < 1e-6f) return false;
            toward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            if (!HandOn(feet + side * (0.5f * handsApart * h), toward, h, out Vector3 r, out Vector3 normal, out Vector3 fingers)) return false;
            if (!HandOn(feet - side * (0.5f * handsApart * h), toward, h, out Vector3 l, out _, out _)) return false;
            _palmR = transform.InverseTransformPoint(r); _palmL = transform.InverseTransformPoint(l);
            _normal = transform.InverseTransformDirection(normal); _fingers = transform.InverseTransformDirection(fingers);
            return true;
        }

        private bool HandOn(Vector3 feet, Vector3 toward, float h, out Vector3 palm, out Vector3 normal, out Vector3 fingers)
        {
            palm = normal = fingers = Vector3.zero;
            foreach (float up in new[] { 0.55f, 0.35f, 0.75f })   // waist first, then knee and chest height
            {
                if (!Cast(new Ray(feet + Vector3.up * (up * h), toward), 2f * h, out RaycastHit face)) continue;
                // Its top edge just past the face, if the top is in reach: palms down on it, fingers toward the middle.
                Vector3 over = face.point + toward * (0.06f * h);
                if (Cast(new Ray(new Vector3(over.x, feet.y + 1.05f * h, over.z), Vector3.down), 0.8f * h, out RaycastHit top) && top.point.y > feet.y + 0.3f * h)
                {
                    palm = top.point; normal = Vector3.down; fingers = toward;
                    return true;
                }
                palm = face.point; normal = toward; fingers = Vector3.up;   // the face: palms flat on it, fingers up
                return true;
            }
            return false;
        }

        /// <summary>The nearest hit of its own colliders along <paramref name="ray"/> (nothing else counts).</summary>
        private bool Cast(Ray ray, float distance, out RaycastHit best)
        {
            best = default;
            float bestDistance = float.MaxValue;
            foreach (var c in _mine)
                if (c != null && c.enabled && !c.isTrigger && c.Raycast(ray, out RaycastHit hit, distance) && hit.distance < bestDistance) { best = hit; bestDistance = hit.distance; }
            return bestDistance < float.MaxValue;
        }

        public float CarryWeight
        {
            get
            {
                if (!_hasHands || !isActiveAndEnabled) return 0f;
                float k = _who != null ? (Time.time - _grabbedAt) / handsFade : 1f - (Time.time - _releasedAt) / handsFade;
                k = Mathf.Clamp01(k);
                return k * k * (3f - 2f * k);
            }
        }

        public bool CarryGoals(out HandGoal right, out HandGoal left)
        {
            right = left = default;
            if (!_hasHands) return false;
            Vector3 normal = transform.TransformDirection(_normal), fingers = transform.TransformDirection(_fingers);
            right = new HandGoal { palm = transform.TransformPoint(_palmR), fingers = fingers, normal = normal };
            left = new HandGoal { palm = transform.TransformPoint(_palmL), fingers = fingers, normal = normal };
            return true;
        }

        /// <summary>Its meshes' box in its own space.</summary>
        private Bounds LocalBox()
        {
            var mf = GetComponentInChildren<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Held.Clear();
    }
}
