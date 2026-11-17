using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// One gun's ammo, after the test room: the rounds in the gun, the rounds carried in reserve for it, and the sizes
    /// that bound them. It lives on the weapon itself (BlasterWeapon > Magazine), so the counts stay with the gun while
    /// it is put away and while it lies dropped. Every kind of ammo on the AmmoBelt (darts, paint) is fired from this one
    /// magazine: the kind only changes what leaves the barrel. A shot spends one round whatever the pellet count.
    /// Size 0 means no magazine: the gun never runs dry and never reloads (how every blaster was before D-0070).
    /// </summary>
    [Serializable]
    public sealed class Magazine
    {
        [Min(0), Tooltip("Rounds in a full magazine. Higher: more shots between reloads. 0 = no magazine: the gun never runs dry and never reloads.")]
        public int size;
        [Min(0), Tooltip("Rounds carried in reserve when the gun is first taken. Higher: more reloads before an ammo box is needed.")]
        public int reserveStart;
        [Min(0), Tooltip("The most rounds the reserve can hold. Ammo boxes stop filling here.")]
        public int reserveMax;
        [Min(0f), Tooltip("Seconds a reload takes. Higher: slower. The rounds count in three quarters of the way through, when the new magazine seats.")]
        public float reloadSeconds = 1.4f;

        private bool _filled;

        /// <summary>Rounds in the gun now.</summary>
        public int Rounds { get; private set; }
        /// <summary>Rounds carried in reserve for this gun now.</summary>
        public int Reserve { get; private set; }
        /// <summary>True for a gun with no magazine: it never runs dry.</summary>
        public bool Endless => size <= 0;
        /// <summary>True when the trigger would only click.</summary>
        public bool Empty => !Endless && Rounds <= 0;
        /// <summary>True when a reload would load something: the gun is not full and there are rounds in reserve.</summary>
        public bool CanReload => !Endless && Rounds < size && Reserve > 0;

        /// <summary>A gun as it is first taken: a full magazine and the starting reserve. Only the first call counts,
        /// so a gun dropped and taken again keeps what it had.</summary>
        public void Fill()
        {
            if (_filled) return;
            _filled = true;
            Rounds = Mathf.Max(0, size);
            Reserve = Mathf.Max(0, reserveStart);
        }

        /// <summary>One shot leaves. False when the gun is empty (nothing is spent).</summary>
        public bool Spend()
        {
            if (Endless) return true;
            if (Rounds <= 0) return false;
            Rounds--;
            return true;
        }

        /// <summary>The new magazine seats: rounds move from the reserve into the gun, as many as fit and as there are.
        /// Returns how many moved.</summary>
        public int Load()
        {
            if (Endless) return 0;
            int loaded = Mathf.Min(size - Rounds, Reserve);
            if (loaded <= 0) return 0;
            Rounds += loaded;
            Reserve -= loaded;
            return loaded;
        }

        /// <summary>An ammo box: this many magazines' worth of rounds into the reserve, up to the most it holds
        /// (0 magazines = fill it to the top). Returns how many rounds were gained.</summary>
        public int Add(int magazines)
        {
            if (Endless) return 0;
            int room = Mathf.Max(0, reserveMax - Reserve);
            int gain = magazines <= 0 ? room : Mathf.Min(room, size * magazines);
            Reserve += gain;
            return gain;
        }

        /// <summary>True when the rounds in the gun are at or under this share of a full magazine (rounded up).</summary>
        public bool Low(float share) => !Endless && Rounds <= Mathf.CeilToInt(size * share);
    }
}
