using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A thing that has lost its colour. Paint brings it back a hit at a time; a smudge takes it away again. Put this on
    /// a prop (the model on it or under it): at start it swaps each plain URP Lit material on the model for the greyable
    /// shader (glass and glowing parts keep their own) and measures the model. How grey it is and its glow are set per
    /// renderer, so every prop that used a material shares one greyable copy of it.
    /// Ported from the Godot test room's colourable.gd.
    /// </summary>
    public sealed class Colourable : MonoBehaviour, IResettable
    {
        private const string LitShader = "Universal Render Pipeline/Lit";
        private static readonly int ColourId = Shader.PropertyToID("_PaintColour");
        private static readonly int FlashId = Shader.PropertyToID("_PaintFlash");
        private static readonly int GreyId = Shader.PropertyToID("_PaintGrey");
        private static readonly int GreyPullId = Shader.PropertyToID("_PaintGreyPull");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");

        // One greyable material for each original material, shared by every prop that used it.
        private static readonly Dictionary<Material, Material> Swapped = new Dictionary<Material, Material>();
        private static readonly List<Colourable> Everything = new List<Colourable>();
        private static readonly List<Material> Wearing = new List<Material>();

        [SerializeField, Tooltip("Paint hits needed to bring the colour all the way back. 0 works it out from the size.")] private int hitsToRestore;
        [SerializeField, Range(0f, 1f)] private float startColour;

        private readonly List<Renderer> _greyed = new List<Renderer>();
        private PaintSettings _settings;
        private MaterialPropertyBlock _block;
        private float _flash, _flashFrom, _flashAt;

        /// <summary>Every Colourable switched on in the scene.</summary>
        public static IReadOnlyList<Colourable> All => Everything;
        /// <summary>0 is grey, 1 is fully its own colours.</summary>
        public float Colour { get; private set; }
        /// <summary>Half its width on the ground and its height, measured from the model.</summary>
        public float Radius { get; private set; } = 1f;
        public float Height { get; private set; } = 1f;
        public int HitsToRestore => hitsToRestore;
        /// <summary>Renderers whose materials were swapped for the greyable ones.</summary>
        public int GreyedRenderers => _greyed.Count;
        /// <summary>Its colour changed (paint, a smudge, a reset).</summary>
        public event Action<Colourable> Changed;
        /// <summary>Paint landed on it and brought some colour back; true if that was the last of it.</summary>
        public event Action<Colourable, bool> Painted;

        /// <summary>The grey thing a collider belongs to, if any.</summary>
        public static Colourable Find(Collider collider) => collider != null ? collider.GetComponentInParent<Colourable>() : null;

        private void Awake()
        {
            _settings = PaintSettings.Load();
            _block = new MaterialPropertyBlock();
            Shader.SetGlobalColor(GreyId, _settings.grey);
            Shader.SetGlobalFloat(GreyPullId, _settings.greyPull);
            Colour = startColour;
            TakeOver();
            hitsToRestore = Mathf.Max(hitsToRestore, 1);
            Show();
        }

        private void OnEnable() => Everything.Add(this);
        private void OnDisable() => Everything.Remove(this);

        /// <summary>Called by a paint ball. Returns true if this hit brought the last of the colour back.</summary>
        public bool TakePaint(float amount = 1f)
        {
            if (Colour >= 1f) return false;
            Colour = Mathf.Min(Colour + amount / hitsToRestore, 1f);
            bool full = Colour >= 1f;
            Pulse(full ? _settings.flash.y : _settings.flash.x);
            Changed?.Invoke(this);
            Painted?.Invoke(this, full);
            return full;
        }

        /// <summary>Takes colour away (a soldier rubbing at it).</summary>
        public void Smudge(float amount)
        {
            if (Colour <= 0f) return;
            Colour = Mathf.Max(Colour - amount, 0f);
            Show();
            Changed?.Invoke(this);
        }

        public void SetColour(float value)
        {
            Colour = Mathf.Clamp01(value);
            Show();
            Changed?.Invoke(this);
        }

        /// <summary>The reset key: back to the colour it started with.</summary>
        public void ResetState()
        {
            _flash = 0f;
            SetColour(startColour);
        }

        private void Pulse(float strength)
        {
            _flash = _flashFrom = strength;
            _flashAt = Time.time;
            Show();
        }

        private void Update()
        {
            if (_flash <= 0f) return;
            float gone = _settings.flashSeconds > 0f ? (Time.time - _flashAt) / _settings.flashSeconds : 1f;
            _flash = Mathf.Lerp(_flashFrom, 0f, Mathf.Clamp01(gone));
            Show();
        }

        private void Show()
        {
            _block.SetFloat(ColourId, Colour);
            _block.SetFloat(FlashId, _flash * _settings.flashGain);
            foreach (Renderer drawn in _greyed) if (drawn != null) drawn.SetPropertyBlock(_block);
        }

        /// <summary>Swaps each solid Lit material on the model for the greyable one, and measures the model.</summary>
        private void TakeOver()
        {
            bool any = false; Bounds box = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer drawn in GetComponentsInChildren<Renderer>(true))
            {
                if (!(drawn is MeshRenderer) && !(drawn is SkinnedMeshRenderer)) continue;
                if (!any) { box = drawn.bounds; any = true; } else box.Encapsulate(drawn.bounds);
                if (_settings.greyableShader == null) continue;
                drawn.GetSharedMaterials(Wearing);
                bool swappedAny = false;
                for (int i = 0; i < Wearing.Count; i++)
                {
                    Material grey = GreyOf(Wearing[i]);
                    if (grey == null) continue;
                    Wearing[i] = grey; swappedAny = true;
                }
                if (!swappedAny) continue;
                drawn.SetSharedMaterials(Wearing);
                _greyed.Add(drawn);
            }
            Wearing.Clear();
            if (!any) return;
            Radius = Mathf.Max(box.size.x, box.size.z) * 0.5f;
            Height = box.max.y - transform.position.y;
            if (hitsToRestore <= 0)
                hitsToRestore = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(box.size.x, box.size.y, box.size.z) / Mathf.Max(_settings.sizePerHit, 1e-4f)), _settings.hitsRange.x, _settings.hitsRange.y);
        }

        /// <summary>The greyable copy of a material; null for one that keeps its own look (not plain Lit, see-through,
        /// or glowing).</summary>
        private Material GreyOf(Material original)
        {
            if (original == null || original.shader == null) return null;
            if (original.shader == _settings.greyableShader) return original;   // already swapped (a second Colourable above it)
            if (original.shader.name != LitShader) return null;
            if (original.HasProperty(SurfaceId) && original.GetFloat(SurfaceId) > 0.5f) return null;   // glass
            if (original.IsKeywordEnabled("_EMISSION")) return null;                                   // glowing parts
            if (Swapped.TryGetValue(original, out Material grey) && grey != null) return grey;
            grey = new Material(original) { shader = _settings.greyableShader, name = original.name + " (greyable)" };
            Swapped[original] = grey;
            return grey;
        }
    }
}
