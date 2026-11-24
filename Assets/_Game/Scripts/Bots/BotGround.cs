using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ShiftJam
{
    /// <summary>
    /// The floor bots can walk in a scene (D-0188). It is a box: as the scene starts it maps the walkable floor inside
    /// the box from the scene's solid colliders (Unity's navigation mesh, built there and then: the whole Stadium takes
    /// about 60 ms). Nothing is baked or saved, so the map is right however the scene was built or changed since.
    /// A bot (BotBrain) asks for its paths over that map (Way) and for spots on it (OnFloor); in a scene with no
    /// BotGround there is no map, both say no, and a bot does what a soldier did before there were maps.
    /// Left out of the map, so it shows the still world only: triggers, the capsules things walk with, anything under
    /// a toy soldier or a player, anything that can be pushed (a Rigidbody that is not kinematic), and anything that
    /// belongs to another scene.
    /// The map is cut for one walker: paths keep Walker Radius away from walls, pass under nothing lower than Walker
    /// Height, climb steps up to Step Height and slopes up to Steepest Slope. Tops of boxes and stands that a walker
    /// cannot reach are on the map too, as islands: Way only answers yes when the whole way there can be walked.
    /// Drop one in any scene and size the box over the floor to map (the box ignores this object's turn and size).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotGround : MonoBehaviour
    {
        [Header("The box to map (world units, round this object's place)")]
        [SerializeField, Tooltip("The box's middle, from this object's place.")] private Vector3 centre = new Vector3(0f, 2f, 0f);
        [SerializeField, Tooltip("The box's size. Floor outside it is not mapped; keep it low so stands and roofs above the play floor stay out.")] private Vector3 size = new Vector3(84f, 6f, 84f);
        [Header("The walker the map is cut for (a toy soldier at its own size)")]
        [SerializeField, Min(0.05f), Tooltip("Paths stay this far from walls and cover. A soldier's own capsule is 0.26 wide from its middle; a little more keeps it from scraping corners.")] private float walkerRadius = 0.35f;
        [SerializeField, Min(0.1f), Tooltip("Nothing lower than this is walked under.")] private float walkerHeight = 1.6f;
        [SerializeField, Min(0f), Tooltip("The highest step a path climbs (a soldier's own step is 0.3).")] private float stepHeight = 0.3f;
        [SerializeField, Range(0f, 60f), Tooltip("The steepest slope a path climbs, degrees.")] private float steepestSlope = 45f;
        [SerializeField, Tooltip("Which layers' colliders are the still world.")] private LayerMask solid = ~0;
        [Header("Seeing it")]
#pragma warning disable 0414   // read only by the editor's drawing below; kept in a build so the part is the same there
        [SerializeField, Tooltip("While this object is selected in Play, draw the mapped floor in the Scene view.")] private bool drawWhenSelected = true;
#pragma warning restore 0414

        /// <summary>How far from a spot the map is searched for the nearest floor when a path starts or ends there.</summary>
        public const float Reach = 1.5f;

        private static readonly List<NavMeshBuildSource> Sources = new List<NavMeshBuildSource>();
        private static readonly List<NavMeshBuildMarkup> NoMarkups = new List<NavMeshBuildMarkup>();
        private static BotGround _current;

        [System.NonSerialized] private NavMeshData _data;
        [System.NonSerialized] private NavMeshDataInstance _placed;
        // Not NonSerialized on purpose (D-0188): a script reload in Play keeps it, so OnEnable maps the floor again (the
        // reload's OnDisable took the map away, and Start does not run twice). Never saved: it is not a serialized field.
        private bool _started;
        [System.NonSerialized] private Mesh _shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetAll() { _current = null; Sources.Clear(); }   // the editor may enter Play without reloading scripts

        /// <summary>True while a scene's floor is mapped: a bot can ask for paths.</summary>
        public static bool Mapped => _current != null && _current._placed.valid;
        /// <summary>The mapped box in the world (an empty one with no map).</summary>
        public static Bounds Box => Mapped ? _current.WorldBox : new Bounds(Vector3.zero, Vector3.zero);
        /// <summary>True while this one's map is in use.</summary>
        public bool Ready => _placed.valid;
        /// <summary>How many triangles of floor the last build mapped.</summary>
        public int Triangles { get; private set; }
        /// <summary>Milliseconds the last build took.</summary>
        public float BuildMilliseconds { get; private set; }

        private Bounds WorldBox => new Bounds(transform.position + centre, size);

        private void Start() { _started = true; Build(); }   // after everything in the scene has woken
        private void OnEnable() { if (_started) Build(); }
        private void OnDisable() => Clear();

        /// <summary>
        /// Maps the floor inside the box now, in place of any map this one made before. It runs by itself as the scene
        /// starts; call it again after the still world changed (and out of Play, from an editor check). False when
        /// nothing walkable was found.
        /// </summary>
        public bool Build()
        {
            Clear();
            float began = Time.realtimeSinceStartup;
            Bounds box = WorldBox;
            Sources.Clear();
            NavMeshBuilder.CollectSources(box, solid, NavMeshCollectGeometry.PhysicsColliders, 0, NoMarkups, Sources);
            for (int i = Sources.Count - 1; i >= 0; i--)
                if (LeftOut(Sources[i].component as Collider)) Sources.RemoveAt(i);
            // The default walker's id: every path question (Way, OnFloor) asks with it. Only its sizes are ours.
            NavMeshBuildSettings walker = NavMesh.GetSettingsByID(0);
            walker.agentRadius = walkerRadius; walker.agentHeight = walkerHeight;
            walker.agentClimb = stepHeight; walker.agentSlope = steepestSlope;
            _data = NavMeshBuilder.BuildNavMeshData(walker, Sources, box, Vector3.zero, Quaternion.identity);
            Sources.Clear();
            if (_data == null) { Debug.LogWarning("BotGround: nothing could be mapped in " + name + "'s box.", this); return false; }
            _data.name = "BotGround (" + name + ")";
            _placed = NavMesh.AddNavMeshData(_data);
            if (!_placed.valid) { Clear(); Debug.LogWarning("BotGround: Unity would not take " + name + "'s map.", this); return false; }
            _placed.owner = this;
            _current = this;
            Triangles = NavMesh.CalculateTriangulation().indices.Length / 3;
            BuildMilliseconds = (Time.realtimeSinceStartup - began) * 1000f;
            if (Triangles == 0)
            {
                // An empty map is no map (D-0188): Mapped says no, and a bot does what a soldier did before there were maps.
                Clear();
                Debug.LogWarning("BotGround: no walkable floor in " + name + "'s box (are its colliders on the Solid layers, and is the box over the floor?).", this);
                return false;
            }
            if (Application.isPlaying)
            {
                string told = "mapped " + Triangles + " triangles of floor for the bots in " + BuildMilliseconds.ToString("F0") + " ms";
                Debug.Log("BotGround: " + told, this);
                Playtest.PlayLog.Tell("bot ground: " + told);
            }
            return true;
        }

        /// <summary>Takes this one's map away (bots then have none, unless another BotGround's is in use).</summary>
        public void Clear()
        {
            if (_placed.valid) _placed.Remove();
            _placed = default(NavMeshDataInstance);
            Drop(_data); _data = null;
            Drop(_shown); _shown = null;
            Triangles = 0;
            if (_current == this) _current = null;
        }

        private static void Drop(Object made)
        {
            if (made == null) return;
            if (Application.isPlaying) Destroy(made); else DestroyImmediate(made);
        }

        /// <summary>True for a collider that is not part of the still world (see the class's note).</summary>
        private bool LeftOut(Collider c)
        {
            if (c == null) return false;
            if (c.gameObject.scene != gameObject.scene) return true;
            if (c.isTrigger || c is CharacterController) return true;
            Rigidbody body = c.attachedRigidbody;
            if (body != null && !body.isKinematic) return true;
            return c.GetComponentInParent<ToySoldier>() != null || c.GetComponentInParent<PlayerMotor>() != null;
        }

        // ---------------------------------------------------------------- what a bot asks

        /// <summary>The spot on the mapped floor nearest <paramref name="at"/>, no further than <paramref name="within"/>
        /// from it. False with no map, or when no floor is that near.</summary>
        public static bool OnFloor(Vector3 at, float within, out Vector3 on)
        {
            on = at;
            if (!Mapped || !NavMesh.SamplePosition(at, out NavMeshHit hit, within, NavMesh.AllAreas)) return false;
            on = hit.position;
            return true;
        }

        /// <summary>
        /// The way over the mapped floor from <paramref name="from"/> to <paramref name="to"/> (each taken to the
        /// nearest floor within Reach), written into <paramref name="path"/> (make one NavMeshPath and keep it: no
        /// garbage). True only when the whole way can be walked; false with no map, an end off the map, or an end on
        /// an island that cannot be reached (the top of a box).
        /// </summary>
        public static bool Way(Vector3 from, Vector3 to, NavMeshPath path)
        {
            if (path == null || !OnFloor(from, Reach, out Vector3 a) || !OnFloor(to, Reach, out Vector3 b)) return false;
            return NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>True when a walker can go straight from <paramref name="from"/> to <paramref name="to"/> over the
        /// mapped floor without leaving it (no wall, cover or edge between them). Both must be on the floor.</summary>
        public static bool Straight(Vector3 from, Vector3 to)
        {
            if (!OnFloor(from, Reach, out Vector3 a) || !OnFloor(to, Reach, out Vector3 b)) return false;
            return !NavMesh.Raycast(a, b, out _, NavMesh.AllAreas);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Bounds box = WorldBox;
            Gizmos.DrawWireCube(box.center, box.size);
            if (!drawWhenSelected || !_placed.valid) return;
            if (_shown == null)
            {
                NavMeshTriangulation floor = NavMesh.CalculateTriangulation();
                if (floor.indices.Length == 0) return;
                _shown = new Mesh { name = "BotGround shown", hideFlags = HideFlags.HideAndDontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                _shown.vertices = floor.vertices; _shown.triangles = floor.indices; _shown.RecalculateNormals();
            }
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
            Gizmos.DrawMesh(_shown, Vector3.up * 0.05f);
            Gizmos.color = new Color(0.1f, 0.5f, 0.2f, 0.8f);
            Gizmos.DrawWireMesh(_shown, Vector3.up * 0.05f);
        }
#endif
    }
}
