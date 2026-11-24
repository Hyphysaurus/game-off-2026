using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A toy soldier's mind in a team match (D-0188), a part beside ToySoldier. While its soldier plays in a match on the
    /// host and the scene's floor is mapped (BotGround), the soldier asks it once a frame where to go (a velocity on the
    /// ground) and which way to face, and it fires through the soldier's Fire. In every other case (warm-up, the house, a
    /// copy on another PC, a scene with no map) it is never asked and the soldier is exactly the practice soldier it was.
    /// The soldier keeps its senses (who it is after, whether it sees them), its body and everything a shot does; the
    /// brain only decides, and never moves or turns the soldier itself.
    ///
    /// What it does, in this order of need:
    ///  - Dodge: a live grenade's blast would reach it, and it noticed (Dodge Chance, rolled once a grenade): after
    ///    Dodge Delay it runs straight out of the blast over the floor.
    ///  - Fall back: under fire with its health under Fall Back Below, it runs to the nearest spot round it (of Cover
    ///    Tries) from which the threat cannot see it, else to its own landing; holds there facing the threat for Hold
    ///    Seconds (shooting what comes into view), mends once out of fire (Heal After, Heal Per Second) and goes back
    ///    out when mended or the hold is over.
    ///  - Fight: it sees its quarry. It closes in from past Keep Range's far end, backs off from inside its near end and
    ///    otherwise strafes across the line (Strafe Share of a fight, changing sides every Strafe Seconds, at once on an
    ///    edge or a wall). It faces its aim and fires bursts (BotAim: lead, a settling error). It never fires with a
    ///    teammate in the line: it sidesteps.
    ///  - Hunt: its quarry went out of sight. It goes to where it last saw it (a little ahead along its way), looks
    ///    about for the rest of Search Seconds, then gives up.
    ///  - Advance: no enemy known. Down its lane (its Match Route, point by point by path), then to goals picked by
    ///    chance (the other team's landing, the end of a teammate's lane, the middle), to a teammate's fight within Help
    ///    Range first; at each goal it stops and looks left and right for Look Seconds.
    /// Always: it walks at its level's Move Speed (times its size), eases into a new direction over Turn Seconds, keeps
    /// Personal Space from other soldiers (meeting one head on, both keep to their right), checks the step ahead is
    /// floor before any move off its way, and when it makes no headway for Stuck Seconds it asks for a new way and
    /// sidesteps; three times on one goal and it picks another.
    /// It thinks about ten times a second (staggered between bots) and steers every frame; its paths and its corners are
    /// kept (BotPath): no garbage a frame. Every timer is a time on the clock, so a gap in the asking of any length
    /// (knocked down, blinded, Build Mode) leaves nothing half-counted; getting up (Rose), a new team or being put away
    /// or back clears its memory, and it starts from Advance with the level in use then (TeamMatch.Skill).
    /// When play ends it says one line of what it did through TeamMatch.Say (darts, fall-backs, dodges, times stuck, and
    /// its time in each state), for whoever reads the log after a play.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ToySoldier))]
    public sealed class BotBrain : MonoBehaviour
    {
        /// <summary>What it is doing (the order its seconds are counted in for the tally).</summary>
        private enum Mode { Advance, Fight, Hunt, FallBack, Dodge }
        private const int Modes = 5;
        private static readonly string[] ModeWords = { "advance", "fight", "hunt", "fall back", "dodge" };

        /// <summary>Advancing: down its lane, to a goal picked by chance, to a teammate's fight, or stopped to look about.</summary>
        private enum Errand { None, Lane, Goal, Help, Look }

        [Header("How it moves (the same at every level; the level's numbers are on Bot Skills)")]
        [SerializeField, Min(0.01f), Tooltip("Seconds it takes to ease into a new direction or speed, so its body never snaps round.")] private float turnSeconds = 0.25f;
        [SerializeField, Min(0f), Tooltip("It keeps this far from other soldiers in play, units at its own size. 0 = it does not.")] private float personalSpace = 1.2f;
        [SerializeField, Min(0f), Tooltip("How hard it steps away from a soldier inside its Personal Space, as a share of its Move Speed.")] private float spaceShove = 0.6f;
        [SerializeField, Min(0.1f), Tooltip("Seconds of no headway before it counts as stuck: it asks for a new way and sidesteps; stuck three times on one goal, it picks another.")] private float stuckSeconds = 0.8f;
        [SerializeField, Range(0.02f, 1f), Tooltip("Seconds between its thoughts (choosing what to do and where to go). It steers every frame.")] private float thinkEvery = 0.1f;
        [SerializeField, Min(0.05f), Tooltip("A corner of its way, or its goal, counts as reached this near, units at its own size.")] private float arriveWithin = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("How far ahead it checks for floor before a step off its way (strafing, backing off, sidestepping, dodging), units at its own size.")] private float floorAhead = 0.8f;
        [SerializeField, Min(0.05f), Tooltip("The floor ahead may be this far to one side of where it would step and still count (the map keeps 0.35 from walls), units at its own size.")] private float floorEdge = 0.45f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Moving slower than this share of the speed it wants counts as no headway.")] private float headwayShare = 0.3f;
        [Header("Changing its mind")]
        [SerializeField, Min(0f), Tooltip("Seconds a fight or a hunt lasts at least before it turns into something else, so it never flickers between two. A grenade or falling back does not wait.")] private float leastStateSeconds = 0.6f;
        [SerializeField, Min(0f), Tooltip("Seconds out of sight before a fight turns into a hunt (a quarry blinking past a post is not lost).")] private float lostSightSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("Seconds after a hit that it still counts as under fire: falling back needs a hit this recent.")] private float underFireSeconds = 2f;
        [SerializeField, Min(0.05f), Tooltip("Seconds before it tries again after finding no way to a teammate's fight or to its quarry.")] private float retrySeconds = 0.75f;
        [Header("Fighting")]
        [SerializeField, Range(0.1f, 1.5f), Tooltip("Its speed while it strafes or backs off in a fight, as a share of its Move Speed (it closes in at full speed).")] private float fightPace = 0.75f;
        [SerializeField, Min(0f), Tooltip("Seconds it sidesteps when stuck or when a teammate stands in its line of fire.")] private float sidestepSeconds = 0.6f;
        [SerializeField, Min(0.01f), Tooltip("A teammate this near the line of fire blocks it, units at its own size.")] private float lineOfFireRoom = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("While it closes in, its way is asked again once its quarry has moved this far from where the way leads, units at its own size.")] private float repathDistance = 1.5f;
        [SerializeField, Min(0.01f), Tooltip("Seconds over which its quarry's speed is smoothed (for leading its darts).")] private float speedSmoothing = 0.15f;
        [SerializeField, Min(1f), Tooltip("A quarry seen moving faster than this, units a second at its own size, jumped (a warp, a comeback, a car door): its speed is measured afresh.")] private float fastestQuarry = 30f;
        [SerializeField, Min(0f), Tooltip("How fast the direction of its aim's error wanders, roughly turns a second.")] private float aimDrift = 0.8f;
        [Header("Looking, hunting, goals")]
        [SerializeField, Range(0f, 90f), Tooltip("How far it looks left and right when it stops to look about, degrees.")] private float lookDegrees = 60f;
        [SerializeField, Min(0f), Tooltip("Hunting: it looks for its quarry this many seconds of the quarry's last speed ahead of where it last saw it.")] private float huntAheadSeconds = 1f;
        [SerializeField, Min(0f), Tooltip("Hunting: once it can see the spot it is going to from this near (units at its own size) and its quarry is not there, it stops and looks about.")] private float huntSeeWithin = 5f;
        [SerializeField, Min(0f), Tooltip("A goal it cannot walk to (the top of a box, off the map) is moved up to this far to find floor it can, units at its own size.")] private float goalSearch = 4f;
        [Header("Grenades")]
        [SerializeField, Range(0.5f, 2f), Tooltip("Its speed running from a grenade, as a share of its Move Speed.")] private float dodgePace = 1.25f;
        [SerializeField, Min(0f), Tooltip("It runs this far past a grenade's blast, units at its own size.")] private float dodgeMargin = 1f;
        [Header("Gaps in the asking")]
        [SerializeField, Min(0.05f), Tooltip("Seconds without being asked (knocked down, blinded, Build Mode) after which it starts its steering afresh: still, its quarry's speed forgotten, its way asked again.")] private float gapSeconds = 0.3f;

        private const int StuckTries = 3;         // stuck this many times on one goal, it picks another (the brief's rule)
        private const int Rolled = 4;             // grenades remembered: its dodge roll is once a grenade
        private const int GoalRings = 2;          // a goal it cannot reach is tried this many rings out (to Goal Search)...
        private const int GoalRingSpots = 8;      // ...at this many spots a ring
        private const int SpotTries = 4;          // spots picked by chance on the floor when no goal can be reached
        private const float HeadOn = 0.5f;        // a soldier within 60 degrees of where it walks is met head on
        private const float PlayerChest = 1.08f;  // a player's chest above their feet at size 1 (as ToySoldier aims)
        private const float DodgeEase = 0.5f;     // it eases into a dodge this much quicker than into other turns
        private const float FloorRise = 0.5f;     // a step whose floor is this much higher or lower (at its size) is not floor
        private const float LeastLook = 0.5f;     // seconds: a look about is never quicker than this, left and right
        private static readonly float[] DodgeTurns = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };   // straight away first

        private static readonly List<BotBrain> Brains = new List<BotBrain>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() { Brains.Clear(); Clock = null; }   // the editor may enter Play without reloading scripts

        /// <summary>The clock its timers read: the game's own. An editor check, out of Play where the game's clock stands
        /// still, may put its own here to run a brain through a stretch of time, and takes it away when it is done.</summary>
        internal static System.Func<float> Clock;
        private static float Now => Clock != null ? Clock() : Time.time;

        // Everything below is its memory: none of it is kept over a script reload (it starts again from Advance).
        [System.NonSerialized] private ToySoldier _soldier;
        [System.NonSerialized] private Damageable _health;
        [System.NonSerialized] private BotPath _path;
        [System.NonSerialized] private BotAim _aim;
        [System.NonSerialized] private System.Random _dice;
        [System.NonSerialized] private BotSkill _skill;
        [System.NonSerialized] private bool _begun;
        [System.NonSerialized] private Mode _mode;
        [System.NonSerialized] private float _modeSince, _nextThink, _lastDrive;
        [System.NonSerialized] private Vector3 _vel, _wanted, _lastPos;
        // advancing
        [System.NonSerialized] private Errand _errand;
        [System.NonSerialized] private int _lane, _stuckOnGoal;
        [System.NonSerialized] private float _lookBegan, _lookUntil, _helpRetryAt, _approachRetryAt;
        [System.NonSerialized] private Vector3 _lookFacing;
        [System.NonSerialized] private Vector3[] _goals;
        // what it saw last
        [System.NonSerialized] private bool _haveMemory;
        [System.NonSerialized] private Transform _lastQuarry;
        [System.NonSerialized] private float _lastSeenTime;
        [System.NonSerialized] private Vector3 _lastSeenRoot, _lastSeenVel;
        // fighting
        [System.NonSerialized] private int _strafeSide;
        [System.NonSerialized] private bool _strafeMoves;
        [System.NonSerialized] private float _strafeUntil, _sidestepUntil;
        [System.NonSerialized] private Vector3 _sidestep;
        // hunting
        [System.NonSerialized] private bool _huntLooking;
        [System.NonSerialized] private float _huntUntil;
        // falling back
        [System.NonSerialized] private float _lastHit, _fallBackBegan, _holdUntil;
        [System.NonSerialized] private bool _holding;
        [System.NonSerialized] private Vector3 _hitFrom, _threatAt;
        // dodging
        [System.NonSerialized] private Grenade[] _rolled;
        [System.NonSerialized] private bool[] _noticed;
        [System.NonSerialized] private float[] _noticedAt;
        [System.NonSerialized] private int _rollNext;
        [System.NonSerialized] private Vector3 _dodgeTo;
        // the tally
        [System.NonSerialized] private bool _tallying;
        [System.NonSerialized] private int _tallyTeam, _darts, _fallBacks, _dodges, _stuck;
        [System.NonSerialized] private float[] _seconds;

        private float Scale => Mathf.Max(0.001f, transform.lossyScale.y);

        /// <summary>True when the soldier should ask it: its soldier plays in a team match, this PC is the host, and the
        /// scene's floor is mapped. (The soldier never asks on a copy of the host's soldier, nor while it is down,
        /// blinded, put away or Build Mode is on.)</summary>
        public bool CanDrive => isActiveAndEnabled && _soldier != null && _soldier.InPlay && TeamMatch.IsHost && BotGround.Mapped;

        // ---------------------------------------------------------------- its life

        private void OnEnable()
        {
            if (!Brains.Contains(this)) Brains.Add(this);
            if (_soldier == null) TryGetComponent(out _soldier);
            if (_health == null) TryGetComponent(out _health);
            if (_dice == null) _dice = new System.Random(unchecked(GetInstanceID() * 7919 + System.Environment.TickCount));
            if (_path == null) _path = new BotPath();
            if (_aim == null) _aim = new BotAim((float)_dice.NextDouble() * 1000f);
            if (_goals == null || _goals.Length != 4) _goals = new Vector3[4];
            if (_rolled == null || _rolled.Length != Rolled) { _rolled = new Grenade[Rolled]; _noticed = new bool[Rolled]; _noticedAt = new float[Rolled]; }
            if (_seconds == null || _seconds.Length != Modes) _seconds = new float[Modes];
            if (_soldier != null)
            {
                _soldier.Rose += OnRose;
                _soldier.TeamChanged += OnTeamChanged;
                _soldier.AwayChanged += OnAwayChanged;
                _soldier.Hurt += OnHurt;
            }
            _begun = false;
        }

        private void OnDisable()
        {
            Brains.Remove(this);
            if (_tallying) SayTally();   // the scene goes (the party left) or the scripts reload: what it did so far
            if (_soldier == null) return;
            _soldier.Rose -= OnRose;
            _soldier.TeamChanged -= OnTeamChanged;
            _soldier.AwayChanged -= OnAwayChanged;
            _soldier.Hurt -= OnHurt;
        }

        // Play ended (the card, warm-up, the party gone): the tally is said once.
        private void Update()
        {
            if (_tallying && TeamMatch.Phase != MatchPhase.Playing) SayTally();
        }

        private void OnRose(bool getUp) => Restart();
        private void OnTeamChanged(int team) => Restart();
        private void OnAwayChanged(bool away) => Restart();

        /// <summary>Hit and still standing: under fire from where the shot came.</summary>
        private void OnHurt(DamageInfo hit)
        {
            _lastHit = Now;
            _hitFrom = hit.Source != null ? hit.Source.position : hit.Point;
        }

        /// <summary>Up at its post, on a new team, put away or back: its memory goes, and the next time it is asked it
        /// starts from Advance with the level in use then.</summary>
        private void Restart()
        {
            if (_tallying && TeamMatch.Phase != MatchPhase.Playing) SayTally();
            _begun = false;
        }

        private void Begin(float now, Vector3 at)
        {
            _begun = true;
            _skill = TeamMatch.Skill;
            _mode = Mode.Advance; _modeSince = now;
            _errand = Errand.None; _lane = 0; _stuckOnGoal = 0;
            _path.Clear(); _aim.Forget();
            _vel = _wanted = Vector3.zero; _lastPos = at;
            _haveMemory = false; _lastQuarry = null; _lastSeenTime = float.NegativeInfinity;
            _lastHit = _fallBackBegan = float.NegativeInfinity; _holding = false;
            _sidestepUntil = _strafeUntil = _helpRetryAt = _approachRetryAt = float.NegativeInfinity;
            _strafeSide = _dice.Next(2) == 0 ? -1 : 1;
            for (int i = 0; i < Rolled; i++) _rolled[i] = null;
            // Bots think in turn, not all in one frame.
            int slot = Mathf.Max(0, Brains.IndexOf(this));
            _nextThink = now + thinkEvery * slot / Mathf.Max(1, Brains.Count);
            if (!_tallying)
            {
                _tallying = true; _tallyTeam = _soldier.Team;
                _darts = _fallBacks = _dodges = _stuck = 0;
                for (int i = 0; i < Modes; i++) _seconds[i] = 0f;
            }
        }

        /// <summary>Asked again after a gap (knocked down, blinded, Build Mode): still, its quarry's speed and its aim's
        /// settling forgotten, its way asked again from where it stands, and a thought at once.</summary>
        private void Resume(float now, Vector3 at)
        {
            _vel = _wanted = Vector3.zero; _lastPos = at;
            _path.ResetHeadway();
            _aim.BreakSight();
            _sidestepUntil = float.NegativeInfinity;
            if (_path.Has) _path.Ask(at, _path.Goal);
            _nextThink = now;
        }

        // ---------------------------------------------------------------- what the soldier asks

        /// <summary>
        /// Once a frame from its soldier (only while CanDrive). <paramref name="quarry"/>: the root of whom it is after
        /// while engaged (a player's body, their car, an enemy soldier), else null; <paramref name="aim"/>: the point on
        /// them; <paramref name="seen"/>: whether it sees them now. Out: <paramref name="move"/>, a velocity on the
        /// ground in world units a second, and <paramref name="face"/>, the way to turn to (zero: stay).
        /// </summary>
        public void Drive(float dt, Transform quarry, Vector3 aim, bool seen, out Vector3 move, out Vector3 face)
        {
            float now = Now;
            Vector3 at = transform.position;
            float scale = Scale;
            if (!_begun) Begin(now, at);
            else if (now - _lastDrive > gapSeconds) Resume(now, at);
            Vector3 moved = at - _lastPos;
            _path.Track(moved, _wanted, dt, headwayShare);
            _lastPos = at; _lastDrive = now;

            Remember(quarry, aim, seen, now, dt, scale);
            if (now >= _nextThink) { _nextThink = now + thinkEvery; Think(now, at, quarry, seen, scale); }

            face = Vector3.zero;
            float speed = _skill.moveSpeed * scale, near = arriveWithin * scale;
            Vector3 want;
            switch (_mode)
            {
                case Mode.Dodge: want = SteerDodge(at, speed, near, scale); break;
                case Mode.FallBack: want = SteerFallBack(now, dt, at, quarry, aim, seen, speed, near, scale, ref face); break;
                case Mode.Fight: want = SteerFight(now, at, quarry, aim, seen, speed, near, scale, ref face); break;
                case Mode.Hunt: want = SteerHunt(now, at, speed, near, ref face); break;
                default: want = SteerAdvance(now, at, speed, near, scale, ref face); break;
            }
            if (_mode != Mode.Dodge && now < _sidestepUntil)
                want = FloorAhead(at, _sidestep, scale) ? _sidestep * (_mode == Mode.Fight ? speed * fightPace : speed) : Vector3.zero;
            want = KeepSpace(want, at, scale, speed);

            if (_soldier.FeetHeld) _vel = Vector3.zero;   // a flinch or a get-up: it eases in again after
            else
            {
                float ease = _mode == Mode.Dodge ? turnSeconds * DodgeEase : turnSeconds;
                _vel = Vector3.Lerp(_vel, want, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, ease)));
            }
            _vel.y = 0f;
            if (face.sqrMagnitude < 1e-6f) face = _vel;   // nothing to look at: it faces where it walks
            move = _vel;
            _wanted = _vel;
            _seconds[(int)_mode] += dt;
        }

        /// <summary>What it saw this frame: its quarry's speed (BotAim) and, while it sees it, where.</summary>
        private void Remember(Transform quarry, Vector3 aim, bool seen, float now, float dt, float scale)
        {
            _aim.Watch(quarry, seen, now, dt, speedSmoothing, fastestQuarry * scale);
            if (quarry == null || !seen) return;
            _haveMemory = true;
            _lastQuarry = quarry;
            _lastSeenTime = now;
            _lastSeenRoot = quarry.position;
            _lastSeenVel = _aim.Velocity;
        }

        // ---------------------------------------------------------------- thinking (about ten times a second)

        private void Think(float now, Vector3 at, Transform quarry, bool seen, float scale)
        {
            bool sees = quarry != null && seen;
            if (_path.NoHeadway >= stuckSeconds) Stuck(now, at, scale);

            // 1. A grenade it noticed: out of the blast, before everything else.
            if (Danger(now, at, scale, out Grenade bomb))
            {
                if (_mode != Mode.Dodge) { Enter(Mode.Dodge, now); _dodges++; }
                _dodgeTo = DodgeSpot(at, bomb, scale);
                return;
            }
            if (_mode == Mode.Dodge) Settle(now, at, sees, scale);

            // 2. Hurt and under fire: to cover.
            if (_mode != Mode.FallBack && ShouldFallBack(now)) { StartFallBack(now, at, scale); return; }

            switch (_mode)
            {
                case Mode.FallBack:
                    ThinkFallBack(now, at, sees, scale);
                    break;
                case Mode.Fight:
                    if (!sees && now - _lastSeenTime >= lostSightSeconds && now - _modeSince >= leastStateSeconds) StartHunt(now, at, scale);
                    else ThinkFight(now, at, scale);
                    break;
                case Mode.Hunt:
                    if (sees) Enter(Mode.Fight, now);
                    else ThinkHunt(now, at, scale);
                    break;
                default:
                    if (sees) Enter(Mode.Fight, now);
                    else if (quarry != null && Fresh(now) && now - _modeSince >= leastStateSeconds) StartHunt(now, at, scale);
                    else ThinkAdvance(now, at, scale);
                    break;
            }
        }

        /// <summary>True while what it saw last is recent enough to hunt (within Search Seconds).</summary>
        private bool Fresh(float now) => _haveMemory && now - _lastSeenTime < _skill.searchSeconds;

        private void Enter(Mode mode, float now)
        {
            _mode = mode; _modeSince = now;
            _path.Clear();
            _stuckOnGoal = 0;
            _holding = false; _huntLooking = false;
            if (mode == Mode.Advance) _errand = Errand.None;
            if (mode == Mode.Fight) NewStrafe(now);
        }

        /// <summary>A grenade or a fall-back is over: on to what fits now.</summary>
        private void Settle(float now, Vector3 at, bool sees, float scale)
        {
            if (sees) Enter(Mode.Fight, now);
            else if (Fresh(now)) StartHunt(now, at, scale);
            else Enter(Mode.Advance, now);
        }

        /// <summary>No headway for Stuck Seconds. In a fight it strafes the other way; otherwise a new way and a
        /// sidestep, and after three times on one goal another goal.</summary>
        private void Stuck(float now, Vector3 at, float scale)
        {
            _path.ResetHeadway();
            Vector3 heading = _wanted.sqrMagnitude > 1e-6f ? _wanted : transform.forward;
            if (_mode == Mode.Fight && !_path.Has) { NewStrafe(now); return; }   // a strafe into something: the other way
            if (_mode == Mode.Dodge) { StartSidestep(now, at, heading, scale); return; }
            _stuck++;
            if (++_stuckOnGoal >= StuckTries) { _stuckOnGoal = 0; GiveUpGoal(now, at); return; }
            if (_path.Has) _path.Ask(at, _path.Goal);
            StartSidestep(now, at, heading, scale);
        }

        /// <summary>Stuck three times on one goal: down the lane it passes that point over, an advance picks another
        /// goal, a hunt looks about where it is, a fall-back holds where it is, a fight stops closing in.</summary>
        private void GiveUpGoal(float now, Vector3 at)
        {
            _path.Clear();
            switch (_mode)
            {
                case Mode.Advance:
                    if (_errand == Errand.Lane) _lane++;
                    else _errand = Errand.None;
                    break;
                case Mode.Hunt: LookAbout(now, _lastSeenRoot - at); break;
                case Mode.FallBack: Hold(now); break;
                case Mode.Fight: _approachRetryAt = now + retrySeconds; break;
            }
        }

        // ---------------------------------------------------------------- advancing

        private void ThinkAdvance(float now, Vector3 at, float scale)
        {
            // A teammate's fight within Help Range comes first.
            if (_errand != Errand.Help && now >= _helpRetryAt && FindHelp(at, scale, out Vector3 foe))
            {
                if (TryWay(at, foe, scale)) { _errand = Errand.Help; return; }
                _helpRetryAt = now + retrySeconds;
            }
            switch (_errand)
            {
                case Errand.Look:
                    if (now < _lookUntil) return;
                    break;
                case Errand.Lane:
                case Errand.Goal:
                case Errand.Help:
                    if (_path.Has) return;
                    break;
            }
            // Somewhere to go: the rest of its lane, else a goal picked by chance; else it looks about and tries again.
            if (NextLanePoint(at, scale)) { _errand = Errand.Lane; return; }
            if (PickGoal(at, scale)) { _errand = Errand.Goal; return; }
            StartLook(now, transform.forward);
        }

        private Vector3 SteerAdvance(float now, Vector3 at, float speed, float near, float scale, ref Vector3 face)
        {
            if (_errand == Errand.Look) { face = LookFace(now); return Vector3.zero; }
            if (_errand == Errand.None || !_path.Has) return Vector3.zero;   // its next thought gives it somewhere to go
            if (_path.Arrived(at, near))
            {
                _path.Clear();
                // Down its lane without stopping at each point; at the lane's end, a goal or a teammate's fight, it looks about.
                if (_errand == Errand.Lane) { _lane++; if (NextLanePoint(at, scale)) return Toward(at, _path.Next(at, near)) * speed; }
                StartLook(now, transform.forward);
                return Vector3.zero;
            }
            return Toward(at, _path.Next(at, near)) * speed;
        }

        /// <summary>A way to the next point of its lane it has not reached; points it cannot walk to are passed over.
        /// False at the lane's end (or with no lane).</summary>
        private bool NextLanePoint(Vector3 at, float scale)
        {
            IReadOnlyList<Transform> lane = _soldier.MatchRoute;
            float near = arriveWithin * scale;
            while (_lane < lane.Count)
            {
                Transform point = lane[_lane];
                if (point == null || BotPath.Flat(at, point.position) <= near) { _lane++; continue; }
                if (TryWay(at, point.position, scale)) return true;
                _lane++;
            }
            return false;
        }

        /// <summary>A goal picked by chance and a way to it: with a lane, the other team's landing, the end of a
        /// teammate's lane or the middle of the two landings; without one, the landings or a spot on the floor. Goals it
        /// stands at or cannot reach are passed over; with none, a few spots on the floor by chance.</summary>
        private bool PickGoal(Vector3 at, float scale)
        {
            int team = _soldier.Team, n = 0;
            NetSpawnPoint theirs = NetSpawnPoint.Landing(TeamMatch.Other(team)), ours = NetSpawnPoint.Landing(team);
            if (theirs != null) _goals[n++] = theirs.transform.position;
            if (_soldier.MatchRoute.Count > 0)
            {
                if (TeammateLaneEnd(out Vector3 end)) _goals[n++] = end;
                if (theirs != null && ours != null) _goals[n++] = (theirs.transform.position + ours.transform.position) * 0.5f;
            }
            else
            {
                if (ours != null) _goals[n++] = ours.transform.position;
                if (RandomSpot(out Vector3 spot)) _goals[n++] = spot;
            }
            float there = goalSearch * scale;
            int first = n > 0 ? _dice.Next(n) : 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 goal = _goals[(first + i) % n];
                if (BotPath.Flat(at, goal) > there && TryWay(at, goal, scale)) return true;
            }
            for (int i = 0; i < SpotTries; i++)
                if (RandomSpot(out Vector3 spot) && BotPath.Flat(at, spot) > there && _path.Ask(at, spot)) return true;
            return false;
        }

        /// <summary>The last point of a teammate's lane (a teammate in play picked by chance), false with none.</summary>
        private bool TeammateLaneEnd(out Vector3 end)
        {
            end = Vector3.zero;
            IReadOnlyList<ToySoldier> all = TeamMatch.Soldiers;
            int count = 0;
            for (int i = 0; i < all.Count; i++) if (IsMate(all[i]) && LaneEnd(all[i]) != null) count++;
            if (count == 0) return false;
            int pick = _dice.Next(count);
            for (int i = 0; i < all.Count; i++)
            {
                if (!IsMate(all[i])) continue;
                Transform last = LaneEnd(all[i]);
                if (last == null || pick-- > 0) continue;
                end = last.position;
                return true;
            }
            return false;
        }

        private static Transform LaneEnd(ToySoldier s)
        {
            IReadOnlyList<Transform> lane = s.MatchRoute;
            for (int i = lane.Count - 1; i >= 0; i--) if (lane[i] != null) return lane[i];
            return null;
        }

        /// <summary>A spot on the mapped floor picked by chance inside the map's box (it may be one no walker reaches:
        /// asking for the way says).</summary>
        private bool RandomSpot(out Vector3 spot)
        {
            Bounds box = BotGround.Box;
            spot = box.center;
            if (box.size.x <= 0f || box.size.z <= 0f) return false;
            var at = new Vector3(Mathf.Lerp(box.min.x, box.max.x, (float)_dice.NextDouble()), box.center.y,
                                 Mathf.Lerp(box.min.z, box.max.z, (float)_dice.NextDouble()));
            return BotGround.OnFloor(at, box.extents.y + BotGround.Reach, out spot);
        }

        /// <summary>The nearest teammate bot within Help Range that is fighting, and where its enemy was last seen.</summary>
        private bool FindHelp(Vector3 at, float scale, out Vector3 foe)
        {
            foe = at;
            float best = _skill.helpRange * scale;
            bool found = false;
            for (int i = 0; i < Brains.Count; i++)
            {
                BotBrain b = Brains[i];
                if (b == null || b == this || !b._begun || b._mode != Mode.Fight || !IsMate(b._soldier)) continue;
                float d = BotPath.Flat(at, b.transform.position);
                if (d > best || BotPath.Flat(at, b._lastSeenRoot) <= goalSearch * scale) continue;   // out of range, or it is there already
                best = d; foe = b._lastSeenRoot; found = true;
            }
            return found;
        }

        private void StartLook(float now, Vector3 facing)
        {
            _errand = Errand.Look;
            _path.Clear();
            BeginLook(now, now + Roll(_skill.lookSeconds), facing);
        }

        private void BeginLook(float now, float until, Vector3 facing)
        {
            _lookBegan = now;
            _lookUntil = Mathf.Max(until, now + LeastLook);
            facing.y = 0f;
            _lookFacing = facing.sqrMagnitude > 1e-6f ? facing.normalized : FlatForward();
        }

        /// <summary>Looking about: once left and once right of where it faced as it stopped, over the look's time.</summary>
        private Vector3 LookFace(float now)
        {
            float span = Mathf.Max(LeastLook, _lookUntil - _lookBegan);
            float turn = lookDegrees * Mathf.Sin(2f * Mathf.PI * (now - _lookBegan) / span);
            return Quaternion.Euler(0f, turn, 0f) * _lookFacing;
        }

        // ---------------------------------------------------------------- fighting

        private void ThinkFight(float now, Vector3 at, float scale)
        {
            float distance = BotPath.Flat(at, _lastSeenRoot);
            if (distance > KeepFar(scale))
            {
                // Too far: closing in by a way (asked again as the quarry moves).
                bool stale = !_path.Has || BotPath.Flat(_path.Goal, _lastSeenRoot) > repathDistance * scale;
                if (stale && now >= _approachRetryAt && !TryWay(at, _lastSeenRoot, scale)) _approachRetryAt = now + retrySeconds;
            }
            else if (_path.Has) _path.Clear();
            if (now >= _strafeUntil) NewStrafe(now);
        }

        /// <summary>Keep Range's far end, world units, never past where it can fire (its fire reach as it uses it).</summary>
        private float KeepFar(float scale) => Mathf.Min(_skill.keepRange.y * scale, _soldier.FireReachNow);

        /// <summary>A new stretch of strafing: the other side, for Strafe Seconds, and whether it moves at all this time
        /// (Strafe Share).</summary>
        private void NewStrafe(float now)
        {
            _strafeUntil = now + Roll(_skill.strafeSeconds);
            _strafeSide = -_strafeSide;
            _strafeMoves = _dice.NextDouble() < _skill.strafeShare;
        }

        private Vector3 SteerFight(float now, Vector3 at, Transform quarry, Vector3 aim, bool seen, float speed, float near, float scale, ref Vector3 face)
        {
            Vector3 to = _lastSeenRoot - at; to.y = 0f;
            float distance = to.magnitude;
            Vector3 dir = distance > 1e-4f ? to / distance : FlatForward();
            face = quarry != null ? aim - at : to;
            TryShoot(now, at, quarry, aim, seen, scale);
            float pace = speed * fightPace;
            if (distance > KeepFar(scale) && _path.Has) return Toward(at, _path.Next(at, near)) * speed;
            if (distance < _skill.keepRange.x * scale && FloorAhead(at, -dir, scale)) return -dir * pace;
            if (!_strafeMoves) return Vector3.zero;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * _strafeSide;
            if (FloorAhead(at, side, scale)) return side * pace;
            // An edge or a wall that way: the other side at once, for a fresh stretch.
            NewStrafe(now);
            _strafeMoves = true;
            side = Vector3.Cross(Vector3.up, dir) * _strafeSide;
            return FloorAhead(at, side, scale) ? side * pace : Vector3.zero;
        }

        /// <summary>A dart if its burst allows one: aimed with lead and error (BotAim), held while a teammate stands in the
        /// line (it sidesteps instead), and fired through the soldier (which still says no while it turns or flinches).</summary>
        private void TryShoot(float now, Vector3 at, Transform quarry, Vector3 aim, bool seen, float scale)
        {
            if (quarry == null || !seen || !_aim.Ready(now)) return;
            Vector3 from = _soldier.ShotFrom;
            Vector3 point = _aim.ShotPoint(from, aim, _soldier.DartSpeed, _skill, now, scale, aimDrift);
            if (MateInLine(from, point, scale))
            {
                if (now >= _sidestepUntil) StartSidestep(now, at, point - from, scale);
                return;
            }
            float wait = _aim.NextWait(_skill, _dice);
            if (!_soldier.Fire(point, wait)) return;
            _aim.Fired(now, wait);
            _darts++;
        }

        /// <summary>True when a teammate (a bot in play or a person) stands within Line Of Fire Room of the dart's line.</summary>
        private bool MateInLine(Vector3 from, Vector3 to, float scale)
        {
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 1e-4f) return false;
            Vector3 dir = line / length;
            float room = lineOfFireRoom * scale;
            IReadOnlyList<ToySoldier> all = TeamMatch.Soldiers;
            for (int i = 0; i < all.Count; i++)
            {
                ToySoldier s = all[i];
                if (!IsMate(s) || s.Down) continue;
                if (NearLine(s.transform.position + Vector3.up * s.ChestHeightNow, from, dir, length, room)) return true;
            }
            int team = _soldier.Team;
            foreach (NetPlayer p in NetPlayer.Spawned)
            {
                if (p == null || !p.IsSpawned || p.Down || TeamMatch.TeamOf(p.OwnerClientId) != team) continue;
                float size = Mathf.Max(0.001f, p.transform.lossyScale.y);
                if (NearLine(p.transform.position + Vector3.up * (PlayerChest * size), from, dir, length, room)) return true;
            }
            return false;
        }

        private static bool NearLine(Vector3 point, Vector3 from, Vector3 dir, float length, float room)
        {
            float along = Vector3.Dot(point - from, dir);
            if (along <= 0f || along >= length) return false;
            return (point - (from + dir * along)).sqrMagnitude < room * room;
        }

        // ---------------------------------------------------------------- hunting

        private void StartHunt(float now, Vector3 at, float scale)
        {
            Enter(Mode.Hunt, now);
            _huntUntil = now + _skill.searchSeconds;
            if (QuarryOut()) { Enter(Mode.Advance, now); return; }   // knocked down: nothing to hunt
            Vector3 ahead = _lastSeenRoot + _lastSeenVel * huntAheadSeconds;
            if (TryWay(at, ahead, scale) || TryWay(at, _lastSeenRoot, scale)) return;
            LookAbout(now, _lastSeenRoot - at);
        }

        /// <summary>True when what it was after is knocked down (a bot or a player): there is nobody there to hunt.</summary>
        private bool QuarryOut()
        {
            if (_lastQuarry == null) return false;
            ToySoldier bot = _lastQuarry.GetComponentInParent<ToySoldier>();
            if (bot != null) return bot.Down;
            NetPlayer player = _lastQuarry.GetComponentInParent<NetPlayer>();
            return player != null && player.Down;
        }

        private void ThinkHunt(float now, Vector3 at, float scale)
        {
            if (!_huntLooking)
            {
                if (!_path.Has) LookAbout(now, _lastSeenRoot - at);
                else if (SpotInView(at, _path.Goal, scale)) LookAbout(now, _path.Goal - at);   // it can see the spot is empty
                return;
            }
            if (now >= _lookUntil) Enter(Mode.Advance, now);   // nothing found: back on its way
        }

        private Vector3 SteerHunt(float now, Vector3 at, float speed, float near, ref Vector3 face)
        {
            if (_huntLooking) { face = LookFace(now); return Vector3.zero; }
            if (!_path.Has) return Vector3.zero;
            if (_path.Arrived(at, near)) { LookAbout(now, _vel); return Vector3.zero; }
            return Toward(at, _path.Next(at, near)) * speed;
        }

        /// <summary>Stops and looks about for the rest of Search Seconds (at least a short look).</summary>
        private void LookAbout(float now, Vector3 facing)
        {
            _huntLooking = true;
            _path.Clear();
            BeginLook(now, Mathf.Max(_huntUntil, now + _skill.lookSeconds.x), facing);
        }

        /// <summary>True when it can see <paramref name="spot"/> from within Hunt See Within (and its sight): nothing in
        /// the way at chest height.</summary>
        private bool SpotInView(Vector3 at, Vector3 spot, float scale)
        {
            if (BotPath.Flat(at, spot) > Mathf.Min(huntSeeWithin * scale, _soldier.SightReachNow)) return false;
            Vector3 chest = Vector3.up * _soldier.ChestHeightNow;
            return !Blocked(at + chest, spot + chest);
        }

        // ---------------------------------------------------------------- falling back

        private bool ShouldFallBack(float now)
        {
            if (_skill.fallBackBelow <= 0f || _health == null || _health.MaxHealth <= 0f) return false;
            return _health.Health < _skill.fallBackBelow * _health.MaxHealth
                   && now - _lastHit <= underFireSeconds && _lastHit > _fallBackBegan;   // a new hit since the last fall-back
        }

        private void StartFallBack(float now, Vector3 at, float scale)
        {
            Enter(Mode.FallBack, now);
            _fallBacks++;
            _fallBackBegan = now;
            _threatAt = _haveMemory && _lastSeenTime > _lastHit ? _lastSeenRoot : _hitFrom;
            if (FindCover(at, scale, out Vector3 cover) && _path.Ask(at, cover)) return;
            NetSpawnPoint home = NetSpawnPoint.Landing(_soldier.Team);
            if (home != null && TryWay(at, home.transform.position, scale)) return;
            Hold(now);   // nowhere better: it holds where it is
        }

        /// <summary>Of Cover Tries spots round it (Cover Near to Cover Far away), the one with the shortest way from which
        /// the line from its chest to the threat's is blocked.</summary>
        private bool FindCover(Vector3 at, float scale, out Vector3 cover)
        {
            cover = at;
            Vector3 chest = Vector3.up * _soldier.ChestHeightNow;
            Vector3 threat = _threatAt + chest;
            float best = float.MaxValue;
            bool found = false;
            int tries = Mathf.Max(1, _skill.coverTries);
            float start = (float)_dice.NextDouble() * 360f;
            for (int i = 0; i < tries; i++)
            {
                float reach = Mathf.Lerp(_skill.coverNear, _skill.coverFar, (float)_dice.NextDouble()) * scale;
                Vector3 spot = at + Quaternion.Euler(0f, start + i * 360f / tries, 0f) * Vector3.forward * reach;
                if (!BotGround.OnFloor(spot, BotGround.Reach, out Vector3 on)) continue;
                if (!Blocked(on + chest, threat)) continue;   // the threat would still see it there
                if (!_path.Probe(at, on, out float length) || length >= best) continue;
                best = length; cover = on; found = true;
            }
            return found;
        }

        private void Hold(float now)
        {
            _holding = true;
            _holdUntil = now + _skill.holdSeconds;
            _path.Clear();
        }

        private void ThinkFallBack(float now, Vector3 at, bool sees, float scale)
        {
            if (!_holding) { if (!_path.Has) Hold(now); return; }
            bool mended = _health != null && _health.Health >= _skill.fallBackBelow * _health.MaxHealth;
            if (now >= _holdUntil || (mended && _skill.healPerSecond > 0f)) Settle(now, at, sees, scale);
        }

        private Vector3 SteerFallBack(float now, float dt, Vector3 at, Transform quarry, Vector3 aim, bool seen, float speed, float near, float scale, ref Vector3 face)
        {
            // Out of fire long enough, it mends (the host's soldier: the only one a brain drives).
            if (_skill.healPerSecond > 0f && _health != null && now - _lastHit >= _skill.healAfter) _health.Heal(_skill.healPerSecond * dt);
            if (!_holding)
            {
                if (!_path.Has) return Vector3.zero;
                if (_path.Arrived(at, near)) { Hold(now); return Vector3.zero; }
                return Toward(at, _path.Next(at, near)) * speed;
            }
            // Holding: it faces where the threat was, and fights what comes into view from where it stands.
            face = quarry != null && seen ? aim - at : _threatAt - at;
            TryShoot(now, at, quarry, aim, seen, scale);
            return Vector3.zero;
        }

        // ---------------------------------------------------------------- grenades

        /// <summary>The nearest live grenade whose blast would reach it and that it noticed in time (Dodge Chance, rolled
        /// once a grenade; Dodge Delay after). While it runs, it keeps running until Dodge Margin past the blast.</summary>
        private bool Danger(float now, Vector3 at, float scale, out Grenade bomb)
        {
            bomb = null;
            float nearest = float.MaxValue;
            float margin = _mode == Mode.Dodge ? dodgeMargin * scale : 0f;
            Vector3 middle = at + Vector3.up * (0.5f * _soldier.ChestHeightNow);
            IReadOnlyList<Grenade> live = Grenade.Live;
            for (int i = 0; i < live.Count; i++)
            {
                Grenade g = live[i];
                if (g == null || g.Gone) continue;
                float d = Vector3.Distance(middle, g.transform.position);
                if (d >= g.Reach + margin || !Noticed(g, now) || d >= nearest) continue;
                nearest = d; bomb = g;
            }
            return bomb != null;
        }

        /// <summary>Its one roll for this grenade (made the first time it threatens), and whether Dodge Delay has passed.</summary>
        private bool Noticed(Grenade g, float now)
        {
            for (int i = 0; i < Rolled; i++)
                if (_rolled[i] == g) return _noticed[i] && now >= _noticedAt[i] + _skill.dodgeDelay;
            int slot = _rollNext;
            _rollNext = (_rollNext + 1) % Rolled;
            _rolled[slot] = g;
            _noticed[slot] = _dice.NextDouble() < _skill.dodgeChance;
            _noticedAt[slot] = now;
            return _noticed[slot] && _skill.dodgeDelay <= 0f;
        }

        /// <summary>Where to run: Dodge Margin past the blast, straight away from the grenade, or the nearest turn from
        /// that which is floor it can run to in a straight line.</summary>
        private Vector3 DodgeSpot(Vector3 at, Grenade bomb, float scale)
        {
            Vector3 middle = bomb.transform.position; middle.y = at.y;
            Vector3 away = at - middle; away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = transform.right;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.right;
            float reach = bomb.Reach + dodgeMargin * scale;
            for (int i = 0; i < DodgeTurns.Length; i++)
            {
                Vector3 spot = middle + Quaternion.Euler(0f, DodgeTurns[i], 0f) * away * reach;
                if (BotGround.OnFloor(spot, BotGround.Reach, out Vector3 on) && BotGround.Straight(at, on)) return on;
            }
            return middle + away * reach;
        }

        private Vector3 SteerDodge(Vector3 at, float speed, float near, float scale)
        {
            Vector3 to = _dodgeTo - at; to.y = 0f;
            if (to.magnitude <= near) return Vector3.zero;
            Vector3 dir = to.normalized;
            return FloorAhead(at, dir, scale) ? dir * (speed * dodgePace) : Vector3.zero;
        }

        // ---------------------------------------------------------------- moving well

        /// <summary>Steps away from soldiers inside its Personal Space; meeting one head on, it keeps to its right (so
        /// does the other) and they pass. Never faster than its dodging speed.</summary>
        private Vector3 KeepSpace(Vector3 want, Vector3 at, float scale, float speed)
        {
            float room = personalSpace * scale;
            if (room <= 0f) return want;
            Vector3 heading = want.sqrMagnitude > 1e-6f ? want.normalized : Vector3.zero;
            Vector3 push = Vector3.zero;
            IReadOnlyList<ToySoldier> all = TeamMatch.Soldiers;
            for (int i = 0; i < all.Count; i++)
            {
                ToySoldier s = all[i];
                if (s == null || s == _soldier || !s.InPlay || s.Down) continue;
                Vector3 off = at - s.transform.position; off.y = 0f;
                float d = off.magnitude;
                if (d >= room || d < 1e-4f) continue;
                Vector3 away = off / d;
                float share = 1f - d / room;
                push += away * share;
                if (Vector3.Dot(heading, -away) > HeadOn) push += Vector3.Cross(Vector3.up, heading) * share;
            }
            if (push.sqrMagnitude < 1e-8f) return want;
            return Vector3.ClampMagnitude(want + push * (speed * spaceShove), speed * Mathf.Max(1f, dodgePace));
        }

        /// <summary>A short step aside (stuck, or a teammate in the line of fire): to a side that is floor, picked by chance.</summary>
        private void StartSidestep(float now, Vector3 at, Vector3 heading, float scale)
        {
            heading.y = 0f;
            Vector3 forward = heading.sqrMagnitude > 1e-6f ? heading.normalized : FlatForward();
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (_dice.Next(2) == 0 ? 1f : -1f);
            if (!FloorAhead(at, side, scale)) side = -side;
            if (!FloorAhead(at, side, scale)) side = -forward;
            _sidestep = side;
            _sidestepUntil = now + sidestepSeconds;
            _path.ResetHeadway();
        }

        /// <summary>True when a step of Floor Ahead along <paramref name="dir"/> lands on the mapped floor, at about its own
        /// height, and can be walked to straight (no wall, edge or cover between).</summary>
        private bool FloorAhead(Vector3 at, Vector3 dir, float scale)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return true;
            Vector3 ahead = at + dir.normalized * (floorAhead * scale);
            if (!BotGround.OnFloor(ahead, BotGround.Reach, out Vector3 on)) return false;
            if (BotPath.Flat(ahead, on) > floorEdge * scale || Mathf.Abs(on.y - ahead.y) > FloorRise * scale) return false;
            return BotGround.Straight(at, on);
        }

        /// <summary>True when something solid (not a soldier or a player) stands between two points, against its sight's
        /// layers.</summary>
        private bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 1e-4f) return false;
            int n = Physics.RaycastNonAlloc(from, line / length, Hits, length, _soldier.SightLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = Hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c is CharacterController) continue;
                if (c.GetComponentInParent<ToySoldier>() != null || c.GetComponentInParent<NetPlayer>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>A way to <paramref name="goal"/>, or failing that to floor it can walk to near it (up to Goal Search
        /// away: a goal on top of a box or off the map).</summary>
        private bool TryWay(Vector3 at, Vector3 goal, float scale)
        {
            if (BotGround.OnFloor(goal, BotGround.Reach, out Vector3 on) && _path.Ask(at, on)) return true;
            float ring = goalSearch * scale;
            if (ring <= 0f) return false;
            float start = (float)_dice.NextDouble() * 360f;
            for (int r = 1; r <= GoalRings; r++)
                for (int k = 0; k < GoalRingSpots; k++)
                {
                    Vector3 spot = goal + Quaternion.Euler(0f, start + k * (360f / GoalRingSpots), 0f) * Vector3.forward * (ring * r / GoalRings);
                    if (BotGround.OnFloor(spot, BotGround.Reach, out on) && _path.Ask(at, on)) return true;
                }
            return false;
        }

        private bool IsMate(ToySoldier s) => s != null && s != _soldier && s.InPlay && s.Team == _soldier.Team;

        private static Vector3 Toward(Vector3 at, Vector3 to)
        {
            Vector3 d = to - at; d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
        }

        private Vector3 FlatForward()
        {
            Vector3 f = transform.forward; f.y = 0f;
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
        }

        private float Roll(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)_dice.NextDouble());

        // ---------------------------------------------------------------- the tally

        /// <summary>One line through TeamMatch.Say when play ends: "bot Blue 1: 41 darts, 2 fall-backs, 1 dodge, stuck 0;
        /// advance 38% fight 34% hunt 21% fall back 7% dodge 0%". Then the counts start again.</summary>
        private void SayTally()
        {
            _tallying = false;
            float total = 0f;
            for (int i = 0; i < Modes; i++) total += _seconds[i];
            var line = new StringBuilder(160);
            line.Append("bot ").Append(TeamMatch.Name(_tallyTeam)).Append(' ').Append(_soldier != null ? _soldier.MatchOrder + 1 : 0).Append(": ");
            line.Append(_darts).Append(_darts == 1 ? " dart, " : " darts, ");
            line.Append(_fallBacks).Append(_fallBacks == 1 ? " fall-back, " : " fall-backs, ");
            line.Append(_dodges).Append(_dodges == 1 ? " dodge, " : " dodges, ");
            line.Append("stuck ").Append(_stuck).Append(';');
            for (int i = 0; i < Modes; i++)
                line.Append(' ').Append(ModeWords[i]).Append(' ').Append(total > 0f ? Mathf.RoundToInt(100f * _seconds[i] / total) : 0).Append('%');
            TeamMatch.Say(line.ToString());
            _darts = _fallBacks = _dodges = _stuck = 0;
            for (int i = 0; i < Modes; i++) _seconds[i] = 0f;
        }
    }
}
