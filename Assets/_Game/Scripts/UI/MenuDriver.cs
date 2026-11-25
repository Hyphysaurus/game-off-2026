using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The hands on a MenuModel (D-0197): the mouse, the keys and the pad choose among the controls on show. The mouse
    /// over a control selects it; up and down (arrows, W S, the pad's stick or cross) move the selection past what
    /// cannot be used; a click on the selected control, Enter, Space or the pad's south button chooses it; Esc,
    /// Backspace or the pad's east button goes back; Shift, F or the pad's north button shifts the model between flat
    /// and deep. It only says what was chosen: its owner (TitleMenu) knows what that means. It reads the devices
    /// directly, on real time: the player's own input is off while a menu is up. Switch it off while another panel has
    /// the screen, and set Typing while a text field has the keys.
    /// </summary>
    public sealed class MenuDriver : MonoBehaviour
    {
        [SerializeField, Min(0.05f), Tooltip("Seconds a held direction waits before it repeats.")] private float repeatAfter = 0.38f;
        [SerializeField, Min(0.02f), Tooltip("Seconds between repeats after that.")] private float repeatEvery = 0.12f;
        [SerializeField, Range(0.1f, 0.9f), Tooltip("How far the stick must be pushed to count as a direction.")] private float stickPush = 0.5f;

        private MenuModel _model;
        private Vector2 _lastMouse = new Vector2(float.NaN, float.NaN);
        private int _held; private float _nextRepeat;
        private MenuModel.Control _pressedOn;

        /// <summary>A control was chosen: its name.</summary>
        public event Action<string> Chosen;
        /// <summary>Back was asked for.</summary>
        public event Action Back;
        /// <summary>The selection moved to another control.</summary>
        public event Action Moved;
        /// <summary>A text field has the keys: letters, Shift and Backspace are its, not the menu's.</summary>
        public bool Typing { get; set; }

        public void Bind(MenuModel model) { _model = model; }

        private void OnDisable() { _held = 0; if (_pressedOn != null) { _pressedOn.Pressed = false; _pressedOn = null; } }

        private void Update()
        {
            if (_model == null) return;
            Mouse mouse = Mouse.current; Keyboard keys = Keyboard.current; Gamepad pad = Gamepad.current;

            // The mouse: where it is leans the deep view; moving it onto a control selects that control.
            if (mouse != null)
            {
                Vector2 at = mouse.position.ReadValue();
                _model.Pointer = new Vector2(Mathf.Clamp(at.x / Mathf.Max(1f, Screen.width) * 2f - 1f, -1f, 1f), Mathf.Clamp(at.y / Mathf.Max(1f, Screen.height) * 2f - 1f, -1f, 1f));
                MenuModel.Control under = _model.Pick(at);
                bool moved = float.IsNaN(_lastMouse.x) || (at - _lastMouse).sqrMagnitude > 1f;
                _lastMouse = at;
                if (moved && under != null && under.Usable && under != _model.Selected) { _model.Selected = under; Moved?.Invoke(); }
                if (mouse.leftButton.wasPressedThisFrame && under != null && under.Usable) { _model.Selected = under; under.Pressed = true; _pressedOn = under; }
                if (mouse.leftButton.wasReleasedThisFrame && _pressedOn != null)
                {
                    MenuModel.Control was = _pressedOn; was.Pressed = false; _pressedOn = null;
                    if (under == was) { Chosen?.Invoke(was.Name); return; }
                }
            }

            // Up and down, with a repeat while held.
            int way = 0;
            if (!Typing && keys != null) way += (keys.downArrowKey.isPressed || keys.sKey.isPressed ? 1 : 0) - (keys.upArrowKey.isPressed || keys.wKey.isPressed ? 1 : 0);
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue(), cross = pad.dpad.ReadValue();
                way += (stick.y < -stickPush || cross.y < -0.5f ? 1 : 0) - (stick.y > stickPush || cross.y > 0.5f ? 1 : 0);
            }
            way = Mathf.Clamp(way, -1, 1);
            if (way == 0) _held = 0;
            else if (way != _held) { _held = way; _nextRepeat = Time.unscaledTime + repeatAfter; Step(way); }
            else if (Time.unscaledTime >= _nextRepeat) { _nextRepeat = Time.unscaledTime + repeatEvery; Step(way); }

            bool choose = (pad != null && pad.buttonSouth.wasPressedThisFrame) || (!Typing && keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame));
            bool back = (pad != null && pad.buttonEast.wasPressedThisFrame) || (keys != null && keys.escapeKey.wasPressedThisFrame) || (!Typing && keys != null && keys.backspaceKey.wasPressedThisFrame);
            bool shift = (pad != null && pad.buttonNorth.wasPressedThisFrame) || (!Typing && keys != null && (keys.leftShiftKey.wasPressedThisFrame || keys.rightShiftKey.wasPressedThisFrame || keys.fKey.wasPressedThisFrame));
            if (shift) _model.Deep = !_model.Deep;
            if (choose && _model.Selected != null && _model.Selected.Usable) { Chosen?.Invoke(_model.Selected.Name); return; }
            if (back) Back?.Invoke();
        }

        /// <summary>The selection, one usable control down (+1) or up (-1), round the ends.</summary>
        private void Step(int way)
        {
            var shown = _model.Shown;
            if (shown.Count == 0) return;
            int at = -1;
            for (int i = 0; i < shown.Count; i++) if (shown[i] == _model.Selected) { at = i; break; }
            for (int tries = 0; tries < shown.Count; tries++)
            {
                at = ((at + way) % shown.Count + shown.Count) % shown.Count;
                if (shown[at].Usable) { if (shown[at] != _model.Selected) { _model.Selected = shown[at]; Moved?.Invoke(); } return; }
            }
        }
    }
}
