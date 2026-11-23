using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A thing lit up as its LightTarget fills (D-0149): its renderers' glow (emission) goes from none to Glow as the fill
    /// goes from 0 to 1, so the player sees it charging. Wire the LightTarget's On Fill to Glow On. The material must have
    /// emission on (URP Lit: Emission ticked).
    /// </summary>
    public sealed class LightGlow : MonoBehaviour
    {
        [SerializeField, Tooltip("What glows. Every renderer under this object if empty.")] private Renderer[] parts;
        [SerializeField, ColorUsage(false, true), Tooltip("The glow when full (over 1 blooms).")] private Color glow = new Color(1.5f, 1.3f, 0.9f);

        private MaterialPropertyBlock _block;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (parts == null || parts.Length == 0) parts = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            GlowOn(0f);
        }

        /// <summary>This much of the full glow, 0 to 1 (a LightTarget's On Fill).</summary>
        public void GlowOn(float amount)
        {
            foreach (Renderer r in parts)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(Emission, glow * Mathf.Clamp01(amount));
                r.SetPropertyBlock(_block);
            }
        }
    }
}
