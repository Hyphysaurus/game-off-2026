using UnityEngine;

namespace ShiftJam
{
    /// <summary>A point light that flares and dies away, then removes itself (the car explosion's flash, D-0100).</summary>
    public sealed class LightFade : MonoBehaviour
    {
        private Light _light;
        private float _start, _seconds, _t;

        /// <summary>A flash at <paramref name="at"/>: full <paramref name="intensity"/> at once, gone after <paramref name="seconds"/>.</summary>
        public static void Flash(Vector3 at, Color color, float intensity, float range, float seconds)
        {
            var go = new GameObject("Flash");
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            var f = go.AddComponent<LightFade>();
            f._light = l; f._start = intensity; f._seconds = Mathf.Max(0.05f, seconds);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(_t / _seconds);
            _light.intensity = _start * k * k;   // falls fast, then lingers a moment
            if (_t >= _seconds) Destroy(gameObject);
        }
    }
}
