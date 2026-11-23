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
    /// that guide the player: checkpoint markers, cores' embers, flashlights, and anything under a KeepLit. Several zones may overlap: the darkest wins, and the
    /// ambient comes back only when the last one is left. Measured from the camera, so it is what the player sees.
    /// </summary>
    public sealed class DarkZone : MonoBehaviour
    {
        [SerializeField, Tooltip("The dark box, in this object's own space: its middle.")] private Vector3 centre = Vector3.zero;
        [SerializeField, Tooltip("The dark box's size, in this object's own space.")] private Vector3 size = new Vector3(10f, 10f, 10f);
        [SerializeField, Range(0f, 1f), Tooltip("The lights inside dim to this share of their own brightness.")] private float lightsTo = 0.08f;
        [SerializeField, Range(0f, 1f), Tooltip("The ambient light (and reflections) dims to this share while inside.")] private float ambientTo = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to go dark, or light again.")] private float fade = 0.6f;
        [SerializeField, Tooltip("Dim every light whose reach touches the box, wherever it stands (on), or only the lights inside it (off).")] private bool reachingIn = true;
        [SerializeField, Tooltip("Dim the sun (directional lights) too.")] private bool dimSun = true;
        [SerializeField, Tooltip("Extra lights to dim that are not found by themselves.")] private Light[] alsoDim;

        private struct Dimmed { public Light light; public float intensity; }
        private readonly List<Dimmed> _lights = new List<Dimmed>();
        private float _dark;   // 0 lit .. 1 dark, eased

        // The ambient as the scene had it, kept while any zone is dark (the darkest zone sets it).
        private static readonly List<DarkZone> Zones = new List<DarkZone>();
        private static bool _saved;
        private static Color _sky, _equator, _ground, _flat;
        private static float _reflection;

        /// <summary>True while the view is inside the box.</summary>
        public bool Inside { get; private set; }

        private void OnEnable() { if (!Zones.Contains(this)) Zones.Add(this); }

        private void OnDisable()
        {
            Zones.Remove(this);
            _dark = 0f; Apply();
            ApplyAmbient();
        }

        private void Start()
        {
            var keep = new HashSet<Light>();
            foreach (var f in FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None)) foreach (var l in f.GetComponentsInChildren<Light>(true)) keep.Add(l);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (keep.Contains(l) || Guides(l)) continue;
                bool reaches = l.type == LightType.Directional ? dimSun
                             : Contains(l.transform.position) || (reachingIn && Gap(l.transform.position) < l.range);
                if (!reaches) continue;
                _lights.Add(new Dimmed { light = l, intensity = l.intensity });
            }
            if (alsoDim != null) foreach (Light l in alsoDim) if (l != null && !_lights.Exists(d => d.light == l)) _lights.Add(new Dimmed { light = l, intensity = l.intensity });
        }

        /// <summary>The lights that guide the player stay lit: checkpoints, cores (their embers), anything under a KeepLit.</summary>
        private static bool Guides(Light l) =>
            l.GetComponentInParent<Checkpoint>() != null || l.GetComponentInParent<ShiftSource>() != null || l.GetComponentInParent<KeepLit>() != null;

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
        public int Dims => _lights.Count;

        private void LateUpdate()
        {
            Camera view = Camera.main;
            Inside = view != null && Contains(view.transform.position);
            float was = _dark;
            _dark = Mathf.MoveTowards(_dark, Inside ? 1f : 0f, Time.deltaTime / fade);
            if (_dark == was) return;
            Apply();
            ApplyAmbient();
        }

        private void Apply()
        {
            float share = Mathf.Lerp(1f, lightsTo, Mathf.SmoothStep(0f, 1f, _dark));
            foreach (Dimmed d in _lights) if (d.light != null) d.light.intensity = d.intensity * share;
        }

        /// <summary>The scene's ambient at the darkest zone's share (kept the first time any goes dark, put back when none is).</summary>
        private static void ApplyAmbient()
        {
            float share = 1f;
            foreach (DarkZone z in Zones) share = Mathf.Min(share, Mathf.Lerp(1f, z.ambientTo, Mathf.SmoothStep(0f, 1f, z._dark)));
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
        private static void ResetStatics() { Zones.Clear(); _saved = false; }

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
