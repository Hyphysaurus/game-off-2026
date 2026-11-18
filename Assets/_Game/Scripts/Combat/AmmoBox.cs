using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Ammo to walk into, after the test room's. A box gives every blaster the player carries a magazine or two for its
    /// reserve, disappears, and comes back after a while; a supply crate (Magazines 0, Respawn Seconds 0) fills every
    /// reserve to the top, fills up the grenades too, and is never used up. A player with nothing to gain leaves it where it is. Only
    /// the guns being carried gain: one lying dropped on the floor does not. It turns slowly to catch the eye and
    /// squashes when taken. The Workshop's reset key (F9) brings a taken box straight back.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class AmmoBox : MonoBehaviour, IResettable
    {
        [SerializeField, Min(0), Tooltip("Magazines' worth of rounds given to each blaster carried. Higher: a bigger refill. 0 fills every reserve to the top.")] private int magazines = 1;
        [SerializeField, Min(0f), Tooltip("Seconds before a taken box comes back. 0 = it is never used up (a supply crate).")] private float respawnSeconds = 15f;
        [SerializeField, Tooltip("How fast the model turns, degrees per second. 0 keeps it still.")] private float spin = 70f;
        [SerializeField, Tooltip("The model that turns, squashes and hides. Nothing shows the pickup if empty.")] private Transform model;
        [SerializeField, Range(0f, 0.9f), Tooltip("How far the model squashes when taken, as a share of its height. Higher: a bigger squash.")] private float squash = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("Seconds the squash takes to spring back.")] private float squashSeconds = 0.3f;

        private Collider _trigger;
        private Vector3 _modelScale;
        private float _backAt = -1f, _squashedAt = -10f;
        private bool _away;

        /// <summary>False while it is away after being taken.</summary>
        public bool Ready => !_away;
        public int Magazines => magazines;

        private void Reset()
        {
            var first = GetComponent<Collider>();
            if (first != null) first.isTrigger = true;
        }

        private void Awake()
        {
            foreach (var c in GetComponents<Collider>()) if (c.isTrigger) { _trigger = c; break; }
            if (model != null) _modelScale = model.localScale;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_away) return;
            Shifter who = PlayerTouch.Find(other, out _);   // on foot, or the first-person body
            if (who == null || !who.TryGetComponent(out WeaponHolder holder)) return;
            Give(holder);
        }

        /// <summary>Hands the ammo to <paramref name="holder"/>. False when nothing was needed (the box stays) or the
        /// box is away.</summary>
        public bool Give(WeaponHolder holder)
        {
            if (_away || holder == null) return false;
            bool took = holder.AddAmmo(magazines) > 0;
            // A supply crate also tops up the grenades (the test room's rule: only the crate, not a box).
            if (magazines <= 0 && holder.TryGetComponent(out GrenadeThrower thrower)) took = thrower.Add(thrower.MostCarried) || took;
            if (!took) return false;
            holder.PlayClip(WeaponAmmoSettings.Load().pickupClip, 1f);
            _squashedAt = Time.time;
            if (respawnSeconds <= 0f) return true;   // a supply crate: never used up
            _away = true;
            _backAt = Time.time + respawnSeconds;
            Show(false);
            return true;
        }

        public void ResetState()
        {
            if (!_away) return;
            _away = false;
            Show(true);
        }

        private void Show(bool on)
        {
            if (model != null) model.gameObject.SetActive(on);
            if (_trigger != null) _trigger.enabled = on;
        }

        private void Update()
        {
            if (_away) { if (Time.time >= _backAt) ResetState(); return; }
            if (model == null) return;
            if (spin != 0f) model.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
            // The squash: flat and wide at the moment it is taken, back to its own shape over the next moments.
            float t = Mathf.Clamp01((Time.time - _squashedAt) / squashSeconds);
            float k = squash * (1f - t) * (1f - t);
            model.localScale = Vector3.Scale(_modelScale, new Vector3(1f + k * 0.5f, 1f - k, 1f + k * 0.5f));
        }
    }
}
