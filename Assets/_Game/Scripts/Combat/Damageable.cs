using System;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>One hit: how much, which way it shoves (a velocity, world units per second, already scaled by the
    /// attacker's size), where it landed and who dealt it.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Knock;
        public readonly Vector3 Point;
        public readonly Transform Source;
        public DamageInfo(float amount, Vector3 knock, Vector3 point, Transform source)
        { Amount = amount; Knock = knock; Point = point; Source = source; }
    }

    /// <summary>
    /// Health for anything that can be hit: the player, cars, practice targets, toy soldiers. Takes a DamageInfo, ignores
    /// hits during a short invulnerability window, flashes the renderers, passes the shove on (Rigidbody, PlayerMotor or the
    /// first-person body) and announces damage and death. It does not decide what death means: listeners do.
    /// </summary>
    public sealed class Damageable : MonoBehaviour, IResettable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float maxHealth = 100f;
        [SerializeField, Tooltip("Seconds after a hit during which further hits are ignored.")] private float invulnerability = 0.1f;
        [SerializeField, Tooltip("Colour the renderers flash on a hit.")] private Color flashColor = new Color(1f, 0.45f, 0.2f);
        [SerializeField] private float flashSeconds = 0.1f;
        [SerializeField, Tooltip("Pass the hit's shove on to this object's Rigidbody or motor.")] private bool takeKnockback = true;
        [SerializeField, Tooltip("Float the damage taken above the hit point.")] private bool showNumbers;
        [SerializeField, Tooltip("Seconds after death before health refills by itself. 0 = stays dead until reset.")] private float refillAfterDeath;
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onDied;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private float _flashUntil, _safeUntil, _refillAt;
        private bool _flashing;

        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        public bool Dead => Health <= 0f;
        public bool Protected => Time.time < _safeUntil;
        /// <summary>Share of every hit that is taken (1 = all of it). The player's block lowers it; 0 turns hits away
        /// like protection does (the middle of a dodge roll).</summary>
        public float DamageScale { get; set; } = 1f;
        /// <summary>The Inspector's death event, for editor tools that wire it.</summary>
        public UnityEvent OnDiedEvent => onDied;
        /// <summary>Raised after a hit is taken.</summary>
        public event Action<DamageInfo> Damaged;
        /// <summary>Raised once when health reaches zero.</summary>
        public event Action<DamageInfo> Died;
        /// <summary>Raised when health is restored by ResetState or the refill.</summary>
        public event Action Restored;
        /// <summary>A hit turned away by protection (a car's shield bubble, the moments after a hit).</summary>
        public event Action<DamageInfo> Blocked;

        private void Awake()
        {
            Health = maxHealth;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        /// <summary>Health of whatever owns the collider: a first-person body hands its hits to the player it stands in for.</summary>
        public static Damageable Find(Collider collider)
        {
            if (collider == null) return null;
            var body = collider.GetComponentInParent<FirstPersonBody>();
            if (body != null && body.Owner != null) return body.Owner.GetComponentInParent<Damageable>();
            return collider.GetComponentInParent<Damageable>();
        }

        /// <summary>Ignore hits for a while (after a respawn or a wreck ejection).</summary>
        public void Protect(float seconds) => _safeUntil = Mathf.Max(_safeUntil, Time.time + seconds);

        /// <summary>Takes the hit. False when it was ignored (dead, protected or nothing to take).</summary>
        public bool Hit(in DamageInfo dealt)
        {
            if (Dead || dealt.Amount <= 0f) return false;
            if (Protected || DamageScale <= 0f) { Blocked?.Invoke(dealt); return false; }
            var hit = new DamageInfo(dealt.Amount * DamageScale, dealt.Knock, dealt.Point, dealt.Source);
            Health = Mathf.Max(0f, Health - hit.Amount);
            _safeUntil = Time.time + invulnerability;
            Flash();
            if (takeKnockback && hit.Knock.sqrMagnitude > 0f) Knockback.Apply(gameObject, hit.Knock, hit.Point);
            if (showNumbers) DamageNumbers.Show(hit.Point, hit.Amount, Mathf.Max(0.01f, transform.lossyScale.y));
            Damaged?.Invoke(hit);
            onDamaged?.Invoke();
            if (Dead)
            {
                _refillAt = refillAfterDeath > 0f ? Time.time + refillAfterDeath : float.PositiveInfinity;
                Died?.Invoke(hit);
                onDied?.Invoke();
            }
            return true;
        }

        public void Heal(float amount)
        {
            if (Dead || amount <= 0f) return;
            Health = Mathf.Min(maxHealth, Health + amount);
        }

        /// <summary>Full health, no flash, no protection.</summary>
        public void ResetState()
        {
            Health = maxHealth;
            _safeUntil = 0f;
            _refillAt = float.PositiveInfinity;
            ClearFlash();
            Restored?.Invoke();
        }

        private void Flash()
        {
            if (flashSeconds <= 0f) return;
            _block.Clear();
            _block.SetColor(BaseColorId, flashColor);
            foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(_block);
            _flashUntil = Time.time + flashSeconds;
            _flashing = true;
        }

        private void ClearFlash()
        {
            if (!_flashing) return;
            _flashing = false;
            foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(null);
        }

        private void Update()
        {
            if (_flashing && Time.time >= _flashUntil) ClearFlash();
            if (Dead && Time.time >= _refillAt) ResetState();
        }
    }
}
