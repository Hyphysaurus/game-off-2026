using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The frame around the game: a start card that goes away on the first press, an end card the last Goal
    /// raises, and R to restart. Lives on the HUD; the cards are plain objects it shows and hides.
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
            if (_ended && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
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

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
