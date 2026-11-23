using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A heavy thing taken hold of and steered, as in Zelda (D-0156, his "a Zelda-like grab onto the item and we can
    /// rotate it"): E takes hold where the player stands; left / right turn it (about its middle, or about its far end),
    /// forward / back push and pull it; E or Jump lets go. The player is held at their grip, facing it, and walks round
    /// with it. Its own physics body moves it (by speed, so walls stop it). The giant flashlight in the toy box has one:
    /// steer its beam onto the core. Who may grab it, how fast it turns and slides, and its pivot are set on it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Grabbable : MonoBehaviour, IInteractable
    {
        public enum Pivot { Middle, FarEnd }

        [SerializeField] private string prompt = "Grab";
        [SerializeField, Tooltip("A toy may grab it.")] private bool toyCan = true;
        [SerializeField, Tooltip("A kid may grab it.")] private bool kidCan;
        [SerializeField, Tooltip("Left / right turn it.")] private bool canTurn = true;
        [SerializeField, Tooltip("Forward / back push and pull it.")] private bool canPush = true;
        [SerializeField, Min(0f), Tooltip("Degrees a second at full stick.")] private float turnSpeed = 45f;
        [SerializeField, Min(0f), Tooltip("Push / pull speed at full stick, in the grabber's own heights a second.")] private float pushSpeed = 0.8f;
        [SerializeField, Tooltip("Middle: it turns about its own middle, the grabber walking round with its end. Far End: it swings about the end away from the grabber, like a log dragged round.")] private Pivot pivot = Pivot.Middle;
        [SerializeField, Min(0.1f), Tooltip("How quickly it starts and stops (higher = snappier; low feels heavy).")] private float ease = 5f;
        [SerializeField, Min(1f), Tooltip("It lets go if the grabber ends up this many of its own heights from the grip.")] private float slipAt = 2f;

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

        /// <summary>True while someone holds it.</summary>
        public bool Grabbed => _who != null;

        private void Awake() => _body = GetComponent<Rigidbody>();

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
            _grip = transform.InverseTransformPoint(_who.transform.position);
            Vector3 middle = transform.InverseTransformPoint(_body.worldCenterOfMass);
            Vector3 away = middle - _grip; away.y = 0f;
            Bounds local = LocalBox();
            float half = Mathf.Abs(away.x) > Mathf.Abs(away.z) ? local.extents.x : local.extents.z;
            _far = middle + (away.sqrMagnitude > 1e-6f ? away.normalized * half : Vector3.zero);
            _wasKinematic = _body.isKinematic;
            _body.isKinematic = false;
            _turn = _push = 0f;
            if (_input != null) _input.Jump += LetGo;
            if (_shifter != null) _shifter.ShiftStarted += OnShift;
        }

        private void OnShift(ShiftSize _) => LetGo();

        /// <summary>Lets go: it stays where it was left.</summary>
        public void LetGo()
        {
            if (_who == null) return;
            if (_input != null) _input.Jump -= LetGo;
            if (_shifter != null) _shifter.ShiftStarted -= OnShift;
            if (_motor != null) _motor.HoldAt = null;
            Held.Remove(_who);
            _who = null;
            _body.isKinematic = _wasKinematic;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        private void OnDisable() => LetGo();

        private float GrabberScale => _shifter != null ? _shifter.Scale : (_who != null ? _who.transform.lossyScale.y : 1f);

        private void FixedUpdate()
        {
            if (_who == null) return;
            Vector2 stick = _input != null && Time.timeScale > 0f ? _input.Move : Vector2.zero;
            float k = 1f - Mathf.Exp(-ease * Time.fixedDeltaTime);
            _turn = Mathf.Lerp(_turn, canTurn ? stick.x : 0f, k);
            _push = Mathf.Lerp(_push, canPush ? stick.y : 0f, k);

            // Turn about the chosen pivot; push along the grabber's line to it, flat.
            Vector3 grabber = _who.transform.position;
            Vector3 along = _body.worldCenterOfMass - grabber; along.y = 0f;
            along = along.sqrMagnitude > 1e-6f ? along.normalized : _who.transform.forward;
            float spin = _turn * turnSpeed * Mathf.Deg2Rad;   // + = to the right (clockwise seen from above)
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
            // The grabber is held at their grip, facing the thing, and walks round with it.
            Vector3 target = transform.TransformPoint(_grip);
            Vector3 gap = target - _who.transform.position; gap.y = 0f;
            float scale = GrabberScale;
            if (gap.magnitude > slipAt * 1.8f * scale) { LetGo(); return; }   // pulled off it (a wall, a fall)
            _motor.HoldAt = target;
            Vector3 look = _body.worldCenterOfMass - _who.transform.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-6f) _motor.HoldYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
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
