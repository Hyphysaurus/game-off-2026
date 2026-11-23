using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Something only the flashlight shows (D-0149): a hidden mark (a tape arrow, a note) that fades in as its LightTarget
    /// fills and away as it drains. Put a LightTarget on the same object (Fill Seconds short, Drain Seconds the fade back)
    /// and wire its On Fill to Show. Its renderers' colour alpha follows the fill, so their material must draw see-through.
    /// </summary>
    public sealed class LightReveal : MonoBehaviour
    {
        [SerializeField, Tooltip("What fades in. Every renderer under this object if empty.")] private Renderer[] parts;
        [SerializeField, Tooltip("The material's colour property.")] private string colourProperty = "_BaseColor";
        [SerializeField, Range(0f, 1f), Tooltip("Below this fill it is not drawn at all.")] private float showFrom = 0.02f;

        private MaterialPropertyBlock _block;
        private Color[] _colours;
        private int _id;

        private void Awake()
        {
            if (parts == null || parts.Length == 0) parts = GetComponentsInChildren<Renderer>(true);
            _id = Shader.PropertyToID(colourProperty);
            _block = new MaterialPropertyBlock();
            _colours = new Color[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                _colours[i] = parts[i] != null && parts[i].sharedMaterial != null && parts[i].sharedMaterial.HasProperty(_id) ? parts[i].sharedMaterial.GetColor(_id) : Color.white;
            Show(0f);
        }

        /// <summary>Shown this much, 0 to 1 (a LightTarget's On Fill).</summary>
        public void Show(float amount)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                Renderer r = parts[i];
                if (r == null) continue;
                r.enabled = amount > showFrom;
                Color c = _colours[i]; c.a *= amount;
                r.GetPropertyBlock(_block);
                _block.SetColor(_id, c);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
