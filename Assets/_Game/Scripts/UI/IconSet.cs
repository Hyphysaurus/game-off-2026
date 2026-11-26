using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The menus' icons and button-prompt glyphs in one place (D-0226), so any of them can be swapped here. Each icon has an
    /// id (workshop, raceway, colours, online, parties, options, quit, resume, title, leave-party, sticker-chart,
    /// playground, quick-play, volume) and a sprite: his pixel icons (Art/UI/Icons, at-icons MIT, drawn by
    /// docs/tools/make_menu_icons.py in his neon palette). The footer's glyphs (Art/UI/Prompts, Kenney CC0) are set per device: the
    /// keyboard, an Xbox-style pad and a PlayStation pad; its words are here too. Read from Resources/IconSet (made and
    /// filled by Shift Jam > UI > Land Menu Icons (D-0226), which never overwrites a sprite he has set). Without the asset,
    /// or with Icons On / Footer On unticked, the menus look as they did before.
    /// The pixel HUD's icons (D-0243: heart, shift, torch, gun, battery-high, battery-low, star, star-locked, ghost, lock,
    /// bulb, hand, trophy, box, door, sticker) are in the same Icons list, drawn the same way; the HUD asks for them with
    /// <see cref="Hud"/>, which the menus' Icons On does not switch off (PixelKit > Pixel On does). The key prompts' Kenney
    /// key glyphs (E, X, Tab, Esc, the mouse buttons...) are in Keys, found by the key's name with <see cref="KeyGlyph"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Icon Set", fileName = "IconSet")]
    public sealed class IconSet : ScriptableObject
    {
        public const string ResourcePath = "IconSet";

        // The ids the menus ask for (an icon's id on the asset must be one of these to show anywhere).
        public const string Workshop = "workshop", Raceway = "raceway", Colours = "colours", Online = "online", Parties = "parties";
        public const string Options = "options", Quit = "quit", Resume = "resume", Title = "title", LeaveParty = "leave-party";
        public const string StickerChart = "sticker-chart", Playground = "playground", QuickPlay = "quick-play", Volume = "volume";
        // The ids the pixel HUD asks for (D-0243).
        public const string Heart = "heart", Shift = "shift", Torch = "torch", Gun = "gun", BatteryHigh = "battery-high", BatteryLow = "battery-low";
        public const string Star = "star", StarLocked = "star-locked", Ghost = "ghost", Lock = "lock", Bulb = "bulb", Hand = "hand";
        public const string Trophy = "trophy", Box = "box", Door = "door", Sticker = "sticker";

        /// <summary>The hands the footer shows glyphs for.</summary>
        public enum Device { Keyboard, Xbox, PlayStation }

        /// <summary>One icon: its id and its picture.</summary>
        [Serializable]
        public struct Icon
        {
            [Tooltip("The id a menu asks for (workshop, raceway, colours, online, parties, options, quit, resume, title, leave-party, sticker-chart, playground, quick-play, volume).")] public string id;
            [Tooltip("Its picture: a pixel icon imported as a Sprite with Point filter (Land Menu Icons sets that up). Empty = no icon there.")] public Sprite sprite;
        }

        /// <summary>The footer's three glyphs for one device.</summary>
        [Serializable]
        public struct Glyphs
        {
            [Tooltip("Beside Move: the arrows, the D-pad.")] public Sprite move;
            [Tooltip("Beside Select: Enter, the south button (A / cross).")] public Sprite select;
            [Tooltip("Beside Back: Esc, the east button (B / circle).")] public Sprite back;
        }

        /// <summary>One key's glyph for the HUD's key prompts (D-0243), and the names the game's words call that key by.</summary>
        [Serializable]
        public struct Key
        {
            [Tooltip("The names this key goes by in the game's prompt words, any case, between commas (\"lmb,click\"). A prompt's key with no glyph here shows as a cream key cap with its name.")] public string names;
            [Tooltip("Its glyph: a Kenney Input Prompts key (Art/UI/Prompts, imported as a Sprite; Land Menu Icons sets that up). Empty = the key cap.")] public Sprite sprite;
        }

        [Header("Menu icons")]
        [Tooltip("Off: no menu shows an icon and the words sit where they did before.")] public bool iconsOn = true;
        [Tooltip("Every icon by id. Swap a sprite to change that icon everywhere it shows; empty it to show none there.")] public Icon[] icons = new Icon[0];
        [Range(0.2f, 1f), Tooltip("How much of a slab's or button's height an icon may take. The icon is drawn a whole number of screen pixels per pixel of its own (at 1080 high), the most that keeps an Icon Pixels high icon inside this share.")] public float iconShare = 0.7f;
        [Min(1), Tooltip("The icons' own height in pixels (his are 16): with Icon Share it decides how many screen pixels each icon pixel takes.")] public int iconPixels = 16;
        [Min(0f), Tooltip("Clear space between an icon and its words, canvas pixels at 1080 high.")] public float iconGap = 14f;
        [Range(0f, 1f), Tooltip("An icon's see-through on a slab that cannot be used (the slab's words go the same way).")] public float unusableAlpha = 0.4f;

        [Header("The button-prompt footer (title, pause menu, Sticker Chart)")]
        [Tooltip("Off: no footer on any menu.")] public bool footerOn = true;
        [Tooltip("The word beside the move glyph.")] public string moveWords = "Move";
        [Tooltip("The word beside the select glyph.")] public string selectWords = "Select";
        [Tooltip("The word beside the back glyph.")] public string backWords = "Back";
        [Tooltip("Shown in capitals, as in his mockup.")] public bool capitals = true;
        [Tooltip("Between two prompts. Empty = none.")] public string separator = "|";
        [Tooltip("The keyboard's glyphs (also used when a pad's are empty).")] public Glyphs keyboard;
        [Tooltip("An Xbox-style pad's glyphs (any pad that is not a PlayStation one).")] public Glyphs xbox;
        [Tooltip("A PlayStation pad's glyphs (DualShock, DualSense).")] public Glyphs playstation;
        [Min(8f), Tooltip("A glyph's height, canvas pixels at 1080 high (the Kenney glyphs are 64: 32 is exactly half, 64 on a 4K screen).")] public float glyphSize = 32f;
        [Min(8f), Tooltip("The words' letter size.")] public float wordsSize = 22f;
        [Tooltip("The words' colour (his pale).")] public Color wordsColour = new Color32(0xff, 0xf0, 0xfc, 0xff);
        [Tooltip("The separators' colour (his gold).")] public Color separatorColour = new Color32(0xf5, 0xc8, 0x4c, 0xff);
        [Tooltip("The words' font. Empty = TextMesh Pro's own (the menus' words use it too).")] public TMP_FontAsset font;
        [Min(0f), Tooltip("Between a glyph and its word, canvas pixels.")] public float glyphGap = 8f;
        [Min(0f), Tooltip("Between a word and the next separator (and the separator and the next glyph), canvas pixels.")] public float itemGap = 22f;
        [Tooltip("On the model menus: the footer's middle this far below the model's Anchor_Hint, canvas pixels at 1080 high (the anchor is 498 below the middle: 22 leaves the glyphs 4 pixels above the screen's bottom).")] public float belowHint = 22f;
        [Tooltip("On the model menus: the hint line moves up this much while the footer is under it, canvas pixels (0 = it stays on Anchor_Hint).")] public float hintLift = 14f;
        [Tooltip("Where the footer sits on the old pause menu (the one that draws itself) and over the Sticker Chart page: its middle this far up from the screen's bottom, canvas pixels at 1080 high.")] public float fromBottom = 22f;

        [Header("The pixel HUD's key glyphs (D-0243)")]
        [Tooltip("Every key the HUD's prompts can show as a glyph, by the names the game's words use (E, X, Tab, Esc, LMB...). Swap a glyph by dragging another sprite in; empty one to show that key as a cream key cap.")] public Key[] keys = new Key[0];

        private static IconSet _loaded;
        private static bool _looked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _loaded = null; _looked = false; }

        /// <summary>Resources/IconSet, or null (no icons, no footer).</summary>
        public static IconSet Get()
        {
            if (!_looked) { _loaded = Resources.Load<IconSet>(ResourcePath); _looked = true; }
            return _loaded;
        }

        /// <summary>The icon of this id, or null (none set, Icons On unticked, or no such id).</summary>
        public Sprite Find(string id)
        {
            if (!iconsOn || string.IsNullOrEmpty(id) || icons == null) return null;
            for (int i = 0; i < icons.Length; i++) if (string.Equals(icons[i].id, id, StringComparison.Ordinal)) return icons[i].sprite;
            return null;
        }

        /// <summary>The HUD's icon of this id (D-0243), or null (none set, or no such id). The menus' Icons On does not hide
        /// these: the pixel HUD has its own switch (PixelKit > Pixel On).</summary>
        public Sprite Hud(string id)
        {
            if (string.IsNullOrEmpty(id) || icons == null) return null;
            for (int i = 0; i < icons.Length; i++) if (string.Equals(icons[i].id, id, StringComparison.Ordinal)) return icons[i].sprite;
            return null;
        }

        /// <summary>The glyph of the key the game's words call <paramref name="key"/> ("E", "Tab", "LMB"; any case), or null.
        /// Reads Keys' comma lists in place: nothing is made.</summary>
        public Sprite KeyGlyph(string key)
        {
            if (string.IsNullOrEmpty(key) || keys == null) return null;
            for (int i = 0; i < keys.Length; i++)
            {
                string names = keys[i].names;
                if (keys[i].sprite == null || string.IsNullOrEmpty(names)) continue;
                int start = 0;
                while (start <= names.Length)
                {
                    int comma = names.IndexOf(',', start);
                    int end = comma < 0 ? names.Length : comma;
                    int a = start, b = end;
                    while (a < b && names[a] == ' ') a++;
                    while (b > a && names[b - 1] == ' ') b--;
                    if (b - a == key.Length && string.Compare(names, a, key, 0, key.Length, StringComparison.OrdinalIgnoreCase) == 0) return keys[i].sprite;
                    if (comma < 0) break;
                    start = comma + 1;
                }
            }
            return null;
        }

        /// <summary>The glyphs of a device; a glyph a pad lacks is the keyboard's.</summary>
        public Sprite Glyph(Device device, int prompt)
        {
            Glyphs g = device == Device.PlayStation ? playstation : device == Device.Xbox ? xbox : keyboard;
            Sprite s = prompt == 0 ? g.move : prompt == 1 ? g.select : g.back;
            if (s != null || device == Device.Keyboard) return s;
            return prompt == 0 ? keyboard.move : prompt == 1 ? keyboard.select : keyboard.back;
        }
    }

    /// <summary>
    /// Puts an icon of the IconSet left of a control's words (D-0226): on a slab of the menu model (it rides the slab as
    /// its words do: a child of them, so a copy of the slab made later leaves it behind) and on a flat button of the old
    /// menus. The words move over by half the icon's room, so the icon and the words together stay in the middle. Called
    /// when a page is shown or a control's words change, never per frame. With no icon for the id (or the set off) it takes
    /// its icon away and puts the words back.
    /// </summary>
    public static class MenuIcons
    {
        private const string IconName = "Icon";

        /// <summary>An icon's size, canvas pixels: its own pixels times the most whole screen pixels per pixel that keep an
        /// Icon Pixels high icon inside Icon Share of <paramref name="roomHigh"/> (0 when not even one fits).</summary>
        public static Vector2 Size(Sprite icon, float roomHigh, IconSet set)
        {
            if (icon == null || set == null) return Vector2.zero;
            int k = Mathf.FloorToInt(roomHigh * set.iconShare / Mathf.Max(1, set.iconPixels) + 1e-4f);
            return k < 1 ? Vector2.zero : icon.rect.size * k;
        }

        /// <summary>
        /// The icon <paramref name="id"/> on a slab of the model, left of its words. Call it after the slab's words are set
        /// and while its page is up (its words must be awake to be measured); a slab off the screen is left for the next
        /// call. False when there is nothing to put it on.
        /// </summary>
        public static bool OnSlab(MenuModel model, string control, string id)
        {
            if (model == null || !model.TryGet(control, out MenuModel.Control c) || c.Words == null || !c.Words.gameObject.activeInHierarchy) return false;
            IconSet set = IconSet.Get();
            TextMeshPro words = c.Words;
            Transform had = words.transform.Find(IconName);
            Sprite sprite = set != null ? set.Find(id) : null;
            Vector2 size = Size(sprite, c.Size.y, set);
            if (size.x <= 0f)
            {
                if (had != null) had.gameObject.SetActive(false);
                if (words.margin != Vector4.zero) { words.margin = Vector4.zero; words.ForceMeshUpdate(true); }
                return true;
            }
            // The words' own units are the world's (MenuModel takes the slab's size out of them): a canvas pixel is this many.
            float perPixel = model.Scale / model.PixelsPerMetre;
            Vector2 icon = size * perPixel;
            float gap = set.iconGap * perPixel;
            words.margin = new Vector4(icon.x + gap, 0f, 0f, 0f);   // the words centre in what is left: half the room to the right
            words.ForceMeshUpdate(true);
            Bounds b = words.textBounds;
            bool any = b.size.x > 1e-5f && !string.IsNullOrEmpty(words.text);
            if (!any) { words.margin = Vector4.zero; words.ForceMeshUpdate(true); }
            Vector2 at = any ? new Vector2(b.min.x - gap - icon.x * 0.5f, b.center.y) : Vector2.zero;

            RectTransform rt;
            Image picture;
            if (had == null)
            {
                rt = new GameObject(IconName, typeof(RectTransform)).GetComponent<RectTransform>();
                rt.gameObject.layer = words.gameObject.layer;
                rt.SetParent(words.transform, false);
                // A world-space canvas of its own: drawn by the model's camera with the UI shader every build has.
                var canvas = rt.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var pic = new GameObject("Picture", typeof(RectTransform)).GetComponent<RectTransform>();
                pic.gameObject.layer = words.gameObject.layer;
                pic.SetParent(rt, false);
                pic.anchorMin = Vector2.zero; pic.anchorMax = Vector2.one; pic.offsetMin = Vector2.zero; pic.offsetMax = Vector2.zero;
                picture = pic.gameObject.AddComponent<Image>();
                picture.raycastTarget = false;
            }
            else
            {
                rt = (RectTransform)had;
                picture = rt.GetComponentInChildren<Image>(true);
                rt.gameObject.SetActive(true);
            }
            rt.localRotation = Quaternion.identity; rt.localScale = Vector3.one;
            rt.localPosition = new Vector3(at.x, at.y, 0f);
            rt.sizeDelta = icon;
            if (picture != null)
            {
                picture.sprite = sprite;
                picture.color = new Color(1f, 1f, 1f, c.Usable ? 1f : set.unusableAlpha);
            }
            return true;
        }

        /// <summary>
        /// The icon <paramref name="id"/> on a flat button of the old menus (TitleMenu's, MenuKit's), left of its label.
        /// Sized to the button, smaller when the words would not fit beside it, none when even the smallest would not.
        /// Call it again after the label's words change.
        /// </summary>
        public static void OnButton(Button button, string id)
        {
            if (button == null) return;
            var rt = (RectTransform)button.transform;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            IconSet set = IconSet.Get();
            Sprite sprite = set != null ? set.Find(id) : null;
            Transform had = rt.Find(IconName);
            float box = label.rectTransform.sizeDelta.x;
            float wide = label.GetPreferredValues(label.text).x;
            float gap = set != null ? set.iconGap : 0f;
            Vector2 size = Size(sprite, rt.sizeDelta.y, set);
            // The most whole pixels per icon pixel that leave the words room beside it.
            int k = sprite != null && size.y > 0f ? Mathf.RoundToInt(size.y / sprite.rect.height) : 0;
            while (k >= 1 && sprite.rect.width * k + gap + wide > box) k--;
            if (k < 1)
            {
                if (had != null) had.gameObject.SetActive(false);
                label.rectTransform.anchoredPosition = new Vector2(0f, label.rectTransform.anchoredPosition.y);
                return;
            }
            size = sprite.rect.size * k;
            Image picture;
            if (had == null)
            {
                var icon = new GameObject(IconName, typeof(RectTransform)).GetComponent<RectTransform>();
                icon.SetParent(rt, false);
                icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
                picture = icon.gameObject.AddComponent<Image>();
                picture.raycastTarget = false;
                had = icon;
            }
            else
            {
                picture = had.GetComponent<Image>();
                had.gameObject.SetActive(true);
            }
            picture.sprite = sprite;
            var place = (RectTransform)had;
            place.sizeDelta = size;
            place.anchoredPosition = new Vector2(-(wide + gap) * 0.5f, 0f);
            label.rectTransform.anchoredPosition = new Vector2((size.x + gap) * 0.5f, label.rectTransform.anchoredPosition.y);
        }

        /// <summary>
        /// The icon <paramref name="id"/> left of a heading (the Sticker Chart's): an Image beside <paramref name="heading"/>
        /// (same parent, same anchors) at its left middle, sized to its height; the heading moves right to make room.
        /// Null (and nothing moved) without an icon.
        /// </summary>
        public static Image BeforeHeading(RectTransform heading, string id)
        {
            if (heading == null || heading.parent == null) return null;
            IconSet set = IconSet.Get();
            Sprite sprite = set != null ? set.Find(id) : null;
            Vector2 size = Size(sprite, heading.sizeDelta.y, set);
            if (size.x <= 0f) return null;
            var rt = new GameObject(IconName, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(heading.parent, false);
            rt.anchorMin = heading.anchorMin; rt.anchorMax = heading.anchorMax;
            rt.pivot = new Vector2(0f, 0.5f);
            // The heading's left edge and middle, wherever its own pivot is.
            Vector2 left = heading.anchoredPosition + new Vector2(-heading.pivot.x * heading.sizeDelta.x, (0.5f - heading.pivot.y) * heading.sizeDelta.y);
            rt.anchoredPosition = left;
            rt.sizeDelta = size;
            var picture = rt.gameObject.AddComponent<Image>();
            picture.sprite = sprite; picture.raycastTarget = false;
            heading.anchoredPosition += new Vector2(size.x + set.iconGap, 0f);
            return picture;
        }
    }
}
