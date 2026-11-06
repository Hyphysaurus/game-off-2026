using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Placing from the catalogue: a see-through ghost follows the mouse and rests on whatever surface is under it,
    /// green when it fits, red when it would sit inside something (amber when overlap is allowed). Left-click places
    /// (Shift keeps the ghost for more); right-click or Escape cancels. R turns 15° (Ctrl 1°, Shift back),
    /// Alt+wheel turns too; [ / ] or Ctrl+wheel raise and lower; G (in Build Mode) toggles the grid.
    /// </summary>
    public sealed class BuildPlacer
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Fits = new Color(0.3f, 1f, 0.45f, 0.45f);
        private static readonly Color Overlaps = new Color(1f, 0.25f, 0.2f, 0.45f);
        private static readonly Color OverlapAllowed = new Color(1f, 0.7f, 0.2f, 0.45f);

        private readonly BuildWorld _world;
        private readonly BuildHistory _history;
        private readonly BuildSelection _selection;
        private readonly BuildCamera _camera;
        private readonly BuildOptions _options;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        private GameObject _ghost;
        private Renderer[] _ghostRenderers;
        private float _yaw, _raise, _grid = 1f;
        private bool _shown = true;

        public BuildCatalog.Entry Entry { get; private set; }
        public bool Active => Entry != null;
        public bool Valid { get; private set; }
        public float Grid => _grid;
        public float Raise => _raise;
        public float Yaw => _yaw;

        public BuildPlacer(BuildWorld world, BuildHistory history, BuildSelection selection, BuildCamera camera, BuildOptions options)
        {
            _world = world; _history = history; _selection = selection; _camera = camera; _options = options;
        }

        public void Begin(BuildCatalog.Entry entry)
        {
            Cancel();
            if (entry == null || (!entry.IsShape && entry.prefab == null)) return;
            Entry = entry;
            _ghost = _world.MakeGhost(entry);
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
            _ghost.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _grid = BuildBounds.World(_ghost, out var b) ? BuildBounds.GridFor(b) : 1f;
            _raise = 0f;
            _shown = true;
            _selection.Clear();
        }

        public void Cancel()
        {
            if (_ghost != null) Object.Destroy(_ghost);
            _ghost = null; _ghostRenderers = null; Entry = null;
        }

        public void Tick()
        {
            if (!Active || _ghost == null) return;
            if (BuildInput.OverUI) { Show(false); return; }
            Show(true);

            float turn = BuildInput.Ctrl ? 1f : 15f;
            if (BuildInput.Pressed(Key.R)) _yaw += BuildInput.Shift ? -turn : turn;
            float wheel = BuildInput.WheelNotches;
            if (Mathf.Abs(wheel) > 0.001f)
            {
                if (BuildInput.Alt) _yaw += Mathf.Sign(wheel) * turn;
                else if (BuildInput.Ctrl) _raise += Mathf.Sign(wheel) * RaiseStep;
            }
            if (BuildInput.Pressed(Key.RightBracket)) _raise += RaiseStep;
            if (BuildInput.Pressed(Key.LeftBracket)) _raise -= RaiseStep;

            var t = _ghost.transform;
            t.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_world.Surface(_camera.Camera, BuildInput.MousePosition, null, _camera.Focus.y, out var point, out var normal))
            {
                BuildBounds.Rest(t, point, normal);
                if (_options.snap) BuildBounds.SnapToGrid(t, _grid);
                t.position += Vector3.up * _raise;
            }
            Physics.SyncTransforms();

            Valid = !BuildBounds.Blocked(t, null, _world.Player);
            Tint(Valid ? Fits : _options.allowOverlap ? OverlapAllowed : Overlaps);

            var mouse = BuildInput.Pointer;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && (Valid || _options.allowOverlap)) Place();
        }

        private float RaiseStep => _options.snap ? _grid : _grid * 0.25f;

        private void Place()
        {
            var t = _ghost.transform;
            var placed = _world.Spawn(Entry, t.position, t.rotation);
            placed.TellMoved();   // its home is where it landed, at the scale it was given after waking up
            _history.RecordCreated(new[] { placed });
            if (BuildInput.Shift) return;   // keep placing
            Cancel();
            _selection.Set(placed);
        }

        private void Show(bool shown)
        {
            if (_shown == shown || _ghostRenderers == null) return;
            _shown = shown;
            foreach (var r in _ghostRenderers) if (r != null) r.enabled = shown;
        }

        private void Tint(Color color)
        {
            foreach (var r in _ghostRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
