using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's hands for tools. A one-handed tool (the flashlight) sits in the right hand with its grip (Carryable.GripLocal,
    /// in the tool's own space) at the palm, aimed the way the arm reaches; the animator's Arms layer fades in so the arm comes
    /// up and the fingers close. A plank is carried flat across the front of the body in both hands, the hands under its two
    /// grips through CarryPose (D-0042). Picking up eases the thing from where it lies into the hands; putting down (Crouch)
    /// lowers it to its resting spot before letting go. Attack goes to the held thing. Never parents the thing, so the
    /// player's 35x scale does not touch it. A shift lets go at once, ahead of the body (D-0041): a toy cannot hold a kid's
    /// flashlight, and setting it down from a toy's hand boxed the toy in.
    /// </summary>
    public sealed class Carrier : MonoBehaviour, ICarryHands
    {
        [SerializeField, Tooltip("Found on this object if empty.")] private PlayerInput input;
        [SerializeField, Tooltip("Found on this object if empty. A shift lets go of what is held.")] private Shifter shifter;
        [SerializeField, Tooltip("Fallback hold point if the rig has no right hand bone.")] private Transform holdPoint;
        [SerializeField, Tooltip("Found in children if empty.")] private Animator animator;
        [SerializeField, Tooltip("Animator layer that poses the arm while holding a one-handed tool; faded in and out.")] private string armsLayer = "Arms";
        [SerializeField, Tooltip("Seconds to fade the arm pose in or out.")] private float armFade = 0.15f;
        [SerializeField, Tooltip("What a put-down thing may rest on.")] private LayerMask groundMask = ~0;
        [Header("Reach and lower (D-0042)")]
        [SerializeField, Tooltip("Seconds a picked-up thing takes from where it lies into the hands.")] private float reachTime = 0.25f;
        [SerializeField, Tooltip("Seconds a put-down thing takes from the hands to where it rests. Crouch again sets it down at once.")] private float lowerTime = 0.25f;
        [SerializeField, Tooltip("Seconds the hands take to come away from a two-handed thing after letting go.")] private float handsFade = 0.2f;
        [Header("Planks in both hands, units at toy size (scaled with size)")]
        [SerializeField, Tooltip("Height of the plank's middle above the feet.")] private float carryHeight = 0.8f;
        [SerializeField, Tooltip("Gap between the body and the plank's near edge.")] private float carryGap = 0.1f;

        private enum Phase { None, Reaching, Holding, Lowering }

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Transform _hand, _shoulder;
        private PlayerBody _wears;   // the body in use: its palm replaces the carryable's (measured for the old rig)
        private CharacterController _body;
        private int _layer = -1;
        private float _armWeight;
        private Phase _phase;
        private float _t0;
        private Vector3 _fromPos, _toPos;
        private Quaternion _fromRot, _toRot;
        private bool _twoHanded;
        private Vector3 _gripRight, _gripLeft, _flat;   // the thing's own space; a one-handed tool uses _gripRight
        private Quaternion _carryTurn;                   // a plank's turn against the body's heading while carried
        private HandGoal _lastRight, _lastLeft;          // in the body's space, for the hands coming away after a let-go
        private float _releasedAt = float.NegativeInfinity;

        public Carryable Held { get; private set; }

        private float Scale => shifter != null ? shifter.Scale : transform.lossyScale.y;
        private Quaternion Heading => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (shifter == null) shifter = GetComponent<Shifter>();
            _body = GetComponent<CharacterController>();
            _wears = GetComponent<PlayerBody>();
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
                var pose = CarryPose.On(animator);   // both hands on a plank (D-0042); added if the body lacks it
                if (pose != null) pose.Add(this);
            }
        }

        private void OnEnable()
        {
            if (input != null) { input.UseHeld += OnUse; input.Drop += PutDown; }
            if (shifter != null) shifter.ShiftStarted += OnShiftStarted;
        }

        private void OnDisable()
        {
            if (input != null) { input.UseHeld -= OnUse; input.Drop -= PutDown; }
            if (shifter != null) shifter.ShiftStarted -= OnShiftStarted;
            // Parked mid put-down (first person puts down, then parks the hands): it rests where it was going, now.
            if (Held != null && _phase == Phase.Lowering) Finish(fadeHands: false);
            _releasedAt = float.NegativeInfinity;
        }

        public void PickUp(Carryable thing)
        {
            if (thing == null || Held != null) return;
            KeepApart.Cancel(thing.gameObject);
            Held = thing;
            foreach (var col in Held.GetComponentsInChildren<Collider>()) col.enabled = false;   // out of the physics while in hand
            Held.SetCarried(true);
            _twoHanded = thing.TwoHanded;
            if (_twoHanded) SetUpTwoHands(thing);
            else _gripRight = thing.GripLocal;
            // Reach: it travels from where it lies into the hands.
            _fromPos = thing.transform.position;
            _fromRot = thing.transform.rotation;
            _t0 = Time.time;
            _phase = reachTime > 0f ? Phase.Reaching : Phase.Holding;
            _releasedAt = float.NegativeInfinity;
        }

        /// <summary>
        /// Lowers the held thing to rest on whatever is under the hand (a plank: ahead of the body, clear of it), facing the way
        /// the player faces, and lets go when it gets there. Pressed again while it is going down, it rests at once.
        /// </summary>
        public void PutDown()
        {
            if (Held == null) return;
            if (_phase == Phase.Lowering) { Finish(fadeHands: true); return; }
            Transform t = Held.transform;
            Vector3 p = t.position; Quaternion r = t.rotation;
            Vector3 from = _twoHanded ? ClearSpot(Held) : (_hand != null ? _hand.position : holdPoint.position);
            RestPose(Held, from);
            _toPos = t.position; _toRot = t.rotation;
            t.SetPositionAndRotation(p, r);
            _fromPos = p; _fromRot = r;
            _t0 = Time.time;
            _phase = Phase.Lowering;
            if (lowerTime <= 0f) Finish(fadeHands: true);
        }

        /// <summary>
        /// A shift lets go at once, as the grabs do (D-0041). The event comes before the size changes, so the body is still the
        /// size that held it: the thing is set down ahead of that body and clear of it, which keeps it clear of the new size too
        /// (a toy stands inside the kid's footprint). A put-down under way is overruled.
        /// </summary>
        private void OnShiftStarted(ShiftSize _)
        {
            if (Held == null) return;
            Carryable thing = Held;
            RestPose(thing, ClearSpot(thing));
            Release(thing, fadeHands: false);
        }

        /// <summary>A point at chest height ahead of the player, far enough that the thing, turned as it will rest, does not reach the body.</summary>
        private Vector3 ClearSpot(Carryable thing)
        {
            Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            thing.transform.rotation = facing * thing.HoldRotation;
            Bounds b = RenderBounds(thing, thing.transform.position);
            float half = Mathf.Max(b.extents.x, b.extents.z);
            float radius = _body != null ? _body.radius * transform.lossyScale.x : 0.5f;
            float chest = _body != null ? _body.height * transform.lossyScale.y * 0.5f : 1f;
            Vector3 offset = b.center - thing.transform.position;   // the pivot is not always the middle
            Vector3 middle = transform.position + facing * Vector3.forward * (radius * 1.25f + half);
            return new Vector3(middle.x - offset.x, transform.position.y + chest, middle.z - offset.z);
        }

        private static Bounds RenderBounds(Carryable thing, Vector3 fallback)
        {
            var rs = thing.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(fallback, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Moves the thing to where it rests from <paramref name="from"/>: on whatever is under that point, facing the way
        /// the player faces (a plank lying along the ground). Does not let go.</summary>
        private void RestPose(Carryable thing, Vector3 from)
        {
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
            Bounds b = new Bounds(thing.transform.position, Vector3.zero);
            if (rs.Length > 0)
            {
                b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                thing.transform.position += Vector3.up * (floorY - b.min.y);
            }
            if (thing.Plank && rs.Length > 0) LayAlongGround(thing, floorY, b.size.z);
        }

        /// <summary>It is where it rests: back in the physics, kept apart from the body if they overlap (D-0041), let go.</summary>
        private void Finish(bool fadeHands)
        {
            Carryable thing = Held;
            if (thing == null) { Held = null; _phase = Phase.None; return; }
            thing.transform.SetPositionAndRotation(_toPos, _toRot);
            Release(thing, fadeHands);
        }

        private void Release(Carryable thing, bool fadeHands)
        {
            // Two hands come away from where they last held it (unless a shift resized the body under them).
            if (fadeHands && _twoHanded && handsFade > 0f)
            {
                Goals(thing.transform.position, thing.transform.rotation, thing, out HandGoal right, out HandGoal left);
                _lastRight = ToBody(right); _lastLeft = ToBody(left);
                _releasedAt = Time.time;
            }
            else _releasedAt = float.NegativeInfinity;
            Held = null;
            _phase = Phase.None;
            Collider[] colliders = thing.GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = true;
            // Set down around the player (a big thing from a small hand), it ignores the body until they part (D-0041).
            KeepApart.Until(thing.gameObject, colliders, _body);
            thing.SetCarried(false);
        }

        /// <summary>
        /// A plank finds its own far support: probe the ground under the near end (the hand) and under the far
        /// end (one length ahead along the player's facing); if the far end lands on something at a different
        /// height, tilt the thing to lie from the near point to the far point.
        /// </summary>
        private void LayAlongGround(Carryable thing, float nearY, float length)
        {
            Vector3 fwd = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
            Vector3 nearTop = new Vector3(thing.transform.position.x, nearY, thing.transform.position.z);
            Vector3 farProbe = nearTop + fwd * length + Vector3.up * 200f;
            float farY = nearY;
            int count = Physics.RaycastNonAlloc(farProbe, Vector3.down, _hits, 1000f, groundMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform) || _hits[i].transform.IsChildOf(thing.transform)) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; farY = _hits[i].point.y; found = true; }
            }
            if (!found) return;
            float drop = farY - nearY;
            if (Mathf.Abs(drop) < 0.05f || Mathf.Abs(drop) > length * 0.75f) return;   // flat, or too steep to be a ramp
            float run = Mathf.Sqrt(Mathf.Max(0f, length * length - drop * drop));
            Vector3 along = fwd * run + Vector3.up * drop;
            Quaternion lie = Quaternion.LookRotation(along.normalized, Vector3.up);
            // pivot the thing about its near end: move so the near end stays where it was
            Vector3 nearEndBefore = thing.transform.position - thing.transform.forward * (length * 0.5f);
            thing.transform.rotation = lie * thing.HoldRotation;
            Vector3 nearEndAfter = thing.transform.position - thing.transform.forward * (length * 0.5f);
            thing.transform.position += nearEndBefore - nearEndAfter;
        }

        private void OnUse()
        {
            if (Held != null && _phase != Phase.Lowering) Held.Use();
        }

        private void Update()
        {
            // The arm pose fades with a one-handed hold; Update so it is in before the animator evaluates this frame.
            // It starts down as soon as a put-down begins. A plank's pose is the Carry layer's (CarryPose).
            if (animator == null || _layer < 0) return;
            float target = Held != null && !_twoHanded && _phase != Phase.Lowering ? 1f : 0f;
            _armWeight = Mathf.MoveTowards(_armWeight, target, Time.deltaTime / Mathf.Max(0.01f, armFade));
            animator.SetLayerWeight(_layer, _armWeight);
        }

        private void LateUpdate()
        {
            if (Held == null) return;
            if (_phase == Phase.Lowering && Time.time - _t0 >= lowerTime) { Finish(fadeHands: true); return; }
            if (_phase == Phase.Reaching && Time.time - _t0 >= reachTime) _phase = Phase.Holding;
            PoseNow(out Vector3 p, out Quaternion r);
            Held.transform.SetPositionAndRotation(p, r);
        }

        // ---------- where the thing is ----------

        /// <summary>Where the held thing is this frame: easing in from where it lay, in the hands, or going down to rest.</summary>
        private void PoseNow(out Vector3 pos, out Quaternion rot)
        {
            if (_phase == Phase.Lowering)
            {
                float e = Ease((Time.time - _t0) / Mathf.Max(0.01f, lowerTime));
                pos = Vector3.Lerp(_fromPos, _toPos, e); rot = Quaternion.Slerp(_fromRot, _toRot, e);
                return;
            }
            if (_twoHanded) TwoHandPose(out pos, out rot); else OneHandPose(out pos, out rot);
            if (_phase != Phase.Reaching) return;
            float k = Ease((Time.time - _t0) / Mathf.Max(0.01f, reachTime));
            pos = Vector3.Lerp(_fromPos, pos, k); rot = Quaternion.Slerp(_fromRot, rot, k);
        }

        /// <summary>In the right hand: the grip at the palm, pointing the way the whole arm reaches (shoulder to hand), so a
        /// torch reads as aimed by the arm rather than held like a microphone.</summary>
        private void OneHandPose(out Vector3 pos, out Quaternion rot)
        {
            if (_hand == null)
            {
                pos = holdPoint.position; rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Held.HoldRotation;
                return;
            }
            Vector3 forward = _shoulder != null ? (_hand.position - _shoulder.position) : _hand.up;
            if (forward.sqrMagnitude < 1e-6f) forward = transform.forward;
            rot = Quaternion.LookRotation(forward.normalized, Vector3.up) * Held.GripRotation;
            pos = _hand.TransformPoint(_wears != null ? _wears.Palm(Held.GripPalm) : Held.GripPalm) - rot * Vector3.Scale(_gripRight, Held.transform.lossyScale);
        }

        /// <summary>Flat across the front of the body at carry height, its near edge a gap ahead of the body.</summary>
        private void TwoHandPose(out Vector3 pos, out Quaternion rot)
        {
            float s = Scale;
            Quaternion heading = Heading;
            rot = heading * _carryTurn;
            Vector3 size = Held.transform.lossyScale;
            Bounds local = Held.LocalBounds;
            Vector3 fwd = heading * Vector3.forward;
            Vector3 d = Quaternion.Inverse(rot) * fwd;   // the body's forward in the thing's own space
            float halfDepth = Mathf.Abs(d.x) * local.extents.x * size.x + Mathf.Abs(d.y) * local.extents.y * size.y + Mathf.Abs(d.z) * local.extents.z * size.z;
            float radius = _body != null ? _body.radius * s : 0.3f * s;
            Vector3 centre = transform.position + Vector3.up * (carryHeight * s) + fwd * (radius + carryGap * s + halfDepth);
            pos = centre - rot * Vector3.Scale(local.center, size);
        }

        /// <summary>A plank goes across the body: its grip-to-grip line along the body's right, its thin side up. Whichever end
        /// is already nearer the right goes right (an authored GripR always does), so the pick-up turns it the short way.</summary>
        private void SetUpTwoHands(Carryable thing)
        {
            thing.TwoHandGrips(out Vector3 a, out Vector3 b);
            Vector3 along = a - b;
            if (along.sqrMagnitude < 1e-8f) along = Vector3.forward;
            Vector3 right = Heading * Vector3.right;
            if (!thing.AuthoredTwoHandGrips &&Vector3.Dot(thing.transform.rotation * along, right) < 0f) { Vector3 swap = a; a = b; b = swap; along = -along; }
            _gripRight = a; _gripLeft = b;
            Vector3 flat = Vector3.ProjectOnPlane(thing.FlatAxis(), along.normalized);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.ProjectOnPlane(Vector3.up, along.normalized);
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.ProjectOnPlane(Vector3.right, along.normalized);
            flat.Normalize();
            if (Vector3.Dot(thing.transform.rotation * flat, Vector3.up) < 0f) flat = -flat;   // the side already up stays up
            _flat = flat;
            _carryTurn = Quaternion.LookRotation(Vector3.right, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(along.normalized, flat));
        }

        // ---------- the hands (CarryPose asks) ----------

        /// <summary>How much both hands are on a plank: easing in with the reach, full while held and going down, easing out after.</summary>
        public float CarryWeight
        {
            get
            {
                if (!isActiveAndEnabled) return 0f;
                if (Held != null) return !_twoHanded ? 0f : _phase == Phase.Reaching ? Ease((Time.time - _t0) / Mathf.Max(0.01f, reachTime)) : 1f;
                float k = (Time.time - _releasedAt) / Mathf.Max(0.01f, handsFade);
                return k < 1f ? 1f - Ease(k) : 0f;
            }
        }

        public bool CarryGoals(out HandGoal right, out HandGoal left)
        {
            if (Held != null && _twoHanded)
            {
                PoseNow(out Vector3 p, out Quaternion r);   // the same pose LateUpdate gives it this frame
                Goals(p, r, Held, out right, out left);
                return true;
            }
            if (Held == null && Time.time - _releasedAt < handsFade)
            {
                right = FromBody(_lastRight); left = FromBody(_lastLeft);
                return true;
            }
            right = left = default;
            return false;
        }

        /// <summary>Both palms up under the plank at its grips, fingers pointing ahead.</summary>
        private void Goals(Vector3 pos, Quaternion rot, Carryable thing, out HandGoal right, out HandGoal left)
        {
            Vector3 size = thing.transform.lossyScale;
            Vector3 under = rot * _flat;   // the plank's up
            Bounds local = thing.LocalBounds;
            float halfThick = Mathf.Abs(_flat.x) * local.extents.x * size.x + Mathf.Abs(_flat.y) * local.extents.y * size.y + Mathf.Abs(_flat.z) * local.extents.z * size.z;
            Vector3 fwd = Heading * Vector3.forward;
            right = new HandGoal { palm = pos + rot * Vector3.Scale(_gripRight, size) - under * halfThick, fingers = fwd, normal = under };
            left = new HandGoal { palm = pos + rot * Vector3.Scale(_gripLeft, size) - under * halfThick, fingers = fwd, normal = under };
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
