using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Something to walk into (D-0115), after the AmmoBox: a heart heals, a ring fills the shield, a bomb gives
    /// grenades, a star fills the shift charge (grow into the kid: the arena's power-up). Taken, it hides and comes back
    /// after a while; a player with nothing to gain leaves it. Online each PC has its own copy and only its own player
    /// takes it (another player's copy here never does). The model (the pack's floating prop) moves by its own scripts.
    /// </summary>
    public sealed class PlayerPickup : MonoBehaviour, IResettable
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
            if (Give(who.gameObject)) Take(who.gameObject);
        }

        private bool Give(GameObject player)
        {
            switch (kind)
            {
                case Kind.Heal:
                    if (!player.TryGetComponent(out Damageable health) || health.Dead || health.Health >= health.MaxHealth - 0.01f) return false;
                    health.Heal(amount);
                    return true;
                case Kind.Shield:
                    if (!player.TryGetComponent(out PlayerShield shield) || shield.Current >= shield.Max - 0.01f) return false;
                    shield.Refill();
                    return true;
                case Kind.Grenades:
                    return player.TryGetComponent(out GrenadeThrower thrower) && thrower.Add(Mathf.Max(1, Mathf.RoundToInt(amount)));
                case Kind.Charge:
                    if (!player.TryGetComponent(out ShiftCharge charge) || charge.Charges >= charge.Max) return false;
                    charge.Refill();
                    return true;
            }
            return false;
        }

        private void Take(GameObject player)
        {
            if (player.TryGetComponent(out WeaponHolder holder)) holder.PlayClip(WeaponAmmoSettings.Load().pickupClip, 1f);   // the ammo box's sound
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
            if (_away && Time.time >= _backAt) ResetState();
        }
    }
}
