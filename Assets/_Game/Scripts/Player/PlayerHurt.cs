using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What a hit does to the player's body. While the block is up only a share of each hit gets through; through the
    /// middle of a dodge roll hits are turned away. A hit that lands plays the light reaction, a big one the heavy
    /// reaction. At zero health the body falls and lies a moment (Jump or Fire after PlayerHealthSettings.skipAfter
    /// cuts it short), then the Respawn beside it puts the player back at the checkpoint (online: the hub's spawn) with
    /// full health and shield, a moment's protection and, with restock on, full guns and grenades (D-0112). Not on
    /// foot (driving, first person) the respawn comes at once.
    /// Every player has health now (D-0112, his "health on single player too"): this adds the Damageable when the
    /// player has none, and the shield, both set from PlayerHealthSettings, and puts the bars on screen.
    /// </summary>
    [RequireComponent(typeof(PlayerMoves))]
    public sealed class PlayerHurt : MonoBehaviour
    {
        private PlayerMoves _moves;
        private PlayerMotor _motor;
        private Damageable _health;
        private PlayerShield _shield;
        private Respawn _respawn;
        private PlayerInput _input;
        private PlayerHealthSettings _s;
        private float _respawnAt = -1f, _skipFrom;

        /// <summary>The player's health.</summary>
        public Damageable Health => _health;
        /// <summary>The player's shield.</summary>
        public PlayerShield Shield => _shield;
        /// <summary>True from the knock-out until the player is back on their feet.</summary>
        public bool KnockedOut => _respawnAt >= 0f;

        private void Awake()
        {
            _s = PlayerHealthSettings.Load();
            _moves = GetComponent<PlayerMoves>();
            _motor = GetComponent<PlayerMotor>();
            _respawn = GetComponent<Respawn>();
            _input = GetComponent<PlayerInput>();
            if (!TryGetComponent(out _health)) _health = gameObject.AddComponent<Damageable>();
            _health.Configure(_s.maxHealth, _s.invulnerability);
            if (!TryGetComponent(out _shield)) _shield = gameObject.AddComponent<PlayerShield>();
            _shield.Setup(_s, _health);
            _health.Absorber = _shield;
        }

        private void Start() { if (_input != null && _input.isActiveAndEnabled) HealthHud.Create(this); }   // our own player only

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            if (_motor != null) _motor.Warped += OnWarped;
            if (_input != null) { _input.Jump += OnSkip; _input.UseHeld += OnSkip; }
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            if (_motor != null) _motor.Warped -= OnWarped;
            if (_input != null) { _input.Jump -= OnSkip; _input.UseHeld -= OnSkip; }
            _health.DamageScale = 1f;
        }

        /// <summary>The share of a hit that gets through right now: the block's share, nothing in the roll's safe stretch, else all.</summary>
        public float Taken
        {
            get
            {
                var s = _moves.Settings;
                if (s == null) return 1f;
                if (_moves.Current == PlayerMoves.Move.Block) return s.blockDamage;
                if (_moves.Rolling && s.rollSafeTo > s.rollSafeFrom && _moves.Share >= s.rollSafeFrom && _moves.Share <= s.rollSafeTo) return 0f;
                return 1f;
            }
        }

        private void Update()
        {
            _health.DamageScale = Taken;
            if (_respawnAt >= 0f && Time.time >= _respawnAt) PutBack();
        }

        private void OnDamaged(DamageInfo hit)
        {
            if (_health.Dead) return;   // the death takes over
            var s = _moves.Settings;
            if (s != null) _moves.React(hit.Amount >= s.heavyHitShare * _health.MaxHealth);
        }

        private void OnDied(DamageInfo hit)
        {
            var s = _moves.Settings;
            if (s == null || !_moves.Die()) { PutBack(); return; }
            _respawnAt = Time.time + s.respawnAfter;
            _skipFrom = Time.time + Mathf.Min(_s.skipAfter, s.respawnAfter);
            _health.Protect(s.respawnAfter + 1f);   // nothing hurts a body that is already down
        }

        /// <summary>Jump or Fire while down: straight back, once the body has had a moment on the floor.</summary>
        private void OnSkip()
        {
            if (_respawnAt >= 0f && Time.time >= _skipFrom) PutBack();
        }

        private void PutBack()
        {
            _respawnAt = -1f;
            _health.ResetState();
            _shield.Refill();
            _health.Protect(_s.respawnProtection);
            if (_s.restock && TryGetComponent(out WeaponHolder holder)) holder.Restock();
            if (_respawn != null) _respawn.Reset();   // its cut warps the player; OnWarped stands the body up
            else _moves.Revive();
        }

        private void OnWarped(Vector3 position, Quaternion rotation) => _moves.Revive();
    }
}
