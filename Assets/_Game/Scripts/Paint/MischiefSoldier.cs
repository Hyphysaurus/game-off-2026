using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A toy soldier that undoes your work: it walks from one coloured thing to the next and rubs the colour off, until
    /// it is knocked down (darts or paint), and gets up again after a while. It never shoots. It stops for a moment when
    /// hit, steps round what is in its way, gives up on a thing it cannot reach, and never strays far from where it
    /// started. When the corner is done it gives up; the reset puts it back at its post. Speeds and distances are for a
    /// soldier at scale 1 and scale with its transform.
    /// Ported from the Godot test room's mischief_bot.gd (whose body was the dummy); the numbers are in PaintSettings.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Damageable))]
    public sealed class MischiefSoldier : MonoBehaviour, IResettable
    {
        private enum Mood { Choosing, Walking, Smudging, Avoiding, Down }

        [SerializeField, Tooltip("Tipped over when knocked down. The first child if empty.")] private Transform visual;
        [SerializeField] private LayerMask feelMask = ~0;

        private static readonly List<Colourable> Near = new List<Colourable>();
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private PaintSettings _settings;
        private CharacterController _cc;
        private Damageable _health;
        private Mood _mood;
        private Colourable _target;
        private Vector3 _home, _lastSpot;
        private Quaternion _homeTurn, _visualRest;
        private float _clock, _side = 1f, _stuckFor, _vertical, _pausedUntil, _downAt;
        private bool _stopped;

        /// <summary>The thing it is walking to or rubbing; null when it has none.</summary>
        public Colourable Target => _target;
        public bool Smudging => _mood == Mood.Smudging;
        public bool Down => _mood == Mood.Down;

        private float Scale => Mathf.Max(0.001f, transform.lossyScale.y);

        private void Awake()
        {
            _settings = PaintSettings.Load();
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Damageable>();
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) _visualRest = visual.localRotation;
            _home = _lastSpot = transform.position; _homeTurn = transform.rotation;
        }

        private void OnEnable() { _health.Damaged += OnDamaged; _health.Died += OnDied; }
        private void OnDisable() { _health.Damaged -= OnDamaged; _health.Died -= OnDied; }

        /// <summary>Called when the corner is finished: the soldiers give up.</summary>
        public void WindDown() => _stopped = true;

        public void Wake()
        {
            _stopped = false;
            if (_mood != Mood.Down) _mood = Mood.Choosing;
        }

        /// <summary>The reset key: back at its post, on its feet, at full health, looking for colour again.</summary>
        public void ResetState()
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(_home, _homeTurn);
            _cc.enabled = true;
            _health.ResetState();
            if (visual != null) visual.localRotation = _visualRest;
            _mood = Mood.Choosing; _target = null; _stopped = false;
            _vertical = 0f; _stuckFor = 0f; _pausedUntil = 0f; _lastSpot = _home;
        }

        private void OnDamaged(DamageInfo _) => _pausedUntil = Time.time + _settings.hitPause;

        private void OnDied(DamageInfo _)
        {
            _mood = Mood.Down; _downAt = Time.time;
            _cc.enabled = false;   // lying down it stops nothing
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || GameMode.Current == GameMode.Mode.Build) return;
            if (_mood == Mood.Down) { LieDown(); return; }

            Vector3 way = Vector3.zero;
            if (!_stopped && Time.time >= _pausedUntil)
            {
                switch (_mood)
                {
                    case Mood.Choosing:
                        _target = Choose();
                        if (_target != null) _mood = Mood.Walking;
                        break;
                    case Mood.Walking:
                        way = Walk(dt);
                        break;
                    case Mood.Avoiding:
                        _clock -= dt;
                        way = SideWay();
                        if (_clock <= 0f) _mood = Mood.Walking;
                        break;
                    case Mood.Smudging:
                        _clock -= dt;
                        if (_target != null) _target.Smudge(_settings.smudgePerSecond * dt);
                        if (_clock <= 0f || _target == null || _target.Colour <= 0f) _mood = Mood.Choosing;
                        break;
                }
            }

            if (_cc.isGrounded && _vertical < 0f) _vertical = -1f * Scale;
            _vertical -= _settings.soldierGravity * Scale * dt;
            _cc.Move((way * (_settings.walkSpeed * Scale) + Vector3.up * _vertical) * dt);
            if (way.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(way), Mathf.Min(_settings.turnSpeed * dt, 1f));
        }

        /// <summary>Tips over, lies there, and gets up where it fell.</summary>
        private void LieDown()
        {
            float lying = Time.time - _downAt;
            if (visual != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lying / Mathf.Max(_settings.toppleSeconds, 1e-3f)));
                visual.localRotation = Quaternion.Slerp(_visualRest, Quaternion.Euler(-90f, 0f, 0f) * _visualRest, t);
            }
            if (lying < _settings.downSeconds) return;
            _health.ResetState();
            if (visual != null) visual.localRotation = _visualRest;
            _cc.enabled = true;
            _vertical = 0f;
            _mood = Mood.Choosing;
        }

        /// <summary>The way to step toward the target this frame; zero when it has arrived, lost it or met something.</summary>
        private Vector3 Walk(float dt)
        {
            if (_target == null || _target.Colour <= 0f) { _mood = Mood.Choosing; return Vector3.zero; }
            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= _target.Radius + _settings.reachGap * Scale) { StartSmudging(); return Vector3.zero; }
            Vector3 way = toTarget.normalized;
            // Something in the way: if it is the thing we want, we have arrived; otherwise step round it.
            Transform blocker = Feel(way);
            if (blocker != null)
            {
                if (blocker.IsChildOf(_target.transform)) StartSmudging();
                else { _mood = Mood.Avoiding; _clock = _settings.avoidSeconds; _side = -_side; }
                return Vector3.zero;
            }
            // Not getting anywhere: give up on this one.
            _stuckFor += dt;
            if (_stuckFor > _settings.stuck.y)
            {
                if (Vector3.Distance(transform.position, _lastSpot) < _settings.stuck.x * Scale) { _mood = Mood.Choosing; _side = -_side; }
                _lastSpot = transform.position;
                _stuckFor = 0f;
            }
            return way;
        }

        private void StartSmudging()
        {
            _mood = Mood.Smudging;
            _clock = _settings.smudgeSeconds;
        }

        private Vector3 SideWay()
        {
            Vector3 forward = transform.forward; forward.y = 0f;
            return (forward + Vector3.Cross(Vector3.up, forward) * (_side * _settings.avoidLean)).normalized;
        }

        /// <summary>Looks a short way ahead at chest height. Returns what is there, ignoring the soldier's own body.</summary>
        private Transform Feel(Vector3 way)
        {
            Vector3 from = transform.position + Vector3.up * (_settings.feelHeight * Scale);
            int count = Physics.RaycastNonAlloc(from, way, _hits, _settings.feelAhead * Scale, feelMask, QueryTriggerInteraction.Ignore);
            Transform first = null; float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (_hits[i].distance < nearest) { nearest = _hits[i].distance; first = _hits[i].transform; }
            }
            return first;
        }

        /// <summary>Picks one of the nearest things that still has colour on it.</summary>
        private Colourable Choose()
        {
            Near.Clear();
            float roam = _settings.roam * Scale;
            foreach (Colourable thing in Colourable.All)
                if (thing.Colour > _settings.leastColour && Vector3.Distance(thing.transform.position, _home) <= roam) Near.Add(thing);
            if (Near.Count == 0) return null;
            int choices = Mathf.Min(Near.Count, Mathf.Max(_settings.nearestChoices, 1));
            // The nearest few, by picking the nearest left each time (the list is short).
            Vector3 here = transform.position;
            for (int i = 0; i < choices; i++)
            {
                int best = i;
                for (int j = i + 1; j < Near.Count; j++)
                    if ((Near[j].transform.position - here).sqrMagnitude < (Near[best].transform.position - here).sqrMagnitude) best = j;
                (Near[i], Near[best]) = (Near[best], Near[i]);
            }
            Colourable pick = Near[Random.Range(0, choices)];
            Near.Clear();
            return pick;
        }
    }
}
