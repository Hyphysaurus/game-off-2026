using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A thing lit up as its LightTarget fills (D-0149): its renderers' glow (emission) goes from none to Glow as the fill
    /// goes from 0 to 1, so the player sees it charging. Wire the LightTarget's On Fill to Glow On. The material must have
    /// emission on (URP Lit: Emission ticked).
    /// It can also glint by itself now and then (D-0245, the dart gun under the bed): a slow rise and fall every Idle Every
    /// seconds, so it can be found in the dark with no beam on it; a beam's fill shows over it.
    /// </summary>
    public sealed class LightGlow : MonoBehaviour
    {
        [SerializeField, Tooltip("What glows. Every renderer under this object if empty.")] private Renderer[] parts;
        [SerializeField, ColorUsage(false, true), Tooltip("The glow when full (over 1 blooms).")] private Color glow = new Color(1.5f, 1.3f, 0.9f);
        [Header("A slow glint with no light on it (D-0245)")]
        [SerializeField, Range(0f, 1f), Tooltip("Every Idle Every seconds it glints this share of its full glow by itself, so it can be found in the dark (the dart gun under the bed). 0 = never: it glows only as its LightTarget fills.")] private float idlePeak;
        [SerializeField, Min(0.5f), Tooltip("Seconds from one slow glint to the next.")] private float idleEvery = 2.2f;
        [SerializeField, Min(0.05f), Tooltip("Seconds one slow glint lasts: it rises and falls.")] private float idleSeconds = 0.7f;
        [SerializeField, Tooltip("Switch emission on in its parts' materials as the game starts, in case an import switched it off (D-0245: the dart gun's glint material once lost it, and then no beam could light the gun).")] private bool keepEmissionOn;

        private MaterialPropertyBlock _block;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private float _fill, _shown = -1f;

        private void Awake()
        {
            if (parts == null || parts.Length == 0) parts = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
            if (keepEmissionOn)
                foreach (Renderer r in parts)
                {
                    if (r == null) continue;
                    foreach (Material m in r.sharedMaterials)
                        if (m != null && !m.IsKeywordEnabled("_EMISSION")) m.EnableKeyword("_EMISSION");
                }
            GlowOn(0f);
        }

        /// <summary>This much of the full glow, 0 to 1 (a LightTarget's On Fill).</summary>
        public void GlowOn(float amount)
        {
            _fill = Mathf.Clamp01(amount);
            Show(Mathf.Max(_fill, Idle()));
        }

        private void Update()
        {
            if (idlePeak <= 0f) return;
            float amount = Mathf.Max(_fill, Idle());
            if (amount != _shown) Show(amount);
        }

        /// <summary>The slow glint now: up and down once in the first Idle Seconds of every Idle Every.</summary>
        private float Idle()
        {
            if (idlePeak <= 0f) return 0f;
            float t = Mathf.Repeat(Time.time, idleEvery);
            return t < idleSeconds ? idlePeak * Mathf.Sin(t / idleSeconds * Mathf.PI) : 0f;
        }

        private void Show(float amount)
        {
            _shown = amount;
            foreach (Renderer r in parts)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(Emission, glow * amount);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
