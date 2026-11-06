using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The catalogue strip along the bottom: a tab per category that has entries, a search box (searches every tab),
    /// and a scrolling grid of picture cards. Clicking a card starts placing it.
    /// </summary>
    public sealed class BuildCataloguePanel
    {
        private const float Height = 330f;
        private static readonly Vector2 Cell = new Vector2(124f, 156f);

        private readonly BuildCatalog _catalog;
        private readonly Action<BuildCatalog.Entry> _pick;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private readonly TMP_InputField _search;
        private readonly List<(BuildCategory category, Button button)> _tabs = new List<(BuildCategory, Button)>();
        private readonly Dictionary<BuildCatalog.Entry, Button> _cards = new Dictionary<BuildCatalog.Entry, Button>();
        private BuildCategory _current;
        private BuildCatalog.Entry _highlighted;

        public RectTransform Root { get; }

        public BuildCataloguePanel(Transform canvas, BuildCatalog catalog, Action<BuildCatalog.Entry> pick)
        {
            _catalog = catalog; _pick = pick;
            Root = BuildUiKit.Rect("Catalogue", canvas, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, Height));
            BuildUiKit.Panel(Root, BuildUiKit.PanelColor);

            if (catalog == null || catalog.Entries.Count == 0)
            {
                BuildUiKit.Label(Root, "No catalogue yet: in the editor run  Shift Jam > Workshop > Build Catalog", 26);
                return;
            }

            var tabs = BuildUiKit.Rect("Tabs", Root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(-420f, 46f));
            var row = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 6f; row.childControlWidth = false; row.childControlHeight = true; row.childForceExpandWidth = false; row.childForceExpandHeight = true;
            bool first = true;
            foreach (BuildCategory c in Enum.GetValues(typeof(BuildCategory)))
            {
                int count = 0;
                foreach (var e in catalog.Entries) if (e != null && e.category == c) count++;
                if (count == 0) continue;
                var cat = c;
                var b = BuildUiKit.Button(tabs, $"{c}  <alpha=#88>{count}", 22, () => ShowTab(cat), out _);
                ((RectTransform)b.transform).sizeDelta = new Vector2(190f, 46f);
                _tabs.Add((c, b));
                if (first) { _current = c; first = false; }
            }

            var searchRt = BuildUiKit.Rect("Search", Root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -10f), new Vector2(390f, 46f));
            _search = BuildUiKit.Input(searchRt, "search everything…", 22);
            var srt = (RectTransform)_search.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            _search.onValueChanged.AddListener(_ => Rebuild());

            var area = BuildUiKit.Fill("Grid", Root, 12f, 10f, 12f, 66f);
            _content = BuildUiKit.ScrollView(area, out _scroll);
            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = Cell; grid.spacing = new Vector2(8f, 8f); grid.padding = new RectOffset(4, 4, 4, 4);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft; grid.childAlignment = TextAnchor.UpperLeft;
            ShowTab(_current);
        }

        public void SetVisible(bool visible) => Root.gameObject.SetActive(visible);

        /// <summary>Marks the card of the entry being placed (null = none).</summary>
        public void Highlight(BuildCatalog.Entry entry)
        {
            if (entry == _highlighted) return;
            if (_highlighted != null && _cards.TryGetValue(_highlighted, out var old)) BuildUiKit.SetFill(old, BuildUiKit.ButtonColor);
            _highlighted = entry;
            if (entry != null && _cards.TryGetValue(entry, out var now)) BuildUiKit.SetFill(now, BuildUiKit.ActiveColor);
        }

        private void ShowTab(BuildCategory category)
        {
            _current = category;
            if (_search != null && _search.text.Length > 0) _search.SetTextWithoutNotify("");
            Rebuild();
        }

        private void Rebuild()
        {
            if (_content == null) return;
            string query = _search != null ? _search.text.Trim() : "";
            bool searching = query.Length > 0;
            foreach (var (category, button) in _tabs) BuildUiKit.SetFill(button, !searching && category == _current ? BuildUiKit.ActiveColor : BuildUiKit.ButtonColor);

            BuildUiKit.Clear(_content);
            _cards.Clear();
            foreach (var e in _catalog.Entries)
            {
                if (e == null) continue;
                if (searching ? e.displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 : e.category != _current) continue;
                _cards[e] = Card(e);
            }
            var keep = _highlighted; _highlighted = null; Highlight(keep);
            _scroll.verticalNormalizedPosition = 1f;
        }

        private Button Card(BuildCatalog.Entry e)
        {
            var b = BuildUiKit.Button(_content, e.displayName, 15, () => _pick(e), out var label);
            label.textWrappingMode = TextWrappingModes.Normal;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(-8f, 38f); lrt.anchoredPosition = new Vector2(0f, 2f);
            if (e.thumbnail != null)
            {
                var pic = BuildUiKit.Rect("Thumb", b.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(112f, 112f));
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = e.thumbnail; raw.raycastTarget = false;
            }
            else
            {
                label.fontSize = 18;
                lrt.anchorMax = new Vector2(1f, 1f); lrt.sizeDelta = new Vector2(-8f, -8f); lrt.anchoredPosition = Vector2.zero; lrt.pivot = new Vector2(0.5f, 0.5f);
            }
            return b;
        }
    }
}
