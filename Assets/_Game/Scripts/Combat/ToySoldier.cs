using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A plastic toy soldier that guards a spot: walks its route (or wanders near its post), spots the player by line of
    /// sight inside a view cone, waits a forgiving reaction time, then turns and fires pooled projectiles until it has
    /// not seen the player for a while. Knocked out, it tips over and stands up again at its post after a pause.
    /// Aims at whatever stands for the player: the toy/kid, the first-person body, or the car being driven; at the part of
    /// it nearest its own chest height, so a kid towering over it is shot in the legs. Holds still while Build Mode is
    /// on, and wherever Build Mode puts it becomes its post. Speeds and distances are for a soldier at scale 1 and scale
    /// with its transform.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Damageable))]
    public sealed class ToySoldier : MonoBehaviour, IResettable, IBuildMoved
    {
        private enum State { Patrol, Engage, Down }

        [Header("Patrol")]
        [SerializeField, Tooltip("Points to walk between, back and forth. Empty = wander near the post.")] private Transform[] route;
        [SerializeField, Tooltip("Wander radius around the post when there is no route, units at scale 1.")] private float wanderRadius = 4f;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField, Tooltip("Seconds it waits at each point.")] private float pause = 1.2f;
        [Header("Sight")]
        [SerializeField] private float sightRange = 18f;
        [SerializeField, Tooltip("Half-angle of the view cone, degrees. Once engaged it sees all round.")] private float viewAngle = 70f;
        [SerializeField, Tooltip("Seconds the player must stay seen before it reacts.")] private float reactionTime = 0.7f;
        [SerializeField, Tooltip("Seconds without sight before it gives up.")] private float loseSightTime = 2.5f;
        [SerializeField] private LayerMask sightMask = ~0;
        [Header("Fire")]
        [SerializeField] private Projectile projectile;
        [SerializeField, Tooltip("Where shots leave: the rifle tip. Chest height in front if empty.")] private Transform muzzle;
        [SerializeField] private float fireRange = 14f;
        [SerializeField] private float fireInterval = 1.1f;
        [SerializeField, Tooltip("Cone half-angle, degrees.")] private float spread = 3f;
        [SerializeField] private float damage = 10f;
        [SerializeField, Tooltip("Shots push the player this much, units per second at scale 1.")] private float knockback = 2f;
        [SerializeField] private float turnSpeed = 300f;
        [SerializeField] private AudioClip fireClip;
        [Header("Body")]
        [SerializeField, Tooltip("Tipped over on knock-out. The first child if empty.")] private Transform visual;
        [SerializeField] private float gravity = 25f;
        [SerializeField, Tooltip("Seconds lying down before it is back at its post.")] private float downTime = 4f;
        [SerializeField, Tooltip("Chest height, units at scale 1.")] private float chestHeight = 1.0f;

        private const float PerceiveEvery = 0.15f;
        private const float AlertSeconds = 6f;   // a hit keeps it looking this long past its usual sight
        private float _alertUntil = -100f, _alertReach;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private CharacterController _cc;
        private Damageable _health;
        private AudioSource _audio;
        private PlayerMotor _player;
        private State _state;
        private Vector3 _post, _wanderTo;
        private Quaternion _postRot, _visualRest;
        private int _routeIndex, _routeStep = 1;
        private float _stuckFor, _vertical, _seenFor, _lastSeen = -100f, _nextLook, _nextFire, _waitUntil, _downAt;
        private bool _sees;

        private float Scale => Mathf.Max(0.001f, transform.lossyScale.y);

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _health = GetComponent<Damageable>();
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) _visualRest = visual.localRotation;
            _post = transform.position; _postRot = transform.rotation; _wanderTo = _post;
            _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.spatialBlend = 1f;
        }

        private void OnEnable() { _health.Died += OnDied; _health.Damaged += OnHit; }
        private void OnDisable() { _health.Died -= OnDied; _health.Damaged -= OnHit; }

        /// <summary>Shot (D-0100): it engages at once, turns on whoever did it and sees all round, and for a while it sees and
        /// fires as far as the shot came from. Before, a toy shooting from past its sight knocked it down unnoticed.</summary>
        private void OnHit(DamageInfo hit)
        {
            if (_state == State.Down || hit.Source == null || hit.Source.IsChildOf(transform)) return;
            if (hit.Source.GetComponentInParent<ToySoldier>() != null) return;   // a friend's stray dart is not a call to arms
            float reach = Vector3.Distance(hit.Source.position, transform.position) + 2f * Scale;
            _alertReach = Alerted ? Mathf.Max(_alertReach, reach) : reach;
            _alertUntil = Time.time + AlertSeconds;
            _state = State.Engage;
            _sees = true; _lastSeen = Time.time; _seenFor = reactionTime;
            _nextLook = Time.time + PerceiveEvery;
        }

        private bool Alerted => Time.time < _alertUntil;
        private float SightReach => Alerted ? Mathf.Max(sightRange * Scale, _alertReach) : sightRange * Scale;
        private float FireReach => Alerted ? Mathf.Max(fireRange * Scale, _alertReach) : fireRange * Scale;

        public void ResetState()
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(_post, _postRot);
            _cc.enabled = true;
            _health.ResetState();
            if (visual != null) visual.localRotation = _visualRest;
            _state = State.Patrol; _routeIndex = 0; _routeStep = 1; _wanderTo = _post;
            _vertical = 0f; _seenFor = 0f; _lastSeen = -100f; _sees = false; _waitUntil = 0f;
            _alertUntil = -100f; _alertReach = 0f;
        }

        /// <summary>Build Mode placed, moved, copied or re-posed us: stand guard here from now on (F9 and getting up after a
        /// knock-out come back here, a copy no longer walks to the original's spot).</summary>
        public void OnBuildMoved()
        {
            _post = transform.position; _postRot = transform.rotation; _wanderTo = _post;
            _routeIndex = 0; _routeStep = 1; _waitUntil = 0f; _stuckFor = 0f; _vertical = 0f;
        }

        private void OnDied(DamageInfo hit)
        {
            _state = State.Down; _downAt = Time.time; _sees = false; _seenFor = 0f;
            _cc.enabled = false;
            KnockDown.Show(transform, hit, visual, true);   // a burst of bits, a bigger squash and the thud
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (GameMode.Current == GameMode.Mode.Build) return;   // held still while the room is edited, or a drag jitters
            if (_state == State.Down) { Topple(); if (Time.time - _downAt >= downTime) { ResetState(); GameEvents.RaiseSoldierUp(this); } return; }

            Perceive(dt);
            Vector3 move = Vector3.zero, face = Vector3.zero;
            if (_state == State.Engage)
            {
                if (TryTarget(out Vector3 aim, out _)) { face = aim - transform.position; TryFire(aim); }
            }
            else
            {
                move = PatrolVelocity();
                face = move;
            }

            if (_cc.isGrounded && _vertical < 0f) _vertical = -1f * Scale;
            _vertical -= gravity * Scale * dt;
            _cc.Move((move + Vector3.up * _vertical) * dt);
            face.y = 0f;
            if (face.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), turnSpeed * dt);
        }

        private void Topple()
        {
            if (visual == null) return;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _downAt) / 0.3f));
            visual.localRotation = Quaternion.Slerp(_visualRest, Quaternion.Euler(-90f, 0f, 0f) * _visualRest, t);
        }

        private void Perceive(float dt)
        {
            if (Time.time >= _nextLook)
            {
                _nextLook = Time.time + PerceiveEvery;
                _sees = CanSee();
                if (_sees) _lastSeen = Time.time;
            }
            if (_sees) _seenFor += dt; else if (_state != State.Engage) _seenFor = 0f;
            if (_state == State.Patrol && _seenFor >= reactionTime) _state = State.Engage;
            else if (_state == State.Engage && Time.time - _lastSeen >= loseSightTime) { _state = State.Patrol; _seenFor = 0f; }
        }

        /// <summary>The player's stand-in and the point to aim at: first-person body, the car being driven, or the player.
        /// On foot the aim is the point between its legs and its chest nearest our own chest height: a toy on our floor is
        /// aimed at the chest as before, and the kid is shot in the legs (its chest is far out of range above us).</summary>
        private bool TryTarget(out Vector3 aim, out Transform root)
        {
            aim = Vector3.zero; root = null;
            if (GameMode.Current == GameMode.Mode.Build) return false;   // the builder is not a target
            float chest;   // above the target's feet
            var fp = FirstPersonMode.Active;
            if (fp != null && fp.Body != null && fp.Body.isActiveAndEnabled)
            {
                root = fp.Body.transform;
                chest = fp.Body.Centre.y - root.position.y;
            }
            else
            {
                if (_player == null)
                {
                    var own = GameObject.Find("Player");   // online, this PC's own player (others' copies are "Player 2"...): D-0114
                    _player = own != null ? own.GetComponent<PlayerMotor>() : null;
                    if (_player == null) _player = FindFirstObjectByType<PlayerMotor>();
                }
                if (_player == null || !_player.gameObject.activeInHierarchy) return false;
                if (!_player.enabled && _player.transform.parent != null)
                {
                    // Seated: aim at the car.
                    root = _player.transform.parent;
                    aim = root.position + Vector3.up * (0.6f * root.lossyScale.y);
                    return true;
                }
                root = _player.transform;
                chest = 0.6f * 1.8f * _player.Scale;
            }
            float ours = transform.position.y + chestHeight * Scale - root.position.y;
            float legs = Mathf.Min(0.5f * Scale, chest);
            aim = root.position + Vector3.up * Mathf.Clamp(ours, legs, chest);
            return true;
        }

        private Vector3 Eye => muzzle != null ? muzzle.position : transform.position + Vector3.up * (chestHeight * Scale) + transform.forward * (0.3f * Scale);

        private bool CanSee()
        {
            if (!TryTarget(out Vector3 aim, out Transform root)) return false;
            Vector3 from = transform.position + Vector3.up * (chestHeight * Scale);
            Vector3 to = aim - from;
            float distance = to.magnitude;
            if (distance > SightReach) return false;
            if (_state != State.Engage && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(to, Vector3.up)) > viewAngle) return false;
            if (distance < 1e-3f) return true;
            int n = Physics.RaycastNonAlloc(from, to / distance, _hits, distance, Layers.Seen(sightMask), QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue; Transform first = null;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].transform.IsChildOf(transform)) continue;
                if (_hits[i].distance < nearest) { nearest = _hits[i].distance; first = _hits[i].transform; }
            }
            return first == null || first.IsChildOf(root);
        }

        private void TryFire(Vector3 aim)
        {
            if (!_sees || projectile == null || Time.time < _nextFire) return;
            Vector3 from = Eye;
            Vector3 to = aim - from;
            if (to.magnitude > FireReach) return;
            // Facing is judged from the body, not the muzzle: a rifle tip out to the side and in front would read a close
            // target as off to one side and never fire.
            Vector3 flat = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up);
            if (flat.sqrMagnitude > 1e-6f && Vector3.Angle(transform.forward, flat) > 20f) return;   // still turning
            _nextFire = Time.time + fireInterval * Random.Range(0.85f, 1.15f);
            Vector3 dir = to.normalized;
            if (spread > 0f)
            {
                Vector3 axis = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.99f ? Vector3.right : Vector3.up).normalized;
                axis = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * axis;
                dir = Quaternion.AngleAxis(spread * Mathf.Sqrt(Random.value), axis) * dir;
            }
            Projectile.Fire(projectile, from, dir, damage, knockback, Scale, transform);
            // Its own clip, else its dart's (the player's dart pistol sound): the course's soldiers were built with none and fired silently.
            AudioClip clip = fireClip != null ? fireClip : projectile.FireClip();
            if (clip != null) { _audio.pitch = Random.Range(0.9f, 1.05f); _audio.PlayOneShot(clip); }
        }

        private Vector3 PatrolVelocity()
        {
            if (Time.time < _waitUntil) return Vector3.zero;
            Vector3 goal;
            if (route != null && route.Length > 0 && route[_routeIndex] != null) goal = route[_routeIndex].position;
            else goal = _wanderTo;
            Vector3 offset = goal - transform.position; offset.y = 0f;
            if (offset.magnitude <= 0.3f * Scale)
            {
                _waitUntil = Time.time + pause;
                if (route != null && route.Length > 1)
                {
                    int next = _routeIndex + _routeStep;
                    if (next < 0 || next >= route.Length) { _routeStep = -_routeStep; next = _routeIndex + _routeStep; }
                    _routeIndex = next;
                }
                else
                {
                    Vector2 r = Random.insideUnitCircle * (wanderRadius * Scale);
                    _wanderTo = _post + new Vector3(r.x, 0f, r.y);
                }
                return Vector3.zero;
            }
            // Walking into a wall: give up on this point after a moment and pick the next one.
            Vector3 v = _cc.velocity; v.y = 0f;
            _stuckFor = v.magnitude < 0.25f * moveSpeed * Scale ? _stuckFor + Time.deltaTime : 0f;
            if (_stuckFor > 1.5f)
            {
                _stuckFor = 0f;
                Vector2 r = Random.insideUnitCircle * (wanderRadius * Scale);
                _wanderTo = _post + new Vector3(r.x, 0f, r.y);
                if (route != null && route.Length > 1) { _routeStep = -_routeStep; _routeIndex = Mathf.Clamp(_routeIndex + _routeStep, 0, route.Length - 1); }
            }
            return offset.normalized * (moveSpeed * Scale);
        }
    }
}
