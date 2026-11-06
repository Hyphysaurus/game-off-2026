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

        /// <summary>Every buildable that has woken up, shown or hidden. Destroyed ones leave the list.</summary>
        public static IReadOnlyList<Buildable> All => Registry;

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
        private static void ResetRegistry() => Registry.Clear();

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

        /// <summary>Holds loose props still while they are dragged; returns what each body was, for Release.</summary>
        public bool[] Hold()
        {
            _bodies ??= GetComponentsInChildren<Rigidbody>(true);
            var was = new bool[_bodies.Length];
            for (int i = 0; i < _bodies.Length; i++) { if (_bodies[i] == null) continue; was[i] = _bodies[i].isKinematic; _bodies[i].isKinematic = true; }
            return was;
        }

        public void Release(bool[] was)
        {
            if (_bodies == null || was == null) return;
            for (int i = 0; i < _bodies.Length && i < was.Length; i++) if (_bodies[i] != null) _bodies[i].isKinematic = was[i];
            StopBodies();
        }
    }
}
