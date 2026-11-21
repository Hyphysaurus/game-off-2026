using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Dart Line (D-0109, roadmap item 3, his "the dart hookshot with zip lines to bespoke targets"): a dart pistol
    /// with a reel. Click fires a foam dart on a line at the HookAnchor nearest the crosshair (within a few degrees, in
    /// range, in sight); it sticks, and the line pulls the toy up to the anchor's ledge, or onto a ZipLine it then rides to
    /// the end. At anything else the dart drops and reels back: only placed targets hold. Click or Jump while on the
    /// line lets go, keeping the speed. Toy only (his "TOY ONLY"): at kid size these heights are a step. Held as the
    /// pistol is (third person the arm aims it, first person the hands are on it). A shift, a holster or a swap lets go.
    /// </summary>
    public sealed class DartLineWeapon : Weapon
    {
        [Header("Reach (units at toy size)")]
        [SerializeField, Tooltip("How far a dart reaches.")] private float range = 200f;
        [SerializeField, Tooltip("How far off the crosshair an anchor may be and still be the one hit, degrees.")] private float snapDegrees = 7f;
        [SerializeField, Tooltip("The dart's speed out to its target.")] private float dartSpeed = 160f;
        [Header("On the line (units a second at toy size)")]
        [SerializeField, Tooltip("How fast the reel pulls the toy up.")] private float reelSpeed = 38f;
        [SerializeField, Tooltip("A zip line: the slowest it rides.")] private float zipMinSpeed = 10f;
        [SerializeField, Tooltip("A zip line: the fastest it rides.")] private float zipMaxSpeed = 46f;
        [SerializeField, Tooltip("A zip line: how much of gravity speeds it up down the slope.")] private float zipGravityShare = 0.7f;
        [SerializeField, Tooltip("A zip line: how far the toy's feet hang below the string.")] private float hangBelow = 2.4f;
        [SerializeField, Tooltip("Letting go at the end of a zip line: the most speed carried on.")] private float zipExitSpeed = 8f;
        [SerializeField, Tooltip("Letting go with Jump: the hop up.")] private float letGoHop = 6f;
        [SerializeField, Tooltip("Seconds without getting nearer before a pull gives up (snagged on an edge).")] private float stuckSeconds = 0.4f;
        [Header("Look and sound")]
        [SerializeField] private Material lineMaterial;
        [SerializeField, Tooltip("The line's width, toy units.")] private float lineWidth = 0.12f;
        [SerializeField, Tooltip("The dart in flight. Empty: a small orange capsule.")] private GameObject dartLook;
        [SerializeField, Tooltip("Said when a kid tries it.")] private string kidText = "A toy's tool: shrink to use the Dart Line";
        [SerializeField] private AudioClip fireClip, latchClip, missClip, letGoClip;

        private enum State { Idle, Flying, Missing, Reeling, Pulling, Zipping }

        private State _state;
        private HookAnchor _anchor, _shown;
        private Vector3 _dartAt, _dartTo;
        private Transform _muzzle, _dart;
        private LineRenderer _line;
        private PlayerMotor _motor;
        private PlayerInput _input;
        private Shifter _shifter;
        private readonly List<Vector3> _path = new List<Vector3>();
        private int _next;
        private float _bestDistance, _stuckFor, _zipT, _zipSpeed;

        public override bool ToyOnly => true;
        public override bool Busy => _state == State.Pulling || _state == State.Zipping;
        /// <summary>True while the line holds the toy.</summary>
        public bool OnLine => _state == State.Pulling || _state == State.Zipping;

        private bool IsToy => _shifter == null || _shifter.Size == ShiftSize.Toy;

        private void Awake()
        {
            _muzzle = transform.Find("Muzzle");
            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(transform, false);
            _line = lineGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 0;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (lineMaterial != null) _line.sharedMaterial = lineMaterial;
            if (dartLook != null) _dart = Instantiate(dartLook).transform;
            else
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(d.GetComponent<Collider>());
                d.transform.localScale = new Vector3(0.25f, 0.35f, 0.25f);
                if (lineMaterial != null) d.GetComponent<Renderer>().sharedMaterial = lineMaterial;
                _dart = d.transform;
            }
            _dart.name = "DartLine_Dart";
            _dart.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_dart != null) Destroy(_dart.gameObject);
            if (_input != null) _input.Jump -= OnJump;
            if (_shifter != null) _shifter.ShiftStarted -= OnShiftStarted;
        }

        private void OnDisable() => Cancel();

        private void Find()
        {
            if (Holder == null) return;
            if (_motor == null) _motor = Holder.GetComponent<PlayerMotor>();
            if (_input == null) { _input = Holder.GetComponent<PlayerInput>(); if (_input != null) _input.Jump += OnJump; }
            if (_shifter == null) { _shifter = Holder.GetComponent<Shifter>(); if (_shifter != null) _shifter.ShiftStarted += OnShiftStarted; }
        }

        private void OnShiftStarted(ShiftSize _) => LetGo(false);
        private void OnJump() { if (OnLine && isActiveAndEnabled) LetGo(true); }

        private Vector3 MuzzlePoint => _muzzle != null ? _muzzle.position : transform.position;

        public override void TriggerPressed()
        {
            Find();
            if (Holder == null) return;
            if (OnLine) { LetGo(false); return; }
            if (_state != State.Idle) return;
            if (!IsToy) { Hint(kidText); return; }
            float s = Scale;
            HookAnchor target = Best(s);
            _dartAt = MuzzlePoint;
            if (target != null) { _anchor = target; _dartTo = target.Point; _state = State.Flying; }
            else
            {
                // A miss: the dart flies to whatever the crosshair is on (or as far as it reaches), drops, and reels back.
                _anchor = null;
                Vector3 from = Holder.AimOrigin, way = Holder.AimForward;
                _dartTo = from + way * (range * s);
                foreach (var h in Physics.RaycastAll(from, way, range * s, ~0, QueryTriggerInteraction.Ignore))
                    if (!Holder.IsSelf(h.transform) && (h.point - from).sqrMagnitude < (_dartTo - from).sqrMagnitude) _dartTo = h.point;
                _state = State.Missing;
            }
            _dart.gameObject.SetActive(true);
            Holder.PlayClip(fireClip, 1f);
        }

        public override void Cancel() => LetGo(false);

        /// <summary>The anchor the dart would hit now: in range, nearest the crosshair within the snap, nothing in between.</summary>
        private HookAnchor Best(float s)
        {
            if (Holder == null) return null;
            Vector3 from = Holder.AimOrigin, way = Holder.AimForward;
            HookAnchor best = null; float bestAngle = snapDegrees;
            foreach (var a in HookAnchor.All)
            {
                if (a == null) continue;
                Vector3 to = a.Point - from;
                float d = to.magnitude;
                if (d > range * s || d < 0.5f * s) continue;
                float angle = Vector3.Angle(way, to);
                if (angle > bestAngle) continue;
                if (Blocked(MuzzlePoint, a)) continue;
                best = a; bestAngle = angle;
            }
            return best;
        }

        private bool Blocked(Vector3 from, HookAnchor a)
        {
            Vector3 to = a.Point - from; float d = to.magnitude;
            if (d < 1e-3f) return false;
            foreach (var h in Physics.RaycastAll(from, to / d, d - 0.3f * Scale, ~0, QueryTriggerInteraction.Ignore))
                if (!Holder.IsSelf(h.transform) && !h.transform.IsChildOf(a.transform) && !h.transform.IsChildOf(transform)) return true;
            return false;
        }

        private void Update()
        {
            if (Holder == null) { Draw(false); return; }
            Find();
            float s = Scale, dt = Time.deltaTime;
            // the anchor the crosshair would hit glows (only while the line is free and a toy holds it)
            HookAnchor shown = _state == State.Idle && IsToy ? Best(s) : null;
            if (shown != _shown) { if (_shown != null) _shown.SetTargeted(false); _shown = shown; if (_shown != null) _shown.SetTargeted(true); }

            switch (_state)
            {
                case State.Flying:
                case State.Missing:
                    _dartAt = Vector3.MoveTowards(_dartAt, _dartTo, dartSpeed * s * dt);
                    if ((_dartAt - _dartTo).sqrMagnitude < 1e-4f)
                    {
                        if (_state == State.Missing) { _state = State.Reeling; Holder.PlayClip(missClip, 1f); }
                        else Latch();
                    }
                    break;
                case State.Reeling:
                    _dartAt = Vector3.MoveTowards(_dartAt, MuzzlePoint, dartSpeed * 1.5f * s * dt);
                    if ((_dartAt - MuzzlePoint).sqrMagnitude < 0.04f * s * s) { _state = State.Idle; _dart.gameObject.SetActive(false); }
                    break;
                case State.Pulling: Pull(s, dt); break;
                case State.Zipping: Zip(s, dt); break;
            }
            if (_state != State.Idle)
            {
                _dart.position = _dartAt;
                Vector3 back = MuzzlePoint - _dartAt;
                if (back.sqrMagnitude > 1e-6f) _dart.rotation = Quaternion.LookRotation(-back) * Quaternion.Euler(90f, 0f, 0f);
                _dart.localScale = new Vector3(0.25f, 0.35f, 0.25f) * s;
            }
            Draw(_state != State.Idle);
        }

        /// <summary>The dart holds: the glider folds, and the line takes the toy up to the ledge, or onto the zip line.</summary>
        private void Latch()
        {
            if (_anchor == null || _motor == null || !IsToy) { _state = State.Reeling; return; }
            Holder.PlayClip(latchClip, 1f);
            var glider = Holder.GetComponent<Glider>();
            if (glider != null && glider.Gliding) glider.FoldNow("line");
            if (_anchor.Type == HookAnchor.Kind.ZipStart && _anchor.Zip != null)
            {
                _path.Clear();
                _path.Add(_anchor.Zip.At(0f) + Vector3.down * (hangBelow * Scale));   // up to the string's start, hanging under it
            }
            else _anchor.Path(_path);
            _next = 0;
            _bestDistance = float.MaxValue; _stuckFor = 0f;
            _state = State.Pulling;
        }

        private void Pull(float s, float dt)
        {
            if (_next >= _path.Count) { Arrive(); return; }
            Vector3 feet = _motor.transform.position, to = _path[_next] - feet;
            float d = to.magnitude, step = reelSpeed * s * dt;
            if (d <= Mathf.Max(step * 1.5f, 0.3f * s))
            {
                _next++;
                _bestDistance = float.MaxValue; _stuckFor = 0f;
                if (_next >= _path.Count) { Arrive(); return; }
                to = _path[_next] - feet; d = to.magnitude;
            }
            // snagged on an edge: no nearer for a moment, let go with a hop up
            if (d < _bestDistance - 0.05f * s) { _bestDistance = d; _stuckFor = 0f; }
            else if ((_stuckFor += dt) > stuckSeconds) { LetGo(true); return; }
            _motor.SetPull(to / Mathf.Max(d, 1e-4f) * (reelSpeed * s));
            _dartAt = _anchor != null ? _anchor.Point : _dartAt;
        }

        /// <summary>The end of the reel: on the ledge (a pull-up), or under the string's start (a zip line, which it now rides).</summary>
        private void Arrive()
        {
            if (_anchor != null && _anchor.Type == HookAnchor.Kind.ZipStart && _anchor.Zip != null)
            {
                _zipT = 0f; _zipSpeed = zipMinSpeed * Scale;
                _state = State.Zipping;
                return;
            }
            _motor.EndPull(Vector3.zero);
            Done();
        }

        private void Zip(float s, float dt)
        {
            ZipLine z = _anchor != null ? _anchor.Zip : null;
            if (z == null || z.Length < 1e-3f) { LetGo(false); return; }
            Vector3 along = (z.End - z.Start).normalized;
            float g = _motor.Gravity;
            _zipSpeed = Mathf.Clamp(_zipSpeed + g * zipGravityShare * Mathf.Max(0f, -along.y) * dt, zipMinSpeed * s, zipMaxSpeed * s);
            _zipT += _zipSpeed * dt / z.Length;
            Vector3 hang = z.At(_zipT) + Vector3.down * (hangBelow * s);
            Vector3 v = (hang - _motor.transform.position) / Mathf.Max(dt, 1e-4f);
            if (v.magnitude > _zipSpeed * 3f) v = v.normalized * _zipSpeed * 3f;
            _motor.SetPull(v);
            _dartAt = z.At(Mathf.Min(1f, _zipT + 0.02f));   // the dart's hook rides the string just ahead
            if (_zipT >= 1f)
            {
                Vector3 flat = new Vector3(along.x, 0f, along.z).normalized * Mathf.Min(_zipSpeed, zipExitSpeed * s);
                _motor.EndPull(flat);
                Done();
            }
        }

        /// <summary>Lets go of the line. <paramref name="hop"/>: Jump (or snagged): the speed it had and a hop up.</summary>
        private void LetGo(bool hop)
        {
            if (OnLine && _motor != null)
            {
                Vector3 v = _motor.Pull ?? Vector3.zero;
                if (_state == State.Pulling) v *= 0.3f;   // reeled straight up a wall, keeping all of it would fling the toy
                if (hop) v.y = Mathf.Max(v.y, letGoHop * Scale);
                _motor.EndPull(v);
                if (Holder != null) Holder.PlayClip(letGoClip, 1f);
            }
            if (_state == State.Flying || _state == State.Missing) { _state = State.Reeling; return; }
            Done();
        }

        private void Done()
        {
            _state = State.Idle;
            _anchor = null;
            if (_dart != null) _dart.gameObject.SetActive(false);
            Draw(false);
        }

        /// <summary>The line from the muzzle to the dart: slack while it flies or reels, taut once it holds.</summary>
        private void Draw(bool on)
        {
            if (_line == null) return;
            if (!on) { _line.positionCount = 0; return; }
            float s = Scale;
            _line.startWidth = _line.endWidth = lineWidth * s;
            bool taut = OnLine;
            int n = taut ? 2 : 10;
            _line.positionCount = n;
            Vector3 a = MuzzlePoint, b = _dartAt;
            float droop = taut ? 0f : Vector3.Distance(a, b) * 0.06f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                _line.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (4f * t * (1f - t) * droop));
            }
        }

        private void Hint(string text)
        {
            var charge = Holder != null ? Holder.GetComponent<ShiftCharge>() : null;
            if (charge == null) charge = FindFirstObjectByType<ShiftCharge>();
            if (charge != null && !string.IsNullOrEmpty(text)) charge.ShowHint(text);
        }
    }
}
