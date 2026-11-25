using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// Esc or the pad's Start pauses (D-0044): Resume, Options, Back to Title, Quit. Time stops, sounds pause, the player's
    /// input and the camera switch off and the mouse is freed; Resume puts each back as it was. It only opens on foot or
    /// driving in Play mode: Esc keeps its other jobs first (leaving Build Mode or first person, closing the H sheet, the
    /// console, the garage), which is why it runs before every other script each frame and checks their state as it was
    /// before this frame's Esc. Made by code at start, lives across scenes, like KeyHelp.
    /// On the menu model (D-0204): with the title menu's model prefab there, the pause menu is that model's slabs over
    /// the paused game (its own copy of the prefab, made at the first pause of a scene and kept until the scene goes,
    /// stacked on the game camera: switching the ShoulderCamera component off leaves its Camera drawing), page "Pause"
    /// (Made Controls on the prefab): the guns, the paint and Shift for 3D work as on the title; the title's letters do
    /// not show (only the title stands them). Options is its page "Options" on the same model (MenuOptions), Back
    /// returns to the pause page. Esc, Start and the pad's B are still read here, first in the frame, so its driver is
    /// told they are not its own. Without the model (no prefab, or a slab missing) it is the old menu, as before.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string CourseScene = "Greybox";
        private const string Page = "Pause";
        private const string Resume_ = "Pause_Resume", Options_ = "Pause_Options", Title_ = "Pause_Title", Quit_ = "Pause_Quit";

        public static bool IsOpen { get; private set; }

        private GameObject _root;
        private Button _resume, _optionsButton;
        private OptionsPanel _options;

        private PlayerInput _input; private ShoulderCamera _camera;
        private bool _inputWasOn, _cameraWasOn;
        private float _timeScale = 1f;

        // ---- on the menu model (D-0204)
        private MenuModel _model;           // this scene's copy of the menu model; null = the old menu
        private MenuDriver _driver;
        private MenuOptions _modelOptions;  // Options on the model; null = the old panel over it
        private bool _onModel;              // this pause is on the model
        private bool _modelFailed;          // the model could not be had in this scene: not tried again until the next
        private GameObject _modelCanvas;    // the few flat words over the model: its heading and its hint
        private TMPro.TextMeshProUGUI _heading, _hint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            IsOpen = false;
            if (FindFirstObjectByType<PauseMenu>() != null) return;
            var go = new GameObject("PauseMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            Build();
            _options = OptionsPanel.Create(transform, 56);
        }

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        /// <summary>A new scene starts unpaused (Back to Title and Restart load one). The model went with the old scene.</summary>
        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (_options.IsOpen) _options.Close();
            _root.SetActive(false);
            HideModel();
            if (_model != null) Destroy(_model.gameObject);
            _model = null; _modelOptions = null; _modelFailed = false;
            IsOpen = false;
            AudioListener.pause = false;
            _input = null; _camera = null;
        }

        private void Update()
        {
            var k = Keyboard.current; var pad = Gamepad.current;
            bool press = (k != null && k.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (IsOpen)
            {
                // Options handles its own way back; B steps back from the pause menu too.
                if (_options.IsOpen) return;
                if (press || (pad != null && pad.buttonEast.wasPressedThisFrame))
                {
                    if (_onModel && _model != null && _model.Page != Page) { UiAudio.Play(UiAudio.Kind.Back); ToPause(Options_); }   // from Options on the model: back to the pause page
                    else Resume();
                }
                return;
            }
            if (press && CanPause()) Open();
        }

        private static bool CanPause()
        {
            if (GameMode.Current != GameMode.Mode.Play || TitleMenu.IsOpen || Opening.Playing || KeyHelp.IsOpen || DebugConsole.IsOpen) return false;
            if (Time.timeScale <= 0f || GameObject.Find("Player") == null) return false;
            var cam = FindFirstObjectByType<ShoulderCamera>();
            return cam == null || !cam.MenuHeld;   // the garage menu keeps Start for driving out
        }

        private void Open()
        {
            IsOpen = true;
            var player = GameObject.Find("Player");
            _input = player != null ? player.GetComponent<PlayerInput>() : null;
            _camera = FindFirstObjectByType<ShoulderCamera>();
            _inputWasOn = _input != null && _input.enabled;
            _cameraWasOn = _camera != null && _camera.enabled;
            if (_input != null) _input.enabled = false;
            if (_camera != null) _camera.enabled = false;   // its OnDisable frees the mouse (its Camera keeps drawing: the model is stacked on it)
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _timeScale = Time.timeScale;
            if (!NetSession.Online) { Time.timeScale = 0f; AudioListener.pause = true; }   // online the game goes on for the others (D-0111)
            MenuKit.EnsureEventSystem();
            _onModel = HaveModel();
            UiAudio.Play(UiAudio.Kind.Open);
            UiAudio.Hush();
            if (_onModel) { ShowModel(); return; }
            if (_backLabel != null) _backLabel.text = NetSession.Online ? "Leave Party" : "Back to Title";
            _root.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(_resume.gameObject);
        }

        private void Resume()
        {
            _root.SetActive(false);
            HideModel();
            IsOpen = false;
            UiAudio.Play(UiAudio.Kind.Back);
            Time.timeScale = _timeScale;
            AudioListener.pause = false;
            if (_input != null && _inputWasOn) _input.enabled = true;
            if (_camera != null && _cameraWasOn) _camera.enabled = true;   // its OnEnable locks the mouse again
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OpenOptions()
        {
            _root.SetActive(false);
            _options.Open(() =>
            {
                _root.SetActive(true);
                EventSystem.current?.SetSelectedGameObject(_optionsButton.gameObject);
            });
        }

        /// <summary>The course reloads to its title; the Workshop has none, so it goes to the course's.</summary>
        private TMPro.TMP_Text _backLabel;   // "Leave Party" while online (D-0121)

        private void BackToTitle()
        {
            if (NetSession.Online) { IsOpen = false; _root.SetActive(false); HideModel(); NetSession.Leave(); return; }   // leave the party, then the title (D-0111)
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameMode.ResetQuietly();
            var active = SceneManager.GetActiveScene();
            if (active.name == CourseScene) SceneManager.LoadScene(active.buildIndex);
            else SceneManager.LoadScene(CourseScene);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ on the menu model (D-0204)

        /// <summary>This scene's model, made at the first pause on the game camera and dressed with the pause page (and
        /// Options). False when there is none to have: the old menu shows.</summary>
        private bool HaveModel()
        {
            Camera game = _camera != null ? _camera.View : Camera.main;
            if (_model != null && _model.Under != game) { Destroy(_model.gameObject); _model = null; _modelOptions = null; }   // the game camera changed (a party's): a new one on it
            if (_model != null) return game != null && game.rect.width >= 0.999f && game.rect.height >= 0.999f;
            if (_modelFailed || game == null || !game.isActiveAndEnabled) return false;
            Rect view = game.rect;      // a party sharing the screen (D-0110): the model would only draw in the first player's part of it
            if (view.width < 0.999f || view.height < 0.999f) return false;
            _model = MenuModel.Spawn(game, null);
            string missing = null;
            if (_model != null && !_model.MakePage(Page, out missing)) { Debug.LogWarning("PauseMenu: the old pause menu shows (" + missing + ").", _model); Destroy(_model.gameObject); _model = null; }
            if (_model == null) { _modelFailed = true; return false; }
            _modelOptions = MenuOptions.Make(_model);      // null: Options opens the old panel over the paused game
            _model.Show(false);
            if (_driver == null)
            {
                _driver = gameObject.AddComponent<MenuDriver>();
                _driver.OwnerTakesEsc = true;       // Esc, Start and B are read here, first in the frame
                _driver.Chosen += OnChosen; _driver.Back += OnBack; _driver.Moved += OnMoved;
                _driver.Stepped += (control, way) => { if (_modelOptions != null) _modelOptions.Step(control, way); };
            }
            _driver.enabled = false;
            _driver.Bind(_model);
            if (_driver.Darts != null)
            {
                _driver.Darts.Swapped += () => { UiAudio.Play(UiAudio.Kind.Select); ShowHint(); };
                _driver.Darts.Reloaded += () => { UiAudio.Play(UiAudio.Kind.Select); ShowHint(); };   // the hint names the ammo loaded (D-0201)
            }
            BuildModelCanvas();
            return true;
        }

        /// <summary>The model on the screen with the pause page, its words as they are now.</summary>
        private void ShowModel()
        {
            _model.SetWords(Resume_, "Resume");
            _model.SetWords(Options_, "Options");
            _model.SetWords(Title_, NetSession.Online ? "Leave Party" : "Back to Title", NetSession.Online ? null : "progress keeps from the last save point");
            _model.SetWords(Quit_, "Quit");
            _model.Show(true);
            _modelCanvas.SetActive(true);
            _driver.enabled = true;
            ToPause(null);
        }

        private void HideModel()
        {
            if (_model != null) _model.Show(false);
            if (_driver != null) _driver.enabled = false;
            if (_modelCanvas != null) _modelCanvas.SetActive(false);
        }

        /// <summary>The pause page, with <paramref name="select"/> selected (null: the first, Resume).</summary>
        private void ToPause(string select)
        {
            _model.ShowPage(Page);
            if (select != null && _model.TryGet(select, out MenuModel.Control c) && c.Shown) _model.Selected = c;
            _heading.text = "<b>PAUSED</b>";
            ShowHint();
        }

        private void ShowHint()
        {
            if (_hint == null || _driver == null || _model == null) return;
            _hint.text = _model.Page == MenuOptions.Page ? _driver.HintLine("Esc / B goes back", MenuOptions.HintMore) : _driver.HintLine("Esc / Start resumes");
        }

        /// <summary>Options on the model: its page, the options as they are now.</summary>
        private void ToOptions()
        {
            _modelOptions.Open();
            _model.ShowPage(MenuOptions.Page);
            _heading.text = "<b>OPTIONS</b>";
            ShowHint();
        }

        private static void OnMoved() => UiAudio.Play(UiAudio.Kind.Select);

        /// <summary>The model's driver says a control was chosen.</summary>
        private void OnChosen(string control)
        {
            switch (control)
            {
                case Resume_: Resume(); break;      // Resume plays Back itself (Esc too)
                case Options_:
                    UiAudio.Play(UiAudio.Kind.Click);
                    if (_modelOptions != null) { ToOptions(); break; }
                    // Without its slabs, the old Options panel over the paused game: the model hides while it is up and comes back with Options selected.
                    HideModel();
                    _options.Open(() => { if (!IsOpen || _model == null) return; _model.Show(true); _modelCanvas.SetActive(true); _driver.enabled = true; ToPause(Options_); UiAudio.Hush(); });
                    break;
                case Title_: UiAudio.Play(UiAudio.Kind.Confirm); BackToTitle(); break;
                case Quit_: UiAudio.Play(UiAudio.Kind.Click); Quit(); break;
                case MenuOptions.Back: UiAudio.Play(UiAudio.Kind.Back); ToPause(Options_); break;
                default:
                    if (_modelOptions != null && MenuOptions.Owns(control)) { UiAudio.Play(UiAudio.Kind.Click); _modelOptions.Choose(control); }
                    break;
            }
        }

        /// <summary>Backspace (Esc, Start and B are read in Update).</summary>
        private void OnBack()
        {
            if (_model != null && _model.Page != Page) { UiAudio.Play(UiAudio.Kind.Back); ToPause(Options_); }
            else Resume();
        }

        /// <summary>The heading and the hint over the model, at its Anchor_Title and Anchor_Hint (canvas pixels from the
        /// middle of a 1080-high screen, as the model is drawn). Made once; the model's places are the same every scene.</summary>
        private void BuildModelCanvas()
        {
            if (_modelCanvas == null)
            {
                _modelCanvas = new GameObject("PauseModelCanvas", typeof(RectTransform));
                _modelCanvas.transform.SetParent(transform, false);
                var canvas = _modelCanvas.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 55;
                var scaler = _modelCanvas.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1f;
                _heading = MenuKit.Text("Heading", _modelCanvas.transform, "<b>PAUSED</b>", 72, new Vector2(0.5f, 0.5f), new Vector2(900, 100));
                _hint = MenuKit.Text("Hint", _modelCanvas.transform, "", 20, new Vector2(0.5f, 0.5f), new Vector2(1500, 30));
                _hint.alpha = 0.6f;
                _modelCanvas.SetActive(false);
            }
            ((RectTransform)_heading.transform).anchoredPosition = _model.Point("Anchor_Title", out Vector2 top) ? top : new Vector2(0f, 400f);
            ((RectTransform)_hint.transform).anchoredPosition = _model.Point("Anchor_Hint", out Vector2 bottom) ? bottom : new Vector2(0f, -500f);
        }

        // ------------------------------------------------------------------ building the canvas
        private void Build()
        {
            _root = new GameObject("PauseCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 55;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            MenuKit.Rect("Dim", _root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.72f);
            MenuKit.Text("Title", _root.transform, "<b>PAUSED</b>", 72, new Vector2(0.5f, 0.7f), new Vector2(900, 100));

            float y = 0.55f;
            _resume = MenuKit.Button(_root.transform, "Resume", new Vector2(0.5f, y), 0f, 380f, 40f, Resume); y -= 0.075f;
            UiSound.On(_resume).Press = UiAudio.Kind.None;   // Resume plays Back itself (Esc too)
            _optionsButton = MenuKit.Button(_root.transform, "Options", new Vector2(0.5f, y), 0f, 380f, 40f, OpenOptions); y -= 0.075f;
            var back = MenuKit.Button(_root.transform, "Back to Title", new Vector2(0.5f, y), 0f, 380f, 40f, BackToTitle);
            UiSound.On(back).Press = UiAudio.Kind.Confirm; y -= 0.075f;
            _backLabel = back.GetComponentInChildren<TMPro.TMP_Text>();
            MenuKit.Button(_root.transform, "Quit", new Vector2(0.5f, y), 0f, 380f, 40f, Quit);
            MenuKit.Text("Hint", _root.transform, "Esc / Start to resume · progress keeps from the last save point", 22, new Vector2(0.5f, 0.05f), new Vector2(1000, 40)).alpha = 0.6f;
            _root.SetActive(false);
        }
    }
}
