using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The beam of a spot light seen in the air (D-0140, his "add the beam"): a soft glowing cone from the lens along
    /// the light, shown only while the light is on. It sizes itself from the light every frame: as long as a share of
    /// the light's range, as wide as its cone, so it goes with the flashlight's size (D-0138) and anything that changes
    /// the light. Drawn by the ShiftJam/BeamShaft shader (Resources/FX/BeamShaft), tinted with the light's colour; no
    /// shadows, nothing to collide with. Made at runtime by <see cref="Add"/>. It is NOT a child of the light: everything
    /// that measures a carried thing by its meshes (its grip and front, the put-down, the pick-up glint) would take the cone
    /// for part of the flashlight (it did: the torch was held off the hand, pointing back at the player). It follows the
    /// lens instead, after everything else has moved this frame, and goes when the light does.
    /// It can also light the bulb (D-0142): a small glowing ball round it (ShiftJam/BulbGlow, Resources/FX/BulbGlow),
    /// placed behind the light in the light's own units, so it goes with the flashlight's size; brighter with Boost.
    /// It stops at what it meets (D-0244, his note on the demo reel: the glow showed through the cardboard box): one ray
    /// down the beam's middle each frame, and the cone ends at the first solid thing on it that is as big as the beam is
    /// wide there (a wall, the floor, the bed), fading out just before it; smaller things (a core, a toy) it goes round.
    /// </summary>
    [DefaultExecutionOrder(10000)]   // after the Carrier puts the torch in the hand
    public sealed class BeamShaft : MonoBehaviour
    {
        private const int Sides = 24;
        private static Mesh _cone;
        private static Shader _shader, _bulbShader;
        private static bool _warned;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];   // shared by every shaft: filled and read within one Cut
        private static readonly int CutPointId = Shader.PropertyToID("_CutPoint");
        private static readonly int CutNormalId = Shader.PropertyToID("_CutNormal");
        private static readonly int CutSoftId = Shader.PropertyToID("_CutSoft");

        private Light _light;
        private MeshRenderer _renderer;
        private Material _material;
        private float _lengthShare, _lensShare, _strength, _boostWas = -1f;
        private Transform _bulb;                 // the bulb's glow, or null
        private Material _bulbMaterial;
        private float _bulbBack, _bulbRadius, _bulbStrength;
        private Transform _self;                 // the flashlight's own body: the beam passes it (D-0244)
        private bool _stops;                     // end the cone at the first solid thing on the beam (D-0244)
        private float _softShare;                // how far before that thing the cone fades out, as a share of the light's range
        private bool _cutSet;                    // the material holds a stop now

        /// <summary>Brightness multiplier on top of the strength it was made with (a focused flashlight, D-0141).</summary>
        public float Boost { get; set; } = 1f;

        /// <summary>Puts a shaft on <paramref name="light"/>: <paramref name="lengthShare"/> of its range long, starting
        /// <paramref name="lensShare"/> of its far width wide at the lens, <paramref name="strength"/> bright (the shader's
        /// default if 0 or less). With <paramref name="stops"/> the cone ends at the first solid thing the beam's middle
        /// meets, fading out <paramref name="softShare"/> of the light's range before it; <paramref name="self"/> (the
        /// flashlight's own body) never stops it (D-0244). Null when the shader is missing.</summary>
        public static BeamShaft Add(Light light, float lengthShare, float lensShare, float strength,
                                    Transform self = null, bool stops = true, float softShare = 0.02f)
        {
            if (light == null) return null;
            if (_shader == null) _shader = Resources.Load<Shader>("FX/BeamShaft");
            if (_shader == null)
            {
                if (!_warned) { Debug.LogWarning("BeamShaft: Resources/FX/BeamShaft shader not found; flashlights show no beam in the air."); _warned = true; }
                return null;
            }
            var go = new GameObject("BeamShaft");
            go.layer = light.gameObject.layer;
            SceneManager.MoveGameObjectToScene(go, light.gameObject.scene);
            var shaft = go.AddComponent<BeamShaft>();
            shaft._light = light;
            shaft._self = self;
            shaft._stops = stops;
            shaft._softShare = Mathf.Max(0f, softShare);
            shaft._lengthShare = Mathf.Max(0.01f, lengthShare);
            shaft._lensShare = Mathf.Clamp(lensShare, 0f, 0.9f);
            go.AddComponent<MeshFilter>().sharedMesh = Cone(shaft._lensShare);
            shaft._renderer = go.AddComponent<MeshRenderer>();
            shaft._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shaft._renderer.receiveShadows = false;
            shaft._renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            shaft._renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            shaft._material = new Material(_shader) { name = "BeamShaft (instance)" };
            if (strength > 0f) shaft._material.SetFloat("_Strength", strength);
            shaft._strength = shaft._material.GetFloat("_Strength");
            shaft._renderer.sharedMaterial = shaft._material;
            shaft.Fit();
            return shaft;
        }

        /// <summary>Lights the bulb while the light is on (D-0142): a glowing ball <paramref name="radius"/> across the
        /// bulb's middle, <paramref name="back"/> behind the light along its axis (both in the light's own units, so they
        /// scale with the flashlight), <paramref name="strength"/> bright (over 1 blooms).</summary>
        public void AddBulb(float back, float radius, float strength)
        {
            if (_bulb != null || radius <= 0f || strength <= 0f) return;
            if (_bulbShader == null) _bulbShader = Resources.Load<Shader>("FX/BulbGlow");
            if (_bulbShader == null) { Debug.LogWarning("BeamShaft: Resources/FX/BulbGlow shader not found; bulbs do not glow."); return; }
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "BulbGlow";
            Destroy(ball.GetComponent<Collider>());
            ball.layer = gameObject.layer;
            SceneManager.MoveGameObjectToScene(ball, gameObject.scene);
            var r = ball.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _bulbMaterial = new Material(_bulbShader) { name = "BulbGlow (instance)" };
            r.sharedMaterial = _bulbMaterial;
            _bulb = ball.transform;
            _bulbBack = back; _bulbRadius = radius; _bulbStrength = strength;
            _boostWas = -1f;
            Fit();
        }

        private void LateUpdate()
        {
            if (_light == null) { if (_bulb != null) Destroy(_bulb.gameObject); Destroy(gameObject); return; }   // the flashlight went (a scene closing, a destroy)
            Fit();
        }

        /// <summary>Shown while the light is on; as long and wide as the light reaches, in world units whatever the parents' scale.</summary>
        private void Fit()
        {
            bool on = _light != null && _light.enabled && _light.isActiveAndEnabled && _light.type == LightType.Spot;
            if (_renderer.enabled != on) _renderer.enabled = on;
            if (_bulb != null && _bulb.gameObject.activeSelf != on) _bulb.gameObject.SetActive(on);
            if (!on) return;
            float length = _light.range * _lengthShare;
            float radius = Mathf.Tan(_light.spotAngle * 0.5f * Mathf.Deg2Rad) * length;
            transform.SetPositionAndRotation(_light.transform.position, _light.transform.rotation);
            transform.localScale = new Vector3(radius, radius, length);   // no parent: world units
            Cut(length, radius);
            if (Boost != _boostWas)
            {
                _boostWas = Boost;
                _material.SetFloat("_Strength", _strength * Boost);
                if (_bulbMaterial != null) _bulbMaterial.SetFloat("_Strength", _bulbStrength * Boost);
            }
            Color c = _light.color;
            if (_bulb != null)
            {
                Transform l = _light.transform;
                float unit = Mathf.Abs(l.lossyScale.z);   // the light's own units (the flashlight's size)
                _bulb.SetPositionAndRotation(l.position - l.forward * (_bulbBack * unit), l.rotation);
                _bulb.localScale = Vector3.one * (2f * _bulbRadius * unit);   // the primitive ball is 1 across
                _bulbMaterial.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
            }
            _material.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
        }

        /// <summary>Ends the cone at the first solid thing the beam's middle meets (D-0244, his note on the demo reel: the
        /// flashlight's glow showed through the cardboard box). The cone is a fixed length, so before this it ran on through
        /// walls and showed on their far side. One ray down the middle, as far as the cone reaches (<paramref name="length"/>,
        /// <paramref name="radius"/> wide at its end). Triggers and the flashlight's own body do not count (a carried torch's
        /// colliders are off anyway, and its holder's capsule is behind the lens); nor does a thing smaller than the beam is
        /// wide where it meets it (a core, a toy on the floor): the beam goes on round it, as it always did. The shader draws
        /// nothing behind the surface it stops at and fades the cone out over a short way in front of it. The light itself
        /// already stops there: it casts shadows.</summary>
        private void Cut(float length, float radius)
        {
            bool cut = false;
            if (_stops && length > 0f)
            {
                Transform l = _light.transform;
                int n = Physics.RaycastNonAlloc(l.position, l.forward, Hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                int nearest = -1;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    Collider c = Hits[i].collider;
                    if (c == null || (_self != null && c.transform.IsChildOf(_self))) continue;   // its own body
                    float at = Hits[i].distance;
                    float beamThere = radius * (_lensShare + (1f - _lensShare) * (at / length));   // the cone's radius at that point
                    if (c.bounds.extents.magnitude < beamThere) continue;   // smaller than the beam there: it goes round
                    if (at < best) { best = at; nearest = i; }
                }
                if (nearest >= 0)
                {
                    RaycastHit h = Hits[nearest];
                    float soft = Mathf.Max(1e-4f, Mathf.Min(_light.range * _softShare, h.distance * 0.5f));   // never more than half the way
                    _material.SetVector(CutPointId, h.point);
                    _material.SetVector(CutNormalId, new Vector4(h.normal.x, h.normal.y, h.normal.z, 1f));
                    _material.SetFloat(CutSoftId, soft);
                    cut = true;
                }
            }
            if (!cut && _cutSet) _material.SetVector(CutNormalId, Vector4.zero);   // nothing in the way: the whole cone
            _cutSet = cut;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_bulbMaterial != null) Destroy(_bulbMaterial);
            if (_bulb != null) Destroy(_bulb.gameObject);
        }

        /// <summary>An open cone along +Z, 1 long, radius <paramref name="lens"/> at z 0 and 1 at z 1; uv.y = z.</summary>
        private static Mesh Cone(float lens)
        {
            if (_cone != null) return _cone;
            var verts = new Vector3[(Sides + 1) * 2];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[Sides * 6];
            float slope = 1f - lens;
            for (int i = 0; i <= Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                var round = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 n = (round + Vector3.back * slope).normalized;
                verts[i * 2] = round * lens; uvs[i * 2] = new Vector2(i / (float)Sides, 0f);
                verts[i * 2 + 1] = round + Vector3.forward; uvs[i * 2 + 1] = new Vector2(i / (float)Sides, 1f);
                normals[i * 2] = n; normals[i * 2 + 1] = n;
            }
            for (int i = 0; i < Sides; i++)
            {
                int k = i * 6, v = i * 2;
                tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 2;
                tris[k + 3] = v + 2; tris[k + 4] = v + 1; tris[k + 5] = v + 3;
            }
            _cone = new Mesh { name = "BeamShaftCone", vertices = verts, normals = normals, uv = uvs, triangles = tris };
            _cone.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1f));
            return _cone;
        }
    }
}
