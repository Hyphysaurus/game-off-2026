using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// "This one matters" (D-0046): a gold glint that floats over a puzzle piece (a switch, a flashlight, the safe, the
    /// ruler) so it reads from across the room, apart from the clutter. It breathes and bobs, and fades away once the
    /// piece has done its job: a switch pressed, a flashlight on, a carryable picked up, a push switch pushed. Only
    /// puzzle pieces get one (CourseBuilder.PuzzleCues), so the glint keeps its meaning. The glint is made at start
    /// from the material the builder sets (the spark texture: his drawing replaces it).
    /// </summary>
    public sealed class Highlight : MonoBehaviour
    {
        [SerializeField, Tooltip("The glint's look: an additive particle material.")] private Material material;
        [SerializeField] private Color color = new Color(1f, 0.82f, 0.35f, 1f);
        [SerializeField, Tooltip("Glint size from the piece's size, clamped (world units).")] private float minSize = 5f, maxSize = 14f;
        [SerializeField, Tooltip("Bob height, as a share of the glint's size.")] private float bob = 0.15f;

        private Switch _switch; private PushSwitch _push; private Carryable _carry; private Flashlight _light;
        private Transform _root; private Vector3 _rootLocal;
        private float _size, _fade = 1f;
        private bool _done;

        private void Awake()
        {
            _switch = GetComponentInChildren<Switch>(true);
            _push = GetComponentInChildren<PushSwitch>(true);
            _carry = GetComponentInChildren<Carryable>(true);
            _light = GetComponentInChildren<Flashlight>(true);
            MakeGlint();
        }

        private bool Solved =>
            (_switch != null && _switch.Used) || (_push != null && _push.Pressed) ||
            (_carry != null && _carry.IsCarried) || (_light != null && _light.IsOn);

        private void Update()
        {
            if (_root == null) return;
            if (!_done && Solved) _done = true;
            if (_done)
            {
                _fade -= Time.deltaTime / 0.35f;
                if (_fade <= 0f) { _root.gameObject.SetActive(false); enabled = false; return; }
            }
            // Bob in world units, whatever the piece's own scale.
            float y = Mathf.Sin(Time.time * 2.1f) * bob * _size;
            _root.position = transform.TransformPoint(_rootLocal) + Vector3.up * y;
            _root.localScale = Vector3.one * Mathf.Max(0f, _fade);
        }

        private void MakeGlint()
        {
            if (material == null) return;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            _size = Mathf.Clamp(Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.6f, minSize, maxSize);

            _root = new GameObject("GlintRoot").transform;
            _root.SetParent(transform, true);
            _root.position = new Vector3(b.center.x, b.max.y + _size * 0.7f, b.center.z);
            _rootLocal = transform.InverseTransformPoint(_root.position);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Glint";
            if (quad.TryGetComponent(out Collider col)) DestroyImmediate(col);   // a picture, never something to stand on
            quad.layer = 2;   // Ignore Raycast
            quad.transform.SetParent(_root, false);
            // World size whatever the parent's scale: undo the lossy scale once, then let the root's fade scale it.
            Vector3 lossy = _root.lossyScale;
            quad.transform.localScale = new Vector3(_size / Mathf.Max(1e-4f, lossy.x), _size / Mathf.Max(1e-4f, lossy.y), 1f / Mathf.Max(1e-4f, lossy.z));
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            mr.SetPropertyBlock(block);
            quad.AddComponent<Billboard>();
        }
    }
}
