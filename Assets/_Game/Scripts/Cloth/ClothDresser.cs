using System.Collections.Generic;
using UnityEngine;
using MC = MagicaCloth2;

namespace ShiftJam
{
    /// <summary>
    /// Makes this object's model a Magica Cloth 2 cloth as the scene starts (D-0233): a curtain (the Playroom pack's
    /// Curtain_01, held at the top of its model) or a toy flag (Army_Flag_01, held at its pole). Put it on the object with
    /// the MeshFilter and MeshRenderer, choose its kind and its place; every number is on Resources/ClothSettings.
    /// The cloth is built from code, as Magica's RuntimeBuildDemo does: a MagicaCloth added here, set up (MeshCloth on this
    /// renderer, the numbers of its kind), each point of the model marked held, moving or left out by ClothShape's rule
    /// (Magica's vertex attribute list: one mark a point, in place of painted data), then BuildAndRun. Nothing is saved in
    /// the scene but this component and its two fields; a maker run replaces it cleanly.
    /// Magica works in metres: the build is made with this object shrunk to 1 / Units Per Metre for that one call (Magica
    /// records the size there) and its size put straight back in the same frame, so Magica scales every force, speed and
    /// thickness by Units Per Metre (its scale ratio) and the curtain moves like a real one in a house of 50 units a metre.
    /// Nothing is drawn at the small size.
    /// Around it: a Magica wind zone (a box round the cloth, its place's Breeze; a curtain's blows from the window into the
    /// room, a flag's from its pole to its free end), the players' pushing capsules (ClothPlayerColliders, given to every
    /// player found, a look every few seconds), and a poke from any shot whose flight passes through the moving part (a
    /// Magica AddForce along the flight as the shot gets there; the shot flies on). Magica's own camera culling pauses a
    /// cloth nobody sees; a place with Off When Far stops when the camera is far.
    /// Builds wait in a queue (Builds Per Frame), each running on Magica's worker threads after its start. A model that is
    /// not Read/Write stays still with one warning (the maker turns Read/Write on). Each PC dresses its own cloths; nothing
    /// is sent online. The asset is read again whenever it changes (in Play too).
    /// OFF SWITCHES: ClothSettings > Cloth On, the kind's On, the place's On (the model stands still as modelled).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClothDresser : MonoBehaviour
    {
        [SerializeField, Tooltip("Which numbers on Resources/ClothSettings this cloth uses: Curtain (held at the top of its model) or Flag (held at its pole).")]
        private ClothSettings.ClothKind kind = ClothSettings.ClothKind.Curtain;
        [SerializeField, Tooltip("Its place on Resources/ClothSettings > Places (Bedroom, Window, Downstairs, PlayroomFort, TanFort): its breeze, its off switch and whether it stops when the camera is far. A place not on the list: it moves, with no breeze.")]
        private string place = "Bedroom";

        private const string Tag = "[Cloth D-0233]";
        private const int MostPokes = 4;
        private const float MostPokeSeconds = 3f;   // a shot that would get there later than this has long gone (a dart lives 2.5 s)

        private static readonly List<ClothDresser> Live = new List<ClothDresser>();
        private static readonly List<ClothDresser> Waiting = new List<ClothDresser>();
        private static readonly List<MC.ColliderComponent> Pushers = new List<MC.ColliderComponent>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly System.Predicate<MC.ColliderComponent> Gone = c => c == null;
        private static int _tickFrame = -1;
        private static float _lookAt;

        private MC.MagicaCloth _cloth;
        private MC.MagicaWindZone _breeze;
        private MeshRenderer _look;
        private Bounds _moving;            // the moving pieces, in this object's own space: the pokes' target, a flag's breeze
        private Bounds _held;              // the held points, own space: Magica's cloth centre (their middle) is in here
        private int _points, _movers;
        private bool _queued, _ready, _failed;
        private int _seen = -1;            // the asset's change count this cloth was last fitted to
        private int _madeWith;             // the numbers its build was made with (kind, hold, join, metre): a change builds it again
        private float _buildStart;
        private readonly float[] _pokeAt = new float[MostPokes];
        private readonly Vector3[] _pokeWay = new Vector3[MostPokes];
        private int _pokes;

        /// <summary>Which kind of cloth it is.</summary>
        public ClothSettings.ClothKind Kind => kind;
        /// <summary>Its place on ClothSettings > Places.</summary>
        public string Place => place;
        /// <summary>True once its cloth is built and running.</summary>
        public bool Ready => _ready;
        /// <summary>Its Magica cloth while there is one; null before the build, and when it is off.</summary>
        public MC.MagicaCloth Cloth => _cloth;

        /// <summary>Sets its kind and place (the maker).</summary>
        public void Setup(ClothSettings.ClothKind of, string where)
        {
            kind = of;
            place = where;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll()
        {
            Live.Clear();
            Waiting.Clear();
            Pushers.Clear();
            _tickFrame = -1;
            _lookAt = 0f;
        }

        private void OnValidate() => _seen = -1;   // its kind or place changed in the Inspector: fit again

        private void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
            Projectile.Fired += OnFired;
            _seen = -1;
        }

        private void OnDisable()
        {
            Live.Remove(this);
            Waiting.Remove(this);
            _queued = false;
            Projectile.Fired -= OnFired;
            _pokes = 0;
        }

        private void OnDestroy() => TearDown();

        private void Update()
        {
            Tick();
            if (_seen != ClothSettings.Changes) Fit();
            if (_pokes > 0) Pokes();
        }

        // ------------------------------------------------------------------------------------------------ fitting

        /// <summary>Brings it in line with the asset: built when it should be, gone when it is off, built again when a
        /// number its build was made with changed, otherwise its numbers and its breeze set again.</summary>
        private void Fit()
        {
            ClothSettings s = ClothSettings.Load();
            ClothSettings.Kind k = s.For(kind);
            ClothSettings.Place p = s.Find(place);
            if (!Wanted(s, k, p))
            {
                TearDown();
                Waiting.Remove(this);
                _queued = false;
                _seen = ClothSettings.Changes;
                return;
            }
            if (_cloth != null && _madeWith != MadeWith(k)) TearDown();
            if (_cloth == null)
            {
                if (!_queued && !_failed) { Waiting.Add(this); _queued = true; }
                _seen = ClothSettings.Changes;   // the build reads the asset as it starts
                return;
            }
            if (!_ready) return;   // still building: this runs again each frame until it is done, then fits it
            _seen = ClothSettings.Changes;
            Tune(_cloth.SerializeData, k, s, p);
            _cloth.SetParameterChange();
            Breeze(p);
        }

        private static bool Wanted(ClothSettings s, ClothSettings.Kind k, ClothSettings.Place p) => s.clothOn && k.on && (p == null || p.on);

        private int MadeWith(ClothSettings.Kind k) => System.HashCode.Combine(kind, k.hold, k.holdDistance, k.joinPieces, k.unitsPerMetre);

        /// <summary>Once a frame for every cloth: the waiting builds (Builds Per Frame), and now and then the look for players
        /// to give a pushing capsule.</summary>
        private static void Tick()
        {
            if (_tickFrame == Time.frameCount) return;
            _tickFrame = Time.frameCount;
            ClothSettings s = ClothSettings.Load();
            int budget = Mathf.Max(1, s.buildsPerFrame);
            while (budget > 0 && Waiting.Count > 0)
            {
                ClothDresser next = Waiting[0];
                Waiting.RemoveAt(0);
                if (next == null || !next.isActiveAndEnabled) continue;
                next._queued = false;
                next.Build();
                budget--;
            }
            if (s.playerPushes && Time.unscaledTime >= _lookAt)
            {
                _lookAt = Time.unscaledTime + Mathf.Max(0.25f, s.lookForPlayersSeconds);
                LookForPlayers();
            }
        }

        /// <summary>Gives every player (a Shifter with a walking capsule: this PC's own and the others' copies) its pushing
        /// capsule. A small list every few seconds, never every frame.</summary>
        private static void LookForPlayers()
        {
            Shifter[] players = FindObjectsByType<Shifter>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                Shifter p = players[i];
                if (p == null || p.TryGetComponent(out ClothPlayerColliders _) || !p.TryGetComponent(out CharacterController _)) continue;
                p.gameObject.AddComponent<ClothPlayerColliders>();
            }
        }

