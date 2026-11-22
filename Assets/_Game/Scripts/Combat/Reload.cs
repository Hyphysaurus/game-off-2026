using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// One reload, timed, after the test room: the gun tips in front of the chest, the old magazine drops out with the
    /// free hand, a new one comes up, and the rounds count in when it seats (three quarters of the way through). Stopped
    /// before the seat it loads nothing; stopped after, the rounds are already in. It only keeps the time and says how
    /// far each part has got: the holder moves the gun, the arms and the magazine from that.
    /// Not a component: the WeaponHolder owns one.
    /// </summary>
    public sealed class Reload
    {
        private float _left, _total;
        private bool _seated;

        /// <summary>The gun being reloaded; null when no reload runs.</summary>
        public BlasterWeapon Weapon { get; private set; }
        public bool Active => Weapon != null;
        /// <summary>How far through the reload we are, 0 to 1.</summary>
        public float Done => Active && _total > 0f ? 1f - _left / _total : 0f;
        /// <summary>The whole length of this reload, seconds.</summary>
        public float Length => _total;
        /// <summary>True once the new magazine has seated: the rounds are in, whatever stops the reload now.</summary>
        public bool Seated => Active && _seated;

        /// <summary>Starts reloading <paramref name="weapon"/>. False when there is nothing to do (it has no magazine,
        /// it is full, or the reserve is empty) or one is running already.</summary>
        public bool Begin(BlasterWeapon weapon, WeaponAmmoSettings s)
        {
            if (Active || weapon == null || !weapon.Magazine.CanReload) return false;
            Weapon = weapon;
            _total = Mathf.Max(weapon.Magazine.reloadSeconds, s.shortestReload, 0.01f);
            _left = _total;
            _seated = false;
            return true;
        }

        /// <summary>Another player's reload, played on this PC (D-0129): the same moves over <paramref name="seconds"/>,
        /// the length their PC gave it. Nothing is checked or loaded here: their PC keeps their rounds. A reload already
        /// running starts over.</summary>
        public bool Mirror(BlasterWeapon weapon, float seconds)
        {
            if (weapon == null || seconds <= 0f) return false;
            Weapon = weapon;
            _total = seconds;
            _left = seconds;
            _seated = false;
            return true;
        }

        /// <summary>One step. <paramref name="seated"/> is true on the step the new magazine seats (count the rounds
        /// in, play the sound); the return is true on the step the reload is over.</summary>
        public bool Tick(float deltaTime, WeaponAmmoSettings s, out bool seated)
        {
            seated = false;
            if (!Active) return false;
            _left = Mathf.Max(0f, _left - deltaTime);
            if (!_seated && Done >= s.magazineBack.y) { _seated = true; seated = true; }
            return _left <= 0f;
        }

        /// <summary>Ends the reload wherever it is.</summary>
        public void Stop() { Weapon = null; _left = 0f; }

        /// <summary>How far the gun is in its reload position: 0 the normal hold, 1 tipped in front of the chest.</summary>
        public float Pose(WeaponAmmoSettings s) => Active ? Ease(0f, s.poseInBy, Done) * (1f - Ease(s.poseOutFrom, 1f, Done)) : 0f;

        /// <summary>How far the magazine is out of the gun: 0 seated, 1 fully dropped.</summary>
        public float MagazineOut(WeaponAmmoSettings s) => Active ? Ease(s.magazineOut.x, s.magazineOut.y, Done) * (1f - Ease(s.magazineBack.x, s.magazineBack.y, Done)) : 0f;

        /// <summary>True for the moment the magazine is not shown (the old one gone, the new one not yet up).</summary>
        public bool MagazineHidden(WeaponAmmoSettings s) => Active && Done > s.magazineGone.x && Done < s.magazineGone.y;

        /// <summary>0 before <paramref name="from"/>, 1 after <paramref name="to"/>, eased in between.</summary>
        private static float Ease(float from, float to, float x) => to > from ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x)) : x >= to ? 1f : 0f;
    }
}
