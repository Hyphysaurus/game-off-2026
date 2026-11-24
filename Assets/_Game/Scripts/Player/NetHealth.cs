using System;
using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Online health and shield (D-0112), on the NetPlayer beside PlayerHurt. Each player's own PC owns its health: a
    /// shot that lands on another player's copy here is handed to that player's PC (Damageable.Relay ->
    /// TakeHitRpc), which takes it as in single player (block, roll, shield, knock-out, respawn). The owner writes
    /// health and shield into network variables, so every copy can show them; a knock-out tells everyone who did it
    /// (KnockedOut: victim, by; by = NoOne for a fall or a soldier), for the scores to come.
    /// </summary>
    public sealed class NetHealth : NetworkBehaviour
    {
        /// <summary>"By" for a knock-out no player dealt.</summary>
        public const ulong NoOne = ulong.MaxValue;

        /// <summary>"By" for a knock-out dealt by one of a team's bots in a team match (D-0182): a number no player can
        /// have, one a team (team 1: the one below NoOne, team 2: the one below that). The score counts it for the bot's
        /// team; the kill feed names the team's bot.</summary>
        public static ulong BotOf(int team) => team > 0 ? NoOne - (ulong)team : NoOne;

        /// <summary>True when <paramref name="by"/> is a team's bot (BotOf), with its team.</summary>
        public static bool IsBot(ulong by, out int team)
        {
            team = by < NoOne && NoOne - by <= MostTeams ? (int)(NoOne - by) : 0;
            return team != 0;
        }
        private const ulong MostTeams = 8;   // room for more teams than the two there are

        /// <summary>Raised on every PC when a player is knocked out: (victim's client id, the client id of whoever did it, or NoOne).</summary>
        public static event Action<ulong, ulong> KnockedOut;

        private readonly NetworkVariable<float> _health = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> _shield = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Damageable _hp;
        private PlayerShield _sh;
        private ulong _lastBy = NoOne;
        private float _lastByAt = -100f;
        private const float CreditSeconds = 10f;   // a knock-out within this long of a player's hit is theirs

        /// <summary>This player's health as their own PC last said.</summary>
        public float Health => _health.Value;
        /// <summary>This player's shield as their own PC last said.</summary>
        public float Shield => _shield.Value;

        public override void OnNetworkSpawn()
        {
            _hp = GetComponent<Damageable>();
            _sh = GetComponent<PlayerShield>();
            if (_hp == null) return;
            if (IsOwner)
            {
                _hp.Died += OnDied;
                Write();
            }
            else
            {
                _hp.Relay = RelayHit;
                _shield.OnValueChanged += OnShieldSeen;
                if (_sh != null) _sh.Mirror(_shield.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_hp == null) return;
            if (IsOwner) _hp.Died -= OnDied;
            else { _hp.Relay = null; _shield.OnValueChanged -= OnShieldSeen; }
        }

        private void Update()
        {
            if (IsSpawned && IsOwner) Write();
        }

        private void Write()
        {
            if (_hp != null && !Mathf.Approximately(_health.Value, _hp.Health)) _health.Value = _hp.Health;
            float shield = _sh != null ? _sh.Current : 0f;
            if (!Mathf.Approximately(_shield.Value, shield)) _shield.Value = shield;
        }

        private void OnShieldSeen(float before, float now) { if (_sh != null) _sh.Mirror(now); }

        /// <summary>On a shooter's PC: the hit goes to this player's own PC, with who dealt it.</summary>
        private bool RelayHit(DamageInfo hit)
        {
            if (_health.Value <= 0f) return false;   // already down
            TakeHitRpc(hit.Amount, hit.Knock, hit.Point, hit.Loose, ById(hit.Source));
            return true;
        }

        [Rpc(SendTo.Owner)]
        private void TakeHitRpc(float amount, Vector3 knock, Vector3 point, float loose, ulong by)
        {
            if (_hp == null) return;
            if (by != NoOne) { _lastBy = by; _lastByAt = Time.time; }
            _hp.Hit(new DamageInfo(amount, knock, point, null, loose));
        }

        private void OnDied(DamageInfo hit)
        {
            ulong by = hit.Source != null ? ById(hit.Source) : NoOne;
            // In a team match (D-0182) a player finished by their own grenade after another player's hit is that
            // player's knock-out, as one nobody dealt is: the score counts it. Outside a match it stays their own.
            bool ownInMatch = by == OwnerClientId && TeamMatch.Running;
            if ((by == NoOne || ownInMatch) && Time.time - _lastByAt <= CreditSeconds) by = _lastBy;
            if (by == OwnerClientId) by = NoOne;   // our own grenade
            _lastBy = NoOne;
            KnockedOutRpc(OwnerClientId, by);
        }

        [Rpc(SendTo.Everyone)]
        private void KnockedOutRpc(ulong victim, ulong by) => KnockedOut?.Invoke(victim, by);

        /// <summary>The client id of the player a hit's source belongs to, or NoOne (a soldier, a fall, a prop).</summary>
        private static ulong ById(Transform source)
        {
            if (source == null) return NoOne;
            var player = source.GetComponentInParent<NetPlayer>();
            return player != null && player.IsSpawned ? player.OwnerClientId : NoOne;
        }
    }
}
