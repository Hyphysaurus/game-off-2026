using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's shield (D-0112): it takes hits before health does, and a while after the last hit it fills back up
    /// by itself (PlayerHealthSettings: shieldMax, shieldDelay, shieldRate). Every hit restarts the wait, even one the
    /// shield had nothing left to stop. Added and set up by PlayerHurt; the Damageable asks it first (IHitAbsorber).
    /// </summary>
    public sealed class PlayerShield : MonoBehaviour, IHitAbsorber
    {
        private PlayerHealthSettings _s;
        private Damageable _health;
        private float _backAt;

        public float Max => _s != null ? _s.shieldMax : 0f;
        public float Current { get; private set; }
        public float Share => Max > 0f ? Current / Max : 0f;
        /// <summary>True while it is filling back up.</summary>
        public bool Refilling { get; private set; }

        /// <summary>Raised when the shield changes (a hit, refilling, a refill).</summary>
        public event Action Changed;
        /// <summary>Raised when a hit empties it.</summary>
        public event Action Broke;

        public void Setup(PlayerHealthSettings settings, Damageable health)
        {
            _s = settings; _health = health;
            Current = Max;
            Changed?.Invoke();
        }

        public float Absorb(float amount)
        {
            if (_s == null) return amount;
            _backAt = Time.time + _s.shieldDelay;
            Refilling = false;
            if (Current <= 0f || amount <= 0f) return amount;
            float kept = Mathf.Min(Current, amount);
            Current -= kept;
            Changed?.Invoke();
            if (Current <= 0f) Broke?.Invoke();
            return amount - kept;
        }

        /// <summary>Full again at once (a respawn, a pickup).</summary>
        public void Refill()
        {
            Current = Max;
            Refilling = false;
            _backAt = 0f;
            Changed?.Invoke();
        }

        /// <summary>Online, another player's copy shows what that player's own PC says (NetHealth).</summary>
        public void Mirror(float current)
        {
            if (Mathf.Approximately(current, Current)) return;
            Current = Mathf.Clamp(current, 0f, Max);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (_s == null || Current >= Max || Time.time < _backAt) { Refilling = false; return; }
            if (_health != null && _health.Dead) return;
            Refilling = true;
            Current = Mathf.Min(Max, Current + _s.shieldRate * Time.deltaTime);
            Changed?.Invoke();
        }
    }
}
