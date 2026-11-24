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
    /// The copies are made as the scene starts, not at the first shift (D-0178): the first shift of a play used to
    /// freeze for a second or two while every copy in the house was made at once. And a shift's own sweep of what is
    /// near no longer collects the same renderers over and over or makes new lists each time.
    /// </summary>
    [RequireComponent(typeof(CurvedWorldController))]
    public sealed class WorldBend : MonoBehaviour
    {
        public const string TypeKeyword = "CURVEDWORLD_BEND_TYPE_LITTLEPLANET_Y", IdKeyword = "CURVEDWORLD_BEND_ID_1", NormalKeyword = "CURVEDWORLD_NORMAL_TRANSFORMATION_ON";
        private static readonly int SettingsId = Shader.PropertyToID("_CurvedWorldBendSettings");
        private const string Lit = "Universal Render Pipeline/Lit";
        /// <summary>What the name of every copy made for a shift ends with. Code that must not write a renderer's materials while the bend has them (WeaponBuild, D-0167) knows a borrowed material by it.</summary>
        public const string CopySuffix = " (bend)";

        [Header("At rest: the garden")]
        [SerializeField, Tooltip("Where the bend is pivoted at rest (the window).")] private Transform restPivot;
        [SerializeField, Tooltip("How round the garden is: higher = a smaller planet.")] private float restCurvature = 0.8f;
        [SerializeField, Tooltip("Flat this far round the pivot.")] private float restOffset = 30f;

        [Header("On a shift")]
        [SerializeField, Tooltip("How round the room goes at the pulse's peak, at toy size (scaled by the size: the drop grows with the room).")] private float pulseCurvature = 12f;
        [SerializeField, Tooltip("Seconds the pulse lasts.")] private float pulseSeconds = 0.7f;
        [SerializeField, Tooltip("Renderers this many player heights round the player bend with it.")] private float pulseReach = 8f;
        [SerializeField, Tooltip("A Curved World Lit material (any garden one): the template the shift's copies are made from.")] private Material template;
        [SerializeField, Tooltip("On: the copies for everything in the scene are made as the scene starts (D-0178), so the first shift of a play does not freeze while they are made. Off: each is made the first time a shift reaches it, as before.")] private bool makeCopiesAtStart = true;

        /// <summary>What the bend knows of one renderer: its own materials as they were last seen, and their copies
        /// (null when it does not bend: something on it is not URP Lit).</summary>
        private sealed class Known { public Material[] own, bent; }

        private enum Kind : byte { Other, Lit, Overlay }

        private CurvedWorldController _bend;
        private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        private readonly Dictionary<Renderer, Known> _known = new Dictionary<Renderer, Known>();
        private readonly Dictionary<Shader, Kind> _kinds = new Dictionary<Shader, Kind>();   // a shader's name is asked once, not for every material of every renderer
        private readonly List<Renderer> _swapped = new List<Renderer>();          // those wearing copies now
        private readonly HashSet<Renderer> _seen = new HashSet<Renderer>();       // met already in this sweep (many colliders share a renderer)
        private readonly List<Renderer> _found = new List<Renderer>();            // one collider's renderers, used again for the next
        private readonly List<Material> _now = new List<Material>();              // one renderer's materials as they are now
        private readonly List<Renderer> _gone = new List<Renderer>();
        private Collider[] _near = new Collider[4096];
        private int _pruneAt = 4096;
        private Coroutine _pulse;

        private void Awake()
        {
            _bend = GetComponent<CurvedWorldController>();
            Rest();
        }

        /// <summary>As the scene starts (every object in it is there by now): the copies for everything that could
        /// bend are made here, in the frame the scene is already busy loading, instead of in the middle of the first
        /// shift.</summary>
        private void Start()
        {
            if (!makeCopiesAtStart || template == null) return;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (Bends(r)) Learn(r);
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
        /// materials wears Curved World copies of them until SwapBack (the copies are made once each and kept). What is
        /// near is asked of the physics, as before, so things made during play bend too; each renderer is taken once
        /// however many colliders lead to it.</summary>
        private void SwapIn(Transform centre, float reach)
        {
            if (template == null) return;
            Vector3 c = centre.position;
            int count = Physics.OverlapSphereNonAlloc(c, reach, _near, ~0, QueryTriggerInteraction.Collide);
            while (count >= _near.Length)
            {
                // more than the list holds (a kid's shift reaches the whole house): a longer list, kept for next time
                _near = new Collider[_near.Length * 2];
                count = Physics.OverlapSphereNonAlloc(c, reach, _near, ~0, QueryTriggerInteraction.Collide);
            }
            _seen.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider hit = _near[i];
                _near[i] = null;   // the list keeps nothing alive between shifts
                if (hit == null) continue;
                hit.GetComponentsInChildren(false, _found);
                for (int k = 0; k < _found.Count; k++)
                {
                    Renderer r = _found[k];
                    if (!_seen.Add(r) || !Bends(r) || r.transform.IsChildOf(centre)) continue;
                    Known known = Learn(r);
                    if (known == null || known.bent == null) continue;
                    r.sharedMaterials = known.bent;
                    _swapped.Add(r);
                }
            }
            _found.Clear();
            _seen.Clear();
            Prune();
        }

        /// <summary>The kinds of renderer that can bend at all: not particles, not skinned bodies.</summary>
        private static bool Bends(Renderer r) => r != null && !(r is ParticleSystemRenderer) && !(r is SkinnedMeshRenderer);

        /// <summary>What the bend knows of this renderer, brought up to date: if its materials are the ones last seen,
        /// that is the answer; if they changed (a skin, paint, another script's own copy) it is worked out again.
        /// Only what is drawn with URP Lit bends. A sight's own lens and reticle (SightLook, D-0166) may ride along as
        /// they are, so the sight's housing still bends with its gun and does not float off it.</summary>
        private Known Learn(Renderer r)
        {
            r.GetSharedMaterials(_now);
            if (_known.TryGetValue(r, out Known known) && Same(known.own, _now)) return known;
            if (known == null) _known[r] = known = new Known();
            known.own = _now.ToArray();
            known.bent = null;
            bool ok = _now.Count > 0, anyLit = false;
            for (int i = 0; i < _now.Count && ok; i++)
            {
                Kind kind = KindOf(_now[i]);
                if (kind == Kind.Lit) anyLit = true;
                else if (kind != Kind.Overlay) ok = false;
            }
            if (!ok || !anyLit) return known;
            var bent = new Material[_now.Count];
            for (int i = 0; i < bent.Length; i++) bent[i] = KindOf(_now[i]) == Kind.Lit ? CopyOf(_now[i]) : _now[i];
            known.bent = bent;
            return known;
        }

        private static bool Same(Material[] was, List<Material> now)
        {
            if (was == null || was.Length != now.Count) return false;
            for (int i = 0; i < was.Length; i++) if (!ReferenceEquals(was[i], now[i])) return false;
            return true;
        }

        private Kind KindOf(Material m)
        {
            if (m == null) return Kind.Other;
            Shader shader = m.shader;
            if (shader == null) return Kind.Other;
            if (_kinds.TryGetValue(shader, out Kind kind)) return kind;
            string name = shader.name;
            kind = name == Lit ? Kind.Lit : name == SightLook.OverlayShader ? Kind.Overlay : Kind.Other;
            _kinds[shader] = kind;
            return kind;
        }

        private Material CopyOf(Material m)
        {
            if (_copies.TryGetValue(m, out var copy) && copy != null) return copy;
            copy = new Material(template) { name = m.name + CopySuffix };
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
            for (int i = 0; i < _swapped.Count; i++)
            {
                Renderer r = _swapped[i];
                if (r != null && _known.TryGetValue(r, out Known known) && known.own != null) r.sharedMaterials = known.own;
            }
            _swapped.Clear();
        }

        /// <summary>Renderers that have been destroyed since are forgotten, now and then (when the list has doubled).</summary>
        private void Prune()
        {
            if (_known.Count < _pruneAt) return;
            _gone.Clear();
            foreach (var pair in _known) if (pair.Key == null) _gone.Add(pair.Key);
            for (int i = 0; i < _gone.Count; i++) _known.Remove(_gone[i]);
            _gone.Clear();
            _pruneAt = Mathf.Max(4096, _known.Count * 2);
        }

        private void OnDestroy() { foreach (var m in _copies.Values) if (m != null) Destroy(m); }
    }
}
