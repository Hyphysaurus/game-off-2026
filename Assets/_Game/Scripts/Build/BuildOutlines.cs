using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Wire boxes around things: the selection, and a fainter one around whatever the mouse is over. Each box is one
    /// LineRenderer tracing the 12 edges of the thing's own (turned) box, line width following the camera distance.
    /// Uses the catalogue's ghost material, tinted per box.
    /// </summary>
    public sealed class BuildOutlines
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        // A path over all 12 edges of a box (corners 0-3 bottom, 4-7 top), retracing a few.
        private static readonly int[] Path = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };

        private readonly Material _material;
        private readonly Transform _root;
        private readonly List<LineRenderer> _pool = new List<LineRenderer>();
        private readonly Vector3[] _corners = new Vector3[8];
        private readonly Vector3[] _points = new Vector3[Path.Length];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private int _used;

        public BuildOutlines(Material material)
        {
            _material = material;
            _root = new GameObject("BuildOutlines").transform;
        }

        public void SetVisible(bool visible) { if (_root != null) _root.gameObject.SetActive(visible); }

        /// <summary>Call once per frame before Draw calls; boxes not drawn again this frame are hidden by Finish.</summary>
        public void BeginFrame() { _used = 0; }

        public void Draw(Transform thing, Color color, Camera cam)
        {
            if (_material == null || thing == null || !BuildBounds.Local(thing, out var lb)) return;
            var line = Next();
            BuildBounds.CornersOf(lb, _corners);
            for (int i = 0; i < Path.Length; i++) _points[i] = thing.TransformPoint(_corners[Path[i]]);
            line.positionCount = _points.Length;
            line.SetPositions(_points);
            float distance = cam != null ? Vector3.Distance(cam.transform.position, thing.TransformPoint(lb.center)) : 10f;
            line.widthMultiplier = Mathf.Max(0.005f, distance * 0.003f);
            line.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            line.SetPropertyBlock(_block);
            line.enabled = true;
        }

        public void Finish()
        {
            for (int i = _used; i < _pool.Count; i++) if (_pool[i] != null) _pool[i].enabled = false;
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        private LineRenderer Next()
        {
            if (_used < _pool.Count) return _pool[_used++];
            var go = new GameObject("Box");
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.useWorldSpace = true;
            line.loop = false;
            line.numCornerVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            _pool.Add(line);
            _used++;
            return line;
        }
    }
}
