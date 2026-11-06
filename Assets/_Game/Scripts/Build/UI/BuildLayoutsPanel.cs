using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The layouts window: save the room under a name, and load or delete saved ones. Anything that throws work away
    /// (load, delete, overwrite, start empty) asks twice: the button turns into "Sure?" for a few seconds.
    /// </summary>
    public sealed class BuildLayoutsPanel
    {
        private const float ConfirmSeconds = 3f;

        private readonly Func<string, string> _save;      // returns a message
        private readonly Func<string, string> _load;
        private readonly Action<string> _delete;
        private readonly Func<string> _startEmpty;
        private readonly RectTransform _root;
        private readonly RectTransform _list;
        private readonly TMP_InputField _name;
        private readonly TextMeshProUGUI _message;
        private string _pendingKey;
        private float _pendingUntil;
        private TextMeshProUGUI _pendingLabel;
        private string _pendingText;

        public bool Visible => _root.gameObject.activeSelf;

        public BuildLayoutsPanel(Transform canvas, Func<string, string> save, Func<string, string> load, Action<string> delete, Func<string> startEmpty)
        {
            _save = save; _load = load; _delete = delete; _startEmpty = startEmpty;
            _root = BuildUiKit.Rect("Layouts", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(780f, 640f));
            BuildUiKit.Panel(_root, new Color(0.05f, 0.06f, 0.09f, 0.96f));

            var title = BuildUiKit.Rect("Title", _root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-40f, 44f));
            BuildUiKit.Label(title, "<b>Layouts</b>", 32, TextAlignmentOptions.MidlineLeft);
            var close = BuildUiKit.Button(_root, "Close", 22, Hide, out _);
            Place(close, new Vector2(1f, 1f), new Vector2(-20f, -14f), new Vector2(120f, 44f));

            var nameRt = BuildUiKit.Rect("Name", _root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -72f), new Vector2(520f, 50f));
            _name = BuildUiKit.Input(nameRt, "layout name", 24);
            var nrt = (RectTransform)_name.transform; nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one; nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            Button saveButton = null;
            saveButton = BuildUiKit.Button(_root, "Save", 24, () => OnSave(saveButton), out _);
            Place(saveButton, new Vector2(1f, 1f), new Vector2(-20f, -72f), new Vector2(200f, 50f));

            var listArea = BuildUiKit.Fill("List", _root, 20f, 120f, 20f, 140f);
            BuildUiKit.Panel(listArea, new Color(1f, 1f, 1f, 0.04f));
            _list = BuildUiKit.ScrollView(listArea, out _);
            var column = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 6f; column.padding = new RectOffset(8, 8, 8, 8);
            column.childControlHeight = false; column.childControlWidth = true; column.childForceExpandHeight = false; column.childForceExpandWidth = true;

            Button empty = null;
            empty = BuildUiKit.Button(_root, "Start empty", 22, () => Confirm("empty", empty, () => Say(_startEmpty())), out _);
            Place(empty, new Vector2(0f, 0f), new Vector2(20f, 64f), new Vector2(220f, 44f), new Vector2(0f, 0f));

            var msg = BuildUiKit.Rect("Message", _root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-40f, 40f));
            _message = BuildUiKit.Label(msg, "", 20, TextAlignmentOptions.MidlineLeft);
            _message.color = new Color(1f, 1f, 1f, 0.75f);
            _root.gameObject.SetActive(false);
        }

        public void Show()
        {
            _root.gameObject.SetActive(true);
            _message.text = "Saved in " + BuildLayouts.Folder;
            Refresh();
        }

        public void Hide()
        {
            ClearPending();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Call every frame while shown: lets a "Sure?" lapse back.</summary>
        public void Tick()
        {
            if (_pendingKey != null && Time.unscaledTime > _pendingUntil) ClearPending();
        }

        private void Refresh()
        {
            BuildUiKit.Clear(_list);
            var names = BuildLayouts.List();
            if (names.Count == 0)
            {
                var none = BuildUiKit.Rect("None", _list, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 50f));
                BuildUiKit.Label(none, "<alpha=#88>nothing saved yet", 22);
                return;
            }
            foreach (var n in names) Row(n);
        }

        private void Row(string layout)
        {
            var row = BuildUiKit.Rect(layout, _list, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 50f));
            BuildUiKit.Panel(row, new Color(1f, 1f, 1f, 0.05f));
            var labelRt = BuildUiKit.Fill("Name", row, 14f, 0f, 260f, 0f);
            BuildUiKit.Label(labelRt, layout, 24, TextAlignmentOptions.MidlineLeft);
            Button load = null, delete = null;
            load = BuildUiKit.Button(row, "Load", 22, () => Confirm("load:" + layout, load, () => { Say(_load(layout)); _name.text = layout; Refresh(); }), out _);
            Place(load, new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(116f, 40f), new Vector2(1f, 0.5f));
            delete = BuildUiKit.Button(row, "Delete", 22, () => Confirm("delete:" + layout, delete, () => { _delete(layout); Say($"Deleted '{layout}'."); Refresh(); }), out _);
            Place(delete, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(116f, 40f), new Vector2(1f, 0.5f));
        }

        private void OnSave(Button button)
        {
            string name = BuildLayouts.Clean(_name.text);
            _name.text = name;
            if (System.IO.File.Exists(BuildLayouts.PathOf(name))) Confirm("save:" + name, button, () => { Say(_save(name)); Refresh(); });
            else { Say(_save(name)); Refresh(); }
        }

        /// <summary>First click arms the button ("Sure?"); a second click on the same button within a few seconds runs it.</summary>
        private void Confirm(string key, Button button, Action run)
        {
            if (_pendingKey == key && Time.unscaledTime <= _pendingUntil) { ClearPending(); run(); return; }
            ClearPending();
            _pendingKey = key; _pendingUntil = Time.unscaledTime + ConfirmSeconds;
            _pendingLabel = button.GetComponentInChildren<TextMeshProUGUI>();
            _pendingText = _pendingLabel.text;
            _pendingLabel.text = "Sure?";
            BuildUiKit.SetFill(button, BuildUiKit.WarnColor);
        }

        private void ClearPending()
        {
            if (_pendingLabel != null)
            {
                _pendingLabel.text = _pendingText;
                var b = _pendingLabel.GetComponentInParent<Button>();
                if (b != null) BuildUiKit.SetFill(b, BuildUiKit.ButtonColor);
            }
            _pendingKey = null; _pendingLabel = null; _pendingText = null;
        }

        private void Say(string text) => _message.text = text;

        private static void Place(Button b, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = position; rt.sizeDelta = size;
        }
    }
}
