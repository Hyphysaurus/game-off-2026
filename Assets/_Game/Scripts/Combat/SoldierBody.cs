using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A toy soldier's living body (D-0187): a funny character (CharacterBody, under the soldier's Visual) that walks,
    /// flinches, falls, gets up and holds a toy blaster, in place of the plastic model. It sits beside ToySoldier on the
    /// soldier's root and decides nothing: whatever moves the soldier (its own patrol today, a bot brain later, the host's
    /// word on a copy online) moves the body, and what happens to the soldier comes as ToySoldier's events.
    ///
    /// - Walking: the player's own controller (Animation/PlayerMale) on the body's Animator, driven as PlayerAnimation
    ///   drives the player's: MoveX and MoveZ are how the soldier's root moved this frame, smoothed, in its own facing
    ///   (so forward, backward and sideways read right), in the units the clips were measured in; Pace speeds the legs up
    ///   past the run clip's own speed. The Arms layer (the player's one-arm holding pose) is up while it stands.
    /// - Hurt: the light hit reaction (HitLight), back to walking when it ends or once it moves again. Fell: the death
    ///   (Death), and it stays down. Rose: the get-up (Wake: the death played backwards, as the player's WakeUp) when it
    ///   lay down, else straight to standing. Fired: a small kick of the blaster in the hand.
    /// - Its look: rolled by its own rules from a seed worked out from where it stands guard when the scene loads (the
    ///   same on every PC), in the rules' practice colour outside a match and in its team's colour word in one: the same
    ///   character in a different kit.
    /// - Hit shapes: a ball on the head, a capsule down the chest and one down each leg, on its bones, handed to the
    ///   soldier's HitShapes, so they are shot where they are seen, off while it lies down and put away with it.
    /// - Put away (ToySoldier.PutAway): the soldier hides every renderer and collider under it (the body, the blaster and
    ///   the shapes too); the body's Animator stops until it is back.
    /// As it wakes it sets ToySoldier's switches: BodyFalls (the soldier stops tipping its Visual over), BodyDresses (it
    /// stops painting its renderers in the team's colour) and ShownHeight (the stun effect floats over this body). Without
    /// a CharacterBody under it, it does nothing at all and the soldier stays the plastic one.
    /// Made by Shift Jam > Characters > Build Funny Soldier (D-0187) on Prefabs/Pieces/Interactables/FunnySoldier.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ToySoldier))]
    [DefaultExecutionOrder(-100)]   // after CharacterBody (-200) dresses itself, before Damageable and HitShapes (0) wake: the blaster flashes with the body and the shapes are taken as HitShapes wakes
    public sealed class SoldierBody : MonoBehaviour
    {
        /// <summary>What a team wears: its number (TeamMatch.Blue, TeamMatch.Orange) and a colour word of the pack.</summary>
        [Serializable]
        public struct TeamKit
        {
            [Tooltip("The team's number: 1 = TeamMatch's Blue, 2 = its Orange.")] public int team;
            [Tooltip("The pack's colour word its body, top and trousers are rolled in (Blue, Orange, Green, Red...).")] public string colour;
        }

        private enum Pose { Standing, Hurt, Down, Rising }

        private static readonly int Locomotion = Animator.StringToHash("Locomotion");
        private static readonly int HitLight = Animator.StringToHash("HitLight");
        private static readonly int Death = Animator.StringToHash("Death");
        private static readonly int Wake = Animator.StringToHash("Wake");
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int Pace = Animator.StringToHash("Pace");
        private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        private const string ArmsLayer = "Arms";          // the controller's one-arm holding layer (PlayerPortBuilder)

        private const float Damp = 0.08f;                 // seconds MoveX and MoveZ take to settle, as PlayerAnimation's
        private const float Still = 0.05f;                // clip units a second: slower than this is standing still
        private const float FallbackClipHeight = 1.85f;   // the player's body at toy size, when Resources/PlayerBody is missing
        private const float FallbackRunClip = 2.03f;      // the run clip's ground speed, the same way
        private const float FallbackMostPace = 1.6f;
        private const float FallbackHitSeconds = 1.92f, FallbackHitPlayback = 1.5f, FallbackHitLock = 0.3f;
        private const float FallbackDeathPlayback = 1.5f, FallbackBlend = 0.1f;
        private const float FallbackHeadTop = 1.75f;      // a dressed figure without a hat, at scale 1 (CHARS_CONTRACT FACTS)
        private const float SeedGrid = 100f;              // the post is read in centimetre steps for the seed
        private const float Tiny = 1e-6f;

        [Header("The body")]
        [SerializeField, Tooltip("The funny character under the soldier's Visual. Found under the soldier when empty.")]
        private CharacterBody body;
        [SerializeField, Min(0.01f), Tooltip("How tall the body stands from its soles to the top of its head, in the soldier's units at scale 1 (a hat or tall hair may stand above it). Its soles are at the bottom of the soldier's walking capsule. The builder starts it at the capsule's height (the toy soldier's: 1.6).")]
        private float height = 1.6f;

        [Header("Its look")]
        [SerializeField, Tooltip("How its look is rolled. Empty = the default rules (Resources/CharacterLookRules); their Practice Colour is what it wears outside a match.")]
        private CharacterLookRules rules;
        [SerializeField, Tooltip("What each team wears in a match, as the pack's colour words (the team's NAME is the match's to change; these words are the pack's). A team not listed wears colours by chance.")]
        private TeamKit[] teamKits =
        {
            new TeamKit { team = TeamMatch.Blue, colour = "Blue" },
            new TeamKit { team = TeamMatch.Orange, colour = "Orange" },
        };
        [SerializeField, Tooltip("Added to the number its look is rolled from (worked out from where it stands guard, the same on every PC). Change it to give this soldier another face.")]
        private int seedShift;

        [Header("Walking")]
        [SerializeField, Min(0.001f), Tooltip("Seconds its measured movement takes to settle (the legs follow how far the soldier moved each frame; online a copy moves in steps).")]
        private float moveSmoothing = 0.1f;
        [SerializeField, Min(0.01f), Tooltip("Faster than this, in the soldier's units a second at scale 1, a move is a jump (back to its post, a copy's first place), not a walk: the legs ignore it.")]
        private float fastestWalk = 12f;

        [Header("The arm that holds the blaster")]
        [SerializeField, Range(0f, 1f), Tooltip("How far the player's one-arm holding pose (the controller's Arms layer) is on while it stands: 1 = fully. Off while it lies down and gets up.")]
        private float armWeight = 1f;
        [SerializeField, Min(0.01f), Tooltip("How quickly the arm comes up or goes down, in full swings a second.")]
        private float armRate = 6f;

        [Header("Getting up")]
        [SerializeField, Range(0f, 1f), Tooltip("Where the get-up starts: the Wake state plays the fall backwards, and 0.1 starts from lying still (the player's WakeUp).")]
        private float wakeStartAt = 0.1f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the get-up plays before it blends back to standing (the player's: 1.7).")]
        private float wakeSeconds = 1.7f;
        [SerializeField, Min(0f), Tooltip("Seconds of that blend back to standing.")]
        private float wakeBlend = 0.3f;

        [Header("The toy blaster in its hand")]
        [SerializeField, Tooltip("The weapon whose model it holds: only the look, nothing of it runs (its darts leave from the soldier's Muzzle, as before). Empty = empty-handed.")]
        private Weapon blaster;
        [SerializeField, Tooltip("Where the blaster's root sits in the right hand bone's own space (rig units). Started from the player's grip (Resources/WeaponHold) by the builder or the ⋮ menu \"Hold the gun as the player does\".")]
        private Vector3 gripPosition;
        [SerializeField, Tooltip("How the blaster is turned in the right hand bone's own space, degrees. Started with Grip Position.")]
        private Vector3 gripTurn;
        [SerializeField, Min(0.01f), Tooltip("The blaster's size: 1 = as big against this body as the player's blaster is against the player.")]
        private float gripSize = 1f;
        [SerializeField, Min(0f), Tooltip("Degrees the blaster flips up in the hand on each shot. 0 = no kick.")]
        private float kickDegrees = 8f;
        [SerializeField, Min(0f), Tooltip("Seconds the kick takes to settle. 0 = no kick.")]
        private float kickSeconds = 0.15f;

        [Header("Hit shapes on its bones (the body's own units)")]
        [SerializeField, Min(0.01f), Tooltip("The ball round the head: its radius.")]
        private float headRadius = 0.24f;
        [SerializeField, Tooltip("How far the ball's middle is above the head bone.")]
        private float headLift = 0.2f;
        [SerializeField, Min(0.01f), Tooltip("The capsule from the hips to the neck: its radius.")]
        private float chestRadius = 0.2f;
        [SerializeField, Min(0.01f), Tooltip("The capsule down each leg, from the hip to the sole: its radius.")]
        private float legRadius = 0.11f;

        // Kept through a script reload in Play (plain private fields are carried over; Awake does not run again).
        private int _postSeed;
        private bool _seeded;
        private int _wornTeam;
        private Collider[] _shapes;      // may come back empty, not null: every use tests the length
        private Transform _gun;
        private bool _away;
        private Pose _pose;
        private float _poseAt;
        private float _arm;
        private float _kickAt = float.NegativeInfinity;

        // Found on first use, never saved.
        [NonSerialized] private ToySoldier _soldier;
        [NonSerialized] private HitShapes _hitShapes;
        [NonSerialized] private Animator _animator;
        [NonSerialized] private bool _paramsRead;
        [NonSerialized] private bool _ours;
        [NonSerialized] private int _armsLayer = -1;
        [NonSerialized] private bool _numbersRead;
        [NonSerialized] private float _clipHeight, _runClip, _mostPace, _hitSeconds, _hitPlayback, _hitLock, _deathPlayback, _blend;
        [NonSerialized] private bool _tracking;
        [NonSerialized] private Vector3 _last;
        [NonSerialized] private Vector3 _velocity;
        [NonSerialized] private bool _listening;
        [NonSerialized] private bool _searched;

        /// <summary>The number its look is rolled from: from where it stood guard when the scene loaded, plus Seed Shift.
        /// The same on every PC.</summary>
        public int Seed => unchecked(_postSeed + seedShift);
        /// <summary>The blaster shown in its hand, or null (none set, or no hand: the pack is missing).</summary>
        public Transform Gun => _gun;
        /// <summary>How many hit shapes it made on its bones (0 when there are no bones).</summary>
        public int ShapeCount => _shapes != null ? _shapes.Length : 0;

        private float Scale => Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));

        /// <summary>The funny character it drives, or null (none under the soldier: it does nothing).</summary>
        private CharacterBody Body
        {
            get
            {
                if (body == null && !_searched)
                {
                    _searched = true;   // looked for once: a soldier without one does not search every frame
                    body = GetComponentInChildren<CharacterBody>(true);
                }
                return body;
            }
        }

        private ToySoldier Soldier
        {
            get
            {
                if (_soldier == null) TryGetComponent(out _soldier);
                return _soldier;
            }
        }

        private void Awake()
        {
            if (!_seeded)
            {
                _postSeed = SeedAt(transform.position);
                _seeded = true;
            }
            CharacterBody b = Body;
            if (b == null || Soldier == null) return;
            Dress(_soldier.Team);
            Fit();
            MakeShapes();
            MakeGun();
            _arm = armWeight;
            _pose = Pose.Standing;
        }

        private void OnEnable()
        {
            if (Body == null || Soldier == null) return;
            // The switches and the events are not kept by a script reload in Play: set again each time.
            _soldier.BodyFalls = true;
            _soldier.BodyDresses = true;
            _soldier.ShownHeight = height;
            if (!_listening)
            {
                _soldier.Hurt += OnHurt;
                _soldier.Fell += OnFell;
                _soldier.Rose += OnRose;
                _soldier.Fired += OnFired;
                _soldier.TeamChanged += OnTeam;
                _soldier.AwayChanged += OnAway;
                _listening = true;
            }
            HandShapes();
            if (_wornTeam != _soldier.Team) Dress(_soldier.Team);   // the match moved on while this part was off
            if (_away != _soldier.Away) OnAway(_soldier.Away);     // the same for putting it away
            _tracking = false;
        }

        private void OnDisable()
        {
            if (_soldier == null) return;
            if (_listening)
            {
                _soldier.Hurt -= OnHurt;
                _soldier.Fell -= OnFell;
                _soldier.Rose -= OnRose;
                _soldier.Fired -= OnFired;
                _soldier.TeamChanged -= OnTeam;
                _soldier.AwayChanged -= OnAway;
                _listening = false;
            }
            _soldier.BodyFalls = false;
            _soldier.BodyDresses = false;
            _soldier.ShownHeight = 0f;
            _soldier.GetUpSeconds = 0f;
        }

        // ---------------------------------------------------------------- the look

        /// <summary>Its seed from a place: the place in centimetre steps, mixed (FNV). Integer maths only, so the same
        /// place gives the same seed on every PC.</summary>
        private static int SeedAt(Vector3 place)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)Mathf.RoundToInt(place.x * SeedGrid)) * 16777619u;
                h = (h ^ (uint)Mathf.RoundToInt(place.y * SeedGrid)) * 16777619u;
                h = (h ^ (uint)Mathf.RoundToInt(place.z * SeedGrid)) * 16777619u;
                return (int)(h & 0x7FFFFFFFu);
            }
        }

        /// <summary>The colour word for <paramref name="team"/>: the rules' practice colour outside a match, the team's
        /// kit in one ("" for a team not listed: colours by chance).</summary>
        private string ColourOf(int team)
        {
            if (team == TeamMatch.NoTeam)
            {
                CharacterLookRules used = rules != null ? rules : CharacterLookRules.Default;
                return used != null && used.practiceColour != null ? used.practiceColour : "";
            }
            if (teamKits != null)
                for (int i = 0; i < teamKits.Length; i++)
                    if (teamKits[i].team == team) return teamKits[i].colour ?? "";
            return "";
        }

        /// <summary>Dresses the body in its look for <paramref name="team"/>: the same seed, the team's colour word.</summary>
        private void Dress(int team)
        {
            _wornTeam = team;
            CharacterBody b = Body;
            if (b != null) b.WearRolled(Seed, rules, ColourOf(team));
        }

        // ---------------------------------------------------------------- size and place

        /// <summary>
        /// Stands the body under the soldier: its soles at the bottom of the soldier's walking capsule, facing the
        /// soldier's way, sized so the top of its head (its Body part's top, not a hat) is Height up. The builder calls it
        /// on the prefab; it runs again as the soldier wakes.
        /// </summary>
        public void Fit()
        {
            CharacterBody b = Body;
            if (b == null) return;
            Transform t = b.transform;
            float head = HeadTop(b);
            float feet = TryGetComponent(out CharacterController cc) ? cc.center.y - 0.5f * cc.height : 0f;
            float own = Mathf.Max(1e-4f, Mathf.Abs(t.lossyScale.y));
            float rigShare = Mathf.Abs(b.Rig.lossyScale.y) / own;   // the rig's size against the body's root (1 as the pack is)
            if (rigShare < 1e-4f) rigShare = 1f;
            float parentScale = t.parent != null ? Mathf.Max(1e-4f, Mathf.Abs(t.parent.lossyScale.y)) : 1f;
            t.localScale = Vector3.one * (height * Scale / (head * rigShare) / parentScale);
            t.SetPositionAndRotation(transform.TransformPoint(new Vector3(0f, feet, 0f)), transform.rotation);
        }

        /// <summary>The top of the body's head above its soles in the rig's own units: its Body part's top in the parts
        /// list; with no parts list, what it wears; failing that, 1.75.</summary>
        private static float HeadTop(CharacterBody b)
        {
            CharacterCatalog catalog = CharacterCatalog.Load();
            if (catalog != null && catalog.Ready)
            {
                CharacterCatalog.Part part = catalog.PartById(b.Look.Get(CharacterSlot.Body));
                if (part != null && part.top > 0f) return part.top;
            }
            float rig = Mathf.Abs(b.Rig.lossyScale.y);
            float worn = rig > 1e-4f ? b.Height / rig : 0f;
            return worn > 0f ? worn : FallbackHeadTop;
        }

        /// <summary>How big the body stands against the body the player's clips and grip were measured on.</summary>
        private float BodySize
        {
            get
            {
                ReadNumbers();
                return Mathf.Max(1e-4f, height * Scale / _clipHeight);
            }
        }

        // ---------------------------------------------------------------- hit shapes

        /// <summary>The shapes on its bones, made once from the rest pose (before the Animator poses it).</summary>
        private void MakeShapes()
        {
            if (_shapes != null && _shapes.Length > 0) return;
            CharacterBody b = Body;
            if (b == null || b.Bone(HumanBodyBones.Hips) == null) return;   // no humanoid rig (the pack is missing): no shapes
            float unit = Mathf.Abs(b.Rig.lossyScale.y);
            Vector3 up = transform.up;
            var made = new List<Collider>();
            Transform hips = b.Bone(HumanBodyBones.Hips);
            Transform head = b.Bone(HumanBodyBones.Head);
            Transform neck = b.Bone(HumanBodyBones.Neck);
            if (neck == null) neck = head;
            Transform spine = b.Bone(HumanBodyBones.Spine);
            if (spine == null) spine = hips;
            if (head != null) made.Add(Ball(head, "HitHead", head.position + up * (headLift * unit), headRadius * unit));
            if (neck != null) made.Add(Rod(spine, "HitChest", hips.position, neck.position, chestRadius * unit));
            float soles = b.transform.position.y;
            Leg(b, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, "HitLegL", soles, unit, made);
            Leg(b, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot, "HitLegR", soles, unit, made);
            _shapes = made.ToArray();
            HandShapes();
        }

        private void Leg(CharacterBody b, HumanBodyBones upper, HumanBodyBones foot, string shapeName, float soles, float unit, List<Collider> made)
        {
            Transform top = b.Bone(upper), end = b.Bone(foot);
            if (top == null || end == null) return;
            Vector3 sole = end.position;
            sole.y = soles;
            made.Add(Rod(top, shapeName, top.position, sole, legRadius * unit));
        }

        /// <summary>A ball of <paramref name="radius"/> (world) round <paramref name="centre"/>, riding <paramref name="bone"/>.</summary>
        private Collider Ball(Transform bone, string shapeName, Vector3 centre, float radius)
        {
            Transform t = Holder(bone, shapeName, centre, bone.rotation);
            var ball = t.gameObject.AddComponent<SphereCollider>();
            ball.radius = radius / LocalUnit(t);
            return Shape(ball);
        }

        /// <summary>A capsule of <paramref name="radius"/> (world) from <paramref name="from"/> to <paramref name="to"/>,
        /// riding <paramref name="bone"/>: its own up runs along the line, so the bone's axes do not matter.</summary>
        private Collider Rod(Transform bone, string shapeName, Vector3 from, Vector3 to, float radius)
        {
            Vector3 line = to - from;
            Quaternion along = line.sqrMagnitude > Tiny ? Quaternion.FromToRotation(Vector3.up, line) : bone.rotation;
            Transform t = Holder(bone, shapeName, (from + to) * 0.5f, along);
            var rod = t.gameObject.AddComponent<CapsuleCollider>();
            float unit = LocalUnit(t);
            rod.direction = 1;
            rod.center = Vector3.zero;
            rod.radius = radius / unit;
            rod.height = line.magnitude / unit;
            return Shape(rod);
        }

        /// <summary>A new child of <paramref name="bone"/> at a world place and turn, on the soldier's layer.</summary>
        private Transform Holder(Transform bone, string shapeName, Vector3 at, Quaternion turn)
        {
            var go = new GameObject(shapeName) { layer = gameObject.layer };
            Transform t = go.transform;
            t.SetParent(bone, false);
            t.SetPositionAndRotation(at, turn);
            return t;
        }

        private static float LocalUnit(Transform t) => Mathf.Max(1e-4f, Mathf.Abs(t.lossyScale.y));

        /// <summary>Set up as the soldier's own hit shape is: solid (not a trigger) and touching nothing (HitShapes
        /// does the same; done here too, so a soldier without HitShapes is not bumped by them).</summary>
        private static Collider Shape(Collider c)
        {
            c.isTrigger = false;
            c.excludeLayers = ~0;
            return c;
        }

        /// <summary>Hands the shapes to the soldier's HitShapes (again after a script reload; it ignores ones it has).</summary>
        private void HandShapes()
        {
            if (_shapes == null || _shapes.Length == 0) return;
            if (_hitShapes == null && !TryGetComponent(out _hitShapes)) return;
            for (int i = 0; i < _shapes.Length; i++) if (_shapes[i] != null) _hitShapes.Add(_shapes[i]);
        }

        // ---------------------------------------------------------------- the blaster

        /// <summary>A look-only copy of the blaster in the right hand, as WeaponHolder.ShowPuppet shows another player's:
        /// every part of it switched off, its colliders removed (PutAway switches every collider under the soldier back
        /// on), its bodies kinematic.</summary>
        private void MakeGun()
        {
            if (_gun != null || blaster == null) return;
            Transform hand = Body.Bone(HumanBodyBones.RightHand);
            if (hand == null) return;
            Weapon copy = Instantiate(blaster, hand, false);
            copy.name = blaster.name + " (shown)";
            foreach (Behaviour part in copy.GetComponentsInChildren<Behaviour>(true)) part.enabled = false;   // a look: no shots, no ammo, no sound
            foreach (Collider c in copy.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Destroy(c); }
            foreach (Rigidbody rb in copy.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            _gun = copy.transform;
            float handScale = Mathf.Max(1e-4f, Mathf.Abs(hand.lossyScale.y));
            _gun.localScale = blaster.transform.localScale * (BodySize * gripSize / handScale);
            PoseGun();
        }

        /// <summary>The blaster at its grip in the hand, with what is left of a shot's kick.</summary>
        private void PoseGun()
        {
            Quaternion turn = Quaternion.Euler(gripTurn);
            if (kickDegrees > 0f && kickSeconds > 0f)
            {
                float left = 1f - (Time.time - _kickAt) / kickSeconds;
                if (left > 0f) turn *= Quaternion.AngleAxis(-kickDegrees * left * left, Vector3.right);   // the barrel (its z) flips toward its top (its y)
            }
            _gun.localPosition = gripPosition;
            _gun.localRotation = turn;
        }

        /// <summary>
        /// Sets Grip Position and Grip Turn from the player's own grip (Resources/WeaponHold: the middle of the fist, the
        /// barrel and the top in the hand's knuckle, thumb and palm directions, and the blaster's own Hand Rotation and
        /// Hand Offset), read on this body's right hand, as ArmAim reads the player's. False (nothing changed) when the
        /// body has no humanoid right hand. The builder calls it when it first makes the prefab; the ⋮ menu runs it again.
        /// </summary>
        public bool HoldAsPlayer()
        {
            CharacterBody b = Body;
            Animator a = b != null ? b.Animator : null;
            Transform hand = BoneOf(a, HumanBodyBones.RightHand);
            Transform knuckle = BoneOf(a, HumanBodyBones.RightMiddleProximal);
            Transform thumb = BoneOf(a, HumanBodyBones.RightThumbProximal);
            if (hand == null || knuckle == null || thumb == null) return false;
            WeaponHoldSettings s = WeaponHoldSettings.Load();
            Quaternion fromWorld = Quaternion.Inverse(hand.rotation);
            Vector3 fingers = fromWorld * (knuckle.position - hand.position);
            Vector3 top = fromWorld * (thumb.position - hand.position);
            if (fingers.sqrMagnitude < Tiny) return false;
            fingers.Normalize();
            top -= fingers * Vector3.Dot(top, fingers);
            if (top.sqrMagnitude < Tiny) return false;
            top.Normalize();
            Vector3 palm = Vector3.Cross(fingers, top).normalized;   // a right hand: fingers x thumb (ArmAim)
            Vector3 InHand(Vector3 v) => fingers * v.x + top * v.y + palm * v.z;
            Vector3 along = InHand(s.barrelInHand), upward = InHand(s.topInHand);
            if (along.sqrMagnitude < Tiny) along = fingers;
            if (upward.sqrMagnitude < Tiny) upward = top;
            Quaternion grip = Quaternion.LookRotation(along.normalized, upward.normalized) * Quaternion.Euler(s.gripEuler)
                              * (blaster != null ? blaster.HandRotation : Quaternion.identity);
            float handScale = Mathf.Max(1e-4f, Mathf.Abs(hand.lossyScale.y));
            Vector3 offset = blaster != null ? blaster.HandOffset * (BodySize * gripSize / handScale) : Vector3.zero;
#if UNITY_EDITOR
            if (!Application.isPlaying && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(this))
                UnityEditor.Undo.RecordObject(this, "Hold the gun as the player does");
#endif
            gripPosition = InHand(s.fistCentre) + grip * offset;
            gripTurn = grip.eulerAngles;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
            if (_gun != null) PoseGun();
            return true;
        }

        [ContextMenu("Hold the gun as the player does")]
        private void HoldAsPlayerMenu()
        {
            if (HoldAsPlayer()) Debug.Log("[Characters] " + name + " holds the blaster at " + gripPosition.ToString("F4") + ", turned " + gripTurn.ToString("F1") + ".", this);
            else Debug.LogWarning("[Characters] " + name + ": no humanoid right hand on its body (is the Funny Characters pack in the project?); nothing changed.", this);
        }

        /// <summary>A humanoid bone, by the Animator when it answers (it may not outside Play), else by the avatar's own
        /// bone names, as PlayerPortBuilder finds them.</summary>
        private static Transform BoneOf(Animator a, HumanBodyBones bone)
        {
            if (a == null || a.avatar == null || !a.avatar.isHuman) return null;
            Transform found = a.GetBoneTransform(bone);
            if (found != null) return found;
            string human = HumanTrait.BoneName[(int)bone];
            foreach (HumanBone map in a.avatar.humanDescription.human)
            {
                if (map.humanName != human) continue;
                foreach (Transform t in a.GetComponentsInChildren<Transform>(true)) if (t.name == map.boneName) return t;
            }
            return null;
        }

        // ---------------------------------------------------------------- what happens to the soldier

        private void OnHurt(DamageInfo hit)
        {
            if (_pose == Pose.Down || _pose == Pose.Rising || !Has(HitLight)) return;
            _animator.SetFloat(MoveSpeed, _hitPlayback);
            _animator.CrossFadeInFixedTime(HitLight, _blend, 0, 0f);
            Become(Pose.Hurt);
        }

        private void OnFell(bool quiet)
        {
            Become(Pose.Down);
            _kickAt = float.NegativeInfinity;
            // The soldier waits out the get-up it will be shown (its feet would slide under it otherwise); with no
            // get-up to show it is on its way at once, as a plastic one is.
            if (_soldier != null) _soldier.GetUpSeconds = Has(Wake) ? wakeSeconds : 0f;
            if (!Has(Death)) return;
            _animator.SetFloat(MoveSpeed, _deathPlayback);
            if (quiet) _animator.Play(Death, 0, 1f);   // already lying there: the clip's last frame
            else _animator.CrossFadeInFixedTime(Death, _blend, 0, 0f);
        }

        private void OnRose(bool getUp)
        {
            _tracking = false;   // it may have jumped to its post: no walk from that
            _velocity = Vector3.zero;
            if (getUp && _pose == Pose.Down && Has(Wake))
            {
                _animator.Play(Wake, 0, wakeStartAt);
                Become(Pose.Rising);
                return;
            }
            Become(Pose.Standing);
            _arm = armWeight;
            if (Has(Locomotion)) _animator.Play(Locomotion, 0, 0f);
        }

        private void OnFired(Vector3 from, Vector3 direction)
        {
            if (_pose == Pose.Standing || _pose == Pose.Hurt) _kickAt = Time.time;
        }

        private void OnTeam(int team) => Dress(team);

        private void OnAway(bool away)
        {
            _away = away;
            _tracking = false;
            _velocity = Vector3.zero;
            _kickAt = float.NegativeInfinity;
            Become(Pose.Standing);
            _arm = armWeight;
            Animator a = FindAnimator();
            if (a == null) return;
            a.enabled = !away;   // put away: nothing animates; back: it starts again from standing
            if (!away && Has(Locomotion)) _animator.Play(Locomotion, 0, 0f);
        }

        private void Become(Pose pose)
        {
            _pose = pose;
            _poseAt = Time.time;
        }

        // ---------------------------------------------------------------- every frame

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _away || Body == null) return;
            Vector3 local = Measure(dt);
            if (!CanAnimate()) return;
            float speed = new Vector2(local.x, local.z).magnitude;
            float now = Time.time;
            if (_pose == Pose.Hurt && (now - _poseAt >= _hitSeconds || (now - _poseAt >= _hitLock && speed > Still)))
            {
                _animator.CrossFadeInFixedTime(Locomotion, _blend, 0);
                Become(Pose.Standing);
            }
            else if (_pose == Pose.Rising && now - _poseAt >= wakeSeconds)
            {
                _animator.CrossFadeInFixedTime(Locomotion, wakeBlend, 0);
                Become(Pose.Standing);
            }
            _animator.SetFloat(MoveX, local.x, Damp, dt);
            _animator.SetFloat(MoveZ, local.z, Damp, dt);
            _animator.SetFloat(Pace, speed > _runClip ? Mathf.Clamp(speed / _runClip, 1f, _mostPace) : 1f);
            if (_armsLayer >= 0)
            {
                float want = _pose == Pose.Standing || _pose == Pose.Hurt ? armWeight : 0f;
                _arm = Mathf.MoveTowards(_arm, want, armRate * dt);
                _animator.SetLayerWeight(_armsLayer, _arm);
            }
        }

        private void LateUpdate()
        {
            if (_gun != null && !_away) PoseGun();
        }

        /// <summary>How the soldier's root moved since the last frame, smoothed, in its own facing (x right, z forward),
        /// in the clips' units a second. A jump (faster than Fastest Walk) counts as standing still.</summary>
        private Vector3 Measure(float dt)
        {
            Vector3 at = transform.position;
            if (!_tracking)
            {
                _last = at;
                _tracking = true;
            }
            Vector3 step = at - _last;
            _last = at;
            step.y = 0f;
            Vector3 moved = step / dt;
            if (moved.magnitude > fastestWalk * Scale) moved = Vector3.zero;
            _velocity = Vector3.Lerp(_velocity, moved, 1f - Mathf.Exp(-dt / moveSmoothing));
            return transform.InverseTransformDirection(_velocity) / BodySize;
        }

        // ---------------------------------------------------------------- the Animator

        private Animator FindAnimator()
        {
            if (_animator == null)
            {
                CharacterBody b = Body;
                _animator = b != null ? b.Animator : null;
                _paramsRead = false;
            }
            return _animator;
        }

        /// <summary>True when the body's Animator is running the player's controller (it has the Pace parameter).</summary>
        private bool CanAnimate()
        {
            Animator a = FindAnimator();
            if (a == null || !a.isActiveAndEnabled || a.runtimeAnimatorController == null) return false;
            if (!_paramsRead)
            {
                _paramsRead = true;
                _ours = false;
                foreach (AnimatorControllerParameter p in a.parameters) if (p.nameHash == Pace) _ours = true;
                _armsLayer = _ours ? a.GetLayerIndex(ArmsLayer) : -1;
                ReadNumbers();
            }
            return _ours;
        }

        /// <summary>True when the controller is ours and has this base-layer state (asking for a missing one warns).</summary>
        private bool Has(int state) => CanAnimate() && _animator.HasState(0, state);

        /// <summary>The player's measured clip numbers (Resources/PlayerBody, PlayerMoves), once.</summary>
        private void ReadNumbers()
        {
            if (_numbersRead) return;
            _numbersRead = true;
            PlayerBodySettings bodies = PlayerBodySettings.Load();
            PlayerBodySettings.Body clips = bodies != null ? bodies.At(0) : null;
            PlayerMoveSettings moves = PlayerMoveSettings.Load();
            _clipHeight = bodies != null && bodies.bodyHeight > 0.01f ? bodies.bodyHeight : FallbackClipHeight;
            _runClip = clips != null && clips.run > 0.01f ? clips.run : FallbackRunClip;
            _mostPace = bodies != null ? Mathf.Max(1f, bodies.maxPlayback) : FallbackMostPace;
            _hitPlayback = moves != null ? Mathf.Max(0.01f, moves.hitLightPlayback) : FallbackHitPlayback;
            _hitSeconds = (clips != null && clips.hitLight > 0f ? clips.hitLight : FallbackHitSeconds) / _hitPlayback;
            _hitLock = moves != null ? moves.hitLightLock : FallbackHitLock;
            _deathPlayback = moves != null ? Mathf.Max(0.01f, moves.deathPlayback) : FallbackDeathPlayback;
            _blend = moves != null ? moves.blend : FallbackBlend;
        }
    }
}
