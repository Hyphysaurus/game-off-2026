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
    /// </summary>
    [DefaultExecutionOrder(10000)]   // after the Carrier puts the torch in the hand
    public sealed class BeamShaft : MonoBehaviour
    {
        private const int Sides = 24;
        private static Mesh _cone;
        private static Shader _shader;
        private static bool _warned;

        private Light _light;
        private MeshRenderer _renderer;
        private Material _material;
        private float _lengthShare, _lensShare, _strength, _boostWas = -1f;

        /// <summary>Brightness multiplier on top of the strength it was made with (a focused flashlight, D-0141).</summary>
        public float Boost { get; set; } = 1f;

        /// <summary>Puts a shaft on <paramref name="light"/>: <paramref name="lengthShare"/> of its range long, starting
        /// <paramref name="lensShare"/> of its far width wide at the lens, <paramref name="strength"/> bright (the shader's
        /// default if 0 or less). Null when the shader is missing.</summary>
        public static BeamShaft Add(Light light, float lengthShare, float lensShare, float strength)
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

        private void LateUpdate()
        {
            if (_light == null) { Destroy(gameObject); return; }   // the flashlight went (a scene closing, a destroy)
            Fit();
        }

        /// <summary>Shown while the light is on; as long and wide as the light reaches, in world units whatever the parents' scale.</summary>
        private void Fit()
        {
            bool on = _light != null && _light.enabled && _light.isActiveAndEnabled && _light.type == LightType.Spot;
            if (_renderer.enabled != on) _renderer.enabled = on;
            if (!on) return;
            float length = _light.range * _lengthShare;
            float radius = Mathf.Tan(_light.spotAngle * 0.5f * Mathf.Deg2Rad) * length;
            transform.SetPositionAndRotation(_light.transform.position, _light.transform.rotation);
            transform.localScale = new Vector3(radius, radius, length);   // no parent: world units
            if (Boost != _boostWas) { _boostWas = Boost; _material.SetFloat("_Strength", _strength * Boost); }
            Color c = _light.color;
            _material.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
        }

        private void OnDestroy() { if (_material != null) Destroy(_material); }

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
