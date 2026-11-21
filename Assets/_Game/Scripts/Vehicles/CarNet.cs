using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A car online (D-0116), beside its ArcadeCar, NetworkObject, owner-driven NetworkTransform and NetworkRigidbody.
    /// Whoever drives it owns it: getting in asks the host for the car, and from then on that PC runs its physics (the
    /// drive feels as it does alone); every other PC sees it moved by the network, kinematic, which the car reads as
    /// parked. A car someone else is driving cannot be got into (CarSeat asks TakenByOther). Hits on a car someone
    /// else owns are handed to that PC, as players' hits are (NetHealth). Idle cars belong to the host.
    /// </summary>
    public sealed class CarNet : NetworkBehaviour
    {
        public const ulong NoDriver = ulong.MaxValue;

        private readonly NetworkVariable<ulong> _driver = new NetworkVariable<ulong>(NoDriver, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private CarSeat _seat;
        private Damageable _hp;
        private bool _asked;

        /// <summary>True when another player is at the wheel.</summary>
        public bool TakenByOther => IsSpawned && _driver.Value != NoDriver && _driver.Value != NetworkManager.LocalClientId;

        private void Awake()
        {
            _seat = GetComponentInChildren<CarSeat>(true);
            _hp = GetComponent<Damageable>();
        }

        public override void OnNetworkSpawn() { if (_hp != null && !IsOwner) _hp.Relay = RelayHit; }
        public override void OnNetworkDespawn() { if (_hp != null) _hp.Relay = null; }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            base.OnOwnershipChanged(previous, current);
            if (_hp != null) _hp.Relay = IsOwner ? null : RelayHit;
            _asked = false;
        }

        private bool Mine => _seat != null && _seat.Occupied && Driving.Vehicle != null && Driving.Vehicle == _seat.transform;

        private void Update()
        {
            if (!IsSpawned) return;
            bool mine = Mine;
            if (mine && !IsOwner && !_asked) { _asked = true; AskRpc(); }
            if (!mine) _asked = false;
            if (IsOwner)
            {
                ulong want = mine ? NetworkManager.LocalClientId : NoDriver;
                if (_driver.Value != want) _driver.Value = want;
            }
        }

        [Rpc(SendTo.Server)]
        private void AskRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (_driver.Value != NoDriver && _driver.Value != who) return;   // someone is already at the wheel
            if (OwnerClientId != who) NetworkObject.ChangeOwnership(who);
        }

        private bool RelayHit(DamageInfo hit)
        {
            HitRpc(hit.Amount, hit.Knock, hit.Point, hit.Loose);
            return true;
        }

        [Rpc(SendTo.Owner)]
        private void HitRpc(float amount, Vector3 knock, Vector3 point, float loose)
        {
            if (_hp != null) _hp.Hit(new DamageInfo(amount, knock, point, null, loose));
        }
    }
}
