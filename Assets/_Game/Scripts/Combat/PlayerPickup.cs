using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Something to walk into (D-0115), after the AmmoBox: a heart heals, a ring fills the shield, a bomb gives
    /// grenades, a star fills the shift charge (grow into the kid: the arena's power-up). Taken, it hides and comes back
    /// after a while; a player with nothing to gain leaves it. Online (D-0131, CoopWorld) the host hands it out: a
    /// player who touches it and would gain asks the host, the first to ask has it, and it is gone for everyone until
    /// the host's timer brings it back (another player's copy here never touches it). The model (the pack's floating
    /// prop) moves by its own scripts.
    /// </summary>
    public sealed class PlayerPickup : MonoBehaviour, IResettable, ISharedPickup
    {
        public enum Kind { Heal, Shield, Grenades, Charge }

        [SerializeField] private Kind kind = Kind.Heal;
        [SerializeField, Min(0f), Tooltip("Heal: health given. Grenades: how many. Shield and Charge fill to the top.")] private float amount = 50f;
        [SerializeField, Min(0f), Tooltip("Seconds before a taken one comes back.")] private float respawnSeconds = 20f;
        [SerializeField, Tooltip("What shows; hidden while it is away.")] private GameObject model;

        private Collider _trigger;
        private float _backAt;
        private bool _away;

        public Kind Type => kind;
        /// <summary>Every power-up comes back after a while: online the host hands each one out (D-0131).</summary>
        public bool Shared => true;
        /// <summary>Its number in the party's world, the same on every PC (CoopWorld); -1 offline.</summary>
        public int NetId { get; set; } = -1;
        /// <summary>True while it is taken and not back yet.</summary>
        public bool Away => _away;

        private void Awake()
        {
            foreach (var c in GetComponents<Collider>()) if (c.isTrigger) { _trigger = c; break; }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_away) return;
            Shifter who = PlayerTouch.Find(other, out _);
            if (who == null) return;
            if (who.TryGetComponent(out NetPlayer net) && net.IsSpawned && !net.IsOwner) return;   // another player's copy
            if (CoopWorld.Shares(this)) { if (CanGive(who.gameObject)) CoopWorld.AskTake(this); return; }   // online: the host hands it out
            if (Give(who.gameObject)) { PlaySound(who.gameObject); SetAway(true); }
        }

        /// <summary>Would <paramref name="player"/> gain anything from it now?</summary>
        public bool CanGive(GameObject player)
        {
            switch (kind)
            {
                case Kind.Heal:
                    return player.TryGetComponent(out Damageable health) && !health.Dead && health.Health < health.MaxHealth - 0.01f;
                case Kind.Shield:
                    return player.TryGetComponent(out PlayerShield shield) && shield.Current < shield.Max - 0.01f;
                case Kind.Grenades:
                    return player.TryGetComponent(out GrenadeThrower thrower) && thrower.Carried < thrower.MostCarried;
                case Kind.Charge:
                    return player.TryGetComponent(out ShiftCharge charge) && charge.Charges < charge.Max;
            }
            return false;
        }

        /// <summary>The host gave it to <paramref name="player"/>, this PC's own: it takes the gift, with the sound.</summary>
        public void GiveTo(GameObject player)
        {
            if (player != null && Give(player)) PlaySound(player);
        }

        private bool Give(GameObject player)
        {
            if (!CanGive(player)) return false;
            switch (kind)
            {
                case Kind.Heal: player.GetComponent<Damageable>().Heal(amount); return true;
                case Kind.Shield: player.GetComponent<PlayerShield>().Refill(); return true;
                case Kind.Grenades: return player.GetComponent<GrenadeThrower>().Add(Mathf.Max(1, Mathf.RoundToInt(amount)));
                case Kind.Charge: player.GetComponent<ShiftCharge>().Refill(); return true;
            }
            return false;
        }

        private static void PlaySound(GameObject player)
        {
            if (player.TryGetComponent(out WeaponHolder holder)) holder.PlayClip(WeaponAmmoSettings.Load().pickupClip, 1f);   // the ammo box's sound
        }

        /// <summary>Taken (hidden until its timer brings it back) or back. Online, the host's word on every PC.</summary>
        public void SetAway(bool away)
        {
            if (!away) { ResetState(); return; }
            _away = true;
            _backAt = Time.time + respawnSeconds;
            Show(false);
        }

        public void ResetState()
        {
            if (!_away) return;
            _away = false;
            Show(true);
        }

        private void Show(bool on)
        {
            if (model != null) model.SetActive(on);
            if (_trigger != null) _trigger.enabled = on;
        }

        private void Update()
        {
            // Online, not the host: it comes back when the host's does (CoopWorld).
            if (_away && Time.time >= _backAt && !CoopWorld.Waits(this)) { ResetState(); CoopWorld.PickupBack(this); }
        }
    }
}
