using Unity.Netcode;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What other players see of this player's fighting (D-0117), on the NetPlayer. Every shot this player fires is sent
    /// on (fast and lossy: a lost dart's look does not matter, lateness does) and the other PCs fly a harmless copy of
    /// it, no damage, no shove, with its sound: the damage itself still comes from the shooter's PC (NetHealth). The
    /// weapon in this player's hand is sent as a number into Weapons, with whether their aim is held and how high it
    /// points; on other PCs this player's own WeaponHolder, made a look-only copy (D-0128), holds a copy of that weapon
    /// exactly as they hold it: in the fist, both hands on a long gun, the arm up on the aim and after a shot.
    /// </summary>
    public sealed class NetCombat : NetworkBehaviour
    {
        [SerializeField, Tooltip("Every shot a player can fire (FoamDart, PaintBall...): a shot is sent as its place here.")]
        private Projectile[] shots = System.Array.Empty<Projectile>();
        [SerializeField, Tooltip("Every weapon a player can hold, matched by name (DartPistol, DartSMG...).")]
        private Weapon[] weapons = System.Array.Empty<Weapon>();

        private const int None = -1;
        private readonly NetworkVariable<int> _held = new NetworkVariable<int>(None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> _aimHeld = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<sbyte> _pitch = new NetworkVariable<sbyte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);   // degrees above level

        private WeaponHolder _holder;
        private int _shownIndex = None;

        public override void OnNetworkSpawn()
        {
            _holder = GetComponent<WeaponHolder>();
            if (IsOwner) { Projectile.Fired += OnFired; GrenadeThrower.Released += OnThrown; return; }
            if (_holder != null) _holder.UsePuppet();
            _held.OnValueChanged += OnHeldChanged;
            // The weapon already in their hand when this copy appeared (OnValueChanged reports only later changes: a
            // friend who joined after the host drew a gun never saw it, D-0128).
            Show(_held.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) { Projectile.Fired -= OnFired; GrenadeThrower.Released -= OnThrown; return; }
            _held.OnValueChanged -= OnHeldChanged;
            if (_holder != null) _holder.ShowPuppet(null);
            _shownIndex = None;
        }

        private void OnHeldChanged(int before, int now) => Show(now);

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
            if (_holder != null) _holder.PuppetShot();   // their arm comes up with the shot
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
            if (!IsSpawned || _holder == null) return;
            if (!IsOwner) { _holder.PuppetAim(_aimHeld.Value, _pitch.Value); return; }
            // Armed: drawn, in hand, and not put away for a carried thing or a car seat (the holder is off when seated).
            int now = _holder.Armed ? IndexOf(_holder.Current) : None;
            if (_held.Value != now) _held.Value = now;
            bool aiming = now != None && _holder.Aiming;
            if (_aimHeld.Value != aiming) _aimHeld.Value = aiming;
            if (now == None) return;
            float y = Mathf.Clamp(_holder.AimForward.y, -1f, 1f);
            int pitch = Mathf.Clamp(Mathf.RoundToInt(Mathf.Asin(y) * Mathf.Rad2Deg), -90, 90);
            if (Mathf.Abs(pitch - _pitch.Value) >= 2) _pitch.Value = (sbyte)pitch;   // 2 degrees: the arm reads the same, half the sends
        }

        private int IndexOf(Weapon w)
        {
            string name = w.name.Replace("(Clone)", "").Trim();
            for (int i = 0; i < weapons.Length; i++) if (weapons[i] != null && weapons[i].name == name) return i;
            return None;
        }

        private void Show(int index)
        {
            if (index == _shownIndex || _holder == null) return;
            _shownIndex = index;
            _holder.ShowPuppet(index >= 0 && index < weapons.Length ? weapons[index] : null);
        }
    }
}
