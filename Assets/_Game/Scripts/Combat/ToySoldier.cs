using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A plastic toy soldier that guards a spot: walks its route (or wanders near its post), spots the player by line of
    /// sight inside a view cone, waits a forgiving reaction time, then turns and fires pooled projectiles until it has
    /// not seen the player for a while. Knocked out, it tips over and stands up again at its post after a pause.
    /// Aims at whatever stands for the player: the toy/kid, the first-person body, or the car being driven; at the part of
    /// it nearest its own chest height, so a kid towering over it is shot in the legs. Holds still while Build Mode is
    /// on, and wherever Build Mode puts it becomes its post. Speeds and distances are for a soldier at scale 1 and scale
    /// with its transform.
    /// Online (D-0131, CoopWorld) the host's soldiers are the real ones: they aim at the nearest player they can see,
    /// turn on whoever shoots them, and tell every other PC their place, shots, falls and getting up. On those PCs each
    /// soldier is a copy (UseMirror) that only shows the host's, and a hit on it goes to the host.
    /// In a team match (D-0182) the scene's match part (TeamMatch) puts it on the team of the half its post stands in and
    /// says whether it plays (JoinMatch, PutAway). In play it is a bot: it walks its Match Route instead of its own, goes
    /// after the nearest enemy it can see (the other team's people and bots in play, never its own team), sees and shoots
    /// with the match's numbers and wears its team's colour. Out of a match (warm-up, the house, single player) none of
    /// that applies and it is the practice soldier it always was.
    /// What happens to it is told as plain events (D-0187), on the host's soldier and on every copy alike: Hurt (hit and
    /// still standing), Fell (knocked down), Rose (up again at its post), Fired (a shot left), TeamChanged and
    /// AwayChanged. A living body beside it (SoldierBody) listens to them and sets two switches as it wakes: BodyFalls
    /// (the soldier no longer tips its Visual over: the body plays its own fall) and BodyDresses (the soldier no longer
    /// paints its renderers in its team's colour: the body wears its team's kit). Neither switch is set on a soldier
    /// without a body, so the plastic soldiers behave exactly as before.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Damageable))]
    public sealed class ToySoldier : MonoBehaviour, IResettable, IBuildMoved
    {
        private enum State { Patrol, Engage, Down }

        [Header("Patrol")]
        [SerializeField, Tooltip("Points to walk between, back and forth. Empty = wander near the post.")] private Transform[] route;
        [SerializeField, Tooltip("Wander radius around the post when there is no route, units at scale 1.")] private float wanderRadius = 4f;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField, Tooltip("Seconds it waits at each point.")] private float pause = 1.2f;
        [Header("Blinded by a focused light (D-0149)")]
        [SerializeField, Min(0f), Tooltip("Seconds it is blinded by Dazzle (a LightTarget's On Filled): it cannot see or shoot, stands still and sways.")] private float blindSeconds = 3f;
        [SerializeField, Tooltip("Shown over its head while blinded (a looping stun effect: Polygon Arsenal's Stun), sized with it.")] private GameObject dazzleEffect;
        [SerializeField, Tooltip("One of these plays as it is blinded (a startled voice, a sparkle).")] private AudioClip[] dazzleSounds;
        [SerializeField, Range(0f, 1f)] private float dazzleVolume = 0.8f;
        [SerializeField, Min(0.01f), Tooltip("The stun effect's size, as a share of its own (D-0159).")] private float dazzleSize = 0.6f;
        [SerializeField, Min(0f), Tooltip("How far above its head the stun effect floats, as a share of its height (D-0159).")] private float dazzleLift = 0.35f;

        [Header("Sight")]
        [SerializeField] private float sightRange = 18f;
        [SerializeField, Tooltip("Half-angle of the view cone, degrees. Once engaged it sees all round.")] private float viewAngle = 70f;
        [SerializeField, Tooltip("Seconds the player must stay seen before it reacts.")] private float reactionTime = 0.7f;
        [SerializeField, Tooltip("Seconds without sight before it gives up.")] private float loseSightTime = 2.5f;
        [SerializeField] private LayerMask sightMask = ~0;
        [Header("Fire")]
        [SerializeField] private Projectile projectile;
        [SerializeField, Tooltip("Where shots leave: the rifle tip. Chest height in front if empty.")] private Transform muzzle;
        [SerializeField] private float fireRange = 14f;
        [SerializeField] private float fireInterval = 1.1f;
        [SerializeField, Tooltip("Cone half-angle, degrees.")] private float spread = 3f;
        [SerializeField] private float damage = 10f;
        [SerializeField, Tooltip("Shots push the player this much, units per second at scale 1.")] private float knockback = 2f;
        [SerializeField] private float turnSpeed = 300f;
        [SerializeField] private AudioClip fireClip;
        [Header("Body")]
        [SerializeField, Tooltip("Tipped over on knock-out. The first child if empty.")] private Transform visual;
        [SerializeField] private float gravity = 25f;
        [SerializeField, Tooltip("Seconds lying down before it is back at its post.")] private float downTime = 4f;
        [SerializeField, Tooltip("Chest height, units at scale 1.")] private float chestHeight = 1.0f;
        [Header("A hit makes it flinch (D-0180)")]
        [SerializeField, Min(0f), Tooltip("Seconds after a hit before it may fire again. Each new hit starts the wait over, so under steady fire it never gets a shot off. 0 = hits do not hold its fire.")] private float flinchFireDelay = 0.45f;
        [SerializeField, Min(0f), Tooltip("Seconds it stops walking after a hit. It still turns to face whoever it is after. 0 = it keeps walking.")] private float flinchStopSeconds = 0.25f;
        [SerializeField, Min(0f), Tooltip("How far its body leans back, away from the shot, degrees. 0 = no lean.")] private float flinchDegrees = 10f;
        [SerializeField, Min(0f), Tooltip("Seconds the lean takes to ease back upright (it goes over in a twentieth of a second). 0 = no lean.")] private float flinchSeconds = 0.3f;
        [Header("As a bot in a team match (D-0182)")]
        [SerializeField, Min(0), Tooltip("Which of its team's soldiers plays first when a team needs bots: 0 first, then 1, and so on. Set by the scene's layout.")] private int matchOrder;
        [SerializeField, Tooltip("Points it walks between, there and back, while it plays in a team match (from its post toward the middle). Empty = its own Route or wander, as in practice. Not used outside a match.")] private Transform[] matchRoute;

        private const float PerceiveEvery = 0.15f;
        private const float AlertSeconds = 6f;   // a hit keeps it looking this long past its usual sight
        private float _alertUntil = -100f, _alertReach;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private CharacterController _cc;
        private Damageable _health;
        private AudioSource _audio;
        private PlayerMotor _player;
        private State _state;
        private float _blindUntil = float.NegativeInfinity;
        private bool _wasBlind;
        private float _holdFireUntil = float.NegativeInfinity, _standUntil = float.NegativeInfinity;   // a flinch (D-0180)

        /// <summary>Blinds it for <paramref name="seconds"/> (D-0149): no sight, no shots, it stands and sways.</summary>
        public void Blind(float seconds)
        {
            if (_state == State.Down || _away) return;
            bool fresh = Time.time >= _blindUntil;
            _blindUntil = Mathf.Max(_blindUntil, Time.time + seconds);
            _wasBlind = true;
            if (!fresh) return;
            // What shows and sounds (D-0153): the stun effect over its head, a startled cry.
            if (dazzleEffect != null && _dazzleFx == null)
            {
                Bounds b = Drawn();
                // Its own turn kept (the effect's ring lies flat by it; a plain upright turn stood it on its edge), smaller,
                // higher above the head (D-0159).
                _dazzleFx = Instantiate(dazzleEffect, new Vector3(b.center.x, b.max.y + dazzleLift * b.size.y, b.center.z), dazzleEffect.transform.rotation, transform);
                _dazzleFx.transform.localScale = dazzleEffect.transform.localScale * dazzleSize;   // under the soldier: its size with it
            }
            if (dazzleSounds != null && dazzleSounds.Length > 0)
            {
                AudioClip clip = dazzleSounds[Random.Range(0, dazzleSounds.Length)];
                if (clip != null) PaintSplats.InScene().Play(clip, transform.position, dazzleVolume);
            }
        }

        private GameObject _dazzleFx;

        /// <summary>Its drawn body's box (for where the stun effect sits). A living body tells its height instead
        /// (ShownHeight, D-0187): its renderers' boxes are its culling boxes, far bigger than the body.</summary>
        private Bounds Drawn()
        {
            if (ShownHeight > 0f)
            {
                float tall = ShownHeight * Scale;
                return new Bounds(transform.position + Vector3.up * (0.5f * tall), new Vector3(0f, tall, 0f));
            }
            var rs = GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(transform.position + Vector3.up * Scale, Vector3.zero);
            bool any = false;
            foreach (var r in rs) { if (r is ParticleSystemRenderer) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        private void EndDazzle()
        {
            if (_dazzleFx != null) Destroy(_dazzleFx);
            _dazzleFx = null;
        }

        /// <summary>Blinds it for its own Blind Seconds: wire a LightTarget's On Filled here (focus, then shoot).</summary>
        public void Dazzle()
        {
            if (_state != State.Down && Time.time >= _blindUntil) Toasts.Once("dazzle", "He can't see you for a few seconds: slip past or tag him.", ToastKind.Tip, "Dazzled!");   // the first time a save sees it (D-0162)
            Blind(blindSeconds);
        }

        /// <summary>True while blinded.</summary>
        public bool Blinded => Time.time < _blindUntil;
        private Vector3 _post, _wanderTo;
        private Quaternion _postRot, _visualRest;
        private int _routeIndex, _routeStep = 1;
        private float _stuckFor, _vertical, _seenFor, _lastSeen = -100f, _nextLook, _nextFire, _waitUntil, _downAt;
        private bool _sees;
        private NetPlayer _other;      // online host (D-0131): another player it is after; null = this PC's own player
        private NetPlayer _shotBy;     // online host: the other player who shot it last; null = this PC's own player
        private bool _mirror;          // online, not the host: a copy that only shows the host's soldier
        private bool _netPose;         // a copy has heard where the host's soldier stands
        private Vector3 _netPos;
        private float _netYaw;

        private float Scale => Mathf.Max(0.001f, transform.lossyScale.y);

        /// <summary>Its number in the party's world, the same on every PC (CoopWorld); -1 offline.</summary>
        public int NetId { get; set; } = -1;
        /// <summary>True while it lies knocked down.</summary>
        public bool Down => _state == State.Down;
        /// <summary>True while it is put away for a team match (D-0182, PutAway).</summary>
        public bool Away => _away;

        private bool _away;

        // ---------------------------------------------------------------- what a living body hears (D-0187)

        /// <summary>Hit and still standing: the moment it flinches (on a copy, the moment the shooter's hit is sent to the
        /// host). The hit as it was dealt. Not raised for the hit that knocks it down (Fell is), nor while it lies down.</summary>
        public event System.Action<DamageInfo> Hurt;
        /// <summary>Knocked down: it lies there until Rose. True = it already lay there when this PC arrived (a copy told
        /// so by the host), so the fall is not shown, only the lying.</summary>
        public event System.Action<bool> Fell;
        /// <summary>Standing again at its post, whole. True = after lying its Down Time (the get-up is worth showing);
        /// false = put back at once (a reset, put away or back, the match's end, a copy told to stand), even when it was
        /// not down.</summary>
        public event System.Action<bool> Rose;
        /// <summary>A shot left: where from and which way (world). On a copy, the host's shot as it is shown here.</summary>
        public event System.Action<Vector3, Vector3> Fired;
        /// <summary>Its team changed (TeamMatch.Blue, TeamMatch.Orange, or TeamMatch.NoTeam when it left the match).</summary>
        public event System.Action<int> TeamChanged;
        /// <summary>Put away (true) or back (false), after it has been stood at its post and its renderers and colliders
        /// switched.</summary>
        public event System.Action<bool> AwayChanged;

        /// <summary>Set by a living body as it wakes (D-0187): true = the soldier never tips its Visual over to fall (the
        /// body plays its own fall and get-up). The blinded sway, the flinch lean and the squash still move the Visual.
        /// False on every soldier without a body: it falls as it always did.</summary>
        public bool BodyFalls { get; set; }
        /// <summary>Set by a living body as it wakes (D-0187): true = the soldier never swaps its renderers' materials for
        /// its team's colour (the body wears its team's kit). False on every soldier without a body.</summary>
        public bool BodyDresses { get; set; }
        /// <summary>Set by a living body as it wakes (D-0187): how tall it stands, in the soldier's units at scale 1 (the
        /// stun effect floats over it). 0 = measured from what it draws, as always.</summary>
        public float ShownHeight { get; set; }
        /// <summary>Set by a living body as it wakes (D-0187): seconds its get-up takes to show. After lying its Down
        /// Time the soldier neither walks nor fires for that long, so its feet do not slide under a body still getting
        /// up. 0 on every soldier without a body: it is on its way at once, as always.</summary>
        public float GetUpSeconds { get; set; }

        /// <summary>
        /// Put away for a team match, or back (D-0182). Put away it stays an active object (CoopWorld numbers only the
        /// active soldiers, on every PC, and a friend arriving numbers whatever is active then), but it is unseen (its
        /// renderers off), untouchable (its colliders off, and on the host it takes no damage) and does nothing: no sight,
        /// no shots, no walking, no falls or shots shown from the host. It first stands up at its post, whole. Back, it is
        /// at its post, whole, seen and solid again (a copy on another PC goes to wherever the host's next word puts it).
        /// Each PC puts its own soldiers away (TeamMatch reads the match's phase); the host's word on them is not needed.
        /// </summary>
        public void PutAway(bool away)
        {
            if (_away == away || _cc == null) return;   // not awake yet: the match asks again as it changes
            _away = away;
            if (away)
            {
                if (_mirror) StandAsCopy(); else ResetState();   // whole at its post (its capsule stays off: ResetState reads Away)
                _health.DamageScale = 0f;   // a hit that still reaches the host's (a copy shot before its PC heard) does nothing
            }
            else _health.DamageScale = 1f;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                if (c != _cc) c.enabled = !away;   // its walking capsule follows its state below (and ResetState); the hit shapes come back with it
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = away;
            if (!away) { if (_mirror) { StandAsCopy(); _netPose = false; } else ResetState(); }   // a copy: the host's next word puts it there at once
            else _cc.enabled = false;
            AwayChanged?.Invoke(away);   // D-0187
        }

        /// <summary>A copy standing as the host's does: up, not leaning, its capsule on unless it is put away (its place
        /// comes with the host's next word). <paramref name="getUp"/>: it got up after lying down (Rose, D-0187).</summary>
        private void StandAsCopy(bool getUp = false)
        {
            _state = State.Patrol;
            EndFlinch();
            if (visual != null) visual.localRotation = _visualRest;
            _cc.enabled = !_away;
            Rose?.Invoke(getUp);   // D-0187
        }

        // ---------------------------------------------------------------- a bot in a team match (D-0182)

        /// <summary>Raised wherever a soldier is knocked down by a hit it takes (the host's soldiers online; copies never
        /// take hits): the team match counts a bot's fall on the host.</summary>
        public static event System.Action<ToySoldier> KnockedDown;

        // Run-time team copies of the soldiers' materials, one per material and team, made once and shared by every
        // soldier (the assets themselves are never changed). Forgotten with the other statics on a new play.
        private static readonly Dictionary<(Material own, int team), Material> TeamCopies = new Dictionary<(Material own, int team), Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), MainTexId = Shader.PropertyToID("_MainTex");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() { KnockedDown = null; TeamCopies.Clear(); }   // the editor may enter Play without reloading scripts

        private int _team;                                              // 0 outside a match
        private float _matchSight, _matchFire, _matchReaction, _matchDamage;   // the match's numbers while it plays
        private ToySoldier _otherBot, _shotByBot;                       // in play on the host: an enemy bot it is after / that shot it last
        // What it wore before it put its team's colour on: each renderer dressed, how many materials it has, and those
        // materials in turn. Plain fields, so a script reload in Play keeps them and the match's end still undresses it
        // (an array that was empty may come back empty rather than null: every use tests the lengths).
        private Renderer[] _dressed;
        private int[] _dressCounts;
        private Material[] _ownMaterials;
        private int _dressedTeam;

        /// <summary>Its team in a team match (TeamMatch.Blue, TeamMatch.Orange), from the countdown until warm-up returns;
        /// 0 at any other time.</summary>
        public int Team => _team;
        /// <summary>True while it plays as a bot: on a team, not put away, and play is on. (The phase is asked too, so
        /// a soldier the match has not reached again, after a script reload, say, is never a bot outside play.)</summary>
        public bool InPlay => Fielded && TeamMatch.Phase == MatchPhase.Playing;
        /// <summary>On a team and not put away: what the match last told it.</summary>
        private bool Fielded => _team != TeamMatch.NoTeam && !_away;
        /// <summary>Which of its team's soldiers plays first (0 first), as the layout set it.</summary>
        public int MatchOrder => matchOrder;
        /// <summary>Where it stands guard and gets up again: the same on every PC (a copy's post is the scene's).</summary>
        public Vector3 Post => _post;

        private float SightRange => InPlay ? _matchSight : sightRange;
        private float FireRange => InPlay ? _matchFire : fireRange;
        private float ReactionTime => InPlay ? _matchReaction : reactionTime;
        private float Damage => InPlay ? _matchDamage : damage;
        private Transform[] Path => InPlay && matchRoute != null && matchRoute.Length > 0 ? matchRoute : route;

        /// <summary>
        /// On <paramref name="team"/> for a team match (D-0182), with the match's numbers for while it plays: how far it
        /// sees and fires (units at scale 1), how long it waits before it reacts (seconds) and its darts' damage. It
        /// wears its team's colour from now on (unseen while it is put away). Whether it plays is PutAway's to say.
        /// </summary>
        public void JoinMatch(int team, float sight, float fire, float reaction, float hurt)
        {
            if (team == TeamMatch.NoTeam) { LeaveMatch(); return; }
            _matchSight = sight; _matchFire = fire; _matchReaction = reaction; _matchDamage = hurt;
            if (_team == team) return;
            _team = team;
            _otherBot = _shotByBot = null;
            Dress(team);
            TeamChanged?.Invoke(team);   // D-0187
        }

        /// <summary>Out of the match: back to its own colour and numbers. One that was playing stands at its post, whole
        /// (a copy: up, its place from the host's next word), so it is exactly the practice soldier it was.</summary>
        public void LeaveMatch()
        {
            bool playing = Fielded;
            bool had = _team != TeamMatch.NoTeam;
            _team = TeamMatch.NoTeam;
            _otherBot = _shotByBot = null;
            Undress();
            if (had) TeamChanged?.Invoke(TeamMatch.NoTeam);   // D-0187
            if (!playing || _cc == null) return;
            if (_mirror) StandAsCopy(); else ResetState();
        }

        /// <summary>Its drawn parts put on the team's colour: plain plastic in it (the colour, no texture), on run-time
        /// copies of their materials. A hit still flashes over it (Damageable's flash overrides the colour a moment and
        /// lets go), and Undress puts the soldier's own materials back. Not with a living body (BodyDresses, D-0187): it
        /// wears its team's kit itself.</summary>
        private void Dress(int team)
        {
            if (BodyDresses) return;
            if (_dressedTeam == team) return;
            Undress();
            var dressed = new List<Renderer>();
            var counts = new List<int>();
            var own = new List<Material>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;   // its body only: no stun effect, no streak
                Material[] mats = r.sharedMaterials;
                if (mats.Length == 0) continue;
                var worn = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++) worn[i] = TeamCopy(mats[i], team);
                dressed.Add(r); counts.Add(mats.Length); own.AddRange(mats);
                r.sharedMaterials = worn;
            }
            _dressed = dressed.ToArray(); _dressCounts = counts.ToArray(); _ownMaterials = own.ToArray();
            _dressedTeam = team;
        }

        /// <summary>Back in its own materials, if it wore a team's.</summary>
        private void Undress()
        {
            if (_dressed != null && _dressCounts != null && _ownMaterials != null)
            {
                int k = 0;
                for (int i = 0; i < _dressed.Length && i < _dressCounts.Length; i++)
                {
                    int n = _dressCounts[i];
                    if (_dressed[i] != null && n > 0 && k + n <= _ownMaterials.Length)
                    {
                        var mats = new Material[n];
                        System.Array.Copy(_ownMaterials, k, mats, 0, n);
                        _dressed[i].sharedMaterials = mats;
                    }
                    k += n;
                }
            }
            _dressed = null; _dressCounts = null; _ownMaterials = null;
            _dressedTeam = TeamMatch.NoTeam;
        }

        /// <summary>The team's copy of <paramref name="own"/>, made once and kept (null stays null).</summary>
        private static Material TeamCopy(Material own, int team)
        {
            if (own == null) return null;
            if (TeamCopies.TryGetValue((own, team), out Material copy) && copy != null) return copy;
            copy = new Material(own) { name = own.name + " (" + TeamMatch.Name(team) + ")" };
            Color colour = TeamMatch.Colour(team);
            if (copy.HasProperty(BaseColorId)) copy.SetColor(BaseColorId, colour);
            if (copy.HasProperty(ColorId)) copy.SetColor(ColorId, colour);
            if (copy.HasProperty(BaseMapId)) copy.SetTexture(BaseMapId, null);
            if (copy.HasProperty(MainTexId)) copy.SetTexture(MainTexId, null);
            TeamCopies[(own, team)] = copy;
            return copy;
        }

        /// <summary>True when <paramref name="other"/> is an enemy bot to go after: in play, standing, on the other team.</summary>
        private bool IsFoe(ToySoldier other) =>
            other != null && other != this && other.InPlay && !other.Down && other._team != _team;

        /// <summary>True when <paramref name="player"/> is on the other team (whether they can be hit is AimAt's to say).</summary>
        private bool IsFoe(NetPlayer player)
        {
            if (player == null) return false;
            int theirs = TeamMatch.TeamOf(player.OwnerClientId);
            return theirs != TeamMatch.NoTeam && theirs != _team;
        }

        /// <summary>True when this PC's own player is on the other team and not knocked out.</summary>
        private bool OwnIsFoe()
        {
            int mine = TeamMatch.Mine;
            if (mine == TeamMatch.NoTeam || mine == _team) return false;
            foreach (NetPlayer p in NetPlayer.Spawned)
                if (p != null && p.IsSpawned && p.IsOwner) return !p.Down;
            return false;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Damageable>();
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) _visualRest = visual.localRotation;
            _post = transform.position; _postRot = transform.rotation; _wanderTo = _post;
            _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.spatialBlend = 1f;
        }

        private void OnEnable() { _health.Died += OnDied; _health.Damaged += OnHit; }
        private void OnDisable() { _health.Died -= OnDied; _health.Damaged -= OnHit; }

        /// <summary>Shot (D-0100): it engages at once, turns on whoever did it and sees all round, and for a while it sees and
        /// fires as far as the shot came from. Before, a toy shooting from past its sight knocked it down unnoticed.</summary>
        private void OnHit(DamageInfo hit)
        {
            if (_state == State.Down || hit.Source == null || hit.Source.IsChildOf(transform)) return;
            // A friend's stray dart is not a call to arms; in a team match an enemy bot's is (D-0182: a teammate's never
            // gets here, Damageable.Spares turns it away).
            ToySoldier bot = hit.Source.GetComponentInParent<ToySoldier>();
            if (bot != null && !(InPlay && IsFoe(bot))) return;
            if (CoopWorld.IsHost)
            {
                // Online host: it turns on whoever shot it, another player too (their hit came here from their PC).
                var shooter = hit.Source.GetComponentInParent<NetPlayer>();
                _other = _shotBy = shooter != null && !shooter.IsOwner ? shooter : null;
            }
            _otherBot = _shotByBot = bot;   // null but in a match, shot by an enemy bot
            float reach = Vector3.Distance(hit.Source.position, transform.position) + 2f * Scale;
            _alertReach = Alerted ? Mathf.Max(_alertReach, reach) : reach;
            _alertUntil = Time.time + AlertSeconds;
            _state = State.Engage;
            _sees = true; _lastSeen = Time.time; _seenFor = ReactionTime;
            _nextLook = Time.time + PerceiveEvery;
            if (!_health.Dead) Flinched(hit);   // the last hit knocks it down instead
        }

        /// <summary>A hit makes it flinch (D-0180): its next shot waits (each hit starts the wait over, a shot is never
        /// brought forward), it stops walking a moment and its body jolts back, away from the shot.</summary>
        private void Flinched(in DamageInfo hit)
        {
            if (flinchFireDelay > 0f) _holdFireUntil = Mathf.Max(_holdFireUntil, Time.time + flinchFireDelay);
            if (flinchStopSeconds > 0f) _standUntil = Mathf.Max(_standUntil, Time.time + flinchStopSeconds);
            Jolt(hit);
        }

        /// <summary>Only the lean: its body tips back, away from where the hit came from. A living body hears of it
        /// first (Hurt, D-0187), whether or not there is a lean to show.</summary>
        private void Jolt(in DamageInfo hit)
        {
            Hurt?.Invoke(hit);
            if (visual == null || flinchDegrees <= 0f || flinchSeconds <= 0f) return;
            // The shot's shove says which way it travelled; with no shove (a gun without knockback, a grenade), away
            // from whoever dealt it; with neither, straight back.
            Vector3 away = hit.Knock;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f && hit.Source != null) { away = transform.position - hit.Source.position; away.y = 0f; }
            if (away.sqrMagnitude < 1e-6f) away = -transform.forward;
            Flinch.Hit(visual, away, flinchDegrees, flinchSeconds);
        }

        /// <summary>No flinch left: it may fire and walk, its body back in its own pose (a fall, a reset, Build Mode).</summary>
        private void EndFlinch()
        {
            _holdFireUntil = _standUntil = float.NegativeInfinity;
            if (visual != null) Flinch.Stop(visual);
        }

        private bool Alerted => Time.time < _alertUntil;
        private float SightReach => Alerted ? Mathf.Max(SightRange * Scale, _alertReach) : SightRange * Scale;
        private float FireReach => Alerted ? Mathf.Max(FireRange * Scale, _alertReach) : FireRange * Scale;

        public void ResetState() => StandAtPost(false);

        /// <summary>Whole and standing at its post. <paramref name="getUp"/>: it got up after lying its Down Time (Rose,
        /// D-0187).</summary>
        private void StandAtPost(bool getUp)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(_post, _postRot);
            _cc.enabled = !_away;   // put away for a match (D-0182): it stays untouchable
            _health.ResetState();
            _blindUntil = float.NegativeInfinity; _wasBlind = false; EndDazzle();   // a reset clears a blinding (D-0149)
            EndFlinch();
            if (visual != null) visual.localRotation = _visualRest;
            _state = State.Patrol; _routeIndex = 0; _routeStep = 1; _wanderTo = _post;
            _vertical = 0f; _seenFor = 0f; _lastSeen = -100f; _sees = false; _waitUntil = 0f;
            _alertUntil = -100f; _alertReach = 0f;
            _other = _shotBy = null;
            _otherBot = _shotByBot = null;
            // A living body's get-up plays from now (D-0187): it holds its feet and its fire until it is up.
            if (getUp && GetUpSeconds > 0f) _standUntil = _holdFireUntil = Time.time + GetUpSeconds;
            Rose?.Invoke(getUp);   // D-0187
        }

        /// <summary>Build Mode placed, moved, copied or re-posed us: stand guard here from now on (F9 and getting up after a
        /// knock-out come back here, a copy no longer walks to the original's spot).</summary>
        public void OnBuildMoved()
        {
            _post = transform.position; _postRot = transform.rotation; _wanderTo = _post;
            _routeIndex = 0; _routeStep = 1; _waitUntil = 0f; _stuckFor = 0f; _vertical = 0f;
            EndFlinch();
        }

        private void OnDied(DamageInfo hit)
        {
            _state = State.Down; _downAt = Time.time; _sees = false; _seenFor = 0f;
            _cc.enabled = false;
            EndFlinch();   // it tips over from its own pose, not from a lean
            KnockDown.Show(transform, hit, visual, true);   // a burst of bits, a bigger squash and the thud
            CoopWorld.SoldierDown(this, hit.Point);         // online host: it falls on every PC (D-0131)
            KnockedDown?.Invoke(this);                      // a bot's fall is a point in a team match (D-0182, TeamMatch)
            Fell?.Invoke(false);                            // a living body plays its fall (D-0187)
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _away) return;   // put away for a team match (D-0182): it does nothing
            if (_mirror) { MirrorUpdate(dt); return; }   // online, not the host: it only shows the host's soldier
            if (GameMode.Current == GameMode.Mode.Build) { EndFlinch(); return; }   // held still while the room is edited, or a drag jitters
            if (_state == State.Down)
            {
                Topple();
                if (Time.time - _downAt >= downTime) { StandAtPost(true); GameEvents.RaiseSoldierUp(this); CoopWorld.SoldierUp(this); }
                return;
            }

            if (Time.time < _blindUntil)
            {
                // Blinded (D-0149): it cannot see or shoot; it sways where it stands, hands to its eyes in spirit.
                _sees = false; _seenFor = 0f; _state = State.Patrol;
                if (visual != null)
                {
                    float t = Time.time * 9f;
                    visual.localRotation = _visualRest * Quaternion.Euler(Mathf.Sin(t) * 7f, 0f, Mathf.Sin(t * 1.3f) * 7f);
                }
                if (_cc.isGrounded && _vertical < 0f) _vertical = -1f * Scale;
                _vertical -= gravity * Scale * dt;
                _cc.Move(Vector3.up * _vertical * dt);
                return;
            }
            if (_wasBlind) { _wasBlind = false; if (visual != null) visual.localRotation = _visualRest; EndDazzle(); }

            Perceive(dt);
            Vector3 move = Vector3.zero, face = Vector3.zero;
            if (_state == State.Engage)
            {
                if (TryTarget(out Vector3 aim, out _)) { face = aim - transform.position; TryFire(aim); }
            }
            else
            {
                move = Time.time < _standUntil ? Vector3.zero : PatrolVelocity();   // a flinch stops it a moment (D-0180)
                face = move;
            }

            if (_cc.isGrounded && _vertical < 0f) _vertical = -1f * Scale;
            _vertical -= gravity * Scale * dt;
            _cc.Move((move + Vector3.up * _vertical) * dt);
            face.y = 0f;
            if (face.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), turnSpeed * dt);
        }

        private void Topple()
        {
            if (visual == null || BodyFalls) return;   // a living body falls by its own clip (D-0187)
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _downAt) / 0.3f));
            visual.localRotation = Quaternion.Slerp(_visualRest, Quaternion.Euler(-90f, 0f, 0f) * _visualRest, t);
        }

        private void Perceive(float dt)
        {
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + PerceiveEvery;
                _sees = CanSee();
                if (_sees) _lastSeen = Time.time;
            }
            if (_sees) _seenFor += dt; else if (_state != State.Engage) _seenFor = 0f;
            if (_state == State.Patrol && _seenFor >= ReactionTime) _state = State.Engage;
            else if (_state == State.Engage && Time.time - _lastSeen >= loseSightTime) { _state = State.Patrol; _seenFor = 0f; }
        }

        /// <summary>The player it is after and the point to aim at: online on the host (D-0131) another player when
        /// CanSee chose one (_other), else this PC's own (TryOwnTarget).</summary>
        private bool TryTarget(out Vector3 aim, out Transform root)
        {
            if (_otherBot != null)
            {
                // In play (D-0182): an enemy bot CanSee chose, while it stands and plays.
                if (InPlay && IsFoe(_otherBot) && AimAtBot(_otherBot, out aim, out root)) return true;
                _otherBot = null;
            }
            if (_other != null)
            {
                if (CoopWorld.IsHost && (!InPlay || IsFoe(_other)) && AimAt(_other, out aim, out root)) return true;
                _other = null;   // gone, knocked out, or the party is over: back to this PC's own player
            }
            if (InPlay && !OwnIsFoe()) { aim = Vector3.zero; root = null; return false; }   // a teammate, or down: never a target
            return TryOwnTarget(out aim, out root);
        }

        /// <summary>In play (D-0182): the point to aim at on an enemy bot, as AimAt aims at a player: between its legs and
        /// its chest, nearest our own chest height.</summary>
        private bool AimAtBot(ToySoldier bot, out Vector3 aim, out Transform root)
        {
            root = bot.transform;
            float chest = bot.chestHeight * bot.Scale;
            float ours = transform.position.y + chestHeight * Scale - root.position.y;
            aim = root.position + Vector3.up * Mathf.Clamp(ours, Mathf.Min(0.5f * Scale, chest), chest);
            return true;
        }

        /// <summary>This PC's own player's stand-in and the point to aim at: first-person body, the car being driven, or the player.
        /// On foot the aim is the point between its legs and its chest nearest our own chest height: a toy on our floor is
        /// aimed at the chest as before, and the kid is shot in the legs (its chest is far out of range above us).</summary>
        private bool TryOwnTarget(out Vector3 aim, out Transform root)
        {
            aim = Vector3.zero; root = null;
            if (GameMode.Current == GameMode.Mode.Build) return false;   // the builder is not a target
            float chest;   // above the target's feet
            var fp = FirstPersonMode.Active;
            if (fp != null && fp.Body != null && fp.Body.isActiveAndEnabled)
            {
                root = fp.Body.transform;
                chest = fp.Body.Centre.y - root.position.y;
            }
            else
            {
                if (_player == null)
                {
                    var own = GameObject.Find("Player");   // online, this PC's own player (others' copies are "Player 2"...): D-0114
                    _player = own != null ? own.GetComponent<PlayerMotor>() : null;
                    if (_player == null) _player = FindFirstObjectByType<PlayerMotor>();
                }
                if (_player == null || !_player.gameObject.activeInHierarchy) return false;
                if (!_player.enabled && _player.transform.parent != null)
                {
                    // Seated: aim at the car.
                    root = _player.transform.parent;
                    aim = root.position + Vector3.up * (0.6f * root.lossyScale.y);
                    return true;
                }
                root = _player.transform;
                chest = 0.6f * 1.8f * _player.Scale;
            }
            float ours = transform.position.y + chestHeight * Scale - root.position.y;
            float legs = Mathf.Min(0.5f * Scale, chest);
            aim = root.position + Vector3.up * Mathf.Clamp(ours, legs, chest);
            return true;
        }

        /// <summary>Online host (D-0131): the point to aim at on another player's copy here, as TryOwnTarget aims at our
        /// own: the car while they drive, else between their legs and chest nearest our chest height. False while they
        /// are knocked out (as their own PC says).</summary>
        private bool AimAt(NetPlayer player, out Vector3 aim, out Transform root)
        {
            aim = Vector3.zero; root = null;
            if (player == null || !player.IsSpawned || player.Down || !player.gameObject.activeInHierarchy) return false;
            root = player.transform;
            float size = Mathf.Max(0.001f, root.lossyScale.y);   // the network sizes their body with their shift
            if (player.Seated) { aim = root.position + Vector3.up * (0.6f * size); return true; }
            float chest = 0.6f * 1.8f * size;
            float ours = transform.position.y + chestHeight * Scale - root.position.y;
            aim = root.position + Vector3.up * Mathf.Clamp(ours, Mathf.Min(0.5f * Scale, chest), chest);
            return true;
        }

        private Vector3 Eye => muzzle != null ? muzzle.position : transform.position + Vector3.up * (chestHeight * Scale) + transform.forward * (0.3f * Scale);

        /// <summary>Whether it sees the player it is after. Online on the host (D-0131) it looks at every player: shot at
        /// lately, whoever did it comes first while it can see them (D-0100's "turns on whoever did it"); otherwise it goes
        /// after the nearest one it sees. Seeing nobody, it stays after the last.</summary>
        private bool CanSee()
        {
            if (InPlay) return CanSeeInPlay();
            if (!CoopWorld.IsHost)
            {
                _other = null;
                return TryOwnTarget(out Vector3 aim, out Transform root) && Sees(aim, root, out _);
            }
            Vector3 a; Transform r; float d;
            if (Alerted && (_shotBy != null ? AimAt(_shotBy, out a, out r) : TryOwnTarget(out a, out r)) && Sees(a, r, out _))
            {
                _other = _shotBy;
                return true;
            }
            bool any = false; NetPlayer best = null; float nearest = float.MaxValue;
            if (TryOwnTarget(out a, out r) && Sees(a, r, out d)) { any = true; nearest = d; }
            foreach (NetPlayer player in NetPlayer.Spawned)
            {
                if (player == null || player.IsOwner || !AimAt(player, out a, out r) || !Sees(a, r, out d) || d >= nearest) continue;
                any = true; best = player; nearest = d;
            }
            if (any) _other = best;
            return any;
        }

        /// <summary>CanSee for a bot in play (D-0182): only enemies count, people (this PC's own player and, on the host,
        /// the others) and the other team's bots in play, never its own team or a put-away soldier. Shot at lately,
        /// whoever did it comes first while it can see them; otherwise the nearest enemy it sees. Seeing nobody, it
        /// stays after the last.</summary>
        private bool CanSeeInPlay()
        {
            Vector3 a; Transform r; float d;
            if (Alerted)
            {
                bool seen;
                if (_shotByBot != null) seen = IsFoe(_shotByBot) && AimAtBot(_shotByBot, out a, out r) && Sees(a, r, out _);
                else if (_shotBy != null) seen = CoopWorld.IsHost && IsFoe(_shotBy) && AimAt(_shotBy, out a, out r) && Sees(a, r, out _);
                else seen = OwnIsFoe() && TryOwnTarget(out a, out r) && Sees(a, r, out _);
                if (seen) { _other = _shotBy; _otherBot = _shotByBot; return true; }
            }
            bool any = false; NetPlayer best = null; ToySoldier bestBot = null; float nearest = float.MaxValue;
            if (OwnIsFoe() && TryOwnTarget(out a, out r) && Sees(a, r, out d)) { any = true; nearest = d; }
            if (CoopWorld.IsHost)
                foreach (NetPlayer player in NetPlayer.Spawned)
                {
                    if (player == null || player.IsOwner || !IsFoe(player) || !AimAt(player, out a, out r) || !Sees(a, r, out d) || d >= nearest) continue;
                    any = true; best = player; nearest = d;
                }
            IReadOnlyList<ToySoldier> bots = TeamMatch.Soldiers;
            for (int i = 0; i < bots.Count; i++)
            {
                ToySoldier bot = bots[i];
                if (!IsFoe(bot) || !AimAtBot(bot, out a, out r) || !Sees(a, r, out d) || d >= nearest) continue;
                any = true; best = null; bestBot = bot; nearest = d;
            }
            if (any) { _other = best; _otherBot = bestBot; }
            return any;
        }

        /// <summary>True when <paramref name="aim"/> on <paramref name="root"/> is in range, in the view cone (all round
        /// once engaged) and not behind anything. <paramref name="distance"/>: how far away it is.</summary>
        private bool Sees(Vector3 aim, Transform root, out float distance)
        {
            Vector3 from = transform.position + Vector3.up * (chestHeight * Scale);
            Vector3 to = aim - from;
            distance = to.magnitude;
            if (distance > SightReach) return false;
            if (_state != State.Engage && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(to, Vector3.up)) > viewAngle) return false;
            if (distance < 1e-3f) return true;
            int n = Physics.RaycastNonAlloc(from, to / distance, _hits, distance, Layers.Seen(sightMask), QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; Transform first = null;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (_hits[i].distance < nearest) { nearest = _hits[i].distance; first = _hits[i].transform; }
            }
            return first == null || first.IsChildOf(root);
        }

        private void TryFire(Vector3 aim)
        {
            if (!_sees || projectile == null || Time.time < _nextFire || Time.time < _holdFireUntil) return;   // a flinch holds its fire (D-0180)
            Vector3 from = Eye;
            Vector3 to = aim - from;
            if (to.magnitude > FireReach) return;
            // Facing is judged from the body, not the muzzle: a rifle tip out to the side and in front would read a close
            // target as off to one side and never fire.
            Vector3 flat = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up);
            if (flat.sqrMagnitude > 1e-6f && Vector3.Angle(transform.forward, flat) > 20f) return;   // still turning
            _nextFire = Time.time + fireInterval * Random.Range(0.85f, 1.15f);
            Vector3 dir = to.normalized;
            if (spread > 0f)
            {
                Vector3 axis = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
                axis = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * axis;
                dir = Quaternion.AngleAxis(spread * Mathf.Sqrt(Random.value), axis) * dir;
            }
            Projectile.Fire(projectile, from, dir, Damage, knockback, Scale, transform);
            CoopWorld.SoldierShot(this, from, dir);   // online host: a harmless copy of the dart on every other PC (D-0131)
            PlayFire();
            Fired?.Invoke(from, dir);                 // D-0187
        }

        /// <summary>Its own clip, else its dart's (the player's dart pistol sound): the course's soldiers were built with none and fired silently.</summary>
        private void PlayFire()
        {
            AudioClip clip = fireClip != null ? fireClip : projectile.FireClip();
            if (clip != null) { _audio.pitch = Random.Range(0.9f, 1.05f); _audio.PlayOneShot(clip); }
        }

        private Vector3 PatrolVelocity()
        {
            if (Time.time < _waitUntil) return Vector3.zero;
            Transform[] way = Path;   // its own route; its Match Route while it plays in a team match (D-0182)
            if (way != null && way.Length > 0 && _routeIndex >= way.Length) { _routeIndex = 0; _routeStep = 1; }   // a route of another length than the last
            Vector3 goal;
            if (way != null && way.Length > 0 && way[_routeIndex] != null) goal = way[_routeIndex].position;
            else goal = _wanderTo;
            Vector3 offset = goal - transform.position; offset.y = 0f;
            if (offset.magnitude <= 0.3f * Scale)
            {
                _waitUntil = Time.time + pause;
                if (way != null && way.Length > 1)
                {
                    int next = _routeIndex + _routeStep;
                    if (next < 0 || next >= way.Length) { _routeStep = -_routeStep; next = _routeIndex + _routeStep; }
                    _routeIndex = next;
                }
                else
                {
                    Vector2 r = Random.insideUnitCircle * (wanderRadius * Scale);
                    _wanderTo = _post + new Vector3(r.x, 0f, r.y);
                }
                return Vector3.zero;
            }
            // Walking into a wall: give up on this point after a moment and pick the next one.
            Vector3 v = _cc.velocity; v.y = 0f;
            _stuckFor = v.magnitude < 0.25f * moveSpeed * Scale ? _stuckFor + Time.deltaTime : 0f;
            if (_stuckFor > 1.5f)
            {
                _stuckFor = 0f;
                Vector2 r = Random.insideUnitCircle * (wanderRadius * Scale);
                _wanderTo = _post + new Vector3(r.x, 0f, r.y);
                if (way != null && way.Length > 1) { _routeStep = -_routeStep; _routeIndex = Mathf.Clamp(_routeIndex + _routeStep, 0, way.Length - 1); }
            }
            return offset.normalized * (moveSpeed * Scale);
        }

        // ---------------------------------------------------------------- online: a copy of the host's soldier (D-0131)

        /// <summary>Online, on every PC but the host's: from now on this soldier only shows the host's. It thinks and fires
        /// nothing of its own; it stands where the host's stands (MirrorPose), falls and gets up when it does, and a hit
        /// on it goes to the host (<paramref name="relay"/>: the copy only flashes, and jolts back at once so the shooter
        /// sees the hit land: D-0180; holding its fire and its feet is the host's soldier's to do).</summary>
        public void UseMirror(System.Func<DamageInfo, bool> relay)
        {
            _mirror = true;
            _netPose = false;
            _sees = false; _seenFor = 0f; _other = null;
            EndFlinch();
            _health.Relay = relay == null ? null : hit =>
            {
                if (!relay(hit)) return false;
                if (_mirror && _state != State.Down) Jolt(hit);
                return true;
            };
        }

        /// <summary>Back to a soldier of this PC's own (the host's world turned out not to match ours): at its post, whole.</summary>
        public void LeaveMirror()
        {
            if (!_mirror) return;
            _mirror = false;
            _health.Relay = null;
            ResetState();
        }

        /// <summary>Where the host's soldier stands and which way it faces. The copy follows a tenth of a second behind;
        /// the first word, or a jump (back at its post after a fall), puts it there at once.</summary>
        public void MirrorPose(Vector3 at, float yaw)
        {
            if (!_mirror) return;
            bool jump = !_netPose || (at - transform.position).sqrMagnitude > 4f * Scale * Scale;
            _netPos = at; _netYaw = yaw; _netPose = true;
            if (jump) transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>The host's soldier was knocked down: it tips over here too, with the burst and the thud
        /// (<paramref name="quiet"/>: it already lay there when we arrived, so it simply lies there).</summary>
        public void MirrorDown(Vector3 point, bool quiet)
        {
            if (!_mirror || _away || _state == State.Down) return;   // put away (D-0182): nothing shown
            _state = State.Down;
            _downAt = quiet ? Time.time - 1f : Time.time;
            _cc.enabled = false;   // darts pass over it, as over the host's
            EndFlinch();
            if (!quiet) KnockDown.Show(transform, new DamageInfo(0f, Vector3.zero, point, null), visual, true);
            Fell?.Invoke(quiet);   // D-0187
        }

        /// <summary>The host's soldier got up (its place at its post comes with the next MirrorPose).</summary>
        public void MirrorUp()
        {
            if (!_mirror) return;
            StandAsCopy(_state == State.Down);   // a get-up worth showing only if it lay here (D-0187)
        }

        /// <summary>The host's soldier fired: a harmless copy of its dart flies here, with its sound. The real dart is the
        /// host's: if it hits this PC's player, the hit comes from the host.</summary>
        public void MirrorShot(Vector3 from, Vector3 direction)
        {
            if (!_mirror || _away || projectile == null) return;
            Projectile.Fire(projectile, from, direction, 0f, 0f, Scale, transform);
            PlayFire();
            Fired?.Invoke(from, direction);   // D-0187
        }

        private void MirrorUpdate(float dt)
        {
            if (_state == State.Down) Topple();
            if (!_netPose) return;
            float k = 1f - Mathf.Exp(-12f * dt);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, _netPos, k),
                Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _netYaw, 0f), k));
        }
    }
}
