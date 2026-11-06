using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// Selecting and editing what is already in the room. Click selects (Shift/Ctrl+click adds or removes), dragging on
    /// empty floor draws a selection box, dragging a selected thing moves the whole selection over surfaces (hold X or Z
    /// to slide along that world axis only). Keys: R turn 15° (Ctrl 1°, Shift back), + / - scale, [ / ] raise and lower,
    /// Ctrl+D copy, Delete remove, F frame the selection; shapes also take 1-8 (size and steps) and C (colour).
    /// Every edit goes through the history, so Ctrl+Z / Ctrl+Y work on all of it.
    /// </summary>
    public sealed class BuildSelection
    {
        private enum Press { None, OnThing, OnEmpty, Moving, Boxing }

        private const float DragSlop = 5f;
        private static readonly Color SelectedColor = new Color(0.3f, 0.85f, 1f, 0.95f);
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color[] Palette =
        {
            new Color(0.86f, 0.84f, 0.8f), new Color(0.95f, 0.55f, 0.35f), new Color(0.98f, 0.82f, 0.3f), new Color(0.45f, 0.75f, 0.4f),
            new Color(0.35f, 0.6f, 0.95f), new Color(0.7f, 0.5f, 0.9f), new Color(0.95f, 0.5f, 0.7f), new Color(0.35f, 0.32f, 0.3f),
        };

        private readonly BuildWorld _world;
        private readonly BuildHistory _history;
        private readonly BuildCamera _camera;
        private readonly BuildOptions _options;
        private readonly BuildOutlines _outlines;
        private readonly List<Buildable> _items = new List<Buildable>();
        private readonly HashSet<Transform> _ignore = new HashSet<Transform>();

        private Press _press;
        private Vector2 _pressAt;
        private bool _additive;
        private Buildable _primary;
        private Vector3[] _startPositions;
        private bool[][] _held;
        private Vector3 _primaryStart, _planeStart;
        private float _planeY;

        public IReadOnlyList<Buildable> Items => _items;
        public Buildable Hover { get; private set; }
        public bool Boxing => _press == Press.Boxing;
        /// <summary>True while a drag-move is under way: undo and other edits wait for it to finish.</summary>
        public bool Busy => _press == Press.Moving;
        /// <summary>The selection box in screen pixels while dragging one.</summary>
        public Rect BoxRect { get; private set; }
        public static IReadOnlyList<Color> Colors => Palette;
        /// <summary>Raised when what is selected changes.</summary>
        public event Action Changed;

        public BuildSelection(BuildWorld world, BuildHistory history, BuildCamera camera, BuildOptions options, BuildOutlines outlines)
        {
            _world = world; _history = history; _camera = camera; _options = options; _outlines = outlines;
        }

        // ------------------------------------------------------------------ the set

        public void Set(Buildable b) { _items.Clear(); if (b != null) _items.Add(b); Changed?.Invoke(); }
        public void Set(IEnumerable<Buildable> list) { _items.Clear(); foreach (var b in list) if (b != null && !_items.Contains(b)) _items.Add(b); Changed?.Invoke(); }
        public void Clear() { if (_items.Count == 0) return; _items.Clear(); Changed?.Invoke(); }

        private void Toggle(Buildable b)
        {
            if (!_items.Remove(b)) _items.Add(b);
            Changed?.Invoke();
        }

        /// <summary>Drops things that were deleted, undone away or destroyed.</summary>
        public void Prune()
        {
            if (_items.RemoveAll(b => b == null || !b.gameObject.activeInHierarchy) > 0) Changed?.Invoke();
        }

        /// <summary>The one selected shape, or null.</summary>
        public BuildShape SingleShape => _items.Count == 1 && _items[0] != null ? _items[0].Shape : null;

        // ------------------------------------------------------------------ per frame

        /// <summary>Mouse and keys. Call only while not placing and not typing.</summary>
        public void Tick()
        {
            Prune();
            var mouse = BuildInput.Pointer;
            if (mouse == null) return;
            Vector2 at = BuildInput.MousePosition;
            bool overUI = BuildInput.OverUI;
            Hover = overUI || _press == Press.Moving ? null : _world.Pick(_camera.Camera, at);

            if (mouse.leftButton.wasPressedThisFrame && !overUI)
            {
                _pressAt = at;
                _additive = BuildInput.Shift || BuildInput.Ctrl;
                if (Hover != null)
                {
                    if (_additive) { Toggle(Hover); _press = Press.None; }
                    else
                    {
                        if (!_items.Contains(Hover)) Set(Hover);
                        _primary = Hover;
                        _press = Press.OnThing;
                    }
                }
                else _press = Press.OnEmpty;
            }

            bool moved = (at - _pressAt).magnitude > DragSlop;
            if (_press == Press.OnThing && mouse.leftButton.isPressed && moved) StartMove();
            if (_press == Press.OnEmpty && mouse.leftButton.isPressed && moved) _press = Press.Boxing;
            if (_press == Press.Moving) DragMove(at);
            if (_press == Press.Boxing) BoxRect = Rect.MinMaxRect(Mathf.Min(_pressAt.x, at.x), Mathf.Min(_pressAt.y, at.y), Mathf.Max(_pressAt.x, at.x), Mathf.Max(_pressAt.y, at.y));

            if (mouse.leftButton.wasReleasedThisFrame || (!mouse.leftButton.isPressed && _press != Press.None))
            {
                if (_press == Press.Moving) EndMove();
                else if (_press == Press.Boxing) SelectInBox();
                else if (_press == Press.OnEmpty && !_additive) Clear();
                else if (_press == Press.OnThing && _items.Count > 1) Set(_primary);   // a plain click on one of a group picks just it
                _press = Press.None;
            }

            if (_press != Press.Moving) Keys();
        }

        /// <summary>Forgets the hover box (while placing, the ghost is the only thing under the mouse).</summary>
        public void ClearHover() => Hover = null;

        /// <summary>Stops a move in progress (Escape, leaving Build Mode) and keeps where things are.</summary>
        public void Interrupt()
        {
            if (_press == Press.Moving) EndMove();
            _press = Press.None;
        }

        public void DrawOutlines()
        {
            _outlines.BeginFrame();
            var cam = _camera.Camera;
            foreach (var b in _items) if (b != null) _outlines.Draw(b.transform, SelectedColor, cam);
            if (Hover != null && !_items.Contains(Hover)) _outlines.Draw(Hover.transform, HoverColor, cam);
            _outlines.Finish();
        }

        // ------------------------------------------------------------------ moving

        private void StartMove()
        {
            _press = Press.Moving;
            if (_primary == null || !_items.Contains(_primary)) _primary = _items.Count > 0 ? _items[0] : null;
            if (_primary == null) { _press = Press.None; return; }
            _history.Begin(_items);
            _ignore.Clear();
            _startPositions = new Vector3[_items.Count];
            _held = new bool[_items.Count][];
            for (int i = 0; i < _items.Count; i++)
            {
                _ignore.Add(_items[i].transform);
                _startPositions[i] = _items[i].transform.position;
                _held[i] = _items[i].Hold();
            }
            _primaryStart = _primary.transform.position;
            _planeY = BuildBounds.World(_primary.gameObject, out var b) ? b.min.y : _primaryStart.y;
            _planeStart = PlanePoint(_pressAt);
        }

        private void DragMove(Vector2 at)
        {
            if (_primary == null) return;
            float grid = GridOf(_primary);
            Vector3 delta;
            bool lockX = BuildInput.Held(Key.X), lockZ = BuildInput.Held(Key.Z);
            if (lockX || lockZ)
            {
                Vector3 d = PlanePoint(at) - _planeStart;
                d = lockX ? new Vector3(d.x, 0f, 0f) : new Vector3(0f, 0f, d.z);
                if (_options.snap) { d.x = Mathf.Round(d.x / grid) * grid; d.z = Mathf.Round(d.z / grid) * grid; }
                delta = d;
            }
            else
            {
                var t = _primary.transform;
                t.position = _primaryStart;
                if (!_world.Surface(_camera.Camera, at, _ignore, _planeY, out var point, out var normal)) return;
                BuildBounds.Rest(t, point, normal);
                if (_options.snap) BuildBounds.SnapToGrid(t, grid);
                delta = t.position - _primaryStart;
            }
            for (int i = 0; i < _items.Count; i++) if (_items[i] != null) _items[i].transform.position = _startPositions[i] + delta;
            Physics.SyncTransforms();
        }

        private void EndMove()
        {
            for (int i = 0; i < _items.Count && i < _held.Length; i++) if (_items[i] != null) _items[i].Release(_held[i]);
            _history.Commit();
            _held = null; _startPositions = null;
            _press = Press.None;
        }

        private Vector3 PlanePoint(Vector2 screen)
        {
            Ray ray = _camera.Camera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0f, _planeY, 0f));
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : _primaryStart;
        }

        private void SelectInBox()
        {
            var cam = _camera.Camera;
            var picked = _additive ? new List<Buildable>(_items) : new List<Buildable>();
            foreach (var b in Buildable.All)
            {
                if (b == null || !b.gameObject.activeInHierarchy || _world.IsPlayer(b.transform)) continue;
                if (!BuildBounds.World(b.gameObject, out var bounds)) continue;
                Vector3 s = cam.WorldToScreenPoint(bounds.center);
                if (s.z > 0f && BoxRect.Contains(new Vector2(s.x, s.y)) && !picked.Contains(b)) picked.Add(b);
            }
            Set(picked);
        }

        // ------------------------------------------------------------------ keys and commands

        private void Keys()
        {
            if (_items.Count == 0) return;
            bool ctrl = BuildInput.Ctrl, shift = BuildInput.Shift;
            if (BuildInput.Pressed(Key.Delete) || BuildInput.Pressed(Key.Backspace)) { Delete(); return; }
            if (ctrl && BuildInput.Pressed(Key.D)) { Duplicate(); return; }
            if (BuildInput.Pressed(Key.F)) Frame();

            float turn = ctrl ? 1f : 15f;
            if (BuildInput.Pressed(Key.R)) Rotate(shift ? -turn : turn);
            float wheel = BuildInput.WheelNotches;
            if (Mathf.Abs(wheel) > 0.001f && !BuildInput.OverUI)
            {
                if (BuildInput.Alt) Rotate(Mathf.Sign(wheel) * turn);
                else if (ctrl) Raise(Mathf.Sign(wheel));
            }
            if (BuildInput.Pressed(Key.RightBracket)) Raise(shift ? 5f : 1f);
            if (BuildInput.Pressed(Key.LeftBracket)) Raise(shift ? -5f : -1f);

            float scaleStep = ctrl ? 1.01f : 1.1f;
            if (BuildInput.Pressed(Key.Equals) || BuildInput.Pressed(Key.NumpadPlus)) Scale(scaleStep);
            if (BuildInput.Pressed(Key.Minus) || BuildInput.Pressed(Key.NumpadMinus)) Scale(1f / scaleStep);

            if (SingleShape == null) return;
            float step = ctrl ? 0.25f : shift ? 5f : 1f;
            if (BuildInput.Pressed(Key.Digit1)) ResizeShape(new Vector3(-step, 0f, 0f), 0);
            if (BuildInput.Pressed(Key.Digit2)) ResizeShape(new Vector3(step, 0f, 0f), 0);
            if (BuildInput.Pressed(Key.Digit3)) ResizeShape(new Vector3(0f, -step, 0f), 0);
            if (BuildInput.Pressed(Key.Digit4)) ResizeShape(new Vector3(0f, step, 0f), 0);
            if (BuildInput.Pressed(Key.Digit5)) ResizeShape(new Vector3(0f, 0f, -step), 0);
            if (BuildInput.Pressed(Key.Digit6)) ResizeShape(new Vector3(0f, 0f, step), 0);
            if (BuildInput.Pressed(Key.Digit7)) ResizeShape(Vector3.zero, -1);
            if (BuildInput.Pressed(Key.Digit8)) ResizeShape(Vector3.zero, 1);
            if (BuildInput.Pressed(Key.C)) CycleColor(shift ? -1 : 1);
        }

        public void Delete()
        {
            if (_items.Count == 0) return;
            _history.Begin(_items);
            foreach (var b in _items) if (b != null) b.gameObject.SetActive(false);
            _history.Commit();
            Clear();
        }

        public void Duplicate()
        {
            if (_items.Count == 0 || !GroupBounds(out var group)) return;
            Vector3 right = _camera.Camera.transform.right; right.y = 0f; right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
            float reach = Mathf.Abs(right.x) * group.size.x + Mathf.Abs(right.z) * group.size.z;
            Vector3 offset = right * (reach + GridOf(_items[0]));
            var copies = new List<Buildable>();
            foreach (var b in _items)
            {
                if (b == null) continue;
                var copy = _world.Duplicate(b);
                copy.transform.position += offset;
                copy.StopBodies();
                copies.Add(copy);
            }
            _history.RecordCreated(copies);
            Set(copies);
        }

        public void Rotate(float degrees)
        {
            if (_items.Count == 0 || !GroupBounds(out var group)) return;
            _history.Begin(_items);
            foreach (var b in _items) if (b != null) b.transform.RotateAround(group.center, Vector3.up, degrees);
            Settle();
        }

        public void Raise(float steps)
        {
            if (_items.Count == 0) return;
            float step = (_options.snap ? GridOf(_items[0]) : GridOf(_items[0]) * 0.25f) * steps;
            _history.Begin(_items);
            foreach (var b in _items) if (b != null) b.transform.position += Vector3.up * step;
            Settle();
        }

        /// <summary>Uniform scale; each thing keeps its footprint centre and its bottom where they were.</summary>
        public void Scale(float factor)
        {
            if (_items.Count == 0) return;
            _history.Begin(_items);
            foreach (var b in _items)
            {
                if (b == null || !BuildBounds.World(b.gameObject, out var before)) continue;
                var t = b.transform;
                Vector3 s = t.localScale * factor;
                if (Mathf.Abs(s.x) < 0.01f || Mathf.Abs(s.x) > 1000f) continue;
                t.localScale = s;
                BuildBounds.World(b.gameObject, out var after);
                t.position += new Vector3(before.center.x - after.center.x, before.min.y - after.min.y, before.center.z - after.center.z);
            }
            Settle();
        }

        /// <summary>Grows or shrinks the selected shape by <paramref name="size"/> (world units) and its steps by <paramref name="steps"/>.</summary>
        public void ResizeShape(Vector3 size, int steps)
        {
            var shape = SingleShape;
            if (shape == null) return;
            _history.Begin(_items);
            shape.Set(shape.Kind, shape.Size + size, shape.Steps + steps, shape.Color);
            Settle();
        }

        public void SetShapeColor(Color color)
        {
            var shapes = new List<Buildable>();
            foreach (var b in _items) if (b != null && b.Shape != null) shapes.Add(b);
            if (shapes.Count == 0) return;
            _history.Begin(shapes);
            foreach (var b in shapes) b.Shape.Set(b.Shape.Kind, b.Shape.Size, b.Shape.Steps, color);
            Settle();
        }

        private void CycleColor(int dir)
        {
            var shape = SingleShape;
            int at = 0;
            for (int i = 0; i < Palette.Length; i++) if (Palette[i] == shape.Color) { at = i; break; }
            SetShapeColor(Palette[(at + dir + Palette.Length) % Palette.Length]);
        }

        public void Frame()
        {
            if (GroupBounds(out var group)) _camera.FocusOn(group.center, group.size.magnitude);
        }

        private void Settle()
        {
            foreach (var b in _items) if (b != null) b.StopBodies();
            _history.Commit();
        }

        private bool GroupBounds(out Bounds group)
        {
            group = default;
            bool any = false;
            foreach (var b in _items)
            {
                if (b == null || !BuildBounds.World(b.gameObject, out var one)) continue;
                if (!any) { group = one; any = true; } else group.Encapsulate(one);
            }
            return any;
        }

        private static float GridOf(Buildable b) => b != null && BuildBounds.World(b.gameObject, out var bounds) ? BuildBounds.GridFor(bounds) : 1f;
    }
}
