using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// Build Mode's screen: the toolbar (test-as buttons, grid, overlap, undo, redo, layouts), a status line, the key
    /// sheet (H), the shape panel for a selected shape, the catalogue strip and the box-select rectangle.
    /// Built in code at run time; it only calls BuildMode and shows what BuildMode reports.
    /// </summary>
    public sealed class BuildUI
    {
        private const string KeySheet =
            "<b>Camera</b>  WASD / arrows pan · Q E turn · wheel zoom · middle-drag orbit · right-drag pan · Shift faster\n" +
            "<b>Place</b>  click a card, click the room (Shift: keep placing) · R turn 15° (Ctrl 1°, Shift back) · Alt+wheel turn\n" +
            "      [ ] or Ctrl+wheel raise / lower · G grid · O allow overlap · right-click or Esc cancel\n" +
            "<b>Edit</b>  click select (Shift/Ctrl add) · drag empty floor = box select · drag a thing = move (hold X or Z: one axis)\n" +
            "      R turn · + − scale · [ ] raise · Ctrl+D copy · Delete or Backspace remove · F frame · Ctrl+Z undo · Ctrl+Y redo\n" +
            "<b>Shapes</b>  1 2 width · 3 4 height · 5 6 depth · 7 8 steps (Shift ×5, Ctrl ×¼) · C colour\n" +
            "<b>Mode</b>  F3 build on / off · Esc back · Tab hide this UI · H this sheet";

        private readonly BuildMode _mode;
        private readonly GameObject _canvasGo;
        private readonly Canvas _canvas;
        private readonly BuildCataloguePanel _catalogue;
        private readonly BuildLayoutsPanel _layouts;
        private readonly TextMeshProUGUI _status, _sheet;
        private readonly Button _grid, _overlap, _undo, _redo;
        private readonly TextMeshProUGUI _gridLabel, _overlapLabel;
        private readonly RectTransform _box, _shapePanel, _toolbar;
        private readonly TextMeshProUGUI _shapeTitle;
        private readonly TextMeshProUGUI[] _shapeValues = new TextMeshProUGUI[4];
        private readonly GameObject _stepsRow;
        private bool _hidden;

        public bool LayoutsOpen => _layouts.Visible;

        public BuildUI(BuildMode mode, BuildCatalog catalog)
        {
            _mode = mode;
            _canvasGo = new GameObject("BuildCanvas", typeof(RectTransform));
            _canvas = _canvasGo.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 40;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();
            var root = _canvasGo.transform;

            // toolbar
            _toolbar = BuildUiKit.Rect("Toolbar", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(1500f, 48f));
            var row = _toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 6f; row.childControlWidth = false; row.childControlHeight = true; row.childForceExpandWidth = false;
            Tool("Play as toy", 170f, () => _mode.TestAs(ShiftSize.Toy, false), out _);
            Tool("Play as kid", 170f, () => _mode.TestAs(ShiftSize.Kid, false), out _);
            Tool("First person", 170f, () => _mode.TestAs(ShiftSize.Toy, true), out _);
            Spacer(row.transform, 18f);
            _grid = Tool("Grid", 130f, _mode.ToggleSnap, out _gridLabel);
            _overlap = Tool("Overlap", 150f, _mode.ToggleOverlap, out _overlapLabel);
            Spacer(row.transform, 18f);
            _undo = Tool("Undo", 110f, _mode.Undo, out _);
            _redo = Tool("Redo", 110f, _mode.Redo, out _);
            Spacer(row.transform, 18f);
            Tool("Layouts", 140f, ToggleLayouts, out _);
            Tool("Keys (H)", 130f, ToggleSheet, out _);

            // status line and key sheet
            var statusRt = BuildUiKit.Rect("Status", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -68f), new Vector2(-32f, 34f));
            _status = BuildUiKit.Label(statusRt, "", 22, TextAlignmentOptions.MidlineLeft);
            var sheetRt = BuildUiKit.Rect("Keys", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -108f), new Vector2(1240f, 236f));
            BuildUiKit.Panel(sheetRt, BuildUiKit.PanelColor);
            _sheet = BuildUiKit.Label(BuildUiKit.Fill("Text", sheetRt, 16f, 10f, 16f, 10f), KeySheet, 20, TextAlignmentOptions.TopLeft);
            _sheet.textWrappingMode = TextWrappingModes.Normal;
            _sheet.lineSpacing = 12f;
            sheetRt.gameObject.SetActive(false);

            // shape panel
            _shapePanel = BuildUiKit.Rect("Shape", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 150f), new Vector2(330f, 330f));
            BuildUiKit.Panel(_shapePanel, BuildUiKit.PanelColor);
            var titleRt = BuildUiKit.Rect("Title", _shapePanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-24f, 40f));
            _shapeTitle = BuildUiKit.Label(titleRt, "Shape", 26, TextAlignmentOptions.MidlineLeft);
            string[] names = { "Width", "Height", "Depth", "Steps" };
            for (int i = 0; i < 4; i++)
            {
                int axis = i;
                var r = ShapeRow(names[i], -56f - i * 50f, () => Nudge(axis, -1), () => Nudge(axis, 1), out _shapeValues[i]);
                if (i == 3) _stepsRow = r;
            }
            var swatches = BuildUiKit.Rect("Colours", _shapePanel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-24f, 40f));
            var swRow = swatches.gameObject.AddComponent<HorizontalLayoutGroup>();
            swRow.spacing = 6f; swRow.childControlWidth = true; swRow.childControlHeight = true; swRow.childForceExpandWidth = true;
            foreach (var c in BuildSelection.Colors)
            {
                var colour = c;
                var b = BuildUiKit.Button(swatches, "", 10, () => _mode.Selection.SetShapeColor(colour), out _);
                BuildUiKit.SetFill(b, colour);
            }
            _shapePanel.gameObject.SetActive(false);

            _catalogue = new BuildCataloguePanel(root, catalog, _mode.PickEntry);
            _layouts = new BuildLayoutsPanel(root, _mode.SaveLayout, _mode.LoadLayout, BuildLayouts.Delete, _mode.StartEmpty);

            _box = BuildUiKit.Rect("BoxSelect", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var boxImg = BuildUiKit.Panel(_box, new Color(0.35f, 0.85f, 1f, 0.15f)); boxImg.raycastTarget = false;
            var outline = _box.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0.35f, 0.85f, 1f, 0.9f); outline.effectDistance = new Vector2(2f, -2f);
            _box.gameObject.SetActive(false);

            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _canvasGo.SetActive(visible);
            if (!visible) _layouts.Hide();
        }

        public void ToggleHidden()
        {
            _hidden = !_hidden;
            _toolbar.gameObject.SetActive(!_hidden);
            _status.gameObject.SetActive(!_hidden);
            _catalogue.SetVisible(!_hidden);
            if (_hidden) { _sheet.transform.parent.gameObject.SetActive(false); _shapePanel.gameObject.SetActive(false); _layouts.Hide(); }
        }

        public void ToggleSheet() { var p = _sheet.transform.parent.gameObject; p.SetActive(!p.activeSelf); }
        public void ToggleLayouts() { if (_layouts.Visible) _layouts.Hide(); else _layouts.Show(); }
        public void CloseLayouts() => _layouts.Hide();

        public void Destroy() { if (_canvasGo != null) Object.Destroy(_canvasGo); }

        /// <summary>Called by BuildMode once a frame while building.</summary>
        public void Refresh(string status)
        {
            _layouts.Tick();
            _status.text = status;
            var o = _mode.Options;
            _gridLabel.text = o.snap ? "Grid: on" : "Grid: off";
            BuildUiKit.SetFill(_grid, o.snap ? BuildUiKit.ActiveColor : BuildUiKit.ButtonColor);
            _overlapLabel.text = o.allowOverlap ? "Overlap: ok" : "Overlap: no";
            BuildUiKit.SetFill(_overlap, o.allowOverlap ? BuildUiKit.WarnColor : BuildUiKit.ButtonColor);
            _undo.interactable = _mode.History.CanUndo;
            _redo.interactable = _mode.History.CanRedo;
            _catalogue.Highlight(_mode.Placer.Entry);

            var shape = _mode.Selection.SingleShape;
            bool showShape = shape != null && !_hidden && !_mode.Placer.Active;
            if (_shapePanel.gameObject.activeSelf != showShape) _shapePanel.gameObject.SetActive(showShape);
            if (showShape)
            {
                _shapeTitle.text = "<b>" + shape.Kind + "</b>";
                _shapeValues[0].text = shape.Size.x.ToString("0.##");
                _shapeValues[1].text = shape.Size.y.ToString("0.##");
                _shapeValues[2].text = shape.Size.z.ToString("0.##");
                _shapeValues[3].text = shape.Steps.ToString();
                _stepsRow.SetActive(shape.Kind == ShapeKind.Stairs);
            }

            var sel = _mode.Selection;
            if (_box.gameObject.activeSelf != sel.Boxing) _box.gameObject.SetActive(sel.Boxing);
            if (sel.Boxing)
            {
                float k = Mathf.Max(0.0001f, _canvas.scaleFactor);
                _box.anchoredPosition = sel.BoxRect.position / k;
                _box.sizeDelta = sel.BoxRect.size / k;
            }
        }

        private void Nudge(int axis, int dir)
        {
            float step = BuildInput.Ctrl ? 0.25f : BuildInput.Shift ? 5f : 1f;
            if (axis == 3) { _mode.Selection.ResizeShape(Vector3.zero, dir); return; }
            var d = Vector3.zero; d[axis] = step * dir;
            _mode.Selection.ResizeShape(d, 0);
        }

        private Button Tool(string label, float width, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI text)
        {
            var b = BuildUiKit.Button(_toolbar, label, 22, onClick, out text);
            ((RectTransform)b.transform).sizeDelta = new Vector2(width, 48f);
            return b;
        }

        private static void Spacer(Transform parent, float width)
        {
            var rt = new GameObject("Gap", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.sizeDelta = new Vector2(width, 10f);
        }

        private GameObject ShapeRow(string name, float y, UnityEngine.Events.UnityAction less, UnityEngine.Events.UnityAction more, out TextMeshProUGUI value)
        {
            var row = BuildUiKit.Rect(name, _shapePanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(-24f, 44f));
            BuildUiKit.Label(BuildUiKit.Fill("Name", row, 0f, 0f, 190f, 0f), name, 22, TextAlignmentOptions.MidlineLeft);
            var minus = BuildUiKit.Button(row, "−", 26, less, out _);
            var mrt = (RectTransform)minus.transform; mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1f, 0.5f); mrt.anchoredPosition = new Vector2(-136f, 0f); mrt.sizeDelta = new Vector2(48f, 40f);
            var valueRt = BuildUiKit.Rect("Value", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-54f, 0f), new Vector2(80f, 40f));
            value = BuildUiKit.Label(valueRt, "", 22);
            var plus = BuildUiKit.Button(row, "+", 26, more, out _);
            var prt = (RectTransform)plus.transform; prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0.5f); prt.anchoredPosition = Vector2.zero; prt.sizeDelta = new Vector2(48f, 40f);
            return row.gameObject;
        }
    }
}
