using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// One online player (D-0111), on the root of Resources/Net/NetPlayer: the course's player with a NetworkObject, an
    /// owner-driven NetworkTransform (position, turn and size: the shift scales the root) and an owner-driven
    /// NetworkAnimator. On its owner's PC it is "Player", played as ever, with the scene's camera bound to it and a
    /// spawn point taken. On everyone else's it is a puppet: every script that reads input or moves it is switched
    /// off before its Start, and the network moves it and plays its animation.
    /// </summary>
    public sealed class NetPlayer : NetworkBehaviour
    {
        [SerializeField, Tooltip("How far above a spawn point the player appears, in its own body heights.")]
        private float spawnLift = 0.1f;

        /// <summary>Scripts a puppet keeps running: they only draw or are driven by the network.</summary>
        private static bool KeepOnPuppet(Behaviour b) =>
            b is NetworkBehaviour || b is Animator || b is PlayerBody || b is PlayerAnimation;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) SetUpOwn();
            else SetUpPuppet();
        }

        private void SetUpOwn()
        {
            name = "Player";
            var rig = FindFirstObjectByType<ShoulderCamera>();
            if (rig != null) rig.Bind(transform, GetComponent<PlayerInput>());
            if (TryGetComponent(out PlayerMotor motor) && rig != null) motor.UseCamera(rig.transform);
            MoveToSpawn();
        }

        private void SetUpPuppet()
        {
            name = "Player " + (OwnerClientId + 1);
            foreach (Behaviour b in GetComponentsInChildren<Behaviour>(true))
                if (!KeepOnPuppet(b)) b.enabled = false;
            // The capsule stays: it is what darts and other players bump into. Nothing moves it but the network.
        }

        /// <summary>Everyone starts at the hub's one spawn (his call, 21 Nov), each a little round from the middle so no two
        /// land inside each other: player n at n quarter-turns.</summary>
        private void MoveToSpawn()
        {
            NetSpawnPoint point = FindFirstObjectByType<NetSpawnPoint>();
            if (point == null) return;
            var cc = GetComponent<CharacterController>();
            float height = cc != null ? cc.height * transform.lossyScale.y : 1f;
            float turn = 90f * (OwnerClientId % 4);
            Vector3 offset = Quaternion.Euler(0f, turn, 0f) * point.transform.right * (point.Spread * height);
            if (cc != null) cc.enabled = false;
            Vector3 at = point.transform.position + offset + Vector3.up * (spawnLift * height);
            transform.SetPositionAndRotation(at, point.transform.rotation);
            if (cc != null) cc.enabled = true;
            if (TryGetComponent(out Respawn respawn)) respawn.SetPoint(at, point.transform.rotation);   // knock-outs and falls come back here (D-0112)
        }
    }
}
