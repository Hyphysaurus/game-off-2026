using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// Hides the player's body while the third-person camera is inside it (backed against a wall the camera is pushed in
    /// to the back of the head). The camera says when (ShoulderCamera.InsideBody, which has the slack that stops a
    /// flicker at the limit); this fades the body and the weapon in its hand out over CameraSettings.bodyFadeSeconds,
    /// leaves only their shadows once gone, and brings them back the same way. A prop carried in the arms is not part of
    /// the player and stays drawn. First person and the car seat hide the body their own way and are left alone.
    /// </summary>
    public sealed class BodyFade : MonoBehaviour
    {
        private static readonly int Surface = Shader.PropertyToID("_Surface");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        [SerializeField, Tooltip("The camera that says when it is inside the body. Found in the scene if empty.")]
        private ShoulderCamera shoulderCamera;

        /// <summary>One drawn part while it is not fully shown: how it was, and the see-through copies of its materials.</summary>
        private sealed class Part
        {
            public Renderer renderer;
            public ShadowCastingMode shadows;
            public Material[] shared, faded;
            public Color[] colours;
        }

        private readonly List<Part> _parts = new List<Part>();
        private readonly List<Renderer> _found = new List<Renderer>();
        private float _shown = 1f;

        /// <summary>1 = the body is drawn as usual, 0 = hidden (its shadow stays).</summary>
        public float Shown => _shown;
        /// <summary>True once the body is fully hidden.</summary>
        public bool Hidden => _shown <= 0f;

        private void Start()
        {
            if (shoulderCamera == null) shoulderCamera = PlayerSlot.RigFor(this);
        }

        private void OnDisable() { _shown = 1f; Restore(); }

        private void LateUpdate()
        {
            bool inside = shoulderCamera != null && shoulderCamera.isActiveAndEnabled && shoulderCamera.InsideBody;
            float want = inside ? 0f : 1f;
            if (_shown == want && want >= 1f) return;
            float seconds = shoulderCamera != null && shoulderCamera.Settings != null ? shoulderCamera.Settings.bodyFadeSeconds : 0f;
            _shown = seconds <= 0f ? want : Mathf.MoveTowards(_shown, want, Time.unscaledDeltaTime / seconds);
            if (_shown >= 1f) { Restore(); return; }
            Apply();
        }

        /// <summary>Every frame while not fully shown, so a weapon drawn meanwhile is hidden too.</summary>
        private void Apply()
        {
            GetComponentsInChildren(false, _found);
            foreach (Renderer r in _found)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Part part = _parts.Find(p => p.renderer == r);
                if (part == null) _parts.Add(part = Take(r));
                // Gone: only the shadow is drawn (a part that cast none stays as it is, invisible through its materials).
                bool gone = _shown <= 0f;
                r.shadowCastingMode = gone && part.shadows != ShadowCastingMode.Off ? ShadowCastingMode.ShadowsOnly : part.shadows;
                if (part.faded == null) { r.forceRenderingOff = gone && part.shadows == ShadowCastingMode.Off; continue; }
                for (int i = 0; i < part.faded.Length; i++)
                {
                    if (part.faded[i] == null) continue;
                    Color c = part.colours[i]; c.a *= _shown;
                    part.faded[i].SetColor(BaseColor, c);
                }
            }
        }

        /// <summary>Remembers how a part is drawn and gives it see-through copies of its materials (URP Lit and its
        /// kin: anything with _Surface and _BaseColor). A part with other shaders is not faded: it goes at the end.</summary>
        private static Part Take(Renderer r)
        {
            var part = new Part { renderer = r, shadows = r.shadowCastingMode, shared = r.sharedMaterials };
            foreach (Material m in part.shared)
                if (m == null || !m.HasProperty(Surface) || !m.HasProperty(BaseColor)) return part;
            part.faded = new Material[part.shared.Length];
            part.colours = new Color[part.shared.Length];
            for (int i = 0; i < part.shared.Length; i++)
            {
                var copy = new Material(part.shared[i]);
                part.colours[i] = copy.GetColor(BaseColor);
                copy.SetFloat(Surface, 1f);
                copy.SetOverrideTag("RenderType", "Transparent");
                copy.SetFloat(SrcBlend, (float)BlendMode.SrcAlpha);
                copy.SetFloat(DstBlend, (float)BlendMode.OneMinusSrcAlpha);
                copy.SetFloat(ZWrite, 1f);   // the body keeps writing depth, so its own back faces do not show through
                copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                copy.renderQueue = (int)RenderQueue.Transparent;
                part.faded[i] = copy;
            }
            r.sharedMaterials = part.faded;
            return part;
        }

        /// <summary>Fully shown again: every part gets its own materials and shadows back, the copies are thrown away.</summary>
        private void Restore()
        {
            foreach (Part part in _parts)
            {
                if (part.renderer != null)
                {
                    part.renderer.shadowCastingMode = part.shadows;
                    if (part.faded != null) part.renderer.sharedMaterials = part.shared;
                    else part.renderer.forceRenderingOff = false;
                }
                if (part.faded != null) foreach (Material m in part.faded) if (m != null) Destroy(m);
            }
            _parts.Clear();
        }
    }
}
