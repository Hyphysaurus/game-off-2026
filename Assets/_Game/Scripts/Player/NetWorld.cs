using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The co-op world's messages (D-0131), on every NetPlayer. The host runs the soldiers, the breakable props and the
    /// pickups (CoopWorld) and tells everyone what happened through its own player's copy of this; every other player
    /// asks the host through their own: a hit on a soldier or a prop, a pickup touched, and the world's state as they
    /// arrive. Doors that take the whole party to another room travel the same way (NetTravel, D-0134). It holds nothing
    /// itself: CoopWorld and NetTravel do the work.
    /// </summary>
    public sealed class NetWorld : NetworkBehaviour
    {
        /// <summary>This PC's own player's: what this PC asks the host goes through it.</summary>
        public static NetWorld Local { get; private set; }
        /// <summary>On the host, its own player's: what the host tells everyone goes through it.</summary>
        public static NetWorld Host { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() { Local = null; Host = null; }   // a new play in the editor starts with none

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            Local = this;
            if (IsServer) Host = this;
            else CoopWorld.AskState();   // here now: what the host's world looks like (if the world bound first)
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
            if (Host == this) Host = null;
        }

        private void Update()
        {
            if (Host == this && IsSpawned) CoopWorld.HostTick();
            if (Local == this && IsSpawned) NetTravel.Tick();   // the door countdown, on every PC (D-0134)
        }

        // ---------------------------------------------------------------- doors take the group (D-0134)

        [Rpc(SendTo.Server)]
        public void TravelRpc(string scene, string arriveAt) => NetTravel.Begin(scene, arriveAt);

        [Rpc(SendTo.Everyone)]   // sent by the host on its own player: every PC, the host too, starts the same countdown
        public void GoingRpc(string scene, string arriveAt, float seconds) => NetTravel.OnGoing(scene, arriveAt, seconds);

        // ---------------------------------------------------------------- a player asks the host (on their own player)

        [Rpc(SendTo.Server)]
        public void HitRpc(byte kind, ushort id, float amount, Vector3 knock, Vector3 point, float loose) => CoopWorld.OnHit(OwnerClientId, kind, id, amount, knock, point, loose);

        [Rpc(SendTo.Server)]
        public void TakeRpc(ushort id) => CoopWorld.OnTake(OwnerClientId, id);

        [Rpc(SendTo.Server)]
        public void WantStateRpc() => CoopWorld.OnWantState(this);

        // ---------------------------------------------------------------- the host answers one player (on that player's object)

        [Rpc(SendTo.Owner)]
        public void StateRpc(uint hash, byte[] down, byte[] broken, byte[] away) => CoopWorld.OnState(hash, down, broken, away);

        // ---------------------------------------------------------------- the host tells everyone (on its own player)

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]   // ten a second: a lost one is followed by the next
        public void SoldiersRpc(Vector3[] at, byte[] yaw) => CoopWorld.OnPoses(at, yaw);

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]   // a look, like players' shots: late is worse than lost
        public void SoldierShotRpc(ushort id, Vector3 from, Vector3 direction) => CoopWorld.OnSoldierShot(id, from, direction);

        [Rpc(SendTo.NotServer)]
        public void SoldierDownRpc(ushort id, Vector3 point) => CoopWorld.OnSoldierDown(id, point);

        [Rpc(SendTo.NotServer)]
        public void SoldierUpRpc(ushort id) => CoopWorld.OnSoldierUp(id);

        [Rpc(SendTo.NotServer)]
        public void BrokeRpc(ushort id, Vector3 from, Vector3 knock) => CoopWorld.OnBroke(id, from, knock);

        [Rpc(SendTo.NotServer)]
        public void BackRpc(ushort id) => CoopWorld.OnBack(id);

        [Rpc(SendTo.NotServer)]
        public void PickupTakenRpc(ushort id, ulong taker) => CoopWorld.OnPickupTaken(id, taker);

        [Rpc(SendTo.NotServer)]
        public void PickupBackRpc(ushort id) => CoopWorld.OnPickupBack(id);
    }
}
