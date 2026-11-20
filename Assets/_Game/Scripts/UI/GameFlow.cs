using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The frame around the game: a start card that goes away on the first press, an end card the last Goal
    /// raises, and its two choices: R (pad A) to play again, P (pad Y) to go to the playroom, the Workshop sandbox
    /// (D-0086). Lives on the HUD; the cards are plain objects it shows and hides.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] private GameObject startCard;
        [SerializeField] private GameObject endCard;
        [SerializeField, Tooltip("Found on the HUD if empty.")] private ScreenFade fade;
        [SerializeField] private float endFade = 1.2f;

        private bool _ended, _started;

        private void Awake()
        {
            if (fade == null) fade = ScreenFade.Find();
            // Unity runs Awake even on a switched-off component. A switched-off flow (the Workshop's) must not raise a
            // start card that only its own Update can take down.
            if (!enabled) return;
            // Back through a door from another room (D-0091): the game is under way, no "press any key".
            if (HouseCarry.Arriving) { _started = true; if (startCard != null) startCard.SetActive(false); if (endCard != null) endCard.SetActive(false); return; }
            if (startCard != null) startCard.SetActive(true);
            if (endCard != null) endCard.SetActive(false);
        }

        private void Update()
        {
            if (!_started && AnyPress())
            {
                _started = true;
                if (startCard != null) startCard.SetActive(false);
            }
            // The choices count only once the card is up: A is also jump, and R also reloads, on the way into the goal.
            if (_ended && endCard != null && endCard.activeInHierarchy)
            {
                var k = Keyboard.current; var g = Gamepad.current;
                if ((k != null && k.rKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame)) Restart();
                else if ((k != null && k.pKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame)) Playroom();
            }
        }

        private static bool AnyPress()
        {
            var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
            return (k != null && k.anyKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame) || (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
        }

        /// <summary>The last Goal calls this: fade to black, show the end card.</summary>
        public void End()
        {
            if (_ended) return;
            _ended = true;
            if (fade != null) fade.Cover(endFade, () => { if (endCard != null) endCard.SetActive(true); });
            else if (endCard != null) endCard.SetActive(true);
        }

        /// <summary>The end card's second choice: the Workshop, where everything can be tried freely.</summary>
        public void Playroom()
        {
            Time.timeScale = 1f;
            GameEvents.RaiseEndChoice(true);
            SceneManager.LoadScene(TitleMenu.WorkshopScene);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            GameEvents.RaiseEndChoice(false);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
