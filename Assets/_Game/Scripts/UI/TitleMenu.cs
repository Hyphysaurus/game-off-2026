using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The title screen: the name, three save slots, Continue / New Game / Quit. It builds its own canvas at run time
    /// (nothing to place; it spawns in any scene with a Player) and holds the player still until a choice is made.
    /// Continue loads the active slot through Respawn; New Game empties it and starts in the toy box. Mouse, keyboard
    /// and gamepad all work through the scene's EventSystem. His lettering replaces the title text when it lands.
    /// </summary>
    public sealed class TitleMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded += (s, m) => Spawn();
            Spawn();
        }

        private static void Spawn()
        {
            if (GameObject.Find("Player") == null || FindFirstObjectByType<TitleMenu>() != null) return;
            new GameObject("TitleMenu").AddComponent<TitleMenu>();
        }

        [SerializeField, Tooltip("His title lettering; plain text until it lands.")] private Sprite titleArt;

        private Respawn _respawn; private PlayerInput _input; private ShoulderCamera _camera;
        private GameObject _root; private Button _continue; private readonly Button[] _slots = new Button[SaveGame.Slots];
        private TMP_Text _continueLabel;

        private void Start()
        {
            var player = GameObject.Find("Player");
            _respawn = player.GetComponent<Respawn>(); _input = player.GetComponent<PlayerInput>(); _camera = FindFirstObjectByType<ShoulderCamera>();
            var startCard = GameObject.Find("HUD/StartCard"); if (startCard != null) startCard.SetActive(false);
            if (FindFirstObjectByType<EventSystem>() == null) { var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(); }
            Build();
            Open();
        }

        private void Open()
        {
            _root.SetActive(true);
            if (_input != null) _input.enabled = false;
            if (_camera != null) _camera.enabled = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Time.timeScale = 0f;
            Refresh();
            EventSystem.current?.SetSelectedGameObject((_continue.interactable ? _continue : _slots[SaveGame.Slot]).gameObject);
        }

        private void Close()
        {
            _root.SetActive(false);
            Time.timeScale = 1f;
            if (_input != null) _input.enabled = true;
            if (_camera != null) _camera.enabled = true;   // its OnEnable locks the cursor again
        }

        private void Continue() { if (_respawn.LoadSlot(SaveGame.Slot)) Close(); }
        private void NewGame() { SaveGame.Clear(SaveGame.Slot); _respawn.ToStart(); Close(); }
        private void PickSlot(int i) { SaveGame.Slot = i; Refresh(); }
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Refresh()
        {
            for (int i = 0; i < SaveGame.Slots; i++)
            {
                bool has = SaveGame.TryLoad(i, out var d);
                var label = _slots[i].GetComponentInChildren<TMP_Text>();
                label.text = $"<b>Slot {i + 1}</b>   " + (has ? $"{Pretty(d.room)} · {(d.size == ShiftSize.Toy ? "toy" : "kid")} · {d.when}" : "<alpha=#88>empty");
                _slots[i].GetComponent<Image>().color = i == SaveGame.Slot ? new Color(0.35f, 0.85f, 1f, 0.35f) : new Color(1f, 1f, 1f, 0.08f);
            }
            _continue.interactable = SaveGame.Has(SaveGame.Slot);
            _continueLabel.alpha = _continue.interactable ? 1f : 0.35f;
        }

        private static string Pretty(string room)
        {
            switch (room)
            {
                case "R3_SkirtingRun": return "Skirting run";
                case "R4_Playroom": return "Playroom";
                case "R5_Wardrobe": return "Wardrobe";
                case "R6_Window": return "Window";
                case "Bedroom": return "Bedroom";
                case "ToyBox": return "Toy box";
                default: return string.IsNullOrEmpty(room) ? "?" : room;
            }
        }

        // ------------------------------------------------------------------ building the canvas
        private void Build()
        {
            _root = new GameObject("TitleCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();

            var dim = Rect("Dim", _root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.72f);

            if (titleArt != null)
            {
                var art = Rect("TitleArt", _root.transform, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(1100, 550));
                var img = art.gameObject.AddComponent<Image>(); img.sprite = titleArt; img.preserveAspect = true; img.raycastTarget = false;
            }
            else
            {
                var t = Text("Title", _root.transform, "SHIFT", 180, new Vector2(0.5f, 0.72f), new Vector2(1400, 240));
                t.fontStyle = FontStyles.Bold; t.characterSpacing = 18f;
            }

            float y = 0.47f;
            _continue = MenuButton("Continue", y, Continue); _continueLabel = _continue.GetComponentInChildren<TMP_Text>(); y -= 0.065f;
            MenuButton("New Game", y, NewGame); y -= 0.085f;
            for (int i = 0; i < SaveGame.Slots; i++) { int k = i; _slots[i] = MenuButton("Slot", y, () => PickSlot(k), 560f, 30); y -= 0.058f; }
            y -= 0.025f;
            MenuButton("Quit", y, Quit);
            Text("Hint", _root.transform, "mouse, arrows or stick · Enter / A to choose", 22, new Vector2(0.5f, 0.05f), new Vector2(900, 40)).alpha = 0.6f;
        }

        private Button MenuButton(string label, float y, UnityEngine.Events.UnityAction onClick, float width = 380f, float size = 40f)
        {
            var rt = Rect(label, _root.transform, new Vector2(0.5f, y), new Vector2(0.5f, y), Vector2.zero, new Vector2(width, size + 22f));
            var img = rt.gameObject.AddComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0.08f);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(onClick);
            var cb = b.colors; cb.highlightedColor = new Color(1f, 1f, 1f, 2.5f); cb.selectedColor = new Color(1f, 1f, 1f, 2.5f); cb.pressedColor = new Color(0.6f, 0.9f, 1f, 3f); b.colors = cb;
            var t = Text("Label", rt, label, size, new Vector2(0.5f, 0.5f), new Vector2(width - 20f, size + 16f)); t.raycastTarget = false;
            return b;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 anchor, Vector2 box)
        {
            var rt = Rect(name, parent, anchor, anchor, Vector2.zero, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = min; rt.anchorMax = max; rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }
    }
}
