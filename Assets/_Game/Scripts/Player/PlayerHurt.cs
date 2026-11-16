using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What a hit does to the player's body. While the block is up only a share of each hit gets through; through the
    /// middle of a dodge roll hits are turned away. A hit that lands plays the light reaction, a big one the heavy
    /// reaction. At zero health the body falls, lies a moment, and the Respawn beside it puts the player back as it
    /// always did (not on foot, driving or in first person, the respawn comes at once). Sits beside the Damageable; a
    /// player without one (the course) is never hurt and this does nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerMoves))]
    public sealed class PlayerHurt : MonoBehaviour
    {
        private PlayerMoves _moves;
        private PlayerMotor _motor;
        private Damageable _health;
        private Respawn _respawn;
        private float _respawnAt = -1f;

        private void Awake()
        {
            _moves = GetComponent<PlayerMoves>();
            _motor = GetComponent<PlayerMotor>();
            _health = GetComponent<Damageable>();
            _respawn = GetComponent<Respawn>();
        }

        private void OnEnable()
        {
            if (_health == null) return;
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            if (_motor != null) _motor.Warped += OnWarped;
        }

        private void OnDisable()
        {
            if (_health == null) return;
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            if (_motor != null) _motor.Warped -= OnWarped;
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
            if (_health == null) return;
            _health.DamageScale = Taken;
            if (_respawnAt >= 0f && Time.time >= _respawnAt)
            {
                _respawnAt = -1f;
                PutBack();
            }
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
            _health.Protect(s.respawnAfter + 1f);   // nothing hurts a body that is already down
        }

        private void PutBack()
        {
            if (_respawn != null) _respawn.Reset();   // its cut warps the player; OnWarped stands the body up
            else { _moves.Revive(); _health.ResetState(); }
        }

        private void OnWarped(Vector3 position, Quaternion rotation) => _moves.Revive();
    }
}
