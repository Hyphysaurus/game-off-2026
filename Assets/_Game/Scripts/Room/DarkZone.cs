using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A stretch that goes dark while the player is in it (D-0150, systems plan step 6): the lights inside its box dim to
    /// Lights To and the ambient light (the scene's sky, horizon and ground colours, and its reflections) to Ambient To,
    /// over Fade seconds, and come back as the player leaves. Drop one into any section and size its box (the gizmo); the
    /// lights it dims are found when it starts: every light whose reach touches the box (a room's big lamp lights a lane
    /// through its wall: the hallway was lit by the living room's and the tall end's, D-0152) and the sun, except the ones
    /// that guide the player: checkpoint markers, cores' embers, flashlights, and anything under a KeepLit. Measured from
    /// the camera, so it is what the player sees.
    /// Several zones may overlap (the whole dark bedroom holds the toy box's and the cave's, D-0214): the darkest wins, for
    /// each lamp and for the ambient. A lamp's own brightness is kept once for all zones (the first to find it), so a zone
    /// that starts while another has the lamp dimmed never takes the dim value for the lamp's own; and a zone that ends
    /// (the box's burst) gives a lamp back only as far as the other zones still dark allow.
    /// Release (D-0214, the bedroom's light switch) lifts a zone's dark for good over a few seconds and then switches it
    /// off; Restore brings it back (a New Game in the same loaded scene).
    /// Soften (D-0218, the bedroom's star lamp) lifts the dark part-way while it lasts: the lamps and the ambient go no
    /// lower than the soft shares given (a toy's own starlight, between the dark and the lamps). The darkest-wins rule
    /// reads the softened share, so a darker zone inside this one (the cave under the bed) stays darker.
    /// </summary>
    public sealed class DarkZone : MonoBehaviour
    {
        [SerializeField, Tooltip("The dark box, in this object's own space: its middle.")] private Vector3 centre = Vector3.zero;
        [SerializeField, Tooltip("The dark box's size, in this object's own space.")] private Vector3 size = new Vector3(10f, 10f, 10f);
        [SerializeField, Range(0f, 1f), Tooltip("The lights inside dim to this share of their own brightness.")] private float lightsTo = 0.08f;
        [SerializeField, Range(0f, 1f), Tooltip("The ambient light (and reflections) dims to this share while inside. Keep it over about 0.1: pitch black reads as broken, and video squashes it.")] private float ambientTo = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to go dark, or light again.")] private float fade = 0.6f;
        [SerializeField, Tooltip("Dim every light whose reach touches the box, wherever it stands (on), or only the lights inside it (off).")] private bool reachingIn = true;
        [SerializeField, Tooltip("Dim the sun (directional lights) too.")] private bool dimSun = true;
        [SerializeField, Tooltip("Extra lights to dim that are not found by themselves.")] private Light[] alsoDim;

        private readonly List<Light> _lights = new List<Light>();
        private float _dark;          // 0 lit .. 1 dark, eased
        private float _release = -1f; // seconds the dark takes to lift for good; below 0 = not lifting
        private bool _started;
        private float _soft;                            // Soften: 0 none .. 1 the soft shares in full
        private float _softLights = 1f, _softAmbient = 1f;   // the soft shares (never darker than the zone's own)

        // Every zone switched on, a lamp's own brightness (kept by the first zone to find it), and the ambient as the scene
        // had it, kept while any zone is dark (the darkest zone sets it).
        private static readonly List<DarkZone> Zones = new List<DarkZone>();
        private static readonly Dictionary<Light, float> Own = new Dictionary<Light, float>();
        private static bool _saved;
        private static Color _sky, _equator, _ground, _flat;
        private static float _reflection;

        /// <summary>True while the view is inside the box.</summary>
        public bool Inside { get; private set; }
        /// <summary>How dark it is now, 0 lit to 1 as dark as it goes (eased, as the lights see it). A part that shows only in
        /// the dark (the bedroom's glow round the player, D-0214) follows it.</summary>
        public float Darkness => Mathf.SmoothStep(0f, 1f, _dark);
        /// <summary>True while Release is lifting the dark (it switches the zone off when done).</summary>
        public bool Releasing => _release > 0f;
        /// <summary>The lights dim to this share (Inspector).</summary>
        public float LightsTo => lightsTo;
        /// <summary>The ambient dims to this share (Inspector).</summary>
        public float AmbientTo => ambientTo;
        /// <summary>The box's size in its own space (Inspector).</summary>
        public Vector3 Size => size;

        private void OnEnable()
        {
            if (!Zones.Contains(this)) Zones.Add(this);
            // Switched on again (Restore) after every zone had been off: the lamps are at their own brightness now.
            if (_started) for (int i = 0; i < _lights.Count; i++) if (_lights[i] != null && !Own.ContainsKey(_lights[i])) Own[_lights[i]] = _lights[i].intensity;
        }

        private void OnDisable()
        {
            Zones.Remove(this);
            _dark = 0f; _release = -1f;
            Apply();          // its lamps go back as far as the zones still on allow
            ApplyAmbient();
            if (Zones.Count == 0) Own.Clear();   // nothing dims any lamp now: their own values are their own again
        }

        private void Start()
        {
            _started = true;
            var keep = new HashSet<Light>();
            foreach (var f in FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None)) foreach (var l in f.GetComponentsInChildren<Light>(true)) keep.Add(l);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!keep.Contains(l) && Dims(l)) Take(l);
            if (alsoDim != null) foreach (Light l in alsoDim) if (l != null && !_lights.Contains(l)) Take(l);
            if (_dark > 0f) Apply();
        }

        private void Take(Light l)
        {
            _lights.Add(l);
            if (!Own.ContainsKey(l)) Own[l] = l.intensity;
        }

        /// <summary>The rule this zone dims by (flashlights aside, which are always kept): a light that is not a guide and
        /// whose reach touches the box, or the sun. The maker reports the room's lights with it (D-0214).</summary>
        public bool Dims(Light l)
        {
            if (l == null || Guides(l)) return false;
            if (l.type == LightType.Directional) return dimSun;
            return Contains(l.transform.position) || (reachingIn && Gap(l.transform.position) < l.range);
        }

        /// <summary>The lights that guide the player stay lit: checkpoints, cores (their embers), anything under a KeepLit.</summary>
        public static bool Guides(Light l) =>
            l.GetComponentInParent<Checkpoint>(true) != null || l.GetComponentInParent<ShiftSource>(true) != null || l.GetComponentInParent<KeepLit>(true) != null;

        /// <summary>True when <paramref name="point"/> (the world) is inside the box.</summary>
        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point) - centre;
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>How far <paramref name="point"/> (the world) is from the box: 0 inside.</summary>
        public float Gap(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point) - centre;
            Vector3 half = size * 0.5f;
            Vector3 outside = new Vector3(Mathf.Max(0f, Mathf.Abs(local.x) - half.x), Mathf.Max(0f, Mathf.Abs(local.y) - half.y), Mathf.Max(0f, Mathf.Abs(local.z) - half.z));
            return transform.TransformVector(outside).magnitude;
        }

        /// <summary>How many lights it dims (found at the start).</summary>
        public int DimCount => _lights.Count;

        /// <summary>Lifts this zone's dark for good (D-0214: the bedroom's switch): the lamps and the ambient rise from where
        /// they are to their own over <paramref name="seconds"/>, eased, then the zone switches itself off. 0 or less: at
        /// once. Nothing if it is off already.</summary>
        public void Release(float seconds)
        {
            if (!isActiveAndEnabled) return;
            if (seconds <= 0f || _dark <= 0f) { enabled = false; return; }
            _release = seconds;
        }

        /// <summary>Lifts the dark part-way (D-0218: the star lamp awake): the lamps dim no lower than
        /// <paramref name="lightsShare"/> of their own and the ambient no lower than <paramref name="ambientShare"/>, as far as
        /// <paramref name="amount"/> says (0 = not at all, 1 = in full; the caller eases it). Shares under the zone's own
        /// Lights To / Ambient To change nothing. Applied at once; it lasts until changed (0 takes it away).</summary>
        public void Soften(float lightsShare, float ambientShare, float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (amount == _soft && lightsShare == _softLights && ambientShare == _softAmbient) return;
            _soft = amount; _softLights = lightsShare; _softAmbient = ambientShare;
            Apply();
            ApplyAmbient();
        }

        /// <summary>The share the lamps dim to now (Lights To, lifted by Soften).</summary>
        private float LightsFloor => Mathf.Lerp(lightsTo, Mathf.Max(lightsTo, _softLights), _soft);
        /// <summary>The share the ambient dims to now (Ambient To, lifted by Soften).</summary>
        private float AmbientFloor => Mathf.Lerp(ambientTo, Mathf.Max(ambientTo, _softAmbient), _soft);

        /// <summary>Back on and dark again where the view is inside (a New Game in a scene where the dark had been lifted).</summary>
        public void Restore()
        {
            _release = -1f;
            if (!enabled) enabled = true;
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            Inside = view != null && Contains(view.transform.position);
            float was = _dark;
            if (_release > 0f) _dark = Mathf.MoveTowards(_dark, 0f, Time.deltaTime / _release);
            else _dark = Mathf.MoveTowards(_dark, Inside ? 1f : 0f, Time.deltaTime / fade);
            if (_dark != was)
            {
                Apply();
                ApplyAmbient();
            }
            if (_release > 0f && _dark <= 0f) enabled = false;   // lifted for good
        }

        /// <summary>This zone's share of a lamp's own brightness now.</summary>
        private float Share => Mathf.Lerp(1f, LightsFloor, Mathf.SmoothStep(0f, 1f, _dark));

        /// <summary>Each of this zone's lamps at its own brightness times the darkest share among the zones (switched on)
        /// that dim it.</summary>
        private void Apply()
        {
            if (!_started) return;
            for (int i = 0; i < _lights.Count; i++)
            {
                Light l = _lights[i];
                if (l == null || !Own.TryGetValue(l, out float own)) continue;
                float share = 1f;
                for (int z = 0; z < Zones.Count; z++)
                {
                    DarkZone other = Zones[z];
                    if (other._dark > 0f && other._lights.Contains(l)) share = Mathf.Min(share, other.Share);
                }
                l.intensity = own * share;
            }
        }

        /// <summary>The scene's ambient at the darkest zone's share (kept the first time any goes dark, put back when none is).</summary>
        private static void ApplyAmbient()
        {
            float share = 1f;
            foreach (DarkZone z in Zones) share = Mathf.Min(share, Mathf.Lerp(1f, z.AmbientFloor, Mathf.SmoothStep(0f, 1f, z._dark)));
            if (share >= 1f)
            {
                if (!_saved) return;
                RenderSettings.ambientSkyColor = _sky; RenderSettings.ambientEquatorColor = _equator; RenderSettings.ambientGroundColor = _ground;
                RenderSettings.ambientLight = _flat; RenderSettings.reflectionIntensity = _reflection;
                _saved = false;
                return;
            }
            if (!_saved)
            {
                _sky = RenderSettings.ambientSkyColor; _equator = RenderSettings.ambientEquatorColor; _ground = RenderSettings.ambientGroundColor;
                _flat = RenderSettings.ambientLight; _reflection = RenderSettings.reflectionIntensity;
                _saved = true;
            }
            RenderSettings.ambientSkyColor = _sky * share; RenderSettings.ambientEquatorColor = _equator * share; RenderSettings.ambientGroundColor = _ground * share;
            RenderSettings.ambientLight = _flat * share; RenderSettings.reflectionIntensity = _reflection * share;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Zones.Clear(); Own.Clear(); _saved = false; }

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.2f, 0.45f, 0.25f);
            Gizmos.DrawCube(centre, size);
            Gizmos.color = new Color(0.4f, 0.4f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(centre, size);
        }
    }
}
