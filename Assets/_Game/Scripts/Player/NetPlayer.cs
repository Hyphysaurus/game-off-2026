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
            b is NetworkBehaviour || b is Animator || b is PlayerBody || b is PlayerAnimation || b is TMPro.TMP_Text   // the name tag (D-0117)
            || (b is WeaponHolder holder && holder.IsPuppet)   // made look-only by NetCombat: it holds their weapon as they do (D-0128)
            || (b is GrenadeThrower thrower && thrower.LookOnly);   // the same: it swings their throwing arm (D-0130)

        /// <summary>True while this player sits in a car (D-0116): every other PC hides the body, as the seat does here.</summary>
        private readonly NetworkVariable<bool> _seated = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private Renderer[] _bodies;
        private NetHealth _health;
        [System.NonSerialized] private bool _turnViewOnWarp;   // the next warp also turns the camera to the way the player faces (ToBase)
        [System.NonSerialized] private bool _toBaseWaiting;    // ToBase is waiting for a reset's cut to finish before it asks again

        /// <summary>Every online player spawned on this PC, its own and the others' (D-0131: the host's soldiers aim at
        /// them all, a prop comes back only once all are clear of it).</summary>
        public static readonly List<NetPlayer> Spawned = new List<NetPlayer>();
        /// <summary>True while this player sits in a car.</summary>
        public bool Seated => _seated.Value;
        /// <summary>True while this player is knocked out, as their own PC last said.</summary>
        public bool Down => _health != null && _health.Health <= 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() => Spawned.Clear();   // a new play in the editor starts with nobody

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
            _health = GetComponent<NetHealth>();
            if (!Spawned.Contains(this)) Spawned.Add(this);
            if (!IsOwner) { SetUpPuppet(); return; }
            OnlineWorld.EnsureApplied();   // joined into the scene we already had open: no load came to set it up (D-0126)
            SetUpOwn();
            if (TryGetComponent(out Damageable health)) health.Died += OnKnockedOut;
            if (TryGetComponent(out PlayerMotor motor)) motor.Warped += OnWarped;
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
        /// two land inside each other: player n at n quarter-turns. Knock-outs come back at the safest point (D-0113).
        /// While a team match runs (D-0182) a player who appears goes to their team's base instead, if their team is known.</summary>
        private void MoveToSpawn()
        {
            int team = TeamMatch.Running ? TeamMatch.TeamOf(OwnerClientId) : TeamMatch.NoTeam;
            if (team != TeamMatch.NoTeam && BaseSpot(team, out Vector3 at, out Quaternion facing))
            {
                if (TryGetComponent(out PlayerMotor motor)) motor.Warp(at, facing); else Put(at, facing);
                TurnView(facing);
                if (TryGetComponent(out Respawn spawnRespawn)) spawnRespawn.SetPoint(at, facing);
                return;
            }
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
            Spawned.Remove(this);
            if (IsOwner && TryGetComponent(out Damageable health)) health.Died -= OnKnockedOut;
            if (IsOwner && TryGetComponent(out PlayerMotor motor)) motor.Warped -= OnWarped;
            if (IsOwner) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Knocked out: the way back (PlayerHurt's respawn) goes to the point furthest from everyone else. While a
        /// team match runs (D-0182): the own team's point furthest from the nearest enemy.</summary>
        private void OnKnockedOut(DamageInfo _)
        {
            NetSpawnPoint point = null;
            int team = TeamMatch.Running ? TeamMatch.TeamOf(OwnerClientId) : TeamMatch.NoTeam;
            if (team != TeamMatch.NoTeam)
            {
                var enemies = new List<Vector3>();
                TeamMatch.EnemiesOf(team, enemies);
                point = NetSpawnPoint.Safest(team, enemies);
            }
            if (point == null)
            {
                var others = new List<Vector3>();
                foreach (var p in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
                    if (p != this && p.IsSpawned) others.Add(p.transform.position);
                point = NetSpawnPoint.Safest(others);
            }
            if (point != null && TryGetComponent(out Respawn respawn))
                respawn.SetPoint(point.transform.position + Vector3.up * (spawnLift * BodyHeight()), point.transform.rotation);
        }

        /// <summary>
        /// GO in a team match (D-0182), on this PC's own player: out of any car, toy size at once, then put back at the team's
        /// base whole: full health and shield, a moment's protection, full guns and grenades, a full shift charge, the way a
        /// knock-out's comeback does it (PlayerHurt: its short black cut covers the jump, and ends a knock-out's wait), and
        /// the camera turned to face the way the base faces. Nothing when this player has no team.
        /// </summary>
        public void ToBase()
        {
            if (!IsSpawned || !IsOwner) return;
            int team = TeamMatch.TeamOf(OwnerClientId);
            if (team == TeamMatch.NoTeam) return;
            NetTravel.LeaveVehicle();   // a seat holds its driver in the car: out first
            if (TryGetComponent(out Shifter shifter)) shifter.SetSizeNow(ShiftSize.Toy);
            if (!BaseSpot(team, out Vector3 at, out Quaternion facing)) return;   // measured at toy size
            if (!TryGetComponent(out Respawn respawn)) { Put(at, facing); TurnView(facing); return; }
            respawn.SetPoint(at, facing);
            if (respawn.Busy)
            {
                // A reset's cut is already under way (a fall, a comeback that just landed): Respawn takes one put at a time
                // and would skip this one, and nothing would ask again. So ask again once it is over.
                if (!_toBaseWaiting) StartCoroutine(ToBaseWhenFree(respawn));
                return;
            }
            _turnViewOnWarp = true;
            if (TryGetComponent(out PlayerHurt hurt)) hurt.BackWhole();
            else respawn.Reset();
        }

        private IEnumerator ToBaseWhenFree(Respawn respawn)
        {
            _toBaseWaiting = true;
            while (respawn != null && respawn.Busy) yield return null;
            _toBaseWaiting = false;
            if (IsSpawned && IsOwner && TeamMatch.Running) ToBase();
        }

        /// <summary>Where this player stands at the team's base: its landing point (or, in a scene without one, the team's
        /// first comeback point), a little round its middle by their place in the team so teammates do not land inside
        /// each other, raised as every spawn is. False when the scene has neither.</summary>
        private bool BaseSpot(int team, out Vector3 at, out Quaternion facing)
        {
            NetSpawnPoint point = NetSpawnPoint.Landing(team);
            if (point == null) point = NetSpawnPoint.Safest(team, null);
            if (point == null) { at = default; facing = default; return false; }
            float height = BodyHeight();
            float turn = 90f * (TeamMatch.PlaceInTeam(OwnerClientId) % 4);
            Vector3 offset = Quaternion.Euler(0f, turn, 0f) * point.transform.right * (point.Spread * height);
            at = point.transform.position + offset + Vector3.up * (spawnLift * height);
            facing = point.transform.rotation;
            return true;
        }

        /// <summary>The scene's camera turned at once to face <paramref name="facing"/>, as a portal does after its warp.</summary>
        private static void TurnView(Quaternion facing)
        {
            ShoulderCamera cam = Camera.main != null ? Camera.main.GetComponent<ShoulderCamera>() : null;
            if (cam != null) cam.Warp(facing.eulerAngles.y);
        }

        /// <summary>The motor warped this player: after ToBase, the camera turns with them (under the black cut).</summary>
        private void OnWarped(Vector3 position, Quaternion facing)
        {
            if (!_turnViewOnWarp) return;
            _turnViewOnWarp = false;
            TurnView(facing);
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
