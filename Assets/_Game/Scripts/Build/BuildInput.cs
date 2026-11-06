using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The devices as Build Mode reads them: modifier keys, wheel notches, and whether the mouse is over the build UI or a
    /// text box has the keyboard. Build Mode is an editor tool, so it reads the devices directly like the debug console.
    /// </summary>
    public static class BuildInput
    {
        public static Keyboard Keys => Keyboard.current;
        public static Mouse Pointer => Mouse.current;

        public static bool Ctrl => Keys != null && (Keys.leftCtrlKey.isPressed || Keys.rightCtrlKey.isPressed);
        public static bool Shift => Keys != null && (Keys.leftShiftKey.isPressed || Keys.rightShiftKey.isPressed);
        public static bool Alt => Keys != null && (Keys.leftAltKey.isPressed || Keys.rightAltKey.isPressed);

        public static bool Pressed(Key key) => Keys != null && Keys[key].wasPressedThisFrame;
        public static bool Held(Key key) => Keys != null && Keys[key].isPressed;

        public static Vector2 MousePosition => Pointer != null ? Pointer.position.ReadValue() : Vector2.zero;

        /// <summary>Wheel notches this frame, up positive. Works whether the platform reports 1 or 120 per notch.</summary>
        public static float WheelNotches
        {
            get
            {
                if (Pointer == null) return 0f;
                float y = Pointer.scroll.ReadValue().y;
                return Mathf.Abs(y) > 10f ? y / 120f : y;
            }
        }

        /// <summary>True while the debug console is open. It is drawn with IMGUI, which the EventSystem cannot see, so it is
        /// asked by name: while it is open it has the keyboard and the mouse.</summary>
        public static bool ConsoleOpen => DebugConsole.IsOpen;

        /// <summary>True while the mouse is over any part of the build UI, or the console is open.</summary>
        public static bool OverUI => ConsoleOpen || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());

        /// <summary>True while a text box (or the console) has the keyboard: hotkeys stay quiet.</summary>
        public static bool Typing
        {
            get
            {
                if (ConsoleOpen) return true;
                var es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject == null) return false;
                return es.currentSelectedGameObject.TryGetComponent(out TMP_InputField field) && field.isFocused;
            }
        }
    }
}
