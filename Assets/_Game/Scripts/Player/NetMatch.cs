using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What a team match sends (D-0182), on every online player beside NetWorld. The host writes, everyone reads:
    ///  - on every player: that player's team (0 none, 1 Blue, 2 Orange);
    ///  - on the host's own player only: the match's phase, both scores, the server time the phase ends, the winner and
    ///    the match's number. A friend who joins in the middle of a match has all of it the moment the host's player
    ///    appears on their PC.
    /// The start of play (GO: everyone to their base, whole, restocked, toy size) is a moment rather than a state: the host
    /// sends it to everyone on its own player (GoRpc). This part holds no rules: TeamMatch, the scene's match part, runs
    /// the match and reads what is here. Each change is written to the log once, on the PC that sees it ("TeamMatch: ...").
    /// </summary>
    public sealed class NetMatch : NetworkBehaviour
    {
        // Netcode sends a part's variables in the order they are declared (its code generator walks the fields in order;
        // older versions sorted them by name, which gives the same order here): the scores and the end time come before
        // the phase, so a PC that sees the phase change already has those. The match's number, the team and the winner
        // come AFTER it, so each of them tells the match of its own change too (OnMoreSeen): whoever reads on Changed
        // is told again once the last of them is in. Keep the order, and keep every variable telling.
        private readonly NetworkVariable<int> _blue = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> _endsAt = new NetworkVariable<double>(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _orange = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<byte> _phase = new NetworkVariable<byte>((byte)MatchPhase.Warmup, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _round = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<byte> _team = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<byte> _winner = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>The host's player's part as THIS PC sees it (every PC has a copy of the host's player): the match's
        /// state is read from it. Null until the host's player has appeared here.</summary>
        public static NetMatch OfHost { get; private set; }
        /// <summary>This PC's own player's part, or null.</summary>
        public static NetMatch Local { get; private set; }
        /// <summary>Every player's part spawned on this PC.</summary>
        public static readonly List<NetMatch> All = new List<NetMatch>();
        /// <summary>On the host: a player's part has just appeared (a friend joined): the match gives them a team if one runs.</summary>
        public static event Action<NetMatch> Arrived;

        /// <summary>This player's team: 0 none, 1 Blue, 2 Orange (TeamMatch.Blue, TeamMatch.Orange).</summary>
        public int Team => _team.Value;
        /// <summary>The match's phase (read on the host's player's part).</summary>
        public MatchPhase Phase => (MatchPhase)_phase.Value;
        /// <summary>The server time (NetworkManager.ServerTime.Time) the current phase ends (read on the host's player's part).</summary>
        public double EndsAt => _endsAt.Value;
        /// <summary>The team that won the last match, 0 for a draw or none yet (read on the host's player's part).</summary>
        public int Winner => _winner.Value;
        /// <summary>The match's number, one more at each countdown (read on the host's player's part).</summary>
        public int Round => _round.Value;
        /// <summary>A team's knock-outs this match (read on the host's player's part).</summary>
        public int ScoreOf(int team) => team == TeamMatch.Blue ? _blue.Value : team == TeamMatch.Orange ? _orange.Value : 0;

        /// <summary>True when this PC may write here: the host, while the player is spawned.</summary>
        private bool CanWrite => IsSpawned && IsServer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() { OfHost = null; Local = null; All.Clear(); Arrived = null; }   // a new play in the editor starts with none

        public override void OnNetworkSpawn()
        {
            if (!All.Contains(this)) All.Add(this);
            if (IsOwner) Local = this;
            if (OwnerClientId == NetworkManager.ServerClientId) OfHost = this;
            _team.OnValueChanged += OnTeamSeen;
            _phase.OnValueChanged += OnPhaseSeen;
            _blue.OnValueChanged += OnScoreSeen;
            _orange.OnValueChanged += OnScoreSeen;
            _round.OnValueChanged += OnMoreSeen;
            _winner.OnValueChanged += OnMoreSeen;
            _endsAt.OnValueChanged += OnMoreSeen;
            // Arrived with a match already under way (a friend joining): say what was found, once.
            if (_team.Value != 0) TeamMatch.Say($"{Who} is on {TeamMatch.NameOrNone(_team.Value)}.");
            if (OfHost == this && !IsServer && Phase != MatchPhase.Warmup)
                TeamMatch.Say($"the host's match is in {Phase}: {TeamMatch.Name(TeamMatch.Blue)} {_blue.Value}, {TeamMatch.Name(TeamMatch.Orange)} {_orange.Value}.");
            if (IsServer) Arrived?.Invoke(this);
            TeamMatch.NotifyChanged();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
            if (OfHost == this) OfHost = null;
            _team.OnValueChanged -= OnTeamSeen;
            _phase.OnValueChanged -= OnPhaseSeen;
            _blue.OnValueChanged -= OnScoreSeen;
            _orange.OnValueChanged -= OnScoreSeen;
            _round.OnValueChanged -= OnMoreSeen;
            _winner.OnValueChanged -= OnMoreSeen;
            _endsAt.OnValueChanged -= OnMoreSeen;
            TeamMatch.NotifyChanged();
        }

        private string Who => $"player {OwnerClientId + 1}" + (IsOwner ? " (this PC)" : "");

        private void OnTeamSeen(byte before, byte now)
        {
            TeamMatch.Say($"{Who} is on {TeamMatch.NameOrNone(now)}.");
            TeamMatch.NotifyChanged();
        }

        private void OnPhaseSeen(byte before, byte now)
        {
            var phase = (MatchPhase)now;
            string more = phase == MatchPhase.Ended
                ? $" ({TeamMatch.Name(TeamMatch.Blue)} {_blue.Value}, {TeamMatch.Name(TeamMatch.Orange)} {_orange.Value}: " +
                  (_blue.Value == _orange.Value ? "a draw)" : TeamMatch.Name(_blue.Value > _orange.Value ? TeamMatch.Blue : TeamMatch.Orange) + " wins)")
                : "";
            TeamMatch.Say($"phase {(MatchPhase)before} -> {phase}{more}.");
            TeamMatch.NotifyChanged();
        }

        // The match's number, the winner, the clock: nothing to log (the phase's line covers them), but the match and the
        // screen are told, so nobody is left with the value from before (the card's winner arrives after the phase).
        private void OnMoreSeen(int before, int now) => TeamMatch.NotifyChanged();
        private void OnMoreSeen(byte before, byte now) => TeamMatch.NotifyChanged();
        private void OnMoreSeen(double before, double now) => TeamMatch.NotifyChanged();

        private void OnScoreSeen(int before, int now)
        {
            // A score back to 0 is a match starting or ending: the phase's line says so.
            if (now != 0) TeamMatch.Say($"score {TeamMatch.Name(TeamMatch.Blue)} {_blue.Value}, {TeamMatch.Name(TeamMatch.Orange)} {_orange.Value}.");
            TeamMatch.NotifyChanged();
        }

        // ---------------------------------------------------------------- the host writes (TeamMatch)

        /// <summary>The host puts this player on a team (0: none).</summary>
        public void SetTeam(int team) { if (CanWrite) _team.Value = (byte)Mathf.Clamp(team, TeamMatch.NoTeam, TeamMatch.Orange); }

        /// <summary>The host's player: the match moves to <paramref name="phase"/>, which ends at server time <paramref name="endsAt"/>.</summary>
        public void SetPhase(MatchPhase phase, double endsAt)
        {
            if (!CanWrite) return;
            _endsAt.Value = endsAt;
            _phase.Value = (byte)phase;
        }

        /// <summary>The host's player: both teams' knock-outs.</summary>
        public void SetScores(int blue, int orange)
        {
            if (!CanWrite) return;
            _blue.Value = Mathf.Max(0, blue);
            _orange.Value = Mathf.Max(0, orange);
        }

        /// <summary>The host's player: who won (0 = a draw, or none).</summary>
        public void SetWinner(int team) { if (CanWrite) _winner.Value = (byte)Mathf.Clamp(team, TeamMatch.NoTeam, TeamMatch.Orange); }

        /// <summary>The host's player: a new match begins (its number goes up by one).</summary>
        public void NextRound() { if (CanWrite) _round.Value = _round.Value + 1; }

        // ---------------------------------------------------------------- moments (sent by the host on its own player)

        /// <summary>GO: on every PC, the host's too, this PC's own player goes to its team's base, whole, restocked and toy
        /// size (TeamMatch, NetPlayer.ToBase). <paramref name="round"/>: the match it is for, so a PC that already did it
        /// (it heard the phase change first) does not do it twice.</summary>
        [Rpc(SendTo.Everyone)]
        public void GoRpc(int round) => TeamMatch.OnGo(round);
    }
}
