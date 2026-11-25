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
    /// Continue loads the active slot through Respawn (standing); New Game empties it and plays the opening in the toy box (D-0097). Mouse, keyboard
    /// and gamepad all work through the scene's EventSystem. With his Blender menu's prefab there (MenuModel, D-0196) the
    /// buttons and the text are laid over its slabs instead of drawn here. The title is the title prefab (his animated pixel lettering, D-0189) or, without it, his drawing (TitleArt, D-0099): one title for
    /// every window shape, as large as the top of the screen allows, popping in and then breathing a little.
    /// </summary>
    public sealed class TitleMenu : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            IsOpen = false;
            SceneManager.sceneLoaded += (s, m) => Spawn();
            Spawn();
        }

        private static void Spawn()
        {
            if (GameObject.Find("Player") == null || FindFirstObjectByType<TitleMenu>() != null) return;
            if (SceneManager.GetActiveScene().name == NetSession.PartyScene || NetSession.InParty) return;   // online: the party is under way (D-0114 the Stadium, D-0125 the house)
            if (IsSandbox(SceneManager.GetActiveScene().name)) return;   // the Workshop, the Raceway and the Colour Corner are sandboxes: straight in
            if (HouseCarry.Arriving) return;   // back through a door from another room (D-0091): the game is already under way
            new GameObject("TitleMenu").AddComponent<TitleMenu>();
        }

        public const string WorkshopScene = "Workshop";
        public const string RacewayScene = "Raceway";
        public const string ColourCornerScene = "ColourCorner";

        /// <summary>A sandbox scene has no story to start: no title card, a full shift charge from the first frame.</summary>
        public static bool IsSandbox(string scene) => scene == WorkshopScene || scene == RacewayScene || scene == ColourCornerScene;

        /// <summary>The Raceway is for driving: it has no cores to refill a charge, so the carried shift costs nothing there.
        /// Nor has the Colour Corner.</summary>
        public static bool IsFreeShift(string scene) => scene == RacewayScene || scene == ColourCornerScene;

        /// <summary>The title is up (PauseMenu stays shut under it).</summary>
        public static bool IsOpen { get; private set; }

        private OptionsPanel _options; private Button _optionsButton;
        private MenuModel _model;   // the menu's look as a 3D model (D-0196); null = it draws itself

        /// <summary>The controls the menu needs from a model; with one missing the menu draws itself.</summary>
        private static readonly string[] ModelControls =
        {
            "Btn_Continue", "Btn_NewGame", "Slot_1", "Slot_2", "Slot_3", "Btn_Workshop", "Btn_Raceway", "Btn_Colours", "Btn_Online", "Btn_Parties", "Btn_Options", "Btn_Quit",
        };

        /// <summary>The menu on the screen or off it: its canvas, and its model when it has one.</summary>
        private void ShowMenu(bool on)
        {
            _root.SetActive(on);
            if (_model != null) _model.Show(on);
        }
        private float _menuX = 0.5f;

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
            IsOpen = true;
            ShowMenu(true);
            if (_input != null) _input.enabled = false;
            if (_camera != null) _camera.enabled = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Time.timeScale = 0f;
            Refresh();
            UiAudio.Hush();
            EventSystem.current?.SetSelectedGameObject((_continue.interactable ? _continue : _slots[SaveGame.Slot]).gameObject);
        }

        private void Close()
        {
            IsOpen = false;
            ShowMenu(false);
            Time.timeScale = 1f;
            if (_input != null) _input.enabled = true;
            if (_camera != null) _camera.enabled = true;   // its OnEnable locks the cursor again
        }

        private void OnDestroy()
        {
            IsOpen = false; NetSession.StatusChanged -= OnPartyStatus;   // there is only ever one title
            if (_model != null) Destroy(_model.gameObject);
        }

        private TMP_Text _hint;
        private const string HintLine = "mouse, arrows or stick · Enter / A to choose · Online drops you into a friend's world (or starts one)";

        /// <summary>The hint line shows what the party is doing (starting, the code, what went wrong) while there is news.</summary>
        private void OnPartyStatus()
        {
            if (_hint == null) return;
            bool news = !string.IsNullOrEmpty(NetSession.Status);
            _hint.text = news ? NetSession.Status : HintLine;
            _hint.alpha = news ? 1f : 0.6f;
        }

        private PartiesPanel _parties; private Button _partiesButton;

        /// <summary>The party list over the title (D-0122): the title hides while it is up.</summary>
        private void OpenParties()
        {
            if (_parties == null) _parties = PartiesPanel.Create(transform, 51);
            ShowMenu(false);
            _parties.Open(() => { ShowMenu(true); UiAudio.Hush(); EventSystem.current?.SetSelectedGameObject(_partiesButton.gameObject); });
        }

        /// <summary>Options over the title (D-0044): the title hides while it is up and comes back with Options selected.</summary>
        private void OpenOptions()
        {
            if (_options == null) _options = OptionsPanel.Create(transform, 51);
            ShowMenu(false);
            _options.Open(() => { ShowMenu(true); UiAudio.Hush(); EventSystem.current?.SetSelectedGameObject(_optionsButton.gameObject); });
        }

        private void Continue() { if (_respawn.LoadSlot(SaveGame.Slot)) Close(); }
        private void NewGame() { SaveGame.Clear(SaveGame.Slot); _respawn.ToStart(); Close(); Opening.Play(_respawn.gameObject); }   // the toy wakes in the box (D-0097)
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
                // The slot in use: on a model its slab's frame says so (the button over it is see-through).
                if (_slots[i].TryGetComponent(out MenuSlab slab)) slab.Active = i == SaveGame.Slot;
                else _slots[i].GetComponent<Image>().color = i == SaveGame.Slot ? new Color(0.35f, 0.85f, 1f, 0.35f) : new Color(1f, 1f, 1f, 0.08f);
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

            // His Blender menu (D-0196), when its prefab is there and whole: the slabs are drawn by a camera of their own
            // and this canvas lays the text and the real buttons over them. Sized by the screen's height, as that camera is.
            _model = MenuModel.Spawn(_camera != null ? _camera.GetComponent<Camera>() : Camera.main, ModelControls);
            if (_model != null) { scaler.matchWidthOrHeight = 1f; BuildOnModel(); return; }

            var dim = Rect("Dim", _root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            dim.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.72f);

            // The title over the buttons: the title prefab (his animated pixel lettering, D-0189; its size is the prefab's
            // own Width and Height), or, without it, his drawing, 1160 x 540 on a 16:9 screen. The buttons and the hint
            // sit a little lower and closer to make the room (D-0099).
            if (TitleArt.Place(_root.transform, new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(1500f, 540f), prefabPath: TitleArt.MenuPrefabPath) == null)
            {
                var t = Text("Title", _root.transform, "SHIFT", 180, new Vector2(0.5f, 0.72f), new Vector2(1400, 240));
                t.fontStyle = FontStyles.Bold; t.characterSpacing = 18f;
            }

            float y = 0.45f;
            _continue = MenuButton("Continue", y, Continue); _continueLabel = _continue.GetComponentInChildren<TMP_Text>(); y -= 0.06f;
            MenuButton("New Game", y, NewGame); y -= 0.08f;
            for (int i = 0; i < SaveGame.Slots; i++) { int k = i; _slots[i] = MenuButton("Slot", y, () => PickSlot(k), 560f, 30); y -= 0.055f; }
            y -= 0.02f;
            // The three sandboxes and online play share a row (a row more would run into the hint line), in smaller
            // letters to fit. Host makes a party and copies its code; Join reads a code from the clipboard (D-0111).
            MenuButton("Workshop", y, () => { Time.timeScale = 1f; SceneManager.LoadScene(WorkshopScene); }, 118f, 26f, -252f);
            MenuButton("Raceway", y, () => { Time.timeScale = 1f; SceneManager.LoadScene(RacewayScene); }, 118f, 26f, -126f);
            MenuButton("Colours", y, () => { Time.timeScale = 1f; SceneManager.LoadScene(ColourCornerScene); }, 118f, 26f, 0f);
            // Online (D-0122): Play Online drops you into an open world or starts one; Parties lists them (and keeps host
            // and code joins for private games).
            MenuButton("Online", y, () => NetSession.PlayOnline(), 118f, 26f, 126f);
            _partiesButton = MenuButton("Parties", y, OpenParties, 118f, 26f, 252f); y -= 0.062f;
            // Options and Quit share the last row (a full row more would run into the hint line).
            _optionsButton = MenuButton("Options", y, OpenOptions, 185f, 40f, -97.5f);
            MenuButton("Quit", y, Quit, 185f, 40f, 97.5f);
            _hint = Text("Hint", _root.transform, HintLine, 20, new Vector2(0.5f, 0.017f), new Vector2(1400, 30)); _hint.alpha = 0.6f;
            // The name others see online (D-0118), top right: kept between runs; empty = the PC's user name.
            var nameLabel = Text("NameLabel", _root.transform, "Your name online", 18, new Vector2(0.9f, 0.965f), new Vector2(300, 26)); nameLabel.alpha = 0.6f;
            var nameField = BuildUiKit.Input(_root.transform, System.Environment.UserName, 24);
            var nameRect = (RectTransform)nameField.transform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.9f, 0.925f); nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = Vector2.zero; nameRect.sizeDelta = new Vector2(300f, 44f);
            nameField.characterLimit = PlayerName.MaxLength;
            nameField.text = PlayerName.Typed;
            nameField.onEndEdit.AddListener(PlayerName.Set);
            NetSession.StatusChanged += OnPartyStatus;
            // Which build this is (D-0040): BuildTools stamps the whole version number into the player settings; "dev" = a development build (D-0043).
            var version = Text("Version", _root.transform, "v" + Application.version + (Debug.isDebugBuild && !Application.isEditor ? " dev" : ""), 20, new Vector2(0.95f, 0.035f), new Vector2(160, 32));
            version.alignment = TextAlignmentOptions.Right; version.alpha = 0.45f;
        }

        // ------------------------------------------------------------------ the same menu over a 3D model (D-0196)

        /// <summary>Everything Build makes, laid over the model: each button over its slab, each text at its label or its
        /// anchor. What a button does, its sound and its words are the same as in the menu that draws itself.</summary>
        private void BuildOnModel()
        {
            var middle = new Vector2(0.5f, 0.5f);
            Vector2 at;
            if (!_model.Point("Anchor_Title", out at)) at = new Vector2(0f, 390f);
            if (TitleArt.Place(_root.transform, middle, at, _model.TitleBox, 40f, TitleArt.MenuPrefabPath, true) == null)
            {
                var t = At("Title", "SHIFT", 120f, at, _model.TitleBox, TextAlignmentOptions.Center);
                t.fontStyle = FontStyles.Bold; t.characterSpacing = 18f;
            }

            _continue = ModelButton("Btn_Continue", "Continue", Continue, UiAudio.Kind.Confirm); _continueLabel = _continue.GetComponentInChildren<TMP_Text>();
            ModelButton("Btn_NewGame", "New Game", NewGame, UiAudio.Kind.Confirm);
            for (int i = 0; i < SaveGame.Slots; i++) { int k = i; _slots[i] = ModelButton("Slot_" + (i + 1), "Slot", () => PickSlot(k), UiAudio.Kind.Select, true); }
            ModelButton("Btn_Workshop", "Workshop", () => { Time.timeScale = 1f; SceneManager.LoadScene(WorkshopScene); });
            ModelButton("Btn_Raceway", "Raceway", () => { Time.timeScale = 1f; SceneManager.LoadScene(RacewayScene); });
            ModelButton("Btn_Colours", "Colours", () => { Time.timeScale = 1f; SceneManager.LoadScene(ColourCornerScene); });
            ModelButton("Btn_Online", "Online", () => NetSession.PlayOnline());
            _partiesButton = ModelButton("Btn_Parties", "Parties", OpenParties);
            _optionsButton = ModelButton("Btn_Options", "Options", OpenOptions);
            ModelButton("Btn_Quit", "Quit", Quit);

            if (!_model.Point("Anchor_Hint", out at)) at = new Vector2(0f, -520f);
            _hint = At("Hint", HintLine, 20f, at, new Vector2(1400f, 30f), TextAlignmentOptions.Center); _hint.alpha = 0.6f;
            // The name others see online (D-0118): kept between runs; empty = the PC's user name.
            if (!_model.Point("Anchor_Name", out at)) at = new Vector2(540f, 460f);
            var nameLabel = At("NameLabel", "Your name online", 18f, at + new Vector2(0f, 40f), new Vector2(300f, 26f), TextAlignmentOptions.Center); nameLabel.alpha = 0.6f;
            var nameField = BuildUiKit.Input(_root.transform, System.Environment.UserName, 24);
            var nameRect = (RectTransform)nameField.transform;
            nameRect.anchorMin = nameRect.anchorMax = middle; nameRect.pivot = middle;
            nameRect.anchoredPosition = at; nameRect.sizeDelta = new Vector2(288f, 44f);
            nameField.characterLimit = PlayerName.MaxLength;
            nameField.text = PlayerName.Typed;
            nameField.onEndEdit.AddListener(PlayerName.Set);
            NetSession.StatusChanged += OnPartyStatus;
            // Which build this is (D-0040), as in the menu that draws itself.
            if (!_model.Point("Anchor_Version", out at)) at = new Vector2(620f, -520f);
            var version = At("Version", "v" + Application.version + (Debug.isDebugBuild && !Application.isEditor ? " dev" : ""), 20f, at, new Vector2(170f, 32f), TextAlignmentOptions.Right);
            version.alpha = 0.45f;
        }

        /// <summary>A button over the model's control of this name: see-through, as large as its slab, its caption where
        /// the model's label is; a MenuSlab shows its state on the slab. <paramref name="left"/>: the words start at the
        /// label's left (a save slot's line) and are not centred.</summary>
        private Button ModelButton(string control, string label, UnityEngine.Events.UnityAction onClick, UiAudio.Kind sound = UiAudio.Kind.Click, bool left = false)
        {
            _model.TryGet(control, out MenuModel.Control c);   // there: Spawn checked every name in ModelControls
            var middle = new Vector2(0.5f, 0.5f);
            var rt = Rect(label, _root.transform, middle, middle, c.Centre, c.Size);
            var img = rt.gameObject.AddComponent<Image>(); img.color = Color.clear;   // only what the mouse lands on
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None; b.onClick.AddListener(onClick);
            UiSound.On(b, sound);
            rt.gameObject.AddComponent<MenuSlab>().Bind(_model, c);
            // The words: about the label, as wide as the nearer side of the slab allows.
            float half = Mathf.Min(c.Label.x - (c.Centre.x - c.Size.x * 0.5f), c.Centre.x + c.Size.x * 0.5f - c.Label.x) - _model.TextMargin;
            float size = c.Size.y * _model.CaptionShare * (left ? 0.85f : 1f);
            var words = new Vector2(Mathf.Max(20f, half * 2f), c.Size.y);
            var t = Text("Label", rt, label, size, middle, words);
            ((RectTransform)t.transform).anchoredPosition = c.Label - c.Centre;
            t.alignment = left ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = Mathf.Min(size, 12f);   // a long line shrinks to its slab
            t.raycastTarget = false;
            return b;
        }

        /// <summary>A text about a point of the canvas (pixels from its middle).</summary>
        private TextMeshProUGUI At(string name, string text, float size, Vector2 at, Vector2 box, TextAlignmentOptions align)
        {
            var middle = new Vector2(0.5f, 0.5f);
            var t = Text(name, _root.transform, text, size, middle, box);
            ((RectTransform)t.transform).anchoredPosition = at;
            t.alignment = align;
            return t;
        }

        private Button MenuButton(string label, float y, UnityEngine.Events.UnityAction onClick, float width = 380f, float size = 40f, float dx = 0f)
        {
            var rt = Rect(label, _root.transform, new Vector2(_menuX, y), new Vector2(_menuX, y), new Vector2(dx, 0f), new Vector2(width, size + 22f));
            var img = rt.gameObject.AddComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0.08f);
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = img; b.onClick.AddListener(onClick);
            UiSound.On(b, label == "Continue" || label == "New Game" ? UiAudio.Kind.Confirm : label == "Slot" ? UiAudio.Kind.Select : UiAudio.Kind.Click);   // D-0100
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
