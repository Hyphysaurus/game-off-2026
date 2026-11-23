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
    /// Online (D-0131, CoopWorld) the host's soldiers are the real ones: they aim at the nearest player they can see,
    /// turn on whoever shoots them, and tell every other PC their place, shots, falls and getting up. On those PCs each
    /// soldier is a copy (UseMirror) that only shows the host's, and a hit on it goes to the host.
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
        [Header("Blinded by a focused light (D-0149)")]
        [SerializeField, Min(0f), Tooltip("Seconds it is blinded by Dazzle (a LightTarget's On Filled): it cannot see or shoot, stands still and sways.")] private float blindSeconds = 3f;
        [SerializeField, Tooltip("Shown over its head while blinded (a looping stun effect: Polygon Arsenal's Stun), sized with it.")] private GameObject dazzleEffect;
        [SerializeField, Tooltip("One of these plays as it is blinded (a startled voice, a sparkle).")] private AudioClip[] dazzleSounds;
        [SerializeField, Range(0f, 1f)] private float dazzleVolume = 0.8f;

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
        private float _blindUntil = float.NegativeInfinity;
        private bool _wasBlind;

        /// <summary>Blinds it for <paramref name="seconds"/> (D-0149): no sight, no shots, it stands and sways.</summary>
        public void Blind(float seconds)
        {
            if (_state == State.Down) return;
            bool fresh = Time.time >= _blindUntil;
            _blindUntil = Mathf.Max(_blindUntil, Time.time + seconds);
            _wasBlind = true;
            if (!fresh) return;
            // What shows and sounds (D-0153): the stun effect over its head, a startled cry.
            if (dazzleEffect != null && _dazzleFx == null)
            {
                Bounds b = Drawn();
                _dazzleFx = Instantiate(dazzleEffect, new Vector3(b.center.x, b.max.y + 0.15f * Scale, b.center.z), Quaternion.identity, transform);
                _dazzleFx.transform.localScale = dazzleEffect.transform.localScale;   // under the soldier: its size with it
            }
            if (dazzleSounds != null && dazzleSounds.Length > 0)
            {
                AudioClip clip = dazzleSounds[Random.Range(0, dazzleSounds.Length)];
                if (clip != null) PaintSplats.InScene().Play(clip, transform.position, dazzleVolume);
            }
        }

        private GameObject _dazzleFx;

        /// <summary>Its drawn body's box (for where the stun effect sits).</summary>
        private Bounds Drawn()
        {
            var rs = GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(transform.position + Vector3.up * Scale, Vector3.zero);
            bool any = false;
            foreach (var r in rs) { if (r is ParticleSystemRenderer) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        private void EndDazzle()
        {
            if (_dazzleFx != null) Destroy(_dazzleFx);
            _dazzleFx = null;
        }

        /// <summary>Blinds it for its own Blind Seconds: wire a LightTarget's On Filled here (focus, then shoot).</summary>
        public void Dazzle() => Blind(blindSeconds);

        /// <summary>True while blinded.</summary>
        public bool Blinded => Time.time < _blindUntil;
        private Vector3 _post, _wanderTo;
        private Quaternion _postRot, _visualRest;
        private int _routeIndex, _routeStep = 1;
        private float _stuckFor, _vertical, _seenFor, _lastSeen = -100f, _nextLook, _nextFire, _waitUntil, _downAt;
        private bool _sees;
        private NetPlayer _other;      // online host (D-0131): another player it is after; null = this PC's own player
        private NetPlayer _shotBy;     // online host: the other player who shot it last; null = this PC's own player
        private bool _mirror;          // online, not the host: a copy that only shows the host's soldier
        private bool _netPose;         // a copy has heard where the host's soldier stands
        private Vector3 _netPos;
        private float _netYaw;

        private float Scale => Mathf.Max(0.001f, transform.lossyScale.y);

        /// <summary>Its number in the party's world, the same on every PC (CoopWorld); -1 offline.</summary>
        public int NetId { get; set; } = -1;
        /// <summary>True while it lies knocked down.</summary>
        public bool Down => _state == State.Down;

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
            if (CoopWorld.IsHost)
            {
                // Online host: it turns on whoever shot it, another player too (their hit came here from their PC).
                var shooter = hit.Source.GetComponentInParent<NetPlayer>();
                _other = _shotBy = shooter != null && !shooter.IsOwner ? shooter : null;
            }
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
            _blindUntil = float.NegativeInfinity; _wasBlind = false; EndDazzle();   // a reset clears a blinding (D-0149)
            if (visual != null) visual.localRotation = _visualRest;
            _state = State.Patrol; _routeIndex = 0; _routeStep = 1; _wanderTo = _post;
            _vertical = 0f; _seenFor = 0f; _lastSeen = -100f; _sees = false; _waitUntil = 0f;
            _alertUntil = -100f; _alertReach = 0f;
            _other = _shotBy = null;
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
            CoopWorld.SoldierDown(this, hit.Point);         // online host: it falls on every PC (D-0131)
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_mirror) { MirrorUpdate(dt); return; }   // online, not the host: it only shows the host's soldier
            if (GameMode.Current == GameMode.Mode.Build) return;   // held still while the room is edited, or a drag jitters
            if (_state == State.Down)
            {
                Topple();
                if (Time.time - _downAt >= downTime) { ResetState(); GameEvents.RaiseSoldierUp(this); CoopWorld.SoldierUp(this); }
                return;
            }

            if (Time.time < _blindUntil)
            {
                // Blinded (D-0149): it cannot see or shoot; it sways where it stands, hands to its eyes in spirit.
                _sees = false; _seenFor = 0f; _state = State.Patrol;
                if (visual != null)
                {
                    float t = Time.time * 9f;
                    visual.localRotation = _visualRest * Quaternion.Euler(Mathf.Sin(t) * 7f, 0f, Mathf.Sin(t * 1.3f) * 7f);
                }
                if (_cc.isGrounded && _vertical < 0f) _vertical = -1f * Scale;
                _vertical -= gravity * Scale * dt;
                _cc.Move(Vector3.up * _vertical * dt);
                return;
            }
            if (_wasBlind) { _wasBlind = false; if (visual != null) visual.localRotation = _visualRest; EndDazzle(); }

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

        /// <summary>The player it is after and the point to aim at: online on the host (D-0131) another player when
        /// CanSee chose one (_other), else this PC's own (TryOwnTarget).</summary>
        private bool TryTarget(out Vector3 aim, out Transform root)
        {
            if (_other != null)
            {
                if (CoopWorld.IsHost && AimAt(_other, out aim, out root)) return true;
                _other = null;   // gone, knocked out, or the party is over: back to this PC's own player
            }
            return TryOwnTarget(out aim, out root);
        }

        /// <summary>This PC's own player's stand-in and the point to aim at: first-person body, the car being driven, or the player.
        /// On foot the aim is the point between its legs and its chest nearest our own chest height: a toy on our floor is
        /// aimed at the chest as before, and the kid is shot in the legs (its chest is far out of range above us).</summary>
        private bool TryOwnTarget(out Vector3 aim, out Transform root)
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

        /// <summary>Online host (D-0131): the point to aim at on another player's copy here, as TryOwnTarget aims at our
        /// own: the car while they drive, else between their legs and chest nearest our chest height. False while they
        /// are knocked out (as their own PC says).</summary>
        private bool AimAt(NetPlayer player, out Vector3 aim, out Transform root)
        {
            aim = Vector3.zero; root = null;
            if (player == null || !player.IsSpawned || player.Down || !player.gameObject.activeInHierarchy) return false;
            root = player.transform;
            float size = Mathf.Max(0.001f, root.lossyScale.y);   // the network sizes their body with their shift
            if (player.Seated) { aim = root.position + Vector3.up * (0.6f * size); return true; }
            float chest = 0.6f * 1.8f * size;
            float ours = transform.position.y + chestHeight * Scale - root.position.y;
            aim = root.position + Vector3.up * Mathf.Clamp(ours, Mathf.Min(0.5f * Scale, chest), chest);
            return true;
        }

        private Vector3 Eye => muzzle != null ? muzzle.position : transform.position + Vector3.up * (chestHeight * Scale) + transform.forward * (0.3f * Scale);

        /// <summary>Whether it sees the player it is after. Online on the host (D-0131) it looks at every player: shot at
        /// lately, whoever did it comes first while it can see them (D-0100's "turns on whoever did it"); otherwise it goes
        /// after the nearest one it sees. Seeing nobody, it stays after the last.</summary>
        private bool CanSee()
        {
            if (!CoopWorld.IsHost)
            {
                _other = null;
                return TryOwnTarget(out Vector3 aim, out Transform root) && Sees(aim, root, out _);
            }
            Vector3 a; Transform r; float d;
            if (Alerted && (_shotBy != null ? AimAt(_shotBy, out a, out r) : TryOwnTarget(out a, out r)) && Sees(a, r, out _))
            {
                _other = _shotBy;
                return true;
            }
            bool any = false; NetPlayer best = null; float nearest = float.MaxValue;
            if (TryOwnTarget(out a, out r) && Sees(a, r, out d)) { any = true; nearest = d; }
            foreach (NetPlayer player in NetPlayer.Spawned)
            {
                if (player == null || player.IsOwner || !AimAt(player, out a, out r) || !Sees(a, r, out d) || d >= nearest) continue;
                any = true; best = player; nearest = d;
            }
            if (any) _other = best;
            return any;
        }

        /// <summary>True when <paramref name="aim"/> on <paramref name="root"/> is in range, in the view cone (all round
        /// once engaged) and not behind anything. <paramref name="distance"/>: how far away it is.</summary>
        private bool Sees(Vector3 aim, Transform root, out float distance)
        {
            Vector3 from = transform.position + Vector3.up * (chestHeight * Scale);
            Vector3 to = aim - from;
            distance = to.magnitude;
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
            CoopWorld.SoldierShot(this, from, dir);   // online host: a harmless copy of the dart on every other PC (D-0131)
            PlayFire();
        }

        /// <summary>Its own clip, else its dart's (the player's dart pistol sound): the course's soldiers were built with none and fired silently.</summary>
        private void PlayFire()
        {
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

        // ---------------------------------------------------------------- online: a copy of the host's soldier (D-0131)

        /// <summary>Online, on every PC but the host's: from now on this soldier only shows the host's. It thinks and fires
        /// nothing of its own; it stands where the host's stands (MirrorPose), falls and gets up when it does, and a hit
        /// on it goes to the host (<paramref name="relay"/>: the copy only flashes).</summary>
        public void UseMirror(System.Func<DamageInfo, bool> relay)
        {
            _mirror = true;
            _netPose = false;
            _sees = false; _seenFor = 0f; _other = null;
            _health.Relay = relay;
        }

        /// <summary>Back to a soldier of this PC's own (the host's world turned out not to match ours): at its post, whole.</summary>
        public void LeaveMirror()
        {
            if (!_mirror) return;
            _mirror = false;
            _health.Relay = null;
            ResetState();
        }

        /// <summary>Where the host's soldier stands and which way it faces. The copy follows a tenth of a second behind;
        /// the first word, or a jump (back at its post after a fall), puts it there at once.</summary>
        public void MirrorPose(Vector3 at, float yaw)
        {
            if (!_mirror) return;
            bool jump = !_netPose || (at - transform.position).sqrMagnitude > 4f * Scale * Scale;
            _netPos = at; _netYaw = yaw; _netPose = true;
            if (jump) transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>The host's soldier was knocked down: it tips over here too, with the burst and the thud
        /// (<paramref name="quiet"/>: it already lay there when we arrived, so it simply lies there).</summary>
        public void MirrorDown(Vector3 point, bool quiet)
        {
            if (!_mirror || _state == State.Down) return;
            _state = State.Down;
            _downAt = quiet ? Time.time - 1f : Time.time;
            _cc.enabled = false;   // darts pass over it, as over the host's
            if (!quiet) KnockDown.Show(transform, new DamageInfo(0f, Vector3.zero, point, null), visual, true);
        }

        /// <summary>The host's soldier got up (its place at its post comes with the next MirrorPose).</summary>
        public void MirrorUp()
        {
            if (!_mirror) return;
            _state = State.Patrol;
            if (visual != null) visual.localRotation = _visualRest;
            _cc.enabled = true;
        }

        /// <summary>The host's soldier fired: a harmless copy of its dart flies here, with its sound. The real dart is the
        /// host's: if it hits this PC's player, the hit comes from the host.</summary>
        public void MirrorShot(Vector3 from, Vector3 direction)
        {
            if (!_mirror || projectile == null) return;
            Projectile.Fire(projectile, from, direction, 0f, 0f, Scale, transform);
            PlayFire();
        }

        private void MirrorUpdate(float dt)
        {
            if (_state == State.Down) Topple();
            if (!_netPose) return;
            float k = 1f - Mathf.Exp(-12f * dt);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, _netPos, k),
                Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _netYaw, 0f), k));
        }
    }
}
