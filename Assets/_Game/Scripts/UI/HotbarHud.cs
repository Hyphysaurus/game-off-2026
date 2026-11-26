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
    /// THE PIXEL LOOK (D-0243, PixelKit > Pixel On): his mockup's square pixel slots in the bottom LEFT corner, 84 a side:
    /// the one in hand cyan, one with a thing in it deep purple, an empty one darkest; its key number in its top left; the
    /// thing's picture in the middle; why it cannot be used in small words along its bottom. Over the row, a name tag
    /// with the name of the thing in hand. The key prompt footer keeps clear of the row (it reads PixelKit.HotbarRight).
    /// </summary>
    public sealed class HotbarHud : MonoBehaviour
    {
        private const string LabelFont = "HUD/Fredoka SDF";
        private const float Box = 64f, Gap = 8f, Bottom = 16f;   // D-0132, his "pretty large": a third smaller than the first 96 / 10 / 24
        /// <summary>The bar's top edge above the screen's bottom (1920 x 1080 units): the shift batteries sit above it while it shows.</summary>
        public const float Top = Bottom + Box;
        private static readonly Color Empty = new Color(0f, 0f, 0f, 0.25f), Filled = new Color(0f, 0f, 0f, 0.45f), Lit = new Color(1f, 0.78f, 0.3f, 0.75f);
        // The pixel look (D-0243): the picture's side in a slot, the name tag's gap over the row and its height.
        private const float Picture = 48f, TagGap = 8f, TagHigh = 30f;

        private Inventory _inventory;
        private CanvasGroup _group;
        private Image[] _panels;
        private RawImage[] _icons;
        private TextMeshProUGUI[] _names, _keys, _notes;
        private CanvasGroup[] _slotGroups;
        // The pixel look (D-0243).
        private bool _pixel;
        private PixelKit.Frame[] _frames;
        private RectTransform _tag;
        private TextMeshProUGUI _tagWords;
        private float _rowWide;

        public static HotbarHud Create(Inventory inventory)
        {
            var hud = new GameObject("HotbarHud").AddComponent<HotbarHud>();
            if (inventory.GetComponent<NetPlayer>() != null) DontDestroyOnLoad(hud.gameObject);   // an online player outlives scene loads (it was made on the title, before its world): so does its HUD (D-0126)
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
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
            if (PixelKit.On) { BuildPixel(canvasGo.transform); return; }

            int n = _inventory.MostSlots;
            float width = n * Box + (n - 1) * Gap;
            var row = BuildUiKit.Rect("Row", canvasGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Bottom), new Vector2(width, Box));
            _panels = new Image[n]; _icons = new RawImage[n]; _names = new TextMeshProUGUI[n]; _keys = new TextMeshProUGUI[n]; _notes = new TextMeshProUGUI[n]; _slotGroups = new CanvasGroup[n];
            for (int i = 0; i < n; i++)
            {
                var slot = BuildUiKit.Rect("Slot" + (i + 1), row, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(i * (Box + Gap), 0f), new Vector2(Box, Box));
                _slotGroups[i] = slot.gameObject.AddComponent<CanvasGroup>();
                _panels[i] = BuildUiKit.Panel(slot, Empty); _panels[i].raycastTarget = false;
                // Picture above, name along the bottom, key number top left (sized for a 64 slot; the words shrink less
                // than the slot so they stay readable).
                var iconRt = BuildUiKit.Rect("Icon", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(Box * 0.58f, Box * 0.58f));
                _icons[i] = iconRt.gameObject.AddComponent<RawImage>(); _icons[i].raycastTarget = false;
                var keyRt = BuildUiKit.Rect("Key", slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -1f), new Vector2(18f, 18f));
                _keys[i] = BuildUiKit.Label(keyRt, (i + 1).ToString(), 15f, TextAlignmentOptions.TopLeft);
                var nameRt = BuildUiKit.Rect("Name", slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(Box - 4f, 16f));
                _names[i] = BuildUiKit.Label(nameRt, "", 12f, TextAlignmentOptions.Bottom);
                var noteRt = BuildUiKit.Rect("Note", slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 7f), new Vector2(Box - 4f, 28f));
                _notes[i] = BuildUiKit.Label(noteRt, "", 11f, TextAlignmentOptions.Center);
                if (font != null) { _keys[i].font = font; _names[i].font = font; _notes[i].font = font; }
                _names[i].enableAutoSizing = true; _names[i].fontSizeMin = 9f; _names[i].fontSizeMax = 12f;
            }
        }

        /// <summary>The pixel slots (D-0243): the row in the bottom left corner, the name tag over it.</summary>
        private void BuildPixel(Transform canvas)
        {
            _pixel = true;
            int n = _inventory.MostSlots;
            float slot = PixelKit.Slot, gap = PixelKit.SlotGap, edge = PixelKit.Edge;
            _rowWide = n * slot + (n - 1) * gap;
            var row = BuildUiKit.Rect("Row", canvas, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(edge, edge), new Vector2(_rowWide, slot));
            _panels = new Image[n]; _icons = new RawImage[n]; _keys = new TextMeshProUGUI[n]; _notes = new TextMeshProUGUI[n]; _slotGroups = new CanvasGroup[n];
            _frames = new PixelKit.Frame[n];
            Vector2 middle = new Vector2(0.5f, 0.5f), topLeft = new Vector2(0f, 1f), bottom = new Vector2(0.5f, 0f);
            for (int i = 0; i < n; i++)
            {
                var rt = BuildUiKit.Rect("Slot" + (i + 1), row, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(i * (slot + gap), 0f), new Vector2(slot, slot));
                _slotGroups[i] = rt.gameObject.AddComponent<CanvasGroup>();
                _panels[i] = PixelKit.Panel(rt, PixelKit.Frame.SlotEmpty);
                _frames[i] = PixelKit.Frame.SlotEmpty;
                var iconRt = BuildUiKit.Rect("Icon", rt, middle, middle, middle, new Vector2(0f, 2f), new Vector2(Picture, Picture));
                _icons[i] = iconRt.gameObject.AddComponent<RawImage>(); _icons[i].raycastTarget = false;
                // His mockup: the number 6 in and 4 down inside the frame (the frame is 4).
                var keyRt = BuildUiKit.Rect("Key", rt, topLeft, topLeft, topLeft, new Vector2(10f, -6f), new Vector2(24f, 18f));
                _keys[i] = PixelKit.Text(keyRt, PixelKit.Body, PixelKit.QuietText, false, TextAlignmentOptions.TopLeft);
                _keys[i].text = (i + 1).ToString();
                var noteRt = BuildUiKit.Rect("Note", rt, bottom, bottom, bottom, new Vector2(0f, 8f), new Vector2(slot - 12f, 10f));
                _notes[i] = PixelKit.Text(noteRt, PixelKit.Tiny, PixelKit.Lilac, false, TextAlignmentOptions.Center);
            }
            _tag = BuildUiKit.Rect("NameTag", canvas, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(edge, edge + slot + TagGap), new Vector2(100f, TagHigh));
            PixelKit.Panel(_tag, PixelKit.Frame.NameTag);
            var words = BuildUiKit.Rect("Words", _tag, Vector2.zero, Vector2.one, middle, Vector2.zero, Vector2.zero);
            _tagWords = PixelKit.Text(words, PixelKit.Body, PixelKit.Cyan, true, TextAlignmentOptions.Center, true);
            _tag.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_inventory == null) { Destroy(gameObject); return; }   // the player is gone with its scene
            var items = _inventory.Items;
            bool show = _inventory.isActiveAndEnabled && items.Count > 0
                        && GameMode.Current != GameMode.Mode.Build && !PauseMenu.IsOpen && !TitleMenu.IsOpen && !KeyHelp.IsOpen && !DebugConsole.IsOpen;
            _group.alpha = show ? 1f : 0f;
            if (_pixel) { UpdatePixel(show); return; }
            if (!show) return;
            int usable = _inventory.Usable;
            for (int i = 0; i < _panels.Length; i++)
            {
                bool has = i < items.Count;
                bool can = has && _inventory.CanUse(i);
                _panels[i].color = _inventory.IsInHand(i) ? Lit : has ? Filled : Empty;   // both hands' slots lit (D-0144)
                _icons[i].texture = has ? items[i].Icon : null;
                _icons[i].enabled = has && items[i].Icon != null;
                _names[i].text = has ? items[i].Name : "";
                _notes[i].text = has && !can ? (i >= usable ? "kid's bag" : items[i].ToyOnly ? "toy only" : "too big") : "";
                _icons[i].color = can || !has ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                _slotGroups[i].alpha = i < usable || has ? 1f : 0.45f;   // a toy's slots 3 and 4: there, but out of reach
            }
        }

        /// <summary>The pixel slots as the inventory is now (a frame is swapped only when the slot's state changes), the name
        /// tag over them, and where the row reaches for the key prompt footer.</summary>
        private void UpdatePixel(bool show)
        {
            float right = show ? PixelKit.Edge + _rowWide : 0f;
            if (!show)
            {
                if (PixelKit.HotbarRight != 0f) PixelKit.HotbarRight = PixelKit.HotbarTop = 0f;
                return;
            }
            var items = _inventory.Items;
            int usable = _inventory.Usable, inHand = -1;
            for (int i = 0; i < _panels.Length; i++)
            {
                bool has = i < items.Count;
                bool can = has && _inventory.CanUse(i);
                bool held = _inventory.IsInHand(i);   // both hands' slots lit (D-0144)
                if (held && inHand < 0) inHand = i;
                PixelKit.Frame look = held ? PixelKit.Frame.SlotHeld : has ? PixelKit.Frame.SlotFilled : PixelKit.Frame.SlotEmpty;
                if (_frames[i] != look) { _frames[i] = look; PixelKit.Restyle(_panels[i], look); }
                _keys[i].color = held ? PixelKit.Cyan : has ? PixelKit.Lilac : PixelKit.QuietText;
                _icons[i].texture = has ? items[i].Icon : null;
                _icons[i].enabled = has && items[i].Icon != null;
                _notes[i].text = has && !can ? (i >= usable ? "kid's bag" : items[i].ToyOnly ? "toy only" : "too big") : "";
                _icons[i].color = can || !has ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                _slotGroups[i].alpha = i < usable || has ? 1f : 0.45f;   // a toy's slots 3 and 4: there, but out of reach
            }
            // The name tag: the thing in hand (the first lit slot), sized to its name.
            bool tag = inHand >= 0 && !string.IsNullOrEmpty(items[inHand].Name);
            if (_tag.gameObject.activeSelf != tag) _tag.gameObject.SetActive(tag);
            if (tag && _tagWords.text != items[inHand].Name)
            {
                _tagWords.text = items[inHand].Name;
                _tag.sizeDelta = new Vector2(PixelKit.Wide(_tagWords, _tagWords.text) + 26f, TagHigh);   // 3 frame + 10 pad a side
            }
            float top = PixelKit.Edge + PixelKit.Slot + (tag ? TagGap + TagHigh : 0f);
            if (PixelKit.HotbarRight != right) PixelKit.HotbarRight = right;
            if (PixelKit.HotbarTop != top) PixelKit.HotbarTop = top;
        }

        private void OnDestroy()
        {
            if (_pixel) PixelKit.HotbarRight = PixelKit.HotbarTop = 0f;
        }
    }
}
