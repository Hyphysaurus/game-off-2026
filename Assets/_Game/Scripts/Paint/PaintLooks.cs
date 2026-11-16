using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The paint decals' materials. A URP decal takes its picture and its colour from its material, so there is one
    /// material for each picture in each colour, made the first time that pair is needed and shared by every decal that
    /// wears it (at most pictures x colours: 30 x 6 paint and 4 x 7 shell with the built-in lists). Decals that share
    /// a material are drawn together.
    /// </summary>
    public sealed class PaintLooks
    {
        private static readonly int BaseMapId = Shader.PropertyToID("Base_Map");
        private static readonly int NormalMapId = Shader.PropertyToID("Normal_Map");
        private static readonly int NormalBlendId = Shader.PropertyToID("Normal_Blend");
        private static readonly int TintId = Shader.PropertyToID("_PaintTint");
        private static readonly int GlossId = Shader.PropertyToID("_PaintGloss");
        private static readonly int DrawOrderId = Shader.PropertyToID("_DrawOrder");

        private readonly PaintSettings _settings;
        private readonly Dictionary<(int picture, Color32 tint, int order), Material> _made = new Dictionary<(int, Color32, int), Material>();

        public PaintLooks(PaintSettings settings) { _settings = settings; }

        /// <summary>How many materials have been made so far.</summary>
        public int Count => _made.Count;

        /// <summary>The material for a picture in a colour; null when the picture or the base material is missing.
        /// <paramref name="order"/>: decals with a higher number are drawn over those with a lower one.</summary>
        public Material For(in PaintSettings.Look look, Color tint, int order = 0)
        {
            if (look.albedo == null || _settings.decalMaterial == null) return null;
            Color32 tint32 = tint;
            var key = (look.albedo.GetInstanceID(), tint32, order);
            if (_made.TryGetValue(key, out Material made) && made != null) return made;
            made = new Material(_settings.decalMaterial) { name = look.albedo.name + " paint", enableInstancing = true };
            made.SetTexture(BaseMapId, look.albedo);
            if (look.normal != null) made.SetTexture(NormalMapId, look.normal);
            made.SetFloat(NormalBlendId, look.normal != null ? _settings.normalBlend : 0f);   // flat paint leaves the surface's own bumps
            made.SetColor(TintId, new Color(tint.r, tint.g, tint.b, 1f));
            made.SetFloat(GlossId, _settings.wetGloss);
            made.SetFloat(DrawOrderId, order);
            _made[key] = made;
            return made;
        }

        /// <summary>Destroys every material made (the keeper is going).</summary>
        public void Dispose()
        {
            foreach (var m in _made.Values) if (m != null) Object.Destroy(m);
            _made.Clear();
        }
    }
}
