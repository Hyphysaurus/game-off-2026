using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// A thrown thing's flight drawn ahead of the throw (D-0148): a dashed line along the predicted path and a ring where
    /// it first lands (as wide as what it does there: a grenade's blast). Any thrower can use one: it only draws what it is
    /// given (Show / Hide). Not a child of the player (first person hides the player's own drawings); it lives in the
    /// thrower's scene and goes with it. Drawn by ShiftJam/AimArc (Resources/FX/AimArc).
    /// </summary>
    public sealed class ArcPreview : MonoBehaviour
    {
        private const int RingSides = 40;
        private static Shader _shader;
        private LineRenderer _line, _ring;
        private Material _lineMaterial, _ringMaterial;
        private Transform _owner;

        /// <summary>A preview for <paramref name="owner"/> (it goes when the owner does). Null without the shader.</summary>
        public static ArcPreview For(Transform owner)
        {
            if (_shader == null) _shader = Resources.Load<Shader>("FX/AimArc");
            if (_shader == null) { Debug.LogWarning("ArcPreview: Resources/FX/AimArc shader not found; no throw arcs."); return null; }
            var go = new GameObject("ArcPreview");
            SceneManager.MoveGameObjectToScene(go, owner.gameObject.scene);
            var p = go.AddComponent<ArcPreview>();
            p._owner = owner;
            p._lineMaterial = new Material(_shader) { name = "AimArc line (instance)" };
            p._ringMaterial = new Material(_shader) { name = "AimArc ring (instance)" };
            p._ringMaterial.SetFloat("_Dash", 0f);
            p._line = Line(go.transform, "Path", p._lineMaterial, false);
            p._ring = Line(go.transform, "Ring", p._ringMaterial, true);
            p.Hide();
            return p;
        }

        private static LineRenderer Line(Transform parent, string name, Material m, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.loop = loop;
            l.textureMode = LineTextureMode.Tile;
            l.alignment = LineAlignment.View;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.numCapVertices = 2;
            l.sharedMaterial = m;
            l.positionCount = 0;
            return l;
        }

        /// <summary>Draws the path through <paramref name="points"/>, <paramref name="width"/> wide, and (when
        /// <paramref name="ringRadius"/> is over 0) a ring that wide flat on the surface at <paramref name="ringAt"/>
        /// (<paramref name="ringUp"/> its normal), in <paramref name="ringColour"/>.</summary>
        public void Show(List<Vector3> points, float width, Color lineColour, float dashesPerWidth, Vector3 ringAt, Vector3 ringUp, float ringRadius, Color ringColour)
        {
            _line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) _line.SetPosition(i, points[i]);
            _line.widthMultiplier = width;
            _line.startColor = _line.endColor = lineColour;
            _lineMaterial.SetFloat("_Dash", dashesPerWidth);
            _line.enabled = points.Count > 1;
            if (ringRadius <= 0f) { _ring.enabled = false; return; }
            Vector3 up = ringUp.sqrMagnitude > 1e-6f ? ringUp.normalized : Vector3.up;
            Vector3 a = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.forward).normalized, b = Vector3.Cross(up, a);
            Vector3 centre = ringAt + up * (width * 0.5f);   // just off the surface, so it is not lost in it
            _ring.positionCount = RingSides;
            for (int i = 0; i < RingSides; i++)
            {
                float t = i * Mathf.PI * 2f / RingSides;
                _ring.SetPosition(i, centre + (a * Mathf.Cos(t) + b * Mathf.Sin(t)) * ringRadius);
            }
            _ring.widthMultiplier = width;
            _ring.startColor = _ring.endColor = ringColour;
            _ring.enabled = true;
        }

        public void Hide()
        {
            if (_line != null) _line.enabled = false;
            if (_ring != null) _ring.enabled = false;
        }

        private void LateUpdate() { if (_owner == null) Destroy(gameObject); }

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_ringMaterial != null) Destroy(_ringMaterial);
        }
    }
}
