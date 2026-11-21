using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What other players see of this player's fighting (D-0117), on the NetPlayer. Every shot this player fires is sent
    /// on (fast and lossy: a lost dart's look does not matter, lateness does) and the other PCs fly a harmless copy of
    /// it, no damage, no shove, with its sound: the damage itself still comes from the shooter's PC (NetHealth). The
    /// weapon in this player's hand is sent as a number into Weapons; other PCs show a copy of it at the body's right
    /// hand, pointing the way the body faces, while it is drawn.
    /// </summary>
    public sealed class NetCombat : NetworkBehaviour
    {
        [SerializeField, Tooltip("Every shot a player can fire (FoamDart, PaintBall...): a shot is sent as its place here.")]
        private Projectile[] shots = System.Array.Empty<Projectile>();
        [SerializeField, Tooltip("Every weapon a player can hold, matched by name (DartPistol, DartSMG...).")]
        private Weapon[] weapons = System.Array.Empty<Weapon>();

        private const int None = -1;
        private readonly NetworkVariable<int> _held = new NetworkVariable<int>(None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private WeaponHolder _holder;
        private GameObject _shown; private int _shownIndex = None;
        private Transform _hand;

        public override void OnNetworkSpawn()
        {
            _holder = GetComponent<WeaponHolder>();
            if (IsOwner) { Projectile.Fired += OnFired; GrenadeThrower.Released += OnThrown; }
            else _held.OnValueChanged += (_, now) => Show(now);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) { Projectile.Fired -= OnFired; GrenadeThrower.Released -= OnThrown; }
            if (_shown != null) Destroy(_shown);
        }

        // ---------------------------------------------------------------- shots

        private void OnFired(Projectile prefab, Vector3 position, Vector3 direction, float scale, Transform owner, float speed)
        {
            if (owner != transform || !IsSpawned) return;   // only this player's own shots, not a copy we are flying
            int i = System.Array.IndexOf(shots, prefab);
            if (i < 0) return;
            ShotRpc((byte)i, position, direction, scale, speed);
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        private void ShotRpc(byte index, Vector3 position, Vector3 direction, float scale, float speed)
        {
            if (index >= shots.Length || shots[index] == null) return;
            Projectile.Fire(shots[index], position, direction, 0f, 0f, scale, transform, null, speed);   // a look: no damage, no shove
            AudioClip clip = shots[index].FireClip();
            if (clip != null) AudioSource.PlayClipAtPoint(clip, position, 0.6f);
        }

        // ---------------------------------------------------------------- grenades

        private void OnThrown(GrenadeThrower thrower, Vector3 from, Vector3 velocity, Vector3 spin, float scale)
        {
            if (thrower == null || thrower.gameObject != gameObject || !IsSpawned) return;
            GrenadeRpc(from, velocity, spin, scale);
        }

        [Rpc(SendTo.NotMe)]   // reliable: a grenade is rare and its blast is worth seeing
        private void GrenadeRpc(Vector3 from, Vector3 velocity, Vector3 spin, float scale)
        {
            GrenadeSettings s = GrenadeSettings.Load();
            if (s == null || s.grenade == null) return;
            Grenade copy = Instantiate(s.grenade, from, Quaternion.identity);
            copy.LookOnly = true;
            copy.Throw(transform, null, scale, velocity, spin);
            if (s.throwClip != null) PaintSplats.InScene().Play(s.throwClip, from);
        }

        // ---------------------------------------------------------------- the weapon in hand

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                int now = None;
                if (_holder != null && _holder.Drawn && _holder.Current != null) now = IndexOf(_holder.Current);
                if (_held.Value != now) _held.Value = now;
            }
        }

        private void LateUpdate()
        {
            if (_shown == null) return;
            if (_hand == null) FindHand();
            Vector3 at = _hand != null ? _hand.position : transform.position + transform.up * (0.55f * Body()) + transform.right * (0.2f * Body());
            _shown.transform.SetPositionAndRotation(at, transform.rotation);
        }

        private int IndexOf(Weapon w)
        {
            string name = w.name.Replace("(Clone)", "").Trim();
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null && weapons[i].name == name) return i;
            return None;
        }

        private void Show(int index)
        {
            if (index == _shownIndex) return;
            _shownIndex = index;
            if (_shown != null) { Destroy(_shown); _shown = null; }
            if (index < 0 || index >= weapons.Length || weapons[index] == null) return;
            _shown = Instantiate(weapons[index].gameObject);
            _shown.name = "Shown_" + weapons[index].name;
            foreach (var b in _shown.GetComponentsInChildren<Behaviour>(true)) b.enabled = false;   // a look only
            foreach (var c in _shown.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in _shown.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            _shown.transform.localScale = weapons[index].transform.localScale * transform.lossyScale.y;
        }

        private void FindHand()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private float Body()
        {
            var cc = GetComponent<CharacterController>();
            return cc != null ? cc.height * transform.lossyScale.y : 1f;
        }
    }
}
