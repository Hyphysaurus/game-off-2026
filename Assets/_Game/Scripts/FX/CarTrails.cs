using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Tyre trails for the driven car (DrivingFeelSettings, "Tyre trails"): a mark on the ground under every wheel while
    /// the car slides (dark rubber, turning the drift boost's colour while a level is banked: the charge is read off
    /// the road) and a short bright streak behind each rear wheel while a boost pushes. The trails are Unity
    /// TrailRenderers made at run time; they are NOT children of the car (they sit under DrivingFeel and are moved to the
    /// wheels every frame), so the car's drawn size, which the seat, the camera and Build Mode measure from its
    /// renderers, never grows a tail. A wheel that jumps (a portal, a reset) has its trail cut.
    /// The material is a copy of Resources/FX/BoxDust (URP Particles/Unlit, see-through, tinted per trail), with the dust
    /// picture swapped for a soft-edged strip drawn here: that shader is already in every build because the asset is in
    /// Resources, so nothing is looked up by name. Without that asset the UI's own default material is used.
    /// DrivingFeel binds it to the car and ticks it.
    /// </summary>
    public sealed class CarTrails : MonoBehaviour
    {
        private const string MaterialResource = "FX/BoxDust";
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private DrivingFeelSettings _settings;
        private Material _material;
        private bool _ownsMaterial;
        private Texture2D _strip;
        private readonly List<TrailRenderer> _skids = new List<TrailRenderer>();    // one per wheel of the bound car
        private readonly List<TrailRenderer> _boosts = new List<TrailRenderer>();   // one per wheel; only rear wheels emit
        private readonly List<Vector3> _last = new List<Vector3>();
        private int _wheels;         // wheels of the bound car
        private int _colourLevel = -1;
        private float _scale = -1f;

        /// <summary>Hands over the tuning. Trails are made when a car is first bound.</summary>
        public void Setup(DrivingFeelSettings settings) => _settings = settings;

        /// <summary>Follows <paramref name="car"/>'s wheels from now on (null: none). Old trails are cut at once.</summary>
        public void Bind(ArcadeCar car)
        {
            Cut();
            _wheels = car != null ? car.WheelCount : 0;
            _colourLevel = -1; _scale = -1f;
            if (_wheels == 0 || _settings == null) return;
            if (_material == null) MakeMaterial();
            while (_skids.Count < _wheels)
            {
                _skids.Add(Make("Skid " + _skids.Count, LineAlignment.TransformZ));
                _boosts.Add(Make("Boost " + _boosts.Count, LineAlignment.View));
                _last.Add(Vector3.zero);
            }
            for (int i = 0; i < _wheels; i++) _last[i] = car.WheelHub(i);
        }

        /// <summary>Once a frame, after the car has moved: puts each trail at its wheel and switches it on or off.
        /// <paramref name="car"/> is null when nothing should draw (nobody drives, Build Mode, paused, wrecked).</summary>
        public void Tick(in DriveState state, ArcadeCar car)
        {
            if (_wheels == 0 || _settings == null) return;
            if (car == null || !state.Live || car.WheelCount != _wheels)
            {
                for (int i = 0; i < _wheels; i++) { Emit(_skids[i], false); Emit(_boosts[i], false); }
                return;
            }
            var s = _settings;
            float scale = car.Scale;
            if (!Mathf.Approximately(scale, _scale)) Size(scale);
            int level = Mathf.Clamp(state.DriftLevel, 0, 2);
            if (level != _colourLevel) Colour(level);

            Vector3 up = car.transform.up, forward = car.transform.forward;
            Quaternion flat = Quaternion.LookRotation(up, forward);   // the mark's face looks up from the ground
            bool marking = state.Sliding || state.DriftMeter > 0f;   // a charging slide marks the road from its first moment
            float cut = s.trailBreak * scale;
            for (int i = 0; i < _wheels; i++)
            {
                Vector3 hub = car.WheelHub(i);
                bool jumped = (hub - _last[i]).sqrMagnitude > cut * cut;
                _last[i] = hub;
                var skid = _skids[i]; var boost = _boosts[i];
                if (jumped) { skid.Clear(); boost.Clear(); }
                skid.transform.SetPositionAndRotation(hub - up * (car.WheelRadius(i) - s.trailLift * scale), flat);
                boost.transform.position = hub;
                Emit(skid, marking && car.WheelGrounded(i));
                Emit(boost, state.Boosting && car.WheelIsRear(i));
            }
        }

        private void OnDisable() => Cut();

        private void OnDestroy()
        {
            if (_ownsMaterial && _material != null) Destroy(_material);
            if (_strip != null) Destroy(_strip);
        }

        /// <summary>Every trail off and emptied (a new car, the driver out, a scene change).</summary>
        private void Cut()
        {
            for (int i = 0; i < _skids.Count; i++)
            {
                if (_skids[i] != null) { _skids[i].emitting = false; _skids[i].Clear(); }
                if (_boosts[i] != null) { _boosts[i].emitting = false; _boosts[i].Clear(); }
            }
        }

        private static void Emit(TrailRenderer trail, bool on)
        {
            if (trail.emitting != on) trail.emitting = on;
        }

        /// <summary>Widths and point spacing follow the car's size (a kid-size car leaves kid-size marks).</summary>
        private void Size(float scale)
        {
            _scale = scale;
            var s = _settings;
            for (int i = 0; i < _skids.Count; i++)
            {
                _skids[i].widthMultiplier = s.skidWidth * scale;
                _skids[i].minVertexDistance = s.skidWidth * scale * 0.5f;
                _skids[i].time = Mathf.Max(0.05f, s.skidSeconds);
                _boosts[i].widthMultiplier = s.boostTrailWidth * scale;
                _boosts[i].minVertexDistance = s.boostTrailWidth * scale * 0.5f;
                _boosts[i].time = Mathf.Max(0.05f, s.boostTrailSeconds);
            }
        }

        /// <summary>The marks' colour for a drift level: rubber, level one, level two. Each fades out along its length.</summary>
        private void Colour(int level)
        {
            _colourLevel = level;
            var s = _settings;
            Color c = level >= 2 ? s.driftTwoColour : level == 1 ? s.driftOneColour : s.skidColour;
            Color end = c; end.a = 0f;
            Color b = s.boostTrailColour, bEnd = b; bEnd.a = 0f;
            for (int i = 0; i < _skids.Count; i++)
            {
                _skids[i].startColor = c; _skids[i].endColor = end;
                _boosts[i].startColor = b; _boosts[i].endColor = bEnd;
            }
        }

        private TrailRenderer Make(string label, LineAlignment alignment)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = _material;
            trail.alignment = alignment;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCapVertices = 0; trail.numCornerVertices = 0;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.autodestruct = false;
            trail.emitting = false;
            return trail;
        }

        /// <summary>The see-through material every trail shares (see the class notes for why this one).</summary>
        private void MakeMaterial()
        {
            _strip = Strip();
            var source = Resources.Load<Material>(MaterialResource);
            if (source != null)
            {
                _material = new Material(source) { name = "CarTrails" };
                _ownsMaterial = true;
                if (_material.HasProperty(BaseMapId)) _material.SetTexture(BaseMapId, _strip);
                if (_material.HasProperty(MainTexId)) _material.SetTexture(MainTexId, _strip);
                if (_material.HasProperty(BaseColorId)) _material.SetColor(BaseColorId, Color.white);
                if (_material.HasProperty(ColorId)) _material.SetColor(ColorId, Color.white);
                return;
            }
            // no dust material in Resources: the UI's default (see-through, tinted by the trail's colours, always built in)
            _material = UnityEngine.UI.Graphic.defaultGraphicMaterial;
            _ownsMaterial = false;
        }

        /// <summary>A small white strip, solid along its middle and fading to nothing at both edges, so a trail's sides are soft.</summary>
        private static Texture2D Strip()
        {
            const int width = 4, height = 16;
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float across = Mathf.Sin(Mathf.PI * (y + 0.5f) / height);   // 0 at the edges, 1 in the middle
                byte a = (byte)(Mathf.Clamp01(across * 1.6f) * 255f + 0.5f);
                for (int x = 0; x < width; x++) px[y * width + x] = new Color32(255, 255, 255, a);
            }
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "CarTrails Strip", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
