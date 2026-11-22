using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>A pickup the host hands out online (D-0131): the Stadium's power-ups (PlayerPickup) and the ammo boxes.</summary>
    public interface ISharedPickup
    {
        /// <summary>False for one that is never used up (a supply crate): each PC keeps its own.</summary>
        bool Shared { get; }
        /// <summary>Its number in the party's world (the same on every PC); -1 when it is not shared.</summary>
        int NetId { get; set; }
        /// <summary>True while it is taken and not back yet.</summary>
        bool Away { get; }
        /// <summary>The host's word: taken (hidden; on the host its timer starts) or back.</summary>
        void SetAway(bool away);
        /// <summary>Would <paramref name="player"/> gain anything from it now? Only then is the host asked.</summary>
        bool CanGive(GameObject player);
        /// <summary>The host gave it to <paramref name="player"/>, this PC's own: hand it over, with its sound.</summary>
        void GiveTo(GameObject player);
    }

    /// <summary>
    /// Co-op step 2 (D-0131): online, the host's PC runs the world's toy soldiers, breakable props and pickups, and every
    /// other PC shows the host's. Before, each PC ran its own copy: a soldier one player knocked down still stood and shot
    /// on the other's screen, and both could take the same heart.
    ///  - Every PC numbers them the same way when a party world loads (Bind: sorted by their place in the scene, checked
    ///    against the host's with a hash), so nothing is added to the scenes or the prefabs.
    ///  - The host: its soldiers think, aim at the nearest player they can see (ToySoldier), and fire real darts that hurt
    ///    whoever they hit (a dart landing on another player's copy is handed to that player's PC, NetHealth). Ten times a
    ///    second their places go to everyone; each fall, getting up, shot, break and return as it happens.
    ///  - Everyone else: their copies only show the host's (UseMirror). A hit on a soldier or a prop goes to the host
    ///    (Damageable.Relay), which takes it as its own; a pickup touched asks the host, which gives it to whoever was
    ///    first and hides it for everyone until its own timer brings it back.
    ///  - A player arriving asks for the state (who is down, what is broken, what is taken).
    /// Messages travel on the players' own network objects (NetWorld). Single player is untouched: none of this binds
    /// unless a party runs.
    /// </summary>
    public static class CoopWorld
    {
        public const byte SoldierKind = 0, PropKind = 1;
        private const float PoseEvery = 0.1f;   // seconds between the soldiers' places sent: ten a second

        private static readonly List<ToySoldier> Soldiers = new List<ToySoldier>();
        private static readonly List<Breakable> Props = new List<Breakable>();
        private static readonly List<ISharedPickup> Pickups = new List<ISharedPickup>();
        private static bool _bound, _host, _mirroring;
        private static uint _hash;
        private static float _nextPoses;
        private static Vector3[] _at = new Vector3[0];
        private static byte[] _yaw = new byte[0];

        /// <summary>True while this party world's soldiers, props and pickups are shared.</summary>
        public static bool Active => _bound && NetSession.InParty;
        /// <summary>True on the host: this PC runs them.</summary>
        public static bool IsHost => Active && _host;
        /// <summary>True on every other PC while its copies show the host's.</summary>
        public static bool Mirroring => Active && !_host && _mirroring;

        /// <summary>True when this pickup is the host's to hand out.</summary>
        public static bool Shares(ISharedPickup p) => p != null && p.NetId >= 0 && (IsHost || Mirroring);
        /// <summary>True when this pickup waits for the host's word to come back.</summary>
        public static bool Waits(ISharedPickup p) => p != null && p.NetId >= 0 && Mirroring;

        // ---------------------------------------------------------------- binding

        /// <summary>A party world loaded (OnlineWorld): number its soldiers, props and pickups, the same on every PC. The
        /// host runs them from now on; any other PC turns its own into copies of the host's and asks for the state.</summary>
        public static void Bind(Scene scene)
        {
            Clear();
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !scene.IsValid()) return;
            var soldiers = new List<(string path, ToySoldier s)>();
            var props = new List<(string path, Breakable b)>();
            var pickups = new List<(string path, ISharedPickup p)>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var s in root.GetComponentsInChildren<ToySoldier>(false)) soldiers.Add((PathOf(s.transform), s));
                foreach (var b in root.GetComponentsInChildren<Breakable>(false)) props.Add((PathOf(b.transform), b));
                foreach (var p in root.GetComponentsInChildren<PlayerPickup>(false)) if (p.Shared) pickups.Add((PathOf(p.transform), p));
                foreach (var p in root.GetComponentsInChildren<AmmoBox>(false)) if (p.Shared) pickups.Add((PathOf(p.transform), p));
            }
            soldiers.Sort((x, y) => string.CompareOrdinal(x.path, y.path));
            props.Sort((x, y) => string.CompareOrdinal(x.path, y.path));
            pickups.Sort((x, y) => string.CompareOrdinal(x.path, y.path));

            uint hash = 2166136261u;
            hash = Fnv(hash, "soldiers");
            foreach (var (path, s) in soldiers) { s.NetId = Soldiers.Count; Soldiers.Add(s); hash = Fnv(hash, path); }
            hash = Fnv(hash, "props");
            foreach (var (path, b) in props) { b.NetId = Props.Count; Props.Add(b); hash = Fnv(hash, path); }
            hash = Fnv(hash, "pickups");
            foreach (var (path, p) in pickups) { p.NetId = Pickups.Count; Pickups.Add(p); hash = Fnv(hash, path); }
            _hash = hash;
            _bound = true;
            _host = nm.IsServer;
            _nextPoses = 0f;

            if (!_host)
            {
                _mirroring = true;
                foreach (var s in Soldiers) { var soldier = s; soldier.UseMirror(hit => RelayHit(SoldierKind, soldier.NetId, !soldier.Down, hit)); }
                foreach (var b in Props) { var prop = b; prop.UseMirror(hit => RelayHit(PropKind, prop.NetId, !prop.Broken, hit)); }
                AskState();
            }
            Debug.Log($"CoopWorld: {scene.name}: {Soldiers.Count} soldiers, {Props.Count} breakable props and {Pickups.Count} pickups shared; " +
                      (_host ? "this PC runs them (host)." : "the host runs them, this PC shows its."));
        }

        /// <summary>A scene is loading: whatever was bound went with the last one.</summary>
        public static void Clear()
        {
            Soldiers.Clear(); Props.Clear(); Pickups.Clear();
            _bound = _host = _mirroring = false;
            _hash = 0u;
        }

        /// <summary>The host's world turned out not to match this one: every copy goes back to being this PC's own.</summary>
        private static void Unmirror()
        {
            _mirroring = false;
            foreach (var s in Soldiers) if (s != null) s.LeaveMirror();
            foreach (var b in Props) if (b != null) b.LeaveMirror();
        }

        /// <summary>Where a thing sits in its scene: the root's name, then each part's name and place among its siblings.
        /// The same scene file gives the same path on every PC.</summary>
        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform x = t; x != null; x = x.parent)
                parts.Add(x.parent == null ? x.name : x.name + "#" + x.GetSiblingIndex());
            parts.Reverse();
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++) { if (i > 0) sb.Append('/'); sb.Append(parts[i]); }
            return sb.ToString();
        }

        private static uint Fnv(uint h, string s)
        {
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            h ^= '|'; h *= 16777619u;
            return h;
        }

        private static bool Gone(object o) => o == null || (o is Object u && u == null);

        // ---------------------------------------------------------------- asking the host (any PC but the host)

        /// <summary>Ask the host what its world looks like now (on binding, and once this PC's own player is spawned).</summary>
        public static void AskState()
        {
            if (!_bound || _host || !_mirroring) return;
            if (NetWorld.Local != null && NetWorld.Local.IsSpawned) NetWorld.Local.WantStateRpc();
        }

        /// <summary>A copy was hit here: the hit goes to the host, which takes it on its own (the copy only flashes).
        /// False (no hit marker) when it is down or broken, or there is no way to the host.</summary>
        private static bool RelayHit(byte kind, int id, bool standing, DamageInfo hit)
        {
            if (!Mirroring || !standing || id < 0 || NetWorld.Local == null || !NetWorld.Local.IsSpawned) return false;
            NetWorld.Local.HitRpc(kind, (ushort)id, hit.Amount, hit.Knock, hit.Point, hit.Loose);
            return true;
        }

        /// <summary>This PC's own player touched a shared pickup it would gain from: ask the host for it (on the host, ask
        /// ourselves).</summary>
        public static void AskTake(ISharedPickup p)
        {
            if (!Shares(p)) return;
            if (_host) { OnTake(NetworkManager.Singleton.LocalClientId, (ushort)p.NetId); return; }
            if (NetWorld.Local != null && NetWorld.Local.IsSpawned) NetWorld.Local.TakeRpc((ushort)p.NetId);
        }

        // ---------------------------------------------------------------- the host tells everyone

        /// <summary>Each frame on the host: the soldiers' places to everyone, ten times a second, when anyone else is here.</summary>
        public static void HostTick()
        {
            if (!IsHost || Soldiers.Count == 0 || NetWorld.Host == null) return;
            if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2 || Time.unscaledTime < _nextPoses) return;
            _nextPoses = Time.unscaledTime + PoseEvery;
            if (_at.Length != Soldiers.Count) { _at = new Vector3[Soldiers.Count]; _yaw = new byte[Soldiers.Count]; }
            for (int i = 0; i < Soldiers.Count; i++)
            {
                var s = Soldiers[i];
                if (s == null) continue;
                _at[i] = s.transform.position;
                _yaw[i] = (byte)(Mathf.RoundToInt(Mathf.Repeat(s.transform.eulerAngles.y, 360f) / 360f * 256f) & 255);
            }
            NetWorld.Host.SoldiersRpc(_at, _yaw);
        }

        private static bool Telling => IsHost && NetWorld.Host != null && NetWorld.Host.IsSpawned && NetworkManager.Singleton.ConnectedClientsIds.Count > 1;

        /// <summary>The host's soldier fired: a harmless copy of its dart flies on every other PC.</summary>
        public static void SoldierShot(ToySoldier s, Vector3 from, Vector3 direction)
        {
            if (Telling && s.NetId >= 0) NetWorld.Host.SoldierShotRpc((ushort)s.NetId, from, direction);
        }

        /// <summary>The host's soldier was knocked down (<paramref name="point"/>: where the last hit landed).</summary>
        public static void SoldierDown(ToySoldier s, Vector3 point)
        {
            if (Telling && s.NetId >= 0) NetWorld.Host.SoldierDownRpc((ushort)s.NetId, point);
        }

        /// <summary>The host's soldier got up at its post.</summary>
        public static void SoldierUp(ToySoldier s)
        {
            if (Telling && s.NetId >= 0) NetWorld.Host.SoldierUpRpc((ushort)s.NetId);
        }

        /// <summary>The host's prop broke, from <paramref name="from"/>, shoved by <paramref name="knock"/>.</summary>
        public static void Broke(Breakable b, Vector3 from, Vector3 knock)
        {
            if (Telling && b.NetId >= 0) NetWorld.Host.BrokeRpc((ushort)b.NetId, from, knock);
        }

        /// <summary>The host's prop came back.</summary>
        public static void CameBack(Breakable b)
        {
            if (Telling && b.NetId >= 0) NetWorld.Host.BackRpc((ushort)b.NetId);
        }

        /// <summary>The host's pickup came back.</summary>
        public static void PickupBack(ISharedPickup p)
        {
            if (Telling && p.NetId >= 0) NetWorld.Host.PickupBackRpc((ushort)p.NetId);
        }

        // ---------------------------------------------------------------- on the host: what the others ask

        /// <summary>A player's hit on a copy of one of our soldiers or props: ours takes it, from that player.</summary>
        public static void OnHit(ulong sender, byte kind, ushort id, float amount, Vector3 knock, Vector3 point, float loose)
        {
            if (!IsHost) return;
            Component target = null;
            if (kind == SoldierKind && id < Soldiers.Count) target = Soldiers[id];
            else if (kind == PropKind && id < Props.Count) target = Props[id];
            if (target == null || !target.TryGetComponent(out Damageable health)) return;
            health.Hit(new DamageInfo(amount, knock, point, PlayerOf(sender), loose));
        }

        /// <summary>A player (or the host) wants a pickup: the first to ask has it; it goes away for everyone.</summary>
        public static void OnTake(ulong taker, ushort id)
        {
            if (!IsHost || id >= Pickups.Count) return;
            ISharedPickup p = Pickups[id];
            if (Gone(p) || p.Away) return;   // someone was first
            p.SetAway(true);
            if (taker == NetworkManager.Singleton.LocalClientId) GiveOwn(p);
            if (Telling) NetWorld.Host.PickupTakenRpc(id, taker);
        }

        /// <summary>A player arrived: tell them who is down, what is broken and what is taken, with our numbering's hash.</summary>
        public static void OnWantState(NetWorld asker)
        {
            if (asker == null) return;
            if (!IsHost) { asker.StateRpc(0u, new byte[0], new byte[0], new byte[0]); return; }   // not running this world: theirs stays their own
            var down = new byte[Soldiers.Count];
            for (int i = 0; i < down.Length; i++) down[i] = (byte)(Soldiers[i] != null && Soldiers[i].Down ? 1 : 0);
            var broken = new byte[Props.Count];
            for (int i = 0; i < broken.Length; i++) broken[i] = (byte)(Props[i] != null && Props[i].Broken ? 1 : 0);
            var away = new byte[Pickups.Count];
            for (int i = 0; i < away.Length; i++) away[i] = (byte)(!Gone(Pickups[i]) && Pickups[i].Away ? 1 : 0);
            asker.StateRpc(_hash, down, broken, away);
        }

        /// <summary>The player object of <paramref name="client"/> here (the source of their hits), or null.</summary>
        private static Transform PlayerOf(ulong client)
        {
            foreach (var p in NetPlayer.Spawned) if (p != null && p.OwnerClientId == client) return p.transform;
            return null;
        }

        private static void GiveOwn(ISharedPickup p)
        {
            if (NetWorld.Local != null) p.GiveTo(NetWorld.Local.gameObject);
        }

        // ---------------------------------------------------------------- on every other PC: what the host says

        /// <summary>The host's world on arrival. A different numbering (the scenes differ) keeps everything this PC's own.</summary>
        public static void OnState(uint hash, byte[] down, byte[] broken, byte[] away)
        {
            if (!_bound || _host || !_mirroring) return;
            if (hash != _hash || down == null || broken == null || away == null
                || down.Length != Soldiers.Count || broken.Length != Props.Count || away.Length != Pickups.Count)
            {
                Debug.LogWarning($"CoopWorld: the host's world does not match this PC's (hash {hash:X8} vs {_hash:X8}; host {down?.Length} soldiers, " +
                                 $"{broken?.Length} props, {away?.Length} pickups; here {Soldiers.Count}, {Props.Count}, {Pickups.Count}): they stay this PC's own.");
                Unmirror();
                return;
            }
            for (int i = 0; i < Soldiers.Count; i++)
            {
                var s = Soldiers[i];
                if (s == null) continue;
                if (down[i] != 0) s.MirrorDown(s.transform.position, true); else s.MirrorUp();
            }
            for (int i = 0; i < Props.Count; i++)
            {
                var b = Props[i];
                if (b == null) continue;
                if (broken[i] != 0) b.MirrorBroken(); else b.MirrorBack();
            }
            for (int i = 0; i < Pickups.Count; i++) if (!Gone(Pickups[i]) && Pickups[i].Away != (away[i] != 0)) Pickups[i].SetAway(away[i] != 0);
        }

        public static void OnPoses(Vector3[] at, byte[] yaw)
        {
            if (!Mirroring || at == null || yaw == null || at.Length != Soldiers.Count || yaw.Length != Soldiers.Count) return;
            for (int i = 0; i < Soldiers.Count; i++) if (Soldiers[i] != null) Soldiers[i].MirrorPose(at[i], yaw[i] * (360f / 256f));
        }

        public static void OnSoldierShot(ushort id, Vector3 from, Vector3 direction)
        {
            if (Mirroring && id < Soldiers.Count && Soldiers[id] != null) Soldiers[id].MirrorShot(from, direction);
        }

        public static void OnSoldierDown(ushort id, Vector3 point)
        {
            if (Mirroring && id < Soldiers.Count && Soldiers[id] != null) Soldiers[id].MirrorDown(point, false);
        }

        public static void OnSoldierUp(ushort id)
        {
            if (Mirroring && id < Soldiers.Count && Soldiers[id] != null) Soldiers[id].MirrorUp();
        }

        public static void OnBroke(ushort id, Vector3 from, Vector3 knock)
        {
            if (Mirroring && id < Props.Count && Props[id] != null) Props[id].MirrorBreak(from, knock);
        }

        public static void OnBack(ushort id)
        {
            if (Mirroring && id < Props.Count && Props[id] != null) Props[id].MirrorBack();
        }

        public static void OnPickupTaken(ushort id, ulong taker)
        {
            if (!Mirroring || id >= Pickups.Count || Gone(Pickups[id])) return;
            Pickups[id].SetAway(true);
            if (NetworkManager.Singleton != null && taker == NetworkManager.Singleton.LocalClientId) GiveOwn(Pickups[id]);
        }

        public static void OnPickupBack(ushort id)
        {
            if (Mirroring && id < Pickups.Count && !Gone(Pickups[id])) Pickups[id].SetAway(false);
        }
    }
}
