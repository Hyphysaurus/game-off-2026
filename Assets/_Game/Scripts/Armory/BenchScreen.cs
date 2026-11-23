using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A small screen of rows, each "name  &lt;  value  &gt;", with a Done button under them (D-0167: the armory bench's
    /// screen). It is only a view with its own keys: it knows nothing about guns. Whoever opens it says how many rows
    /// there are and what each shows (SetRows), and is told "row 2, one step right" (onStep) and "the player is done"
    /// (onDone).
    /// WHY it reads the keys itself and selects nothing in the menu system: the player's own input is switched off
    /// while a bench is open, and in a party each player has their own devices (DevicesOf), which the shared menu
    /// system does not know about. The mouse still works: the arrows and Done are ordinary buttons.
    /// WHY it runs at order -60: after the pause menu (-100) has looked at this frame's Esc, and before first person
    /// (-50) and the key sheet (0) act on it, so "is the key sheet open" is read as it was before this frame's key.
    /// First person leaves on Esc too, reading the same key press later in the frame: TookEsc tells it this screen
    /// has already used the key.
    /// Made in code like the garage's menu (no prefab, no scene UI); Hide destroys it.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class BenchScreen : MonoBehaviour
    {
        private const float PanelWidth = 520f, PanelBase = 206f, RowStep = 60f, RowHeight = 52f;
        private const string DoneLabel = "Done";
        private static readonly Color RowLit = new Color(0.35f, 0.85f, 1f, 0.25f);
        private static readonly Color RowDim = new Color(1f, 1f, 1f, 0.05f);
        private static int _escFrame = -1;   // the frame a screen last closed on the keyboard's Esc

        /// <summary>True for the rest of the frame in which a screen closed on the keyboard's Esc (D-0167). WHY: a
        /// key press cannot be used up, and first person (FirstPersonMode, order -50) reads the same Esc after this
        /// screen (order -60) and would leave first person as well. It asks this first and stands back, so Esc at
        /// the bench only closes the bench.</summary>
        public static bool TookEsc => _escFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _escFrame = -1; }

        /// <summary>The parts of one row that change after it is built.</summary>
        private sealed class Row
        {
            public Image fill;
            public TextMeshProUGUI label, value;
        }

        private readonly List<Row> _rowList = new List<Row>();
        private RectTransform _panel, _rows;
        private Button _done;
        private PlayerInput _player;
        private System.Action<int, int> _onStep;
        private System.Action _onDone;
        private float _repeatDelay, _repeatEvery;
        private int _selected;             // 0 .. row count; the last one is Done
        private int _openedFrame;
        private bool _closed;
        private int _heldX, _heldY;        // the direction held last frame, -1 / 0 / +1
        private float _nextX, _nextY;      // when a held direction fires again
        private bool _restX = true, _restY = true;   // a direction held from before does not count until it is let go

        /// <summary>Opens the screen. <paramref name="player"/> is whose devices drive it (null: every device).
        /// <paramref name="onStep"/> gets (row, -1 or +1); <paramref name="onDone"/> is called on Done, Esc, B or
        /// Start and may close the screen. Call SetRows next: it opens with no rows.</summary>
        public static BenchScreen Show(string title, string hint, PlayerInput player, float repeatDelay, float repeatEvery, System.Action<int, int> onStep, System.Action onDone)
        {
            // The canvas, as the garage's menu makes its own.
            var go = new GameObject("BenchScreen", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            MenuKit.EnsureEventSystem();   // the mouse needs one; nothing is selected in it (this script handles Enter)

            var screen = go.AddComponent<BenchScreen>();
            screen._player = player;
            screen._repeatDelay = Mathf.Max(0f, repeatDelay);
            screen._repeatEvery = Mathf.Max(0.02f, repeatEvery);
            screen._onStep = onStep;
            screen._onDone = onDone;
            screen._openedFrame = Time.frameCount;
            screen.Build(title, hint);
            return screen;
        }

        /// <summary>Says what the rows are now. The rows are rebuilt only when their number changed; the words and
        /// the highlight are set every time.</summary>
        public void SetRows(int count, System.Func<int, string> label, System.Func<int, string> value)
        {
            if (_closed || _rows == null) return;
            if (count < 0) count = 0;
            if (count != _rowList.Count) Rebuild(count);
            for (int i = 0; i < _rowList.Count; i++)
            {
                _rowList[i].label.text = label != null ? label(i) : "";
                _rowList[i].value.text = value != null ? value(i) : "";
            }
            if (_selected > count) _selected = count;
            Paint();
        }

        /// <summary>Closes the screen for good.</summary>
        public void Hide()
        {
            if (_closed) return;
            _closed = true;   // its Update may still run once this frame: it does nothing more
            _onStep = null;
            _onDone = null;
            Deselect();
            Destroy(gameObject);
        }

        /// <summary>The keyboard and the pad that drive <paramref name="player"/> (either may be null): its own paired
        /// devices in a party, whatever is connected in single player. So one player's pad never works another
        /// player's bench.</summary>
        public static void DevicesOf(PlayerInput player, out Keyboard keys, out Gamepad pad)
        {
            if (player == null || player.Paired == null) { keys = Keyboard.current; pad = Gamepad.current; return; }
            keys = null;
            pad = null;
            InputDevice[] paired = player.Paired;
            for (int i = 0; i < paired.Length; i++)
            {
                if (keys == null) keys = paired[i] as Keyboard;
                if (pad == null) pad = paired[i] as Gamepad;
            }
        }

        private void Build(string title, string hint)
        {
            _panel = BuildUiKit.Rect("Panel", transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(PanelWidth, PanelBase));
            BuildUiKit.Panel(_panel, BuildUiKit.PanelColor);
            var top = BuildUiKit.Rect("Title", _panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-32f, 56f));
            BuildUiKit.Label(top, title, 36).fontStyle = FontStyles.Bold;
            var note = BuildUiKit.Rect("Hint", _panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(-32f, 30f));
            BuildUiKit.Label(note, hint, 18).color = new Color(1f, 1f, 1f, 0.6f);

            // A plain column from under the hint down to the Done button: a gun has a handful of rows, so no scroll view.
            _rows = BuildUiKit.Rect("Rows", _panel, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _rows.offsetMin = new Vector2(16f, 90f);
            _rows.offsetMax = new Vector2(-16f, -110f);
            var layout = _rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;

            var foot = BuildUiKit.Rect("Foot", _panel, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-32f, 56f));
            _done = BuildUiKit.Button(foot, DoneLabel, 26, PressDone, out _);
            Stretch(_done.GetComponent<RectTransform>());
            Paint();
        }

        private void Rebuild(int count)
        {
            // Destroy waits for the end of the frame: switched off first, the old rows take no room in the column meanwhile.
            for (int i = _rows.childCount - 1; i >= 0; i--)
            {
                GameObject old = _rows.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            _rowList.Clear();
            for (int i = 0; i < count; i++) _rowList.Add(MakeRow(i));
            _panel.sizeDelta = new Vector2(PanelWidth, PanelBase + count * RowStep);
        }

        /// <summary>One row, laid out with the garage menu's numbers (its own row maker is private to it): the name on
        /// the left 38%, then a left arrow, the value, a right arrow.</summary>
        private Row MakeRow(int index)
        {
            var row = BuildUiKit.Rect("Row", _rows, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, RowHeight));
            var made = new Row();
            made.fill = BuildUiKit.Panel(row, RowDim);
            var name = BuildUiKit.Rect("Name", row, new Vector2(0f, 0f), new Vector2(0.38f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            name.offsetMin = new Vector2(14f, 0f);
            name.offsetMax = Vector2.zero;
            made.label = BuildUiKit.Label(name, "", 22, TextAlignmentOptions.MidlineLeft);
            var prev = BuildUiKit.Rect("Prev", row, new Vector2(0.38f, 0.5f), new Vector2(0.38f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            Stretch(BuildUiKit.Button(prev, "<", 26, () => Click(index, -1), out _).GetComponent<RectTransform>());
            var val = BuildUiKit.Rect("Value", row, new Vector2(0.38f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            val.offsetMin = new Vector2(52f, 0f);
            val.offsetMax = new Vector2(-60f, 0f);
            made.value = BuildUiKit.Label(val, "", 22);
            made.value.color = new Color(0.75f, 0.92f, 1f);
            var next = BuildUiKit.Rect("Next", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(44f, 44f));
            Stretch(BuildUiKit.Button(next, ">", 26, () => Click(index, 1), out _).GetComponent<RectTransform>());
            return made;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>The highlight: the picked row is lit, the others dim; Done is lit when it is the pick.</summary>
        private void Paint()
        {
            for (int i = 0; i < _rowList.Count; i++) _rowList[i].fill.color = i == _selected ? RowLit : RowDim;
            BuildUiKit.SetFill(_done, _selected == _rowList.Count ? BuildUiKit.ActiveColor : BuildUiKit.ButtonColor);
        }

        /// <summary>A mouse click on a row's arrow.</summary>
        private void Click(int row, int direction)
        {
            if (_closed || _onStep == null) return;
            _selected = row;
            Paint();
            _onStep(row, direction);
            UiAudio.Play(UiAudio.Kind.Click);
            // The click left that arrow selected in the menu system: cleared, so a later Enter is not sent to it too.
            Deselect();
        }

        /// <summary>A mouse click on Done.</summary>
        private void PressDone()
        {
            if (_closed || _onDone == null) return;
            Deselect();
            _onDone();   // may destroy this screen: nothing after it
        }

        private static void Deselect()
        {
            EventSystem menus = EventSystem.current;
            if (menus != null) menus.SetSelectedGameObject(null);
        }

        private void Update()
        {
            if (_closed) return;
            // The frame it opened (the press that opened it is still "this frame"), and while the key sheet or the
            // console is up (they own the keys): nothing is read, and a direction held meanwhile must be let go first.
            if (Time.frameCount == _openedFrame || KeyHelp.IsOpen || DebugConsole.IsOpen) { _restX = _restY = true; return; }

            DevicesOf(_player, out Keyboard keys, out Gamepad pad);   // every read below is behind a null check
            bool up = false, down = false, left = false, right = false, submit = false, close = false;
            if (keys != null)
            {
                up = keys.wKey.isPressed || keys.upArrowKey.isPressed;
                down = keys.sKey.isPressed || keys.downArrowKey.isPressed;
                left = keys.aKey.isPressed || keys.leftArrowKey.isPressed;
                right = keys.dKey.isPressed || keys.rightArrowKey.isPressed;
                submit = keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame;
                close = keys.escapeKey.wasPressedThisFrame;
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                up |= pad.dpad.up.isPressed || stick.y > 0.5f;
                down |= pad.dpad.down.isPressed || stick.y < -0.5f;
                left |= pad.dpad.left.isPressed || stick.x < -0.5f;
                right |= pad.dpad.right.isPressed || stick.x > 0.5f;
                submit |= pad.buttonSouth.wasPressedThisFrame;
                close |= pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame;
            }

            if (close)
            {
                // Noted before the call (closing may destroy this screen). Only the keyboard's Esc: first person does not read the pad.
                if (keys != null && keys.escapeKey.wasPressedThisFrame) _escFrame = Time.frameCount;
                if (_onDone != null) _onDone();
                return;   // it may have destroyed this screen
            }

            int dy = up == down ? 0 : (up ? 1 : -1);      // none, or both at once = 0
            int dx = left == right ? 0 : (right ? 1 : -1);
            // Held from before the screen had the keys (walking up to the bench with W down): not a pick until let go.
            if (_restY) { if (dy == 0) _restY = false; dy = 0; }
            if (_restX) { if (dx == 0) _restX = false; dx = 0; }

            int count = _rowList.Count;
            if (Fires(dy, ref _heldY, ref _nextY))
            {
                _selected = (_selected - dy + count + 1) % (count + 1);   // up the screen is a lower row number
                Paint();
                UiAudio.Play(UiAudio.Kind.Move);
            }
            if (Fires(dx, ref _heldX, ref _nextX) && _selected < count && _onStep != null)
            {
                _onStep(_selected, dx);
                UiAudio.Play(UiAudio.Kind.Click);
            }
            if (!submit) return;
            if (_selected >= count)
            {
                if (_onDone != null) _onDone();
                return;   // it may have destroyed this screen
            }
            if (_onStep == null) return;
            _onStep(_selected, 1);
            UiAudio.Play(UiAudio.Kind.Click);
        }

        /// <summary>A held direction fires on the frame it starts, again after the repeat delay, then at the repeat
        /// rate for as long as it stays the same (hold right to run through a long list).</summary>
        private bool Fires(int value, ref int held, ref float next)
        {
            if (value == 0) { held = 0; return false; }
            float now = Time.unscaledTime;
            if (value != held) { held = value; next = now + _repeatDelay; return true; }
            if (now < next) return false;
            next = now + _repeatEvery;
            return true;
        }
    }
}
