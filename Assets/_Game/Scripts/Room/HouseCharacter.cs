using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A funny character who lives in the house (D-0185): it stands and idles, now and then looks around, notices the
    /// nearest player, turns its body to them and its head a little more, waves the first time someone comes close, and
    /// says its next line when a player presses E on it. Sits on the root of Prefabs/Characters/HouseCharacter (a variant
    /// of FunnyBody), next to the <see cref="CharacterBody"/> that dresses it; its look comes from that body's seed.
    ///
    /// The Animator (on the body's rig, controller Animation/HouseCharacter, made by HouseCharacterPieces) has the states
    /// Idle, LookAround, Wave and Talk, the last three started by triggers of the same names. A state whose clip was
    /// missing when the controller was made is not there, and its trigger is simply never pulled: the character still
    /// stands, turns and talks with Idle alone, and with no controller at all it stands still in its saved pose.
    ///
    /// Every PC runs its own copy and nothing is sent: it is scenery that answers, so online each PC sees it turn to the
    /// player nearest on that PC's screen. It needs no player, no controller and no parts list to work; it does nothing
    /// that allocates each frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterBody), typeof(CapsuleCollider))]
    public sealed class HouseCharacter : MonoBehaviour, IInteractable
    {
        /// <summary>The controller's resting state.</summary>
        public const string IdleState = "Idle";
        /// <summary>The trigger (and state) of the look round it does now and then.</summary>
        public const string LookAroundTrigger = "LookAround";
        /// <summary>The trigger (and state) of its greeting wave.</summary>
        public const string WaveTrigger = "Wave";
        /// <summary>The trigger (and state) of its talking gesture.</summary>
        public const string TalkTrigger = "Talk";

        private static readonly int IdleHash = Animator.StringToHash(IdleState);
        private static readonly int LookAroundHash = Animator.StringToHash(LookAroundTrigger);
        private static readonly int WaveHash = Animator.StringToHash(WaveTrigger);
        private static readonly int TalkHash = Animator.StringToHash(TalkTrigger);

        private const string PlayerName = "Player";        // this PC's own player (others' copies online are "Player 2"...: D-0114)
        private const float UnknownPlayerHeight = 1.8f;    // the Player prefab's capsule, for a player with no CharacterController
        private const float OwnSearchEvery = 1f;            // seconds between searches for this PC's player while there is none
        private const float Tiny = 1e-6f;                   // a squared length below this is no direction at all
        private const float MinScanGap = 0.02f;             // the shortest wait between two looks for players, whatever Notice Every says
        private const float SameFacing = 0.01f;             // degrees: closer than this to where it wants to face, the body is left alone
        private const float MinCapsuleRadius = 0.01f;       // a capsule is never thinner than this

        [Header("Words (placeholder wording: yours to change)")]
        [SerializeField, Tooltip("Its name, shown as the title of what it says. Placeholder wording.")]
        private string characterName = "Toy";
        [SerializeField, Tooltip("What it says, one line each time a player presses E, in order, starting again after the last. No lines = nothing to talk about: no prompt. Placeholder wording.")]
        private string[] lines = { "Hello." };
        [SerializeField, Tooltip("The words next to the E key while a player can talk to it. Placeholder wording.")]
        private string prompt = "Talk";

        [Header("Noticing players")]
        [SerializeField, Tooltip("How near a player must be for it to notice them and turn to them, in its own body's units (a character scaled up notices further). Measured to the edge of the player's body.")]
        private float noticeRange = 9f;
        [SerializeField, Tooltip("How near a player must come for it to wave at them, in its own body's units.")]
        private float greetRange = 5f;
        [SerializeField, Tooltip("Seconds after a wave before it waves again (at someone coming near again).")]
        private float greetAgainSeconds = 30f;
        [SerializeField, Tooltip("Seconds with nobody near before it turns back to the way it was placed. Also how long it keeps facing someone who talked to it.")]
        private float forgetSeconds = 4f;
        [SerializeField, Tooltip("Seconds between its looks for the nearest player (it turns smoothly in between).")]
        private float noticeEvery = 0.25f;

        [Header("Turning")]
        [SerializeField, Tooltip("How fast its body turns to a player, or back, in degrees a second.")]
        private float turnSpeed = 120f;
        [SerializeField, Tooltip("How far its head turns left or right past its body to look at someone, in degrees.")]
        private float headYawLimit = 60f;
        [SerializeField, Tooltip("How far its head tips up or down to look at someone, in degrees.")]
        private float headPitchLimit = 25f;
        [SerializeField, Range(0f, 1f), Tooltip("The share of the head's turn the neck takes; the head takes the rest.")]
        private float neckShare = 0.35f;
        [SerializeField, Tooltip("How quickly the head eases toward where it looks (and back): higher is snappier.")]
        private float lookEase = 5f;
        [SerializeField, Tooltip("Where on a player it looks, as a share of their height from their feet (0.9 = about the eyes).")]
        private float lookAtHeight = 0.9f;

        [Header("Idling")]
        [SerializeField, Tooltip("The shortest wait, in seconds, between two look-rounds while nobody is near.")]
        private float lookAroundMin = 6f;
        [SerializeField, Tooltip("The longest wait, in seconds, between two look-rounds while nobody is near.")]
        private float lookAroundMax = 14f;

        [Header("Body")]
        [SerializeField, Tooltip("The radius of its solid capsule, in its own body's units. The capsule's height is fitted to what it wears.")]
        private float capsuleRadius = 0.3f;

        // State that must outlive a script reload in Play (plain private fields are carried over by the reload).
        private Quaternion _restRotation;
        private bool _restSet;
        private bool _waved;
        private float _lastWave;
        private bool _wasNear;
        private float _lastSeen;
        private float _nextLookAround;
        private int _nextLine;

        // Caches, found again on first use after a script reload.
        [NonSerialized] private CharacterBody _body;
        [NonSerialized] private Animator _animator;
        [NonSerialized] private Transform _head;
        [NonSerialized] private Transform _neck;
        [NonSerialized] private bool _found;
        [NonSerialized] private bool _paramsRead;
        [NonSerialized] private bool _hasLookAround;
        [NonSerialized] private bool _hasWave;
        [NonSerialized] private bool _hasTalk;
        [NonSerialized] private Transform _own;
        [NonSerialized] private float _nextOwnSearch;
        [NonSerialized] private float _nextScan;
        [NonSerialized] private Transform _target;
        [NonSerialized] private float _targetHeight;
        [NonSerialized] private Transform _talker;
        [NonSerialized] private float _talkHoldUntil;
        [NonSerialized] private float _yaw;
        [NonSerialized] private float _pitch;
        [NonSerialized] private bool _wrote;
        [NonSerialized] private Quaternion _headPose;
        [NonSerialized] private Quaternion _neckPose;
        [NonSerialized] private Quaternion _headWritten;
        [NonSerialized] private Quaternion _neckWritten;

        /// <summary>Its name, shown as the title of what it says.</summary>
        public string CharacterName => characterName ?? "";

        /// <summary>The player it is turned to now, or null.</summary>
        public Transform Target => _target;

        private float Scale => Mathf.Abs(transform.lossyScale.y);

        private void Awake()
        {
            if (!_restSet)
            {
                _restRotation = transform.rotation;
                _restSet = true;
            }
            FitCapsule();
            ScheduleLookAround(Time.time);
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= _nextScan)
            {
                _nextScan = now + Mathf.Max(MinScanGap, noticeEvery);
                Scan(now);
            }
            TurnBody(Time.deltaTime);
            LookAroundNowAndThen(now);
        }

        private void LateUpdate()
        {
            if (!Find() || _head == null) return;
            TurnHead(Time.deltaTime);
        }

        // ---------- talking (IInteractable) ----------

        /// <summary>"Talk" (its Prompt field) while it has a line to say; nothing otherwise.</summary>
        public string PromptFor(Interactor who) => HasLines ? prompt : "";

        /// <summary>E: it says its next line to that player (on their screen only), plays its talking gesture and turns to
        /// them, keeping to them for Forget Seconds.</summary>
        public void Interact(Interactor who)
        {
            if (who == null) return;
            string line = NextLine();
            if (line == null) return;
            Toasts.ShowFor(who, line, ToastKind.Tip, string.IsNullOrWhiteSpace(characterName) ? null : characterName);
            Gesture(TalkHash);
            float now = Time.time;
            _talker = who.transform;
            _talkHoldUntil = now + forgetSeconds;
            Face(_talker, now);
        }

        /// <summary>Sets its name and lines (the editor's placing tool uses it; a game script may too).</summary>
        public void SetWords(string shownName, string[] newLines)
        {
            characterName = shownName ?? "";
            lines = newLines != null ? (string[])newLines.Clone() : new string[0];
            _nextLine = 0;
        }

        private bool HasLines
        {
            get
            {
                if (lines == null) return false;
                for (int i = 0; i < lines.Length; i++) if (!string.IsNullOrWhiteSpace(lines[i])) return true;
                return false;
            }
        }

        /// <summary>The next line that has words, moving on past it; null when there is none.</summary>
        private string NextLine()
        {
            if (lines == null || lines.Length == 0) return null;
            for (int tries = 0; tries < lines.Length; tries++)
            {
                int i = ((_nextLine % lines.Length) + lines.Length) % lines.Length;
                _nextLine = i + 1;
                if (!string.IsNullOrWhiteSpace(lines[i])) return lines[i];
            }
            return null;
        }

        // ---------- noticing ----------

        /// <summary>Finds the nearest player within Notice Range, keeps facing someone who just talked to it, forgets a
        /// player gone for Forget Seconds, and waves at someone coming within Greet Range.</summary>
        private void Scan(float now)
        {
            float scale = Scale;
            Transform nearest = Nearest(noticeRange * scale, out float distance, out float height);

            if (_talker != null && now < _talkHoldUntil && _talker.gameObject.activeInHierarchy) Face(_talker, now);
            else
            {
                _talker = null;
                if (nearest != null)
                {
                    _target = nearest;
                    _targetHeight = height;
                    _lastSeen = now;
                }
                else if (_target != null && (now - _lastSeen > forgetSeconds || !_target.gameObject.activeInHierarchy)) _target = null;
            }

            bool near = nearest != null && distance <= greetRange * scale;
            if (near && !_wasNear && (!_waved || now - _lastWave >= greetAgainSeconds) && Gesture(WaveHash))
            {
                _waved = true;
                _lastWave = now;
            }
            _wasNear = near;
        }

        /// <summary>Turns to <paramref name="who"/> from now on (until forgotten).</summary>
        private void Face(Transform who, float now)
        {
            if (who == null) return;
            _target = who;
            Measure(who, out _, out _targetHeight);
            _lastSeen = now;
        }

        /// <summary>The nearest player on this PC within <paramref name="range"/> (world units): this PC's own player
        /// (the object named Player, as ToySoldier finds it) and, online, every other player's copy here
        /// (NetPlayer.Spawned). Null when there is none.</summary>
        private Transform Nearest(float range, out float distance, out float height)
        {
            Transform best = null;
            distance = range;
            height = 0f;
            Transform own = Own();
            Consider(own, ref best, ref distance, ref height);
            var others = NetPlayer.Spawned;
            for (int i = 0; i < others.Count; i++)
            {
                NetPlayer player = others[i];
                if (player == null) continue;
                Transform t = player.transform;
                if (t == own) continue;   // online this PC's own player is in the list too
                Consider(t, ref best, ref distance, ref height);
            }
            return best;
        }

        private void Consider(Transform player, ref Transform best, ref float bestDistance, ref float bestHeight)
        {
            if (player == null || !player.gameObject.activeInHierarchy) return;
            Measure(player, out float radius, out float tall);
            Vector3 to = player.position - transform.position;
            float up = to.y;
            to.y = 0f;
            float across = Mathf.Max(0f, to.magnitude - radius);   // to the edge of their body: a giant beside it is beside it
            float d = Mathf.Sqrt(across * across + up * up);
            if (d > bestDistance) return;
            best = player;
            bestDistance = d;
            bestHeight = tall;
        }

        /// <summary>A player's radius and height in the world, from their CharacterController (their size included).</summary>
        private static void Measure(Transform player, out float radius, out float height)
        {
            Vector3 size = player.lossyScale;
            if (player.TryGetComponent(out CharacterController cc))
            {
                radius = cc.radius * Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z));
                height = cc.height * Mathf.Abs(size.y);
                return;
            }
            radius = 0f;
            height = UnknownPlayerHeight * Mathf.Abs(size.y);
        }

        /// <summary>This PC's own player, found by name now and then while there is none.</summary>
        private Transform Own()
        {
            if (_own != null) return _own;
            float now = Time.time;
            if (now < _nextOwnSearch) return null;
            _nextOwnSearch = now + OwnSearchEvery;
            GameObject found = GameObject.Find(PlayerName);
            _own = found != null ? found.transform : null;
            return _own;
        }

        // ---------- turning ----------

        /// <summary>The whole body turns (yaw only) to the player it notices, or back to the way it was placed.</summary>
        private void TurnBody(float dt)
        {
            if (!_restSet)
            {
                // Only if a script reload did not carry the placed facing over: the facing of the moment stands in for it.
                _restRotation = transform.rotation;
                _restSet = true;
            }
            Quaternion want = _restRotation;
            if (_target != null)
            {
                Vector3 to = _target.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > Tiny) want = Quaternion.LookRotation(to, Vector3.up);
            }
            Quaternion now = transform.rotation;
            if (Quaternion.Angle(now, want) > SameFacing) transform.rotation = Quaternion.RotateTowards(now, want, turnSpeed * dt);
        }

        /// <summary>The head (and by Neck Share the neck) turned toward the player, ADDED over the pose the Animator wrote
        /// this frame. When it wrote none (off screen, no controller) the pose it wrote last is used, so the turn never
        /// adds up from frame to frame.</summary>
        private void TurnHead(float dt)
        {
            // 1. the pose under the turn
            Quaternion head = _head.localRotation;
            if (_wrote && head == _headWritten) head = _headPose;   // nobody posed it since our turn: our base is still the base
            else _headPose = head;
            _head.localRotation = _headPose;
            if (_neck != null)
            {
                Quaternion neck = _neck.localRotation;
                if (_wrote && neck == _neckWritten) neck = _neckPose;
                else _neckPose = neck;
                _neck.localRotation = _neckPose;
            }

            // 2. where it wants to look, eased
            float wantYaw = 0f, wantPitch = 0f;
            if (_target != null)
            {
                Vector3 to = _target.position + Vector3.up * (_targetHeight * lookAtHeight) - _head.position;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                Vector3 facing = transform.forward;
                facing.y = 0f;
                if (flat.sqrMagnitude > Tiny && facing.sqrMagnitude > Tiny)
                {
                    wantYaw = Mathf.Clamp(Vector3.SignedAngle(facing, flat, Vector3.up), -headYawLimit, headYawLimit);
                    wantPitch = Mathf.Clamp(Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -headPitchLimit, headPitchLimit);
                }
            }
            float k = 1f - Mathf.Exp(-Mathf.Max(0f, lookEase) * dt);
            _yaw = Mathf.Lerp(_yaw, wantYaw, k);
            _pitch = Mathf.Lerp(_pitch, wantPitch, k);

            // 3. the turn over the pose: the neck its share, the head the rest
            float neckPart = _neck != null ? Mathf.Clamp01(neckShare) : 0f;
            Vector3 right = transform.right;
            if (_neck != null) Twist(_neck, _yaw * neckPart, _pitch * neckPart, right);
            Vector3 headRight = Quaternion.AngleAxis(_yaw * neckPart, Vector3.up) * right;   // the neck's turn turned the head's own right
            Twist(_head, _yaw * (1f - neckPart), _pitch * (1f - neckPart), headRight);

            _headWritten = _head.localRotation;
            if (_neck != null) _neckWritten = _neck.localRotation;
            _wrote = true;
        }

        /// <summary>Turns a bone in the world: up by <paramref name="pitch"/> about <paramref name="right"/>, then by
        /// <paramref name="yaw"/> about the world's up.</summary>
        private static void Twist(Transform bone, float yaw, float pitch, Vector3 right)
        {
            bone.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(-pitch, right) * bone.rotation;
        }

        // ---------- the Animator ----------

        /// <summary>Now and then, while nobody is near and it stands idle, it looks round (each character on its own clock).</summary>
        private void LookAroundNowAndThen(float now)
        {
            if (now < _nextLookAround) return;
            ScheduleLookAround(now);
            if (_target != null || !IdleNow()) return;
            Gesture(LookAroundHash);
        }

        private void ScheduleLookAround(float now)
        {
            float low = Mathf.Max(0f, Mathf.Min(lookAroundMin, lookAroundMax));
            float high = Mathf.Max(lookAroundMin, lookAroundMax);
            _nextLookAround = now + UnityEngine.Random.Range(low, high);
        }

        /// <summary>True when the Animator stands in Idle, not blending.</summary>
        private bool IdleNow()
        {
            if (!CanAnimate()) return false;
            return _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == IdleHash && !_animator.IsInTransition(0);
        }

        /// <summary>Pulls one of its triggers, if its controller has it. A wave or a talk calls off a look-round still
        /// waiting. False when nothing could be played.</summary>
        private bool Gesture(int trigger)
        {
            if (!CanAnimate()) return false;
            bool has = trigger == WaveHash ? _hasWave : trigger == TalkHash ? _hasTalk : trigger == LookAroundHash && _hasLookAround;
            if (!has) return false;
            if (trigger != LookAroundHash && _hasLookAround) _animator.ResetTrigger(LookAroundHash);
            _animator.SetTrigger(trigger);
            return true;
        }

        /// <summary>True when there is an Animator with a controller, running; reads which triggers it has, once.</summary>
        private bool CanAnimate()
        {
            if (!Find() || _animator == null || _animator.runtimeAnimatorController == null || !_animator.isActiveAndEnabled || !_animator.isInitialized) return false;
            if (!_paramsRead)
            {
                _paramsRead = true;
                foreach (AnimatorControllerParameter p in _animator.parameters)   // once: the array is made once
                {
                    if (p.type != AnimatorControllerParameterType.Trigger) continue;
                    if (p.nameHash == LookAroundHash) _hasLookAround = true;
                    else if (p.nameHash == WaveHash) _hasWave = true;
                    else if (p.nameHash == TalkHash) _hasTalk = true;
                }
            }
            return true;
        }

        /// <summary>The body, its Animator and the head and neck bones, found once. False with no body.</summary>
        private bool Find()
        {
            if (_found) return _body != null;
            _found = true;
            if (!TryGetComponent(out _body)) return false;
            _animator = _body.Animator;
            _head = _body.Bone(HumanBodyBones.Head);
            _neck = _body.Bone(HumanBodyBones.Neck);
            return true;
        }

        // ---------- the body ----------

        /// <summary>Fits the solid capsule to what it wears: from the soles to the top of its tallest part
        /// (CharacterBody.Height), Capsule Radius wide. Left as it is when the height is not known (no parts list and no
        /// meshes: a copy of the game without the pack).</summary>
        public void FitCapsule()
        {
            if (!TryGetComponent(out CapsuleCollider capsule) || !TryGetComponent(out CharacterBody body)) return;
            float scale = Scale;
            if (scale <= Tiny) return;
            float tall = body.Height / scale;
            if (tall <= 0f) return;
            float radius = Mathf.Clamp(capsuleRadius, MinCapsuleRadius, tall * 0.5f);
            capsule.isTrigger = false;
            capsule.direction = 1;   // up
            capsule.radius = radius;
            capsule.height = tall;
            capsule.center = new Vector3(0f, tall * 0.5f, 0f);
        }

        private void OnDrawGizmosSelected()
        {
            float scale = Scale;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, noticeRange * scale);
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, greetRange * scale);
        }
    }
}
