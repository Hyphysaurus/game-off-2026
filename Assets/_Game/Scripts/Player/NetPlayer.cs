using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            b is NetworkBehaviour || b is Animator || b is PlayerBody || b is PlayerAnimation || b is TMPro.TMP_Text;   // the name tag (D-0117)

        /// <summary>True while this player sits in a car (D-0116): every other PC hides the body, as the seat does here.</summary>
        private readonly NetworkVariable<bool> _seated = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private Renderer[] _bodies;

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                bool seated = Driving.Vehicle != null;
                if (_seated.Value != seated) _seated.Value = seated;
                return;
            }
            if (_bodies == null) _bodies = GetComponentsInChildren<Renderer>(true);
            bool hide = _seated.Value;
            foreach (var r in _bodies) if (r != null && r.forceRenderingOff != hide) r.forceRenderingOff = hide;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { SetUpPuppet(); return; }
            SetUpOwn();
            if (TryGetComponent(out Damageable health)) health.Died += OnKnockedOut;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>A new world loaded under us (D-0125: the host's own player was made on the title, before the party's
        /// scene; later, the group travels): set up again there a frame later, once the world's own rules (OnlineWorld)
        /// and spawns are in place: its camera, its spawn, a full shift charge.</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && isActiveAndEnabled) StartCoroutine(ArriveNextFrame());
        }

        private IEnumerator ArriveNextFrame()
        {
            yield return null;
            if (!IsSpawned || !IsOwner) yield break;
            SetUpOwn();
            if (TryGetComponent(out ShiftCharge charge)) charge.Refill();
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

        /// <summary>Everyone starts at the hub's First point (his call, 21 Nov), each a little round from the middle so no
        /// two land inside each other: player n at n quarter-turns. Knock-outs come back at the safest point (D-0113).</summary>
        private void MoveToSpawn()
        {
            NetSpawnPoint point = NetSpawnPoint.FirstPoint();
            if (point == null) return;
            float height = BodyHeight();
            float turn = 90f * (OwnerClientId % 4);
            Vector3 offset = Quaternion.Euler(0f, turn, 0f) * point.transform.right * (point.Spread * height);
            Put(point.transform.position + offset + Vector3.up * (spawnLift * height), point.transform.rotation);
            if (TryGetComponent(out Respawn respawn)) respawn.SetPoint(transform.position, transform.rotation);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner && TryGetComponent(out Damageable health)) health.Died -= OnKnockedOut;
            if (IsOwner) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Knocked out: the way back (PlayerHurt's respawn) goes to the point furthest from everyone else.</summary>
        private void OnKnockedOut(DamageInfo _)
        {
            var others = new List<Vector3>();
            foreach (var p in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
                if (p != this && p.IsSpawned) others.Add(p.transform.position);
            NetSpawnPoint point = NetSpawnPoint.Safest(others);
            if (point != null && TryGetComponent(out Respawn respawn))
                respawn.SetPoint(point.transform.position + Vector3.up * (spawnLift * BodyHeight()), point.transform.rotation);
        }

        private float BodyHeight()
        {
            var cc = GetComponent<CharacterController>();
            return cc != null ? cc.height * transform.lossyScale.y : 1f;
        }

        private void Put(Vector3 at, Quaternion facing)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(at, facing);
            if (cc != null) cc.enabled = true;
        }
    }
}
