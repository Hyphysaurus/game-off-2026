using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShiftJam
{
    /// <summary>
    /// The key that sends a race off from the grid (D-0061): R, or LB on a pad, while sitting in a car. It only asks the
    /// grid (RaceGrid.StartRace decides whether a race can start) and tells the HUD what to print on its prompt. Like
    /// the Workshop's reset key it reads the device itself: the car's own controls stay PlayerInput's.
    /// </summary>
    [RequireComponent(typeof(RaceGrid))]
    public sealed class RaceStartKey : MonoBehaviour
    {
        [SerializeField] private Key startKey = Key.R;
        [SerializeField] private GamepadButton padButton = GamepadButton.LeftShoulder;
        [SerializeField, Tooltip("What the HUD's prompt calls the key.")] private string keyName = "R";
        [SerializeField, Tooltip("What the HUD's prompt calls the pad button.")] private string padName = "LB";

        private RaceGrid _grid;

        /// <summary>The key's and the pad button's names, for the prompt (null when no grid is open).</summary>
        public static string KeyName { get; private set; }
        public static string PadName { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { KeyName = PadName = null; }

        private void Awake() { _grid = GetComponent<RaceGrid>(); }
        private void OnEnable() { KeyName = keyName; PadName = padName; }
        private void OnDisable() { KeyName = PadName = null; }

        private void Update()
        {
            if (DebugConsole.IsOpen) return;   // typing an r is not a start
            var k = Keyboard.current; var pad = Gamepad.current;
            bool pressed = (k != null && startKey != Key.None && k[startKey].wasPressedThisFrame) || (pad != null && pad[padButton].wasPressedThisFrame);
            if (pressed) _grid.StartRace();
        }
    }
}
