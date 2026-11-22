using System.Collections;
using System.Collections.Generic;
using AmazingAssets.CurvedWorld;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Curved World beats (D-0102), one Little Planet bend (Y, ID 1) used two ways:
    /// - At rest it is pivoted at the window: the garden outside (its materials are Curved World copies, CourseBuilder
    ///   CurvedBeats) rounds away like a little planet seen from the sill. The garden is never walked on.
    /// - On a shift it jumps to the player and pulses: the Lit renderers round the player are swapped for Curved World
    ///   copies for the moment, the room bulges into a little planet and snaps back as the size changes, then the copies
    ///   are swapped out. The player cannot move during a shift, so nothing walked on is bent under the feet.
    /// Physics never bends: only what is drawn.
    /// </summary>
    [RequireComponent(typeof(CurvedWorldController))]
    public sealed class WorldBend : MonoBehaviour
    {
        public const string TypeKeyword = "CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y", IdKeyword = "CURVEDWORLD_BEND_ID_1", NormalKeyword = "CURVEDWORLD_NORMAL_TRANSFORMATION_ON";
        private static readonly int SettingsId = Shader.PropertyToID("_CurvedWorldBendSettings");

        [Header("At rest: the garden")]
        [SerializeField, Tooltip("Where the bend is pivoted at rest (the window).")] private Transform restPivot;
        [SerializeField, Tooltip("How round the garden is: higher = a smaller planet.")] private float restCurvature = 0.8f;
        [SerializeField, Tooltip("Flat this far round the pivot.")] private float restOffset = 30f;

        [Header("On a shift")]
        [SerializeField, Tooltip("How round the room goes at the pulse's peak, at toy size (scaled by the size: the drop grows with the room).")] private float pulseCurvature = 12f;
        [SerializeField, Tooltip("Seconds the pulse lasts.")] private float pulseSeconds = 0.7f;
        [SerializeField, Tooltip("Renderers this many player heights round the player bend with it.")] private float pulseReach = 8f;
        [SerializeField, Tooltip("A Curved World Lit material (any garden one): the template the shift's copies are made from.")] private Material template;

        private CurvedWorldController _bend;
        private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        private readonly List<(Renderer r, Material[] own)> _swapped = new List<(Renderer, Material[])>();
        private Coroutine _pulse;

        private void Awake()
        {
            _bend = GetComponent<CurvedWorldController>();
            Rest();
        }

        private void OnEnable() => GameEvents.ShiftStarted += OnShiftStarted;
        private void OnDisable() { GameEvents.ShiftStarted -= OnShiftStarted; SwapBack(); Rest(); }

        private void Rest()
        {
            if (_bend == null) return;
            _bend.bendPivotPoint = restPivot;
            if (restPivot != null) _bend.bendPivotPointPosition = restPivot.position;
            _bend.bendCurvatureSize = restCurvature;
            _bend.bendCurvatureOffset = restOffset;
        }

        private void OnShiftStarted(Shifter who, ShiftSize size)
        {
            if (who == null || !(who.CompareTag("Player") || who.name == "Player")) return;
            if (_pulse != null) { StopCoroutine(_pulse); SwapBack(); }
            _pulse = StartCoroutine(Pulse(who));
        }

        private IEnumerator Pulse(Shifter who)
        {
            float height = Mathf.Max(0.1f, who.Scale);
            SwapIn(who.transform, pulseReach * 3f * height);
            _bend.bendPivotPoint = null;
            _bend.bendCurvatureOffset = 0f;
            // the drop goes with distance squared: the same look at any size wants the strength over the size
            float peak = pulseCurvature / height;
            for (float t = 0f; t < pulseSeconds; t += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / pulseSeconds));
                _bend.bendPivotPointPosition = who.transform.position;
                _bend.bendCurvatureSize = peak * k * k;
                yield return null;
            }
            SwapBack();
            Rest();
            _pulse = null;
        }

        /// <summary>Every renderer within <paramref name="reach"/> of <paramref name="centre"/> drawn only with URP Lit
        /// materials gets Curved World copies of them (made once each, kept).</summary>
        private void SwapIn(Transform centre, float reach)
        {
            if (template == null) return;
            Vector3 c = centre.position;
            foreach (var hit in Physics.OverlapSphere(c, reach, ~0, QueryTriggerInteraction.Collide))
            {
                foreach (var r in hit.GetComponentsInChildren<Renderer>())
                {
                    if (r == null || r is ParticleSystemRenderer || r is SkinnedMeshRenderer || r.transform.IsChildOf(centre)) continue;
                    if (_swapped.Exists(s => s.r == r)) continue;
                    var own = r.sharedMaterials;
                    bool allLit = own.Length > 0;
                    foreach (var m in own) if (m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Lit") { allLit = false; break; }
                    if (!allLit) continue;
                    var bent = new Material[own.Length];
                    for (int i = 0; i < own.Length; i++) bent[i] = CopyOf(own[i]);
                    _swapped.Add((r, own));
                    r.sharedMaterials = bent;
                }
            }
        }

        private Material CopyOf(Material m)
        {
            if (_copies.TryGetValue(m, out var copy) && copy != null) return copy;
            copy = new Material(template) { name = m.name + " (bend)" };
            copy.CopyPropertiesFromMaterial(m);
            // CopyPropertiesFromMaterial takes values, not the shader's feature switches: without them a copy kept the
            // template's (specular highlights and reflections ON where the toy packs turn them off, no emission, no
            // alpha clip...), so things turned shiny for the shift and the bloom (D-0119) made them glow (his "bright
            // glowing materials when shifting", D-0126). Take the original's switches and draw order, then the bend's.
            copy.shaderKeywords = m.shaderKeywords;
            copy.renderQueue = m.renderQueue;
            copy.globalIlluminationFlags = m.globalIlluminationFlags;
            Bendify(copy);
            _copies[m] = copy;
            return copy;
        }

        /// <summary>The material bends with this Little Planet (Y, ID 1), its normals bent too for the light.</summary>
        public static void Bendify(Material m)
        {
            m.SetVector(SettingsId, new Vector4((int)BendType.LittlePlanet_Y, 1, 1, 0));
            // only this bend type: a fresh Curved World material comes with the shader's first one (Classic Runner) on
            foreach (var k in m.shaderKeywords) if (k.StartsWith("CURVEDWORLD_BEND_TYPE_") && k != TypeKeyword) m.DisableKeyword(k);
            m.EnableKeyword(TypeKeyword); m.EnableKeyword(IdKeyword); m.EnableKeyword(NormalKeyword);
        }

        private void SwapBack()
        {
            foreach (var (r, own) in _swapped) if (r != null) r.sharedMaterials = own;
            _swapped.Clear();
        }

        private void OnDestroy() { foreach (var m in _copies.Values) if (m != null) Destroy(m); }
    }
}
