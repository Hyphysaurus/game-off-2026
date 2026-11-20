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
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string CourseScene = "Greybox";

        public static bool IsOpen { get; private set; }

        private GameObject _root;
        private Button _resume, _optionsButton;
        private OptionsPanel _options;

        private PlayerInput _input; private ShoulderCamera _camera;
        private bool _inputWasOn, _cameraWasOn;
        private float _timeScale = 1f;

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

        /// <summary>A new scene starts unpaused (Back to Title and Restart load one).</summary>
        private void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (_options.IsOpen) _options.Close();
            _root.SetActive(false);
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
                if (press || (pad != null && pad.buttonEast.wasPressedThisFrame)) Resume();
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
            if (_camera != null) _camera.enabled = false;   // its OnDisable frees the mouse
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            MenuKit.EnsureEventSystem();
            _root.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(_resume.gameObject);
        }

        private void Resume()
        {
            _root.SetActive(false);
            IsOpen = false;
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
        private void BackToTitle()
        {
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
            _optionsButton = MenuKit.Button(_root.transform, "Options", new Vector2(0.5f, y), 0f, 380f, 40f, OpenOptions); y -= 0.075f;
            MenuKit.Button(_root.transform, "Back to Title", new Vector2(0.5f, y), 0f, 380f, 40f, BackToTitle); y -= 0.075f;
            MenuKit.Button(_root.transform, "Quit", new Vector2(0.5f, y), 0f, 380f, 40f, Quit);
            MenuKit.Text("Hint", _root.transform, "Esc / Start to resume · progress keeps from the last save point", 22, new Vector2(0.5f, 0.05f), new Vector2(1000, 40)).alpha = 0.6f;
            _root.SetActive(false);
        }
    }
}
