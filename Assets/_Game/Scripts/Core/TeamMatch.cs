using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>Where a team match is (D-0182). None: no match part in this scene, or not online.</summary>
    public enum MatchPhase { None, Warmup, Countdown, Playing, Ended }

    /// <summary>
    /// Team deathmatch (D-0182), a part dropped into a party scene (PartyBuilder makes one in the Stadium, as it makes
    /// ShotRules). Any party scene with this part and team-marked spawn points (NetSpawnPoint) has matches; a scene
    /// without it plays exactly as before.
    ///  - Warm-up: free roam as ever, nobody on a team. The host presses the start key (M): "3, 2, 1" (Countdown), the
    ///    players are put on Blue (the south half) and Orange (the north half) alternately in the order they joined,
    ///    Blue first; a friend who joins while a match runs goes to the team with fewer people.
    ///  - GO (Playing): on every PC its own player is put at its team's base, whole, restocked and toy size
    ///    (NetPlayer.ToBase). Every knock-out of a team's member is one point for the other team (counted once, on the
    ///    host); a player knocked out by their own grenade gives nobody a point. Friendly fire is off (Damageable.Spares).
    ///    A comeback lands at the own team's spawn furthest from the enemy (NetPlayer). Nobody grows; the doors and gates
    ///    out of the scene are shut.
    ///  - The first team to Score To Win, or the team ahead when the minutes run out (level: a draw), wins: a card on
    ///    every PC (Ended, drawn by the scoreboard), then warm-up again.
    /// The practice soldiers are put away (unseen, untouchable, idle: ToySoldier.PutAway) from the countdown until warm-up
    /// returns. The host runs the match; the state travels on the players (NetMatch). This part does not draw the screen
    /// or move anyone itself: the screen reads the static surface below, the players move themselves.
    /// </summary>
    public sealed class TeamMatch : MonoBehaviour
    {
        /// <summary>Team numbers. 0: no team (warm-up, or not in a match).</summary>
        public const int NoTeam = 0, Blue = 1, Orange = 2;

        // Placeholder wording (his to change).
        /// <summary>The shift's refusal while a match runs.</summary>
        public const string NoGrowing = "No growing during a match";
        /// <summary>A door's prompt while a match runs (its E does nothing).</summary>
        public const string DoorShut = "Shut during a match";
        /// <summary>Said to a player who is not the host when they press the start key in warm-up.</summary>
        public const string HostStarts = "The host starts a match";

        [Header("The match")]
        [SerializeField, Min(1), Tooltip("Knock-outs a team needs to win.")] private int scoreToWin = 10;
        [SerializeField, Min(0.1f), Tooltip("How long a match lasts, minutes. When they run out the team ahead wins; level is a draw.")] private float minutes = 5f;
        [SerializeField, Min(1), Tooltip("How many members a team has: people first, then the team's bots (the bots fill the rest).")] private int teamSize = 2;
        [SerializeField, Min(0f), Tooltip("Seconds of \"3, 2, 1\" between the host's key and GO.")] private float countdownSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("Seconds the winner's card stays up before everyone is free to roam again.")] private float endSeconds = 8f;
        [SerializeField, Tooltip("The key the host presses in warm-up to start a match.")] private Key startKey = Key.M;
        [Header("The teams")]
        [SerializeField, Tooltip("Team 1's name: the south half (z below 0).")] private string blueName = "Blue";
        [SerializeField, Tooltip("Team 1's colour: name tags, the score, the card.")] private Color blueColour = BlueColour;
        [SerializeField, Tooltip("Team 2's name: the north half (z above 0).")] private string orangeName = "Orange";
        [SerializeField, Tooltip("Team 2's colour: name tags, the score, the card.")] private Color orangeColour = OrangeColour;

        private static readonly Color BlueColour = new Color(0.25f, 0.55f, 1f), OrangeColour = new Color(1f, 0.55f, 0.15f);

        private static TeamMatch _scene;   // the match part of the scene in use (the last one switched on)
        [NonSerialized] private int _placedRound = -1;   // the match this PC's own player was last put at its base for

        /// <summary>Friendly fire off (Damageable.Spares): a hit from a teammate does nothing while a match runs.</summary>
        private static readonly Func<Transform, Damageable, bool> SparesRule = (source, victim) => victim != null && SameTeam(source, victim.transform);

        /// <summary>Raised on every PC whenever the phase, a score or anybody's team changes (and when a player comes or goes).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() { _scene = null; Changed = null; }   // the editor may enter Play without reloading scripts

        // ---------------------------------------------------------------- what every other script reads

        /// <summary>The host's player's match state as this PC sees it, or null (no match part here, not online, or the
        /// host's player has not appeared on this PC yet).</summary>
        private static NetMatch HostState
        {
            get
            {
                if (_scene == null || !NetSession.InParty) return null;
                NetMatch host = NetMatch.OfHost;
                return host != null && host.IsSpawned ? host : null;
            }
        }

        /// <summary>Where the match is. None without a match part in this scene, offline, or before the host's player is here.</summary>
        public static MatchPhase Phase
        {
            get
            {
                NetMatch host = HostState;
                if (host == null) return MatchPhase.None;
                return host.Phase == MatchPhase.None ? MatchPhase.Warmup : host.Phase;
            }
        }

        /// <summary>True while a match runs: the countdown or play.</summary>
        public static bool Running { get { MatchPhase p = Phase; return p == MatchPhase.Countdown || p == MatchPhase.Playing; } }

        /// <summary>True while players are on teams: from the countdown until warm-up returns (the card too).</summary>
        private static bool HasTeams { get { MatchPhase p = Phase; return p == MatchPhase.Countdown || p == MatchPhase.Playing || p == MatchPhase.Ended; } }

        /// <summary>A team's knock-outs this match (0 outside one).</summary>
        public static int Score(int team) { NetMatch host = HostState; return host != null && HasTeams ? host.ScoreOf(team) : 0; }

        /// <summary>Seconds left of the current phase (the countdown, the match, the card), by the server's clock. 0 in warm-up.</summary>
        public static float SecondsLeft
        {
            get
            {
                NetMatch host = HostState;
                if (host == null || !HasTeams || NetworkManager.Singleton == null) return 0f;
                return (float)Math.Max(0d, host.EndsAt - NetworkManager.Singleton.ServerTime.Time);
            }
        }

        /// <summary>On the card: the team that won, 0 for a draw. 0 at any other time.</summary>
        public static int Winner { get { NetMatch host = HostState; return host != null && Phase == MatchPhase.Ended ? host.Winner : 0; } }

        /// <summary>Knock-outs a team needs to win (0 with no match part here).</summary>
        public static int ScoreToWin => _scene != null ? _scene.scoreToWin : 0;

        /// <summary>How many members a team has, people and bots (0 with no match part here).</summary>
        public static int TeamSize => _scene != null ? _scene.teamSize : 0;

        /// <summary>The start key's name, for a hint on screen ("M"; "" with no match part here).</summary>
        public static string StartKeyName => _scene != null && _scene.startKey != Key.None ? _scene.startKey.ToString() : "";

        /// <summary>True on the PC that may start a match: the host, in a scene with a match part.</summary>
        public static bool IsHost => _scene != null && NetSession.InParty && NetworkManager.Singleton.IsServer;

        /// <summary>The team of the player with this client id: 0 for none, or when no match is on (warm-up).</summary>
        public static int TeamOf(ulong clientId)
        {
            if (!HasTeams) return NoTeam;
            foreach (NetMatch m in NetMatch.All)
                if (m != null && m.IsSpawned && m.OwnerClientId == clientId) return m.Team;
            return NoTeam;
        }

        /// <summary>The team of whatever <paramref name="who"/> is part of: a player (anything under one), or a bot. 0 for
        /// anything else, or when no match is on. (The soldiers have no team yet: they are put away during a match.)</summary>
        public static int TeamOf(Transform who) => HasTeams ? MemberOf(who, out _) : NoTeam;

        /// <summary>This PC's own player's team (0: none, or no match on).</summary>
        public static int Mine => NetworkManager.Singleton != null ? TeamOf(NetworkManager.Singleton.LocalClientId) : NoTeam;

        /// <summary>True only while a match runs and <paramref name="a"/> and <paramref name="b"/> are on the same team but
        /// are not the same player or bot (a player's own grenade is not friendly fire: it still hurts them).</summary>
        public static bool SameTeam(Transform a, Transform b)
        {
            if (!Running) return false;
            int ta = MemberOf(a, out Component wa), tb = MemberOf(b, out Component wb);
            return ta != NoTeam && ta == tb && wa != wb;
        }

        /// <summary>A team's colour (white for 0).</summary>
        public static Color Colour(int team)
        {
            if (team == Blue) return _scene != null ? _scene.blueColour : BlueColour;
            if (team == Orange) return _scene != null ? _scene.orangeColour : OrangeColour;
            return Color.white;
        }

        /// <summary>A team's name ("" for 0).</summary>
        public static string Name(int team)
        {
            if (team == Blue) return _scene != null ? _scene.blueName : "Blue";
            if (team == Orange) return _scene != null ? _scene.orangeName : "Orange";
            return "";
        }

        /// <summary>A team's name, or "no team" (for the log).</summary>
        public static string NameOrNone(int team) => team == NoTeam ? "no team" : Name(team);

        /// <summary>The other team (0 for 0).</summary>
        public static int Other(int team) => team == Blue ? Orange : team == Orange ? Blue : NoTeam;

        /// <summary>Where <paramref name="team"/>'s enemies stand that a comeback should keep away from: the other team's
        /// players who are here and not knocked out. Added to <paramref name="into"/>.</summary>
        public static void EnemiesOf(int team, List<Vector3> into)
        {
            if (into == null || team == NoTeam || !HasTeams) return;
            foreach (NetPlayer p in NetPlayer.Spawned)
            {
                if (p == null || !p.IsSpawned || p.Down) continue;
                int theirs = TeamOf(p.OwnerClientId);
                if (theirs != NoTeam && theirs != team) into.Add(p.transform.position);
            }
        }

        /// <summary>This player's place among their team (0, 1, ...), in the order they joined: where round the base's
        /// landing point they stand at GO, so two teammates never land inside each other.</summary>
        public static int PlaceInTeam(ulong clientId)
        {
            int team = TeamOf(clientId), place = 0;
            if (team == NoTeam) return 0;
            foreach (NetMatch m in NetMatch.All)
                if (m != null && m.IsSpawned && m.Team == team && m.OwnerClientId < clientId) place++;
            return place;
        }

        /// <summary>How many people are on a team (0 when no match is on).</summary>
        public static int People(int team)
        {
            if (team == NoTeam || !HasTeams) return 0;
            int n = 0;
            foreach (NetMatch m in NetMatch.All) if (m != null && m.IsSpawned && m.Team == team) n++;
            return n;
        }

        /// <summary>How many bots a team should have while play is on: what its people leave of Team Size (two people on
        /// a team of two: none). 0 at any other time. The scene may hold fewer soldiers than this asks for.</summary>
        public static int BotsWanted(int team) => team != NoTeam && Phase == MatchPhase.Playing ? Mathf.Max(0, TeamSize - People(team)) : 0;

        /// <summary>The team and the member (its player's NetMatch) that <paramref name="t"/> is part of.</summary>
        private static int MemberOf(Transform t, out Component who)
        {
            who = null;
            if (t == null) return NoTeam;
            NetMatch m = t.GetComponentInParent<NetMatch>();
            if (m == null || !m.IsSpawned) return NoTeam;
            who = m;
            return m.Team;
        }

        // ---------------------------------------------------------------- the part's life

        private void OnEnable()
        {
            _scene = this;
            Damageable.Spares = SparesRule;
            NetHealth.KnockedOut += OnKnockedOut;
            NetMatch.Arrived += OnArrived;
            // A new scene: whatever the host's player still carries from the last one starts over as warm-up.
            if (IsHost) ToWarmup();
        }

        // The rules are first put on this PC once every part of the scene has woken (the soldiers among them).
        private void Start() => NotifyChanged();

        private void OnDisable()
        {
            // The part goes (the party leaves the scene): no state may leak into a scene without one.
            if (IsHost) ToWarmup();
            NetHealth.KnockedOut -= OnKnockedOut;
            NetMatch.Arrived -= OnArrived;
            if (Damageable.Spares == SparesRule) Damageable.Spares = null;
            if (_scene == this) _scene = null;
        }

        private void Update()
        {
            NetMatch host = HostState;
            if (host == null) return;
            var nm = NetworkManager.Singleton;
            if (StartPressed())
            {
                if (nm.IsServer) TryStart(host, nm);
                else if (Phase == MatchPhase.Warmup) Toasts.Show(HostStarts, ToastKind.Refused);
            }
            if (!nm.IsServer) return;
            double now = nm.ServerTime.Time;
            switch (host.Phase)
            {
                case MatchPhase.Countdown: if (now >= host.EndsAt) Go(host, now); break;
                case MatchPhase.Playing: if (now >= host.EndsAt) End(host, now); break;
                case MatchPhase.Ended: if (now >= host.EndsAt) ToWarmup(); break;
            }
        }

        /// <summary>The start key, pressed this frame with nothing else taking the keyboard (a menu, the console).</summary>
        private bool StartPressed()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null || startKey == Key.None || !keys[startKey].wasPressedThisFrame) return false;
            return !PauseMenu.IsOpen && !DebugConsole.IsOpen && !TitleMenu.IsOpen;
        }

        // ---------------------------------------------------------------- the host runs the match

        /// <summary>The host pressed the start key: in warm-up, and with no door's countdown under way, the countdown
        /// starts and everyone is put on a team. Any other time (a match on, the card up) it does nothing.</summary>
        private void TryStart(NetMatch host, NetworkManager nm)
        {
            if (host.Phase != MatchPhase.Warmup || NetTravel.Going) return;
            int i = 0;
            foreach (ulong id in nm.ConnectedClientsIds)   // in the order they joined
            {
                NetMatch m = Of(id);
                if (m == null) continue;   // their player is not here yet: they get a team as it appears (OnArrived)
                m.SetTeam(i % 2 == 0 ? Blue : Orange);
                i++;
            }
            host.SetScores(0, 0);
            host.SetWinner(NoTeam);
            host.NextRound();
            host.SetPhase(MatchPhase.Countdown, nm.ServerTime.Time + countdownSeconds);
        }

        /// <summary>GO: play starts, and every PC puts its own player at its base.</summary>
        private void Go(NetMatch host, double now)
        {
            host.SetPhase(MatchPhase.Playing, now + minutes * 60d);
            host.GoRpc(host.Round);
        }

        /// <summary>The match is over: the team ahead wins (level: a draw), and the card stays up a while.</summary>
        private void End(NetMatch host, double now)
        {
            int blue = host.ScoreOf(Blue), orange = host.ScoreOf(Orange);
            host.SetWinner(blue > orange ? Blue : orange > blue ? Orange : NoTeam);
            host.SetPhase(MatchPhase.Ended, now + endSeconds);
        }

        /// <summary>Back to warm-up: nobody on a team, no score. Safe to call any time on the host.</summary>
        private static void ToWarmup()
        {
            NetMatch host = NetMatch.OfHost;
            if (host == null || !host.IsSpawned || !host.IsServer) return;
            foreach (NetMatch m in NetMatch.All) if (m != null) m.SetTeam(NoTeam);
            host.SetScores(0, 0);
            host.SetWinner(NoTeam);
            host.SetPhase(MatchPhase.Warmup, 0d);
        }

        /// <summary>On the host: a player appeared (a friend joined). While a match runs they go to the team with fewer
        /// people (Blue when level); their own PC puts them at its base once it learns the team.</summary>
        private void OnArrived(NetMatch who)
        {
            if (!IsHost || !Running || who == null || who.Team != NoTeam) return;
            int blue = 0, orange = 0;
            foreach (NetMatch m in NetMatch.All)
            {
                if (m == null || m == who || !m.IsSpawned) continue;
                if (m.Team == Blue) blue++; else if (m.Team == Orange) orange++;
            }
            who.SetTeam(blue <= orange ? Blue : Orange);
        }

        /// <summary>Every PC hears every knock-out; the host alone counts it, once, while play is on: one point for the
        /// victim's other team, whoever did it. A knock-out nobody else had a hand in (their own grenade: NetHealth's
        /// "by" is NoOne when no other player hit them in the last ten seconds) gives no point.</summary>
        private void OnKnockedOut(ulong victim, ulong by)
        {
            if (!IsHost || Phase != MatchPhase.Playing) return;
            NetMatch host = HostState;
            int team = TeamOf(victim);
            if (host == null || team == NoTeam || by == NetHealth.NoOne) return;
            int blue = host.ScoreOf(Blue) + (team == Orange ? 1 : 0);
            int orange = host.ScoreOf(Orange) + (team == Blue ? 1 : 0);
            host.SetScores(blue, orange);
            if (blue >= scoreToWin || orange >= scoreToWin) End(host, NetworkManager.Singleton.ServerTime.Time);
        }

        private static NetMatch Of(ulong clientId)
        {
            foreach (NetMatch m in NetMatch.All) if (m != null && m.IsSpawned && m.OwnerClientId == clientId) return m;
            return null;
        }

        // ---------------------------------------------------------------- every PC follows the state

        /// <summary>NetMatch saw a change (or a player came or went): the rules are put on this PC, then the screen is told.</summary>
        internal static void NotifyChanged()
        {
            if (_scene != null) _scene.Apply();
            Changed?.Invoke();
        }

        /// <summary>The host's GO reached this PC.</summary>
        internal static void OnGo(int round)
        {
            if (_scene != null) _scene.PlaceOwn(round);
        }

        /// <summary>What the state means on this PC: the soldiers away from the countdown until warm-up returns, and this
        /// PC's player at its base once play is on (a friend who joined mid-match, or a GO that has not arrived yet).</summary>
        private void Apply()
        {
            MatchPhase phase = Phase;
            PutSoldiersAway(phase == MatchPhase.Countdown || phase == MatchPhase.Playing || phase == MatchPhase.Ended);
            NetMatch host = HostState;
            if (phase == MatchPhase.Playing && host != null) PlaceOwn(host.Round);
        }

        /// <summary>This PC's own player to its team's base for match <paramref name="round"/>, once.</summary>
        private void PlaceOwn(int round)
        {
            if (_placedRound == round || Mine == NoTeam) return;   // done already, or this PC has not learned its team yet
            NetPlayer own = null;
            foreach (NetPlayer p in NetPlayer.Spawned) if (p != null && p.IsSpawned && p.IsOwner) { own = p; break; }
            if (own == null) return;
            _placedRound = round;
            Debug.Log($"TeamMatch: this PC's player goes to the {Name(Mine)} base.");
            own.ToBase();
        }

        /// <summary>Every soldier of this scene put away, or back (rule 8: never switched off, CoopWorld numbers the active ones).</summary>
        private void PutSoldiersAway(bool away)
        {
            int changed = 0;
            foreach (ToySoldier s in FindObjectsByType<ToySoldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (s == null || s.gameObject.scene != gameObject.scene || s.Away == away) continue;
                s.PutAway(away);
                changed++;
            }
            if (changed > 0) Debug.Log($"TeamMatch: {changed} soldier(s) " + (away ? "put away for the match." : "back for warm-up."));
        }
    }
}
