using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// E to take a weapon. Two kinds: a stand in the world that hands out copies of a weapon prefab (its display model
    /// spins and bobs, hides while taken, and comes back after a while), or a dropped weapon lying loose, which hands
    /// over itself. Works for the third-person Interactor and for the first-person hands.
    /// </summary>
    public sealed class WeaponPickup : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("The weapon prefab handed out (or this object's own Weapon, when dropped).")] private Weapon weapon;
        [SerializeField, Tooltip("Shown on the stand; hidden while the weapon is out.")] private Transform display;
        [SerializeField, Tooltip("Seconds before the stand offers another. 0 = only once.")] private float respawnSeconds = 2f;
        [SerializeField, Tooltip("Display spin, degrees per second.")] private float spin = 60f;
        [SerializeField, Tooltip("Display bob, units at the pickup's scale.")] private float bob = 0.08f;

        private float _backAt = -1f;
        private Vector3 _displayRest;
        private bool _gone;

        private bool Dropped => weapon != null && weapon.gameObject == gameObject;
        public bool Available => enabled && weapon != null && !_gone && (Dropped ? weapon.Holder == null : true);
        public Weapon Weapon => weapon;

        /// <summary>Makes this a dropped weapon that offers itself.</summary>
        public void Offer(Weapon w) { weapon = w; display = null; respawnSeconds = 0f; }

        private void Awake()
        {
            if (display == null) return;
            _displayRest = display.localPosition;
            // The gun shown on a stand is the bare model, not a weapon: its sight takes the game's look here (D-0166).
            SightLook look = SightLook.Load();
            if (look != null) look.Apply(display, weapon != null ? weapon.Reticle : null);
        }

        public string PromptFor(Interactor who)
        {
            if (!Available || who == null) return null;
            var holder = who.GetComponent<WeaponHolder>();
            if (holder == null || !holder.isActiveAndEnabled) return null;
            return holder.Full ? "Swap for " + weapon.DisplayName : "Take " + weapon.DisplayName;
        }

        public void Interact(Interactor who)
        {
            if (who != null) Give(who.GetComponent<WeaponHolder>());
        }

        /// <summary>Hands the weapon to <paramref name="holder"/>. False if there was nothing to give.</summary>
        public bool Give(WeaponHolder holder)
        {
            if (holder == null || !Available) return false;
            if (holder.Equip(weapon) == null) return false;
            GameEvents.RaiseWeaponTaken(holder, weapon);
            if (Dropped) return true;                 // it is in the hand now; its WeaponLooks switched this off
            _gone = true;
            if (display != null) display.gameObject.SetActive(false);
            _backAt = respawnSeconds > 0f ? Time.time + respawnSeconds : -1f;
            return true;
        }

        private void Update()
        {
            if (_gone && _backAt > 0f && Time.time >= _backAt)
            {
                _gone = false;
                if (display != null) display.gameObject.SetActive(true);
            }
            if (display != null && display.gameObject.activeSelf)
            {
                display.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
                display.localPosition = _displayRest + Vector3.up * (bob * Mathf.Sin(Time.time * 2.2f));
            }
        }
    }
}
