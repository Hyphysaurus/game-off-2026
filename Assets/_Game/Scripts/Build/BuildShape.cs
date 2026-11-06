using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A generated block for room topography: box, ramp, stairs, pillar or wall, sized in world units, with its own tint.
    /// The mesh is made on enable (also in the editor, so a baked layout shows its shapes) and is never saved: the
    /// numbers here are the whole description. A MeshCollider on the same object gets the same mesh.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BuildShape : MonoBehaviour
    {
        public static readonly Vector3 MinSize = new Vector3(0.25f, 0.1f, 0.25f);
        public const int MinSteps = 2, MaxSteps = 40;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private ShapeKind kind = ShapeKind.Box;
        [SerializeField, Tooltip("Width, height, depth in world units.")] private Vector3 size = new Vector3(10f, 2f, 10f);
        [SerializeField, Tooltip("Stairs only.")] private int steps = 6;
        [SerializeField, Tooltip("Tint over the shape material.")] private Color color = Color.white;

        private Mesh _mesh;
        private MaterialPropertyBlock _block;

        public ShapeKind Kind => kind;
        public Vector3 Size => size;
        public int Steps => steps;
        public Color Color => color;

        private void OnEnable() { Rebuild(); }

        private void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
        }

        public void Set(ShapeKind newKind, Vector3 newSize, int newSteps, Color newColor)
        {
            kind = newKind;
            size = Vector3.Max(MinSize, newSize);
            steps = Mathf.Clamp(newSteps, MinSteps, MaxSteps);
            color = newColor;
            Rebuild();
        }

        public void Rebuild()
        {
            if (_mesh == null) _mesh = new Mesh { name = "BuildShape_" + kind, hideFlags = HideFlags.DontSave };
            ShapeMesh.Fill(_mesh, kind, Vector3.Max(MinSize, size), Mathf.Clamp(steps, MinSteps, MaxSteps));
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            if (TryGetComponent(out MeshCollider meshCollider)) { meshCollider.sharedMesh = null; meshCollider.sharedMesh = _mesh; }
            ApplyColor(GetComponent<MeshRenderer>(), color, _block ??= new MaterialPropertyBlock());
        }

        /// <summary>Tints a renderer through a property block, so every shape shares one material asset.</summary>
        public static void ApplyColor(Renderer target, Color tint, MaterialPropertyBlock scratch = null)
        {
            if (target == null) return;
            var block = scratch ?? new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, tint);
            target.SetPropertyBlock(block);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Rebuilding inside OnValidate touches the collider, which Unity refuses there: do it right after.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
        }
#endif
    }
}
