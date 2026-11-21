using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The inventory's bar on screen (D-0108): a row of slots along the bottom middle, each with its key (1 to 4), the
    /// thing's picture and name. The one in hand is lit; an empty slot is faint; a slot the size cannot use is greyed
    /// with why (slots 3 and 4 are the kid's backpack; a kid-only thing at toy size). Hidden in Build Mode, menus, the
    /// key sheet and the console, and while the bar is empty. It only reads the Inventory. Made by the Inventory at
    /// start, built in code like the other HUDs.
    /// </summary>
    public sealed class HotbarHud : MonoBehaviour
    {
        private const string LabelFont = "HUD/Fredoka SDF";
        private const float Box = 96f, Gap = 10f, Bottom = 24f;
        private static readonly Color Empty = new Color(0f, 0f, 0f, 0.25f), Filled = new Color(0f, 0f, 0f, 0.45f), Lit = new Color(1f, 0.78f, 0.3f, 0.75f);

        private Inventory _inventory;
        private CanvasGroup _group;
        private Image[] _panels;
        private RawImage[] _icons;
        private TextMeshProUGUI[] _names, _keys, _notes;
        private CanvasGroup[] _slotGroups;

        public static HotbarHud Create(Inventory inventory)
        {
            var hud = new GameObject("HotbarHud").AddComponent<HotbarHud>();
            hud._inventory = inventory;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            var font = Resources.Load<TMP_FontAsset>(LabelFont);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 34;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;

            int n = _inventory.MostSlots;
            float width = n * Box + (n - 1) * Gap;
            var row = BuildUiKit.Rect("Row", canvasGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Bottom), new Vector2(width, Box));
            _panels = new Image[n]; _icons = new RawImage[n]; _names = new TextMeshProUGUI[n]; _keys = new TextMeshProUGUI[n]; _notes = new TextMeshProUGUI[n]; _slotGroups = new CanvasGroup[n];
            for (int i = 0; i < n; i++)
            {
                var slot = BuildUiKit.Rect("Slot" + (i + 1), row, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(i * (Box + Gap), 0f), new Vector2(Box, Box));
                _slotGroups[i] = slot.gameObject.AddComponent<CanvasGroup>();
                _panels[i] = BuildUiKit.Panel(slot, Empty); _panels[i].raycastTarget = false;
                var iconRt = BuildUiKit.Rect("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(Box * 0.62f, Box * 0.62f));
                _icons[i] = iconRt.gameObject.AddComponent<RawImage>(); _icons[i].raycastTarget = false;
                var keyRt = BuildUiKit.Rect("Key", slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -2f), new Vector2(24f, 24f));
                _keys[i] = BuildUiKit.Label(keyRt, (i + 1).ToString(), 20f, TextAlignmentOptions.TopLeft);
                var nameRt = BuildUiKit.Rect("Name", slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(Box - 6f, 22f));
                _names[i] = BuildUiKit.Label(nameRt, "", 15f, TextAlignmentOptions.Bottom);
                var noteRt = BuildUiKit.Rect("Note", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(Box - 6f, 40f));
                _notes[i] = BuildUiKit.Label(noteRt, "", 14f, TextAlignmentOptions.Center);
                if (font != null) { _keys[i].font = font; _names[i].font = font; _notes[i].font = font; }
                _names[i].enableAutoSizing = true; _names[i].fontSizeMin = 10f; _names[i].fontSizeMax = 15f;
            }
        }

        private void Update()
        {
            if (_inventory == null) { Destroy(gameObject); return; }   // the player is gone with its scene
            var items = _inventory.Items;
            bool show = _inventory.isActiveAndEnabled && items.Count > 0
                        && GameMode.Current != GameMode.Mode.Build && !PauseMenu.IsOpen && !TitleMenu.IsOpen && !KeyHelp.IsOpen && !DebugConsole.IsOpen;
            _group.alpha = show ? 1f : 0f;
            if (!show) return;
            int inHand = _inventory.InHand, usable = _inventory.Usable;
            for (int i = 0; i < _panels.Length; i++)
            {
                bool has = i < items.Count;
                bool can = has && _inventory.CanUse(i);
                _panels[i].color = i == inHand ? Lit : has ? Filled : Empty;
                _icons[i].texture = has ? items[i].Icon : null;
                _icons[i].enabled = has && items[i].Icon != null;
                _names[i].text = has ? items[i].Name : "";
                _notes[i].text = has && !can ? (i >= usable ? "kid's bag" : "too big") : "";
                _icons[i].color = can || !has ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                _slotGroups[i].alpha = i < usable || has ? 1f : 0.45f;   // a toy's slots 3 and 4: there, but out of reach
            }
        }
    }
}
