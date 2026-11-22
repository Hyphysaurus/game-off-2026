using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's hands for tools. A one-handed tool (the flashlight) is held as a pistol is (D-0106): its handle through
    /// the closed fist along the hand's own directions, its working end out past the thumb, the fist closed at its handle
    /// end so nothing of it runs back into the wrist (D-0132: Carryable.HandleEndGrip, WeaponHold > Tool End In Fist; a
    /// touch more to the thumb's side than a blade, Tool Tilt), the arm pointing it at the crosshair (ArmAim) and the
    /// fingers closed round it (FingerCurl); the
    /// animator's Arms layer fades in under that, for a body without humanoid arms. A plank is carried flat across the front of the body in both hands, the hands under its two
    /// grips through CarryPose (D-0042). Picking up eases the thing from where it lies into the hands; putting down (Crouch)
    /// lowers it to its resting spot before letting go. Attack goes to the held thing. Never parents the thing, so the
    /// player's 35x scale does not touch it. A shift lets go at once, ahead of the body (D-0041), and a tool in the bar
    /// is stowed; tools in the bar take the hand's size (D-0138): picked up, taken out and after each shift, so the toy
    /// holds a toy-sized flashlight and the kid a kid-sized one.
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
        private ArmAim _armAim;              // points a one-handed tool's arm at the crosshair
        private FingerCurl _fist;            // closes the right hand round its handle
        private WeaponHoldSettings _hold;    // the shared fist numbers (a handle in the fist; the arm's reach and limits)
        private Inventory _inventory;        // a one-handed tool takes a slot (D-0108): stowed there, taken out again

        /// <summary>A one-handed tool goes in the inventory: picked up into a slot, stowed and taken out by its slot.</summary>
        private bool InBar(Carryable thing) => _inventory != null && thing != null && thing.KeepInHand && !thing.TwoHanded;

        public Carryable Held { get; private set; }

        private float Scale => shifter != null ? shifter.Scale : transform.lossyScale.y;
        /// <summary>The hand's size as a share of the kid's: what a tool in the bar is resized to (D-0138).</summary>
        private float HandShare => shifter != null ? shifter.Scale / Mathf.Max(1e-4f, shifter.ScaleOf(ShiftSize.Kid)) : 1f;
        private Quaternion Heading => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (shifter == null) shifter = GetComponent<Shifter>();
            _body = GetComponent<CharacterController>();
            _wears = GetComponent<PlayerBody>();
            if (holdPoint == null) holdPoint = transform;
            _hold = WeaponHoldSettings.Load();
            _inventory = GetComponent<Inventory>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (animator.isHuman)
                {
                    _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    _shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                }
                _layer = animator.GetLayerIndex(armsLayer);
                _armAim = new ArmAim(animator, transform);   // here, before the Animator poses the arm: it reads the rest pose
                _fist = new FingerCurl(animator, right: true);
                var pose = CarryPose.On(animator);   // both hands on a plank (D-0042); added if the body lacks it
                if (pose != null) pose.Add(this);
            }
        }

        private void OnEnable()
        {
            if (input != null) { input.UseHeld += OnUse; input.Drop += PutDown; }
            if (shifter != null) { shifter.ShiftStarted += OnShiftStarted; shifter.Shifted += OnShifted; }
        }

        private void OnDisable()
        {
            if (input != null) { input.UseHeld -= OnUse; input.Drop -= PutDown; }
            if (shifter != null) { shifter.ShiftStarted -= OnShiftStarted; shifter.Shifted -= OnShifted; }
            if (_armAim != null) _armAim.ResetTwist();
            // Parked mid put-down (first person puts down, then parks the hands): it rests where it was going, now.
            if (Held != null && _phase == Phase.Lowering) Finish(fadeHands: false);
            _releasedAt = float.NegativeInfinity;
        }

        public void PickUp(Carryable thing)
        {
            if (thing == null || Held != null) return;
            if (_inventory == null) _inventory = GetComponent<Inventory>();   // the WeaponHolder adds it, maybe after our Awake
            if (InBar(thing) && !_inventory.RoomForTool()) return;
            KeepApart.Cancel(thing.gameObject);
            if (InBar(thing)) thing.Resize(HandShare);   // a tool takes the hand's size (D-0138)
            Held = thing;
            foreach (var col in Held.GetComponentsInChildren<Collider>()) col.enabled = false;   // out of the physics while in hand
            Held.SetCarried(true);
            _twoHanded = thing.TwoHanded;
            if (_twoHanded) SetUpTwoHands(thing);
            else _gripRight = OneHandGrip(thing);
            // Reach: it travels from where it lies into the hands.
            _fromPos = thing.transform.position;
            _fromRot = thing.transform.rotation;
            _t0 = Time.time;
            _phase = reachTime > 0f ? Phase.Reaching : Phase.Holding;
            _releasedAt = float.NegativeInfinity;
            GameEvents.RaisePickedUp(thing.transform);
            if (InBar(thing)) _inventory.Added(thing);
        }

        /// <summary>Puts the one-handed tool in hand away into its slot (D-0108): out of the world, still carried, its
        /// light left as it was. Nothing happens with empty hands, a plank, or a put-down under way.</summary>
        public void Stow()
        {
            if (Held == null || !InBar(Held) || _phase == Phase.Lowering) return;
            Carryable thing = Held;
            Held = null;
            _phase = Phase.None;
            thing.gameObject.SetActive(false);
        }

        /// <summary>Takes a stowed tool out of its slot into the hand (D-0108).</summary>
        public void TakeOut(Carryable thing)
        {
            if (thing == null || Held != null) return;
            thing.Resize(HandShare);
            thing.gameObject.SetActive(true);
            foreach (var col in thing.GetComponentsInChildren<Collider>()) col.enabled = false;
            Held = thing;
            _twoHanded = false;
            _gripRight = OneHandGrip(thing);
            _phase = Phase.Holding;
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
            if (InBar(Held) && _phase != Phase.Lowering) { Stow(); return; }   // it stays in its slot (D-0108)
            Carryable thing = Held;
            RestPose(thing, ClearSpot(thing));
            Release(thing, fadeHands: false);
        }

        /// <summary>After a shift every tool in the bar takes the new size (D-0138); they are all stowed by now.</summary>
        private void OnShifted(ShiftSize _)
        {
            if (_inventory == null) return;
            float share = HandShare;
            foreach (var item in _inventory.Items) if (item.Tool != null) item.Tool.Resize(share);
            if (Held != null && InBar(Held)) _gripRight = OneHandGrip(Held);
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
            if (InBar(thing)) _inventory.Removed(thing);
            Collider[] colliders = thing.GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = true;
            // Set down around the player (a big thing from a small hand), it ignores the body until they part (D-0041).
            KeepApart.Until(thing.gameObject, colliders, _body);
            thing.SetCarried(false);
            GameEvents.RaiseSetDown(thing.transform);
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
            if (Held != null && Held.TryGetComponent(out Flashlight torch)) torch.Aimed(AimHeld);   // aim held: the beam focuses (D-0141)
        }

        /// <summary>A one-handed tool is up in the fist: the camera takes the over-the-shoulder view, as with a gun (D-0141).</summary>
        public bool ToolUp => isActiveAndEnabled && InFist;

        /// <summary>Aim is held with a one-handed tool up (D-0141, Alan Wake 2): the body faces the aim and steps any way
        /// (PlayerMoves), the camera comes in (ShoulderCamera), a flashlight focuses its beam.</summary>
        public bool AimHeld => ToolUp && input != null && input.AimHeld && Time.timeScale > 0f && GameMode.Current != GameMode.Mode.Build;

        /// <summary>Where a held tool points: far along the view, or ahead of the body without one.</summary>
        private Vector3 AimTarget()
        {
            float s = Scale;
            Camera cam = PlayerSlot.ViewFor(this); Transform view = cam != null ? cam.transform : null;
            return view != null ? view.position + view.forward * (100f * s) : transform.position + transform.forward * (100f * s);
        }

        private void LateUpdate()
        {
            if (Held == null) { if (_armAim != null) _armAim.ResetTwist(); return; }
            if (_phase == Phase.Lowering && Time.time - _t0 >= lowerTime) { Finish(fadeHands: true); return; }
            if (_phase == Phase.Reaching && Time.time - _t0 >= reachTime) _phase = Phase.Holding;
            // Not aiming, the body is not turned to the aim: the waist twists toward it and the arm reaches the rest (D-0141).
            if (InFist) _armAim.TwistTorso(AimTarget(), Mathf.SmoothStep(0f, 1f, _armWeight), _hold, Time.deltaTime);
            AimArm();
            PoseNow(out Vector3 p, out Quaternion r);
            Held.transform.SetPositionAndRotation(p, r);
            if (InFist) _fist.Close(Mathf.SmoothStep(0f, 1f, _armWeight), Held.GripCurl(_hold).x, Held.GripCurl(_hold).y, Held.GripCurl(_hold).z);
        }

        /// <summary>A one-handed tool held in a humanoid fist (not a plank, not on its way down).</summary>
        private bool InFist => Held != null && !_twoHanded && _phase != Phase.Lowering && _armAim != null && _armAim.Ready && _hand != null;

        /// <summary>The tool in the fist, as ArmAim reads a gun: z its working end, y its top (hand rotation x this). It
        /// leaves the fist Tool Tilt degrees further to the thumb's side than a blade (D-0132), so its handle end dips
        /// under the wrist rather than lying along the forearm.</summary>
        private Quaternion FistFrame => _armAim.Grip(Quaternion.AngleAxis(_hold.toolTilt, Vector3.forward) * _hold.handleInHand, _hold.handleTopInHand);

        /// <summary>Where the fist closes on a one-handed tool, in its own space: its Grip marker, else its handle end with
        /// the end just past the little finger (D-0132), so nothing of it sticks out behind the hand into the wrist. A body
        /// without humanoid arms keeps the old grip, a quarter of the length in.</summary>
        private Vector3 OneHandGrip(Carryable thing)
        {
            if (_hand == null || _armAim == null || !_armAim.Ready) return thing.GripLocal;
            Vector3 s = _hand.lossyScale;
            float hand = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;   // rig metres to the world
            return thing.HandleEndGrip(_hold.toolEndInFist * hand);
        }

        /// <summary>From that frame to the thing's own turn: its working end along the frame's z, then its own extra turn.</summary>
        private Quaternion ThingInFrame(Carryable thing) => thing.GripRotation * Quaternion.Inverse(Quaternion.LookRotation(thing.HeadLocal, thing.UpLocal));

        /// <summary>Points the arm holding a one-handed tool at the crosshair, as a pistol is pointed, as far in as the arm
        /// pose has faded (so picking up and putting down ease it).</summary>
        private void AimArm()
        {
            if (!InFist) return;
            float s = Scale;
            Vector3 target = AimTarget();
            Quaternion grip = FistFrame;
            var hold = new ArmAim.Hold
            {
                grip = grip,
                seat = Quaternion.Inverse(grip) * Vector3.Scale(_armAim.InHand(_hold.handleCentre), _hand.lossyScale),
                muzzle = Vector3.forward * (Held.ReachFrom(_gripRight) * Held.HeadScale),   // from where the fist holds it to its far end
                kickTurn = Quaternion.identity,
                kickSlide = Vector3.zero,
                frame = Vector3.zero,
                limitWrist = true,
            };
            _armAim.Aim(target, Mathf.SmoothStep(0f, 1f, _armWeight), hold, _hold, s);
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

        /// <summary>In the right hand: its handle through the closed fist (its grip at the fist's middle), its working end
        /// out past the thumb, so it points where the arm (aimed at the crosshair) points it. A body without humanoid arms
        /// keeps the old hold: the grip at the palm, pointing the way the arm reaches.</summary>
        private void OneHandPose(out Vector3 pos, out Quaternion rot)
        {
            if (_hand == null)
            {
                pos = holdPoint.position; rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Held.HoldRotation;
                return;
            }
            if (_armAim != null && _armAim.Ready)
            {
                rot = _hand.rotation * FistFrame * ThingInFrame(Held);
                pos = _hand.TransformPoint(_armAim.InHand(_hold.handleCentre)) - rot * Vector3.Scale(_gripRight, Held.transform.lossyScale);
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