        // ------------------------------------------------------------------------------------------------ the build

        /// <summary>Makes the cloth: Magica's MeshCloth on this renderer, every point marked by ClothShape, then BuildAndRun
        /// (Magica builds on its worker threads and calls OnBuilt).</summary>
        private void Build()
        {
            ClothSettings s = ClothSettings.Load();
            ClothSettings.Kind k = s.For(kind);
            ClothSettings.Place p = s.Find(place);
            if (_cloth != null || _failed || !Wanted(s, k, p)) return;
            if (!TryGetComponent(out MeshFilter filter) || !TryGetComponent(out _look) || filter.sharedMesh == null)
            {
                Fail("there is no MeshFilter with a mesh and MeshRenderer on its object");
                return;
            }
            Mesh mesh = filter.sharedMesh;
            if (!mesh.isReadable)
            {
                Fail($"its model '{mesh.name}' is not Read/Write, and Magica can only build a cloth from a readable mesh. Run Shift Jam > House > Land Cloth (D-0233), which turns it on (a fresh import of the pack turns it off again)");
                return;
            }
            int n = mesh.vertexCount;
            var parts = new ClothShape.Part[n];
            ClothShape.Counts c = ClothShape.Sort(mesh, k.hold, k.holdDistance, k.joinPieces > 0f, parts, null);
            if (c.moving == 0)
            {
                Fail($"nothing in it moves with Hold Distance {k.holdDistance} ({c.pieces} pieces, all held or loose): lower it on ClothSettings");
                return;
            }
            var marks = new MC.VertexAttribute[n];
            for (int i = 0; i < n; i++)
                marks[i] = parts[i] == ClothShape.Part.Moves ? MC.VertexAttribute.Move
                         : parts[i] == ClothShape.Part.Held ? MC.VertexAttribute.Fixed
                         : MC.VertexAttribute.Invalid;   // left out of the cloth: drawn as modelled
            _moving = c.movingBounds;
            _held = c.held > 0 ? c.heldBounds : c.movingBounds;
            _points = n;
            _movers = c.moving;
            _madeWith = MadeWith(k);
            _buildStart = Time.realtimeSinceStartup;

            Transform t = transform;
            Vector3 size = t.localScale;
            try
            {
                // Magica records this object's size as it starts the build (Init, below, in this very call) and from then
                // on scales every force, speed and thickness by (size now / that size): built at 1 / Units Per Metre and put
                // back straight after, the cloth behaves Units Per Metre times bigger than Magica's metres. Same frame:
                // nothing is drawn small.
                t.localScale = size / Mathf.Max(0.01f, k.unitsPerMetre);
                _cloth = gameObject.AddComponent<MC.MagicaCloth>();
                MC.ClothSerializeData sd = _cloth.SerializeData;
                sd.clothType = MC.ClothProcess.ClothType.MeshCloth;
                sd.sourceRenderers.Add(_look);
                sd.paintMode = MC.ClothSerializeData.PaintMode.Manual;
                sd.reductionSetting.simpleDistance = k.joinPieces;
                sd.reductionSetting.shapeDistance = 0f;
                Tune(sd, k, s, p);
                List<MC.ColliderComponent> pushers = sd.colliderCollisionConstraint.colliderList;
                for (int i = 0; i < Pushers.Count; i++)
                    if (Pushers[i] != null && !pushers.Contains(Pushers[i])) pushers.Add(Pushers[i]);
                _cloth.GetSerializeData2().vertexAttributeList.Add(marks);
                _cloth.OnBuildComplete += OnBuilt;
                _cloth.BuildAndRun();   // a failure calls OnBuilt(false) before it returns
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, this);
                TearDown();
                Fail("the build threw (the line above)");
            }
            finally
            {
                t.localScale = size;
            }
        }

        /// <summary>Magica's build has finished (on the main thread).</summary>
        private void OnBuilt(MC.MagicaCloth cloth, bool ok)
        {
            if (cloth == null || cloth != _cloth) return;
            if (!ok)
            {
                // Magica's own Start still runs its auto build once after BuildAndRun has switched auto building off, and that
                // calls back "not built" with no error while the real build is under way (ClothProcess.AutoBuild). Taken for a
                // failure, it tore every cloth down (his 25 Nov 19:20 play: all 12 "stays still"). Only a false that carries
                // an error is one; the real build calls back again when it is done.
                if (cloth.Process.IsState(MC.ClothProcess.State_DisableAutoBuild) && !cloth.Process.Result.IsError()) return;
                TearDown();
                Fail("Magica Cloth's build failed: " + cloth.Process.Result.GetResultString());
                return;
            }
            _ready = true;
            if (TakePushers(cloth)) cloth.SetParameterChange();
            ClothSettings s = ClothSettings.Load();
            Breeze(s.Find(place));
            Debug.Log($"{Tag} {name} ({kind}, place '{place}'): a cloth, {_movers} of its {_points} points move; built in {Time.realtimeSinceStartup - _buildStart:F2} s.", this);
        }

        /// <summary>Magica's numbers from the asset. Every one of these may change while it runs (Magica marks them so).</summary>
        private void Tune(MC.ClothSerializeData sd, ClothSettings.Kind k, ClothSettings s, ClothSettings.Place p)
        {
            sd.updateMode = MC.ClothUpdateMode.Normal;   // follows the game's own time: a pause holds it
            sd.disableMode = MC.ClothDisableMode.Reset;
            sd.gravity = k.gravity;
            sd.gravityFalloff = 0f;
            sd.damping.SetValue(k.damping);
            sd.radius.SetValue(k.thickness);
            sd.distanceConstraint.stiffness.SetValue(k.stretchStiffness);
            sd.triangleBendingConstraint.stiffness = k.bendStiffness;
            sd.angleRestorationConstraint.useAngleRestoration = k.shapeKeep > 0f;
            sd.angleRestorationConstraint.stiffness.SetValue(k.shapeKeep, 1f, k.shapeKeepAtEnd, true);
            sd.angleLimitConstraint.useAngleLimit = false;
            sd.tetherConstraint.distanceCompression = k.tetherSquash;
            sd.inertiaConstraint.particleSpeedLimit.SetValue(k.limitSpeed, k.speedLimit);
            sd.colliderCollisionConstraint.mode = k.edgeCollision ? MC.ColliderCollisionConstraint.Mode.Edge : MC.ColliderCollisionConstraint.Mode.Point;
            sd.colliderCollisionConstraint.friction = k.friction;
            sd.wind.influence = k.windInfluence;
            sd.wind.frequency = k.windFrequency;
            sd.wind.turbulence = k.windTurbulence;
            sd.wind.movingWind = 0f;
            MC.CullingSettings cull = sd.cullingSettings;
            cull.cameraCullingMode = MC.CullingSettings.CameraCullingMode.Keep;   // nobody sees it: it pauses where it is
            cull.cameraCullingMethod = MC.CullingSettings.CameraCullingMethod.ManualRenderer;
            if (_look != null && !cull.cameraCullingRenderers.Contains(_look)) cull.cameraCullingRenderers.Add(_look);
            cull.distanceCullingLength.SetValue(p != null && p.offWhenFar, s.farDistance);
            cull.distanceCullingFadeRatio = s.farFade;
            cull.distanceCullingReferenceObject = null;   // the main camera
        }

        /// <summary>Takes every player's pushing capsule it does not have yet. True when it took one.</summary>
        private static bool TakePushers(MC.MagicaCloth cloth)
        {
            List<MC.ColliderComponent> list = cloth.SerializeData.colliderCollisionConstraint.colliderList;
            list.RemoveAll(Gone);
            bool took = false;
            for (int i = 0; i < Pushers.Count; i++)
            {
                MC.ColliderComponent c = Pushers[i];
                if (c == null || list.Contains(c)) continue;
                list.Add(c);
                took = true;
            }
            return took;
        }

        /// <summary>A player's pushing capsule has come (ClothPlayerColliders): every cloth built now takes it; one still
        /// building takes it when it is done.</summary>
        public static void AddPusher(MC.ColliderComponent pusher)
        {
            if (pusher == null || Pushers.Contains(pusher)) return;
            Pushers.Add(pusher);
            for (int i = 0; i < Live.Count; i++)
            {
                ClothDresser d = Live[i];
                if (d != null && d._ready && d._cloth != null && TakePushers(d._cloth)) d._cloth.SetParameterChange();
            }
        }

        /// <summary>A player's pushing capsule is going: no cloth built after this takes it (Magica drops a destroyed collider
        /// from every cloth by itself).</summary>
        public static void RemovePusher(MC.ColliderComponent pusher) => Pushers.Remove(pusher);

        /// <summary>The cloth, its breeze and its waiting pokes go; the model is drawn as modelled again (Magica puts the
        /// renderer's own mesh back as its cloth goes).</summary>
        private void TearDown()
        {
            _ready = false;
            _pokes = 0;
            if (_cloth != null)
            {
                _cloth.OnBuildComplete -= OnBuilt;
                Destroy(_cloth);
            }
            _cloth = null;
            if (_breeze != null) Destroy(_breeze.gameObject);
            _breeze = null;
        }

        private void Fail(string why)
        {
            _failed = true;
            _queued = false;
            Debug.LogWarning($"{Tag} {name} ({kind}, place '{place}') stays still: {why}.", this);
        }

        // ------------------------------------------------------------------------------------------------ the breeze

        /// <summary>The place's breeze: a Magica wind zone blowing its way at its Breeze. Magica gives a cloth the smallest
        /// zone its centre is in, and a cloth's centre is the middle of its held points: the zone is a box round those (a
        /// cube as wide as their box's diagonal, so turning it to the breeze's way never leaves them out). No breeze: the
        /// zone is switched off.</summary>
        private void Breeze(ClothSettings.Place p)
        {
            bool want = _cloth != null && p != null && p.breeze > 0f;
            if (!want)
            {
                if (_breeze != null && _breeze.gameObject.activeSelf) _breeze.gameObject.SetActive(false);
                return;
            }
            if (_breeze == null)
            {
                var go = new GameObject("Breeze (D-0233)");
                go.transform.SetParent(transform, false);
                _breeze = go.AddComponent<MC.MagicaWindZone>();
            }
            if (!_breeze.gameObject.activeSelf) _breeze.gameObject.SetActive(true);
            Vector3 way = BreezeWay(p);
            _breeze.transform.localPosition = _held.center;
            _breeze.transform.rotation = Quaternion.LookRotation(way, Mathf.Abs(way.y) > 0.99f ? Vector3.forward : Vector3.up);
            _breeze.mode = MC.MagicaWindZone.Mode.BoxDirection;
            float side = _held.size.magnitude + 2f;   // own units (the zone's scale is this object's)
            _breeze.size = new Vector3(side, side, side);
            _breeze.main = p.breeze;
            _breeze.turbulence = p.gusts;
            _breeze.directionAngleX = 0f;
            _breeze.directionAngleY = 0f;
            _breeze.isAddition = false;
        }

        /// <summary>The way the breeze blows, world space: a curtain's from the window into the room (the model's front),
        /// a flag's from its pole toward its free end; then the place's Turn and Tilt.</summary>
        private Vector3 BreezeWay(ClothSettings.Place p)
        {
            Vector3 flat;
            if (kind == ClothSettings.ClothKind.Flag)
            {
                Vector3 mid = _moving.center;
                flat = transform.TransformDirection(new Vector3(mid.x, 0f, mid.z));
            }
            else flat = transform.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-8f) { flat = transform.forward; flat.y = 0f; }
            if (flat.sqrMagnitude < 1e-8f) flat = Vector3.forward;
            flat = Quaternion.AngleAxis(p.turn, Vector3.up) * flat.normalized;
            float tilt = p.tilt * Mathf.Deg2Rad;
            return (flat * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt)).normalized;
        }

        // ------------------------------------------------------------------------------------------------ the pokes

        /// <summary>A shot was fired somewhere (Projectile.Fired, every shot on this PC, others' copies too): if its line
        /// passes through this cloth's moving part within Poke Reach, with nothing solid before it, a poke is kept for the
        /// moment it gets there.</summary>
        private void OnFired(Projectile prefab, Vector3 from, Vector3 way, float scale, Transform owner, float speedScale)
        {
            if (!_ready || prefab == null || _pokes >= MostPokes) return;
            ClothSettings s = ClothSettings.Load();
            if (!s.shotsPoke || s.For(kind).shotPoke <= 0f) return;
            Transform t = transform;
            float unit = Mathf.Abs(t.lossyScale.x);
            if (unit < 1e-6f || way.sqrMagnitude < 1e-8f) return;
            Vector3 dir = way.normalized;
            float reach = s.pokeReach * Mathf.Sqrt(Mathf.Max(0.001f, scale)) / unit;   // a kid's dart is bigger; in this object's own units
            Vector3 grow = new Vector3(reach, reach, reach);
            if (!ClothShape.LineMeetsBox(t.InverseTransformPoint(from), t.InverseTransformDirection(dir), _moving.min - grow, _moving.max + grow, out float along)) return;
            float far = along * unit;
            float pace = Mathf.Max(0.01f, prefab.Speed * Mathf.Max(0.001f, scale) * Mathf.Max(0.01f, speedScale));
            float seconds = far / pace;
            if (seconds > MostPokeSeconds || Blocked(from, dir, far, owner)) return;
            _pokeAt[_pokes] = Time.time + seconds;
            _pokeWay[_pokes] = dir;
            _pokes++;
        }

        /// <summary>Something solid lies on the line before the cloth (not the cloth's own collider, not the shooter).</summary>
        private bool Blocked(Vector3 from, Vector3 dir, float far, Transform owner)
        {
            if (far <= 1e-4f) return false;
            int n = Physics.RaycastNonAlloc(from, dir, Hits, far, Layers.Seen(Physics.DefaultRaycastLayers), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Transform hit = Hits[i].transform;
                if (hit == null || hit.IsChildOf(transform) || (owner != null && hit.IsChildOf(owner))) continue;
                return true;
            }
            return false;
        }

        /// <summary>Pokes whose shot has got there: Magica's AddForce along the shot's flight (VelocityAdd: heavier near the
        /// held points, so the far end moves most).</summary>
        private void Pokes()
        {
            float now = Time.time;
            float push = ClothSettings.Load().For(kind).shotPoke;
            for (int i = _pokes - 1; i >= 0; i--)
            {
                if (now < _pokeAt[i]) continue;
                if (_ready && _cloth != null && push > 0f) _cloth.AddForce(_pokeWay[i], push, MC.ClothForceMode.VelocityAdd);
                _pokes--;
                _pokeAt[i] = _pokeAt[_pokes];
                _pokeWay[i] = _pokeWay[_pokes];
            }
        }
    }
}
