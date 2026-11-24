using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A rope a toy swings on (D-0170, ported from the Goblin game's rope). Drag the Rope piece in at the TOP LEVEL of
    /// the Hierarchy with this object where the rope is tied (under a ceiling or a beam), rotation 0, scale 1, and set
    /// Length. E beside it, or flying into it, grabs it (RopeRider on the player does the grabbing and the swinging).
    /// WHY its physics is for looks only: in play the rope is a chain of points (each keeps its own speed, gravity
    /// pulls, the links hold their length), pinned where it is tied and, while held, at the holder's hands. It bends,
    /// whips and settles by itself, but it never moves the player: the rider works its own swing out, so the ride is
    /// the same whatever the chain does. A free rope that has come to rest stops being worked out until someone takes it.
    /// Outside Play only a gizmo line shows where it hangs: no mesh is made in the editor, so placing one changes
    /// nothing in the scene but the object itself.
    /// <para>The tube is drawn into the Tube child. Five rules keep the size shift's bend (WorldBend) and the body
    /// fade (BodyFade) from breaking it:
    /// 1. Never write the Tube renderer's material, sharedMaterial or sharedMaterials at run time, and never read
    ///    .material (that makes a private copy). WorldBend swaps the array on a shift and puts the SAME array back
    ///    0.7 s later; a write in between is lost or undoes the bend.
    /// 2. The material is URP Lit. Anything else is skipped by WorldBend and the rope stays straight while the room bulges.
    /// 3. The Tube is a CHILD of the object with the grip collider: WorldBend finds renderers under colliders.
    /// 4. The Tube never becomes a child of the player: BodyFade fades every renderer under the Player.
    /// 5. Vertices are written in the Tube's own space and the bounds are worked out again after every rebuild: a bent
    ///    copy moves vertices in the shader, and a stale box would cull the rope.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rope : MonoBehaviour, IInteractable
    {
        private const float DefaultGripRadius = 0.25f;   // the handle's radius before the settings are known (OnValidate must not load them)
        private const float LeastLength = 0.5f;          // a rope shorter than this has no room for a pair of hands
        private const float LeastLink = 0.05f;           // a link is never shorter than this, own units (a wrong setting must not make thousands)
        private const int LeastLinks = 4;                // fewer links than this cannot bend
        private const float LeastStepsPerSecond = 30f;   // the chain falls apart when stepped more slowly
        private const int MostStepsAFrame = 6;           // after a stall the chain catches up by this many steps at most, then lets the rest go
        private const int BumpHits = 8;                  // things one point can meet in one step
        private const float SurfaceGap = 0.002f;         // a stopped point rests this far off what it met, own units
        private const float TouchDrag = 0.5f;            // share of its speed a point loses on what it touches: the rope drags on things
        private const float GizmoKnot = 0.08f;           // radius of the gizmo's end marks, own units

        private static readonly List<Rope> all = new List<Rope>();
        /// <summary>Every rope switched on right now (the rider looks through them for one to catch).</summary>
        public static IReadOnlyList<Rope> All => all;

        [SerializeField, Min(LeastLength), Tooltip("How long the rope is, from where it is tied (this object's place) to its end, in the units of the place it hangs in (a toy is 1.8 tall).")]
        private float length = 8f;
        [SerializeField, Tooltip("The words beside E.")]
        private string prompt = "Grab rope";
        [SerializeField, Tooltip("Resources/Rope is loaded if empty.")]
        private RopeSettings settings;
        [SerializeField, Tooltip("The Tube child's mesh filter: the rope is drawn into it.")]
        private MeshFilter tube;
        [SerializeField, Tooltip("The thin trigger on this object that E finds the rope by. Kept as long as the rope by this script.")]
        private CapsuleCollider grip;
        [SerializeField, Tooltip("What the rope's points stop on (floors, walls). Players and triggers never stop it.")]
        private LayerMask bumpsInto = ~0;

        private Vector3[] _points;       // the chain, world space; 0 is where it is tied
        private Vector3[] _previous;     // where each point was a step ago (its speed is the difference)
        private float _restLink;         // a link's length, world units
        private float _stepTimer;
        private int _heldIndex = -1;     // the point the hands hold; -1 when free
        private Vector3 _lastHands;      // where the hands were at the end of the last frame's steps
        private bool _awake = true;      // false once a free rope has come to rest
        private float _stillFor;         // seconds no point has moved faster than the rest speed
        private readonly RaycastHit[] _bumps = new RaycastHit[BumpHits];

        private Mesh _mesh;              // the tube, made once in Awake; only its vertices are rewritten
        private Transform _tubeSpace;
        private Vector3[] _vertices, _normals;
        private int _sides;

        private Interactor _askedBy;     // the last player who asked for the prompt...
        private RopeRider _askedRider;   // ...and that player's rider, kept so the question is not a search every frame

        /// <summary>Where the rope is tied, world.</summary>
        public Vector3 Anchor => transform.position;
        /// <summary>How big the place it hangs in is: 1 at toy scale. Every rope number is multiplied by it.</summary>
        public float Size => transform.lossyScale.y;
        /// <summary>The rope's length, world units.</summary>
        public float Length => length * Size;
        /// <summary>The tube's radius, world units (the rider puts a palm either side of it).</summary>
        public float Thickness => (settings != null ? settings.thickness : 0f) * Size;
        /// <summary>The player swinging on it; null when free.</summary>
        public RopeRider Holder { get; private set; }
        /// <summary>True while someone swings on it.</summary>
        public bool Held => Holder != null;
        /// <summary>Where its loose end is right now, world.</summary>
        public Vector3 End => _points != null ? _points[_points.Length - 1] : Anchor + Vector3.down * Length;

        private void Awake()
        {
            if (settings == null) settings = RopeSettings.Load();
            FitGrip();
            BuildChain();
            BuildTube();
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            Wake();
        }

        private void OnDisable()
        {
            all.Remove(this);
            // Switched off or destroyed while held: the holder is told, and falls from where it is.
            RopeRider holder = Holder;
            Holder = null;
            _heldIndex = -1;
            if (holder != null) holder.RopeGone();
        }

        /// <summary>The tube's mesh was made at run time: it goes with the rope, or every rope deleted in Play would leave one behind.</summary>
        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void OnValidate()
        {
            length = Mathf.Max(LeastLength, length);
            FitGrip();
        }

        /// <summary>A rider takes hold (or lets go, with null). Letting go, the chain keeps the speed its points had,
        /// so the rope whips on after the toy has left it.</summary>
        public void SetHolder(RopeRider holder)
        {
            if (holder != null && Holder == null) _lastHands = holder.HandPoint;
            Holder = holder;
            if (holder == null) _heldIndex = -1;
            Wake();
        }

        /// <summary>The point of the rope nearest to <paramref name="point"/>, as it hangs or bends right now, and never
        /// nearer to where it is tied than <paramref name="minHold"/> along it. <paramref name="along"/>: how far along
        /// the rope that is, from where it is tied. All world units.</summary>
        public Vector3 ClosestPoint(Vector3 point, float minHold, out float along)
        {
            float whole = Length, lowest = Mathf.Min(minHold, whole);
            if (_points == null)
            {
                along = Mathf.Clamp(Vector3.Dot(point - Anchor, Vector3.down), lowest, whole);
                return Anchor + Vector3.down * along;
            }
            float best = float.MaxValue, bestAlong = 0f;
            Vector3 bestPoint = _points[0];
            for (int i = 0; i < _points.Length - 1; i++)
            {
                Vector3 a = _points[i], ab = _points[i + 1] - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                Vector3 c = a + ab * t;
                float d = (point - c).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                bestPoint = c;
                bestAlong = (i + t) * _restLink;
            }
            if (bestAlong < lowest)
            {
                bestAlong = lowest;
                bestPoint = PointAlong(lowest);
            }
            along = bestAlong;
            return bestPoint;
        }

        /// <summary>The point <paramref name="along"/> the rope from where it is tied (world units), as it bends right now.</summary>
        public Vector3 PointAlong(float along)
        {
            if (_points == null) return Anchor + Vector3.down * Mathf.Clamp(along, 0f, Length);
            float f = Mathf.Clamp(along / _restLink, 0f, _points.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), _points.Length - 2);
            return Vector3.Lerp(_points[i], _points[i + 1], f - i);
        }

        // ---------- E ----------

        public string PromptFor(Interactor who)
        {
            RopeRider rider = RiderOf(who);
            return rider != null && rider.CanGrab(this) ? prompt : "";
        }

        public void Interact(Interactor who)
        {
            RopeRider rider = RiderOf(who);
            if (rider != null) rider.TryGrab(this);
        }

        /// <summary>The rider on the player asking. The Interactor asks for the prompt every frame the rope is in reach,
        /// so the answer for the last asker is kept.</summary>
        private RopeRider RiderOf(Interactor who)
        {
            if (who == null) return null;
            if (!ReferenceEquals(who, _askedBy) || _askedRider == null)
            {
                _askedBy = who;
                _askedRider = who.GetComponent<RopeRider>();
            }
            return _askedRider;
        }

        /// <summary>The handle E finds the rope by: a thin upright capsule trigger from where it is tied to its end. Its
        /// numbers are the collider's own, so they grow with the object by themselves. It stays straight down even while
        /// the rope sways: it is only how E finds the rope (E takes the rope where it hangs at rest). Only written when a
        /// number is off, so fitting it in the editor changes nothing that is already right.</summary>
        private void FitGrip()
        {
            if (grip == null) return;
            float radius = settings != null ? settings.gripRadius : DefaultGripRadius;
            Vector3 centre = new Vector3(0f, -length * 0.5f, 0f);
            if (!grip.isTrigger) grip.isTrigger = true;
            if (grip.direction != 1) grip.direction = 1;   // along Y
            if (!Mathf.Approximately(grip.radius, radius)) grip.radius = radius;
            if (!Mathf.Approximately(grip.height, length)) grip.height = length;
            if (grip.center != centre) grip.center = centre;
        }

        // ---------- the chain ----------

        private void BuildChain()
        {
            float whole = Length;
            float link = Mathf.Max(LeastLink, settings.linkLength) * Size;
            int links = Mathf.Max(LeastLinks, Mathf.CeilToInt(whole / link));
            _restLink = whole / links;
            _points = new Vector3[links + 1];
            _previous = new Vector3[links + 1];
            for (int i = 0; i <= links; i++)
            {
                _points[i] = Anchor + Vector3.down * (i * _restLink);
                _previous[i] = _points[i];
            }
        }

        private void Wake()
        {
            _awake = true;
            _stillFor = 0f;
        }

        private void LateUpdate()
        {
            if (_points == null || !_awake) return;
            bool held = Holder != null;
            if (!held) _heldIndex = -1;
            float step = 1f / Mathf.Max(LeastStepsPerSecond, settings.stepsPerSecond);
            _stepTimer = Mathf.Min(_stepTimer + Time.deltaTime, step * MostStepsAFrame);
            int steps = Mathf.FloorToInt(_stepTimer / step);
            if (steps <= 0) return;   // nothing moved this frame: the tube is as it was
            Vector3 hands = held ? Holder.HandPoint : Vector3.zero;
            float heldAlong = held ? Holder.HeldAlong : 0f;
            float fastest = 0f;
            for (int s = 0; s < steps; s++)
            {
                // The hands move smoothly across this frame's steps.
                Vector3 before = held ? Vector3.Lerp(_lastHands, hands, s / (float)steps) : Vector3.zero;
                Vector3 now = held ? Vector3.Lerp(_lastHands, hands, (s + 1f) / steps) : Vector3.zero;
                fastest = Mathf.Max(fastest, Step(step, held, now, before, heldAlong));
            }
            _stepTimer -= steps * step;
            if (held) _lastHands = hands;
            UpdateTube();

            // A free rope that has hung still for a while is left as it is until someone takes it.
            if (held || fastest > settings.restSpeed * Size) _stillFor = 0f;
            else
            {
                _stillFor += steps * step;
                if (_stillFor >= settings.restSeconds) _awake = false;
            }
        }

        /// <summary>One step of the chain, <paramref name="h"/> seconds long. Hands back the speed of its fastest point,
        /// world units a second.</summary>
        private float Step(float h, bool held, Vector3 hands, Vector3 handsBefore, float heldAlong)
        {
            float size = Size;
            // The rope's own gravity, never Physics.gravity: PlayerMotor sets that to the props' 60.
            Vector3 fall = Vector3.down * (settings.ropeGravity * size * h * h);
            float keep = Mathf.Clamp01(1f - settings.ropeDrag * h);
            int last = _points.Length - 1;
            // Which point the hands hold: by how far along the rope they are.
            if (held) _heldIndex = Mathf.Clamp(Mathf.RoundToInt(heldAlong / _restLink), 1, last);

            for (int i = 1; i <= last; i++)
            {
                Vector3 p = _points[i];
                Vector3 v = (p - _previous[i]) * keep;
                _previous[i] = p;
                _points[i] = p + v + fall;
            }
            _points[0] = Anchor;
            _previous[0] = Anchor;
            if (_heldIndex > 0)
            {
                // Pinned to the hands and carrying their speed this step, so letting go keeps it.
                _previous[_heldIndex] = handsBefore;
                _points[_heldIndex] = hands;
            }

            float stiffness = settings.linkStiffness;
            int passes = Mathf.Max(1, settings.solverPasses);
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < last; i++)
                {
                    Vector3 ab = _points[i + 1] - _points[i];
                    float distance = ab.magnitude;
                    if (distance < 1e-6f) continue;
                    float wa = i == 0 || i == _heldIndex ? 0f : 1f;
                    float wb = i + 1 == _heldIndex ? 0f : 1f;
                    float total = wa + wb;
                    if (total <= 0f) continue;
                    Vector3 correction = ab * ((distance - _restLink) / distance * stiffness);
                    _points[i] += correction * (wa / total);
                    _points[i + 1] -= correction * (wb / total);
                }
            }

            // Bump: a point that went into something stops at its surface.
            float radius = settings.thickness * size;
            int mask = Layers.Seen(bumpsInto);
            float fastest = 0f;
            for (int i = 1; i <= last; i++)
            {
                if (i == _heldIndex) continue;
                Vector3 from = _previous[i];
                Vector3 move = _points[i] - from;
                float distance = move.magnitude;
                if (distance < 1e-5f) continue;
                Vector3 way = move / distance;
                if (Nearest(from, radius, way, distance, mask, out RaycastHit hit))
                {
                    _points[i] = from + way * hit.distance + hit.normal * (SurfaceGap * size);
                    _previous[i] = Vector3.Lerp(_previous[i], _points[i], TouchDrag);
                    distance = (_points[i] - _previous[i]).magnitude;
                }
                fastest = Mathf.Max(fastest, distance / h);
            }
            return fastest;
        }

        /// <summary>The nearest solid thing a point meets on its way. Skipped: a player's capsule (the rider's own would
        /// push the tail about every frame), a figure's hit shape (it stops nothing, the tree's rule for every room
        /// check), and anything the point already starts inside (no way out of it can be told).</summary>
        private bool Nearest(Vector3 from, float radius, Vector3 way, float distance, int mask, out RaycastHit nearest)
        {
            nearest = default;
            int n = Physics.SphereCastNonAlloc(from, radius, way, _bumps, distance, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                RaycastHit hit = _bumps[i];
                if (hit.distance <= 0f || hit.distance >= best) continue;
                if (hit.collider is CharacterController || HitShapes.IsShape(hit.collider)) continue;
                best = hit.distance;
                nearest = hit;
            }
            return best < float.MaxValue;
        }

        // ---------- the tube ----------

        /// <summary>Makes the tube's mesh, once: a ring of points round every point of the chain, joined into a pipe, and
        /// one more point closing its low end (the camera sits under a toy hanging there and would look up into an open
        /// pipe; the top end is in the ceiling). After this only the vertices move.</summary>
        private void BuildTube()
        {
            if (tube == null)
            {
                Debug.LogWarning($"Rope '{name}': no Tube mesh filter is set, so the rope is not drawn (it can still be ridden). Use the Rope piece: Shift Jam > Traversal > Make Rope Piece.", this);
                return;
            }
            _tubeSpace = tube.transform;
            _sides = Mathf.Clamp(settings.sides, 3, 12);
            int rings = _points.Length;
            int cap = rings * _sides;   // the point in the middle of the low end
            _vertices = new Vector3[cap + 1];
            _normals = new Vector3[cap + 1];
            var uvs = new Vector2[cap + 1];   // one colour all over: every point reads the same texel
            var triangles = new int[(rings - 1) * _sides * 6 + _sides * 3];
            int t = 0;
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < _sides; s++)
                {
                    int a = r * _sides + s, b = r * _sides + (s + 1) % _sides;
                    int c = a + _sides, d = b + _sides;
                    // Clockwise seen from outside (Unity's front): a ring runs from "side" round to "up" = down the rope x side.
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                }
            }
            int lastRing = (rings - 1) * _sides;
            for (int s = 0; s < _sides; s++)
            {
                triangles[t++] = cap; triangles[t++] = lastRing + s; triangles[t++] = lastRing + (s + 1) % _sides;
            }

            _mesh = new Mesh { name = "RopeTube" };
            _mesh.MarkDynamic();
            UpdateTube(assign: false);
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            tube.sharedMesh = _mesh;
        }

        /// <summary>Moves the tube onto the chain. Each ring is turned along the rope by carrying the ring before's
        /// sideways direction on (so the tube does not twist); worked out in world space, stored in the Tube's own.</summary>
        private void UpdateTube(bool assign = true)
        {
            if (_mesh == null || _tubeSpace == null) return;
            float radius = settings.thickness * Size;
            int last = _points.Length - 1;
            Vector3 side = Vector3.right, forward = Vector3.down;
            for (int r = 0; r <= last; r++)
            {
                forward = r < last ? _points[r + 1] - _points[r] : _points[r] - _points[r - 1];
                if (forward.sqrMagnitude < 1e-8f) forward = Vector3.down;
                forward.Normalize();
                side = Vector3.ProjectOnPlane(side, forward);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.forward, forward);
                side.Normalize();
                Vector3 up = Vector3.Cross(forward, side);
                for (int s = 0; s < _sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / _sides;
                    Vector3 normal = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    _vertices[r * _sides + s] = _tubeSpace.InverseTransformPoint(_points[r] + normal * radius);
                    _normals[r * _sides + s] = _tubeSpace.InverseTransformDirection(normal);
                }
            }
            int cap = (last + 1) * _sides;
            _vertices[cap] = _tubeSpace.InverseTransformPoint(_points[last]);
            _normals[cap] = _tubeSpace.InverseTransformDirection(forward);
            if (!assign) return;
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.RecalculateBounds();
        }

        // ---------- the editor's picture ----------

        /// <summary>The only picture of the rope outside Play: a line from where it is tied straight down its length, with
        /// a mark at each end. In Play the chain itself is drawn.</summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.3f);
            float knot = GizmoKnot * Size;
            Gizmos.DrawWireSphere(Anchor, knot);
            if (_points != null)
            {
                for (int i = 0; i < _points.Length - 1; i++) Gizmos.DrawLine(_points[i], _points[i + 1]);
                Gizmos.DrawWireSphere(_points[_points.Length - 1], knot);
                return;
            }
            Vector3 end = Anchor + Vector3.down * Length;
            Gizmos.DrawLine(Anchor, end);
            Gizmos.DrawWireSphere(end, knot);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();
    }
}
