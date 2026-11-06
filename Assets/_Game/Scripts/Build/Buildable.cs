using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>Everything undo and the layout files need to put a buildable back: pose, scale, shown, and shape numbers.</summary>
    public struct BuildState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool active;
        public bool hasShape;
        public Vector3 shapeSize;
        public int shapeSteps;
        public Color shapeColor;

        public bool SameAs(in BuildState o)
        {
            return active == o.active
                && (position - o.position).sqrMagnitude < 1e-8f
                && Quaternion.Angle(rotation, o.rotation) < 0.01f
                && (scale - o.scale).sqrMagnitude < 1e-8f
                && (!hasShape || ((shapeSize - o.shapeSize).sqrMagnitude < 1e-8f && shapeSteps == o.shapeSteps && shapeColor == o.shapeColor));
        }
    }

    /// <summary>
    /// Marks a thing Build Mode may select, move and save. Three kinds: placed from the catalogue (has a catalogue id),
    /// a copy of a scene prop (remembers which one), or a prop that belongs to the scene (found again by its path).
    /// Put it on scene props by hand, or let BuildMode add it to every child of its buildable roots. Deleting only hides
    /// the thing, so undo can bring it back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Buildable : MonoBehaviour
    {
        [SerializeField, Tooltip("Catalogue id this was placed from. Leave empty on props that belong to the scene.")]
        private string catalogId;
        [SerializeField, HideInInspector] private string cloneOf;

        private static readonly List<Buildable> Registry = new List<Buildable>();
        private Rigidbody[] _bodies;
        private bool[] _heldKinematic;
        private CollisionDetectionMode[] _heldModes;

        /// <summary>Every buildable that has woken up, shown or hidden. Destroyed ones leave the list.</summary>
        public static IReadOnlyList<Buildable> All => Registry;

        /// <summary>Raised when something other than Build Mode swaps a piece for another (a garage body swap). Build Mode
        /// drops its undo history then: its steps would bring the old piece back.</summary>
        public static event System.Action Replaced;
        public static void ReportReplaced() => Replaced?.Invoke();

        public string CatalogId => catalogId;
        /// <summary>For a copy of a scene prop: the original's scene path.</summary>
        public string CloneOf => cloneOf;
        /// <summary>For a prop that belongs to the scene: its path, taken when it woke up.</summary>
        public string SceneKey { get; private set; }
        public bool IsSceneOriginal => string.IsNullOrEmpty(catalogId) && string.IsNullOrEmpty(cloneOf);
        public BuildShape Shape { get; private set; }
        /// <summary>How a scene prop stood when the scene started; loading a layout resets to it first.</summary>
        public BuildState Original { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { Registry.Clear(); Replaced = null; }

        private void Awake()
        {
            Shape = GetComponent<BuildShape>();
            if (IsSceneOriginal) SceneKey = ScenePath.Of(transform);
            Original = Capture();
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDestroy() { Registry.Remove(this); }

        /// <summary>Called right after spawning from the catalogue.</summary>
        public void InitPlaced(string id) { catalogId = id; cloneOf = null; SceneKey = null; }

        /// <summary>Called right after copying: a copy of a placed thing keeps its id, a copy of a scene prop remembers the prop.</summary>
        public void InitCopyOf(Buildable source)
        {
            catalogId = source.catalogId;
            cloneOf = source.IsSceneOriginal ? source.SceneKey : source.cloneOf;
            SceneKey = null;
        }

        public BuildState Capture()
        {
            var t = transform;
            var s = new BuildState { position = t.position, rotation = t.rotation, scale = t.localScale, active = gameObject.activeSelf };
            if (Shape != null) { s.hasShape = true; s.shapeSize = Shape.Size; s.shapeSteps = Shape.Steps; s.shapeColor = Shape.Color; }
            return s;
        }

        public void Apply(in BuildState s)
        {
            transform.SetPositionAndRotation(s.position, s.rotation);
            transform.localScale = s.scale;
            if (s.hasShape && Shape != null) Shape.Set(Shape.Kind, s.shapeSize, s.shapeSteps, s.shapeColor);
            if (gameObject.activeSelf != s.active) gameObject.SetActive(s.active);
            StopBodies();
            TellMoved();
        }

        /// <summary>Tells every IBuildMoved under this piece that Build Mode just gave it a new pose or scale. A hidden piece
        /// is skipped: it is told again when it is shown.</summary>
        public void TellMoved()
        {
            if (!gameObject.activeInHierarchy) return;
            foreach (var m in GetComponentsInChildren<IBuildMoved>())
            {
                try { m.OnBuildMoved(); }
                catch (System.Exception e) { Debug.LogException(e, this); }   // one piece's hook must not stop an edit or a load halfway
            }
        }

        /// <summary>Loose props keep no speed from before a move.</summary>
        public void StopBodies()
        {
            _bodies ??= GetComponentsInChildren<Rigidbody>(true);
            foreach (var b in _bodies)
            {
                if (b == null || b.isKinematic) continue;
                b.linearVelocity = Vector3.zero;
                b.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }

        /// <summary>Holds loose props still while they are dragged: kinematic, with continuous collision switched to
        /// speculative first (the only kind a kinematic body takes). Release puts each body back as it was.</summary>
        public void Hold()
        {
            _bodies ??= GetComponentsInChildren<Rigidbody>(true);
            _heldKinematic = new bool[_bodies.Length];
            _heldModes = new CollisionDetectionMode[_bodies.Length];
            for (int i = 0; i < _bodies.Length; i++)
            {
                var b = _bodies[i];
                if (b == null) continue;
                _heldKinematic[i] = b.isKinematic;
                _heldModes[i] = b.collisionDetectionMode;
                if (b.isKinematic) continue;   // already held by its owner (a parked car): left alone
                if (b.collisionDetectionMode == CollisionDetectionMode.Continuous || b.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
                    b.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                b.isKinematic = true;
            }
        }

        public void Release()
        {
            if (_bodies == null || _heldKinematic == null) return;
            for (int i = 0; i < _bodies.Length && i < _heldKinematic.Length; i++)
            {
                var b = _bodies[i];
                if (b == null || _heldKinematic[i]) continue;
                b.isKinematic = false;                       // moving again first, then its own collision mode back
                b.collisionDetectionMode = _heldModes[i];
            }
            _heldKinematic = null; _heldModes = null;
            StopBodies();
        }
    }
}
