using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A shockwave ring that grows from the middle of a blast out to its reach and fades (D-0153): it shows how far a blast
    /// reached, the same size as the throw arc's ring. Flat, drawn by ShiftJam/AimArc (solid). Self-cleaning.
    /// </summary>
    public sealed class BlastRing : MonoBehaviour
    {
        private const int Sides = 48;
        private static Shader _shader;
        private LineRenderer _line;
        private Material _material;
        private Vector3 _at;
        private float _radius, _seconds, _t;
        private Color _colour;

        /// <summary>A ring at <paramref name="at"/> growing to <paramref name="radius"/> over <paramref name="seconds"/>.</summary>
        public static void Play(Vector3 at, float radius, Color colour, float width, float seconds)
        {
            if (radius <= 0f) return;
            if (_shader == null) _shader = Resources.Load<Shader>("FX/AimArc");
            if (_shader == null) return;
            var go = new GameObject("BlastRing");
            var ring = go.AddComponent<BlastRing>();
            ring._material = new Material(_shader) { name = "BlastRing (instance)" };
            ring._material.SetFloat("_Dash", 0f);
            ring._line = go.AddComponent<LineRenderer>();
            ring._line.useWorldSpace = true; ring._line.loop = true; ring._line.positionCount = Sides;
            ring._line.alignment = LineAlignment.View;
            ring._line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ring._line.receiveShadows = false;
            ring._line.sharedMaterial = ring._material;
            ring._line.widthMultiplier = width;
            ring._at = at + Vector3.up * width; ring._radius = radius; ring._seconds = Mathf.Max(0.05f, seconds); ring._colour = colour;
            ring.Draw(0f);
        }

        private void Update()
        {
            _t += Time.deltaTime / _seconds;
            if (_t >= 1f) { Destroy(gameObject); return; }
            Draw(_t);
        }

        /// <summary>Out fast then slow (ease out), fading over the second half.</summary>
        private void Draw(float t)
        {
            float r = _radius * (1f - (1f - t) * (1f - t));
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                _line.SetPosition(i, _at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Mathf.Max(r, 0.001f));
            }
            Color c = _colour; c.a *= t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            _line.startColor = _line.endColor = c;
        }

        private void OnDestroy() { if (_material != null) Destroy(_material); }
    }
}
