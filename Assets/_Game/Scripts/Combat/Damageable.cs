using System;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>One hit: how much, which way it shoves (a velocity, world units per second, already scaled by the
    /// attacker's size), where it landed and who dealt it. Loose is the share of the shove a loose Rigidbody takes (1 =
    /// all of it; a shot from a big shooter gives less: Projectile).</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Knock;
        public readonly Vector3 Point;
        public readonly Transform Source;
        public readonly float Loose;
        public DamageInfo(float amount, Vector3 knock, Vector3 point, Transform source, float loose = 1f)
        { Amount = amount; Knock = knock; Point = point; Source = source; Loose = loose; }
    }

    /// <summary>Something that takes a hit before health does (the player's shield, D-0112): given what would come off
    /// health, it keeps what it can and returns the rest.</summary>
    public interface IHitAbsorber
    {
        float Absorb(float amount);
    }

    /// <summary>
    /// Health for anything that can be hit: the player, cars, practice targets, toy soldiers. Takes a DamageInfo, ignores
    /// hits during a short invulnerability window, flashes the renderers, squashes for a moment if it is set to, passes
    /// the shove on (Rigidbody, PlayerMotor or the first-person body) and announces damage and death. It does not
    /// decide what death means: listeners do.
    /// </summary>
    public sealed class Damageable : MonoBehaviour, IResettable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float maxHealth = 100f;
        [SerializeField, Tooltip("Seconds after a hit during which further hits are ignored.")] private float invulnerability = 0.1f;
        [SerializeField, Tooltip("Colour the renderers flash on a hit.")] private Color flashColor = new Color(1f, 0.45f, 0.2f);
        [SerializeField] private float flashSeconds = 0.1f;
        [SerializeField, Range(0f, 0.5f), Tooltip("How far it squashes when hit, as a share of its height: flat and wide at the hit, then it springs back (the test room: a target 0.15, a figure 0.1). 0 = it does not squash.")] private float squash;
        [SerializeField, Min(0.01f), Tooltip("Seconds the squash takes to spring back (the test room: a target 0.3, a figure 0.25).")] private float squashSeconds = 0.3f;
        [SerializeField, Tooltip("The part that squashes. Empty: the first child (never this object itself: its colliders would squash with it).")] private Transform squashPart;
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
        /// <summary>Takes hits before health (the player's shield). Null: none.</summary>
        public IHitAbsorber Absorber { get; set; }
        /// <summary>Online (D-0112): another player's copy on this PC hands every hit to that player's own PC instead
        /// of taking it here. Returns whether the hit counts (for the hit marker).</summary>
        public Func<DamageInfo, bool> Relay { get; set; }
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
            if (Relay != null)
            {
                if (dealt.Amount <= 0f || !Relay(dealt)) return false;
                Flash();   // the shooter sees the hit land at once; the damage is the other PC's to take
                return true;
            }
            if (Dead || dealt.Amount <= 0f) return false;
            if (Protected || DamageScale <= 0f) { Blocked?.Invoke(dealt); return false; }
            var hit = new DamageInfo(dealt.Amount * DamageScale, dealt.Knock, dealt.Point, dealt.Source, dealt.Loose);
            float toHealth = Absorber != null ? Mathf.Max(0f, Absorber.Absorb(hit.Amount)) : hit.Amount;
            Health = Mathf.Max(0f, Health - toHealth);
            _safeUntil = Time.time + invulnerability;
            Flash();
            if (squash > 0f) Squash.Hit(squashPart != null ? squashPart : transform.childCount > 0 ? transform.GetChild(0) : null, squash, squashSeconds);
            if (takeKnockback && hit.Knock.sqrMagnitude > 0f) Knockback.Apply(gameObject, hit.Knock, hit.Point, hit.Loose);
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

        /// <summary>Set the most health and the time between hits from settings made at run time (the player's, D-0112).
        /// Health starts full at the new most.</summary>
        public void Configure(float most, float safeSeconds)
        {
            maxHealth = Mathf.Max(1f, most);
            invulnerability = Mathf.Max(0f, safeSeconds);
            Health = maxHealth;
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
