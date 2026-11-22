using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShiftJam
{
    /// <summary>
    /// The frame around the game: a start card that goes away on the first press (the title menu hides it: it shows only
    /// in a scene with no title), an end card with his title drawing over it that the last Goal
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
            // His drawing over the end card's words (D-0099); it pops in when the card comes up.
            if (endCard != null && endCard.transform.Find("TitleArt") == null)
                TitleArt.Place(endCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(900f, 340f));
            // Unity runs Awake even on a switched-off component. A switched-off flow (the Workshop's) must not raise a
            // start card that only its own Update can take down.
            if (!enabled) return;
            // Back through a door from another room (D-0091), or online (the party is under way: a door took everyone here,
            // or a friend is joining, D-0134): no "press any key".
            if (HouseCarry.Arriving || NetSession.InParty) { _started = true; if (startCard != null) startCard.SetActive(false); if (endCard != null) endCard.SetActive(false); return; }
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
            // Online the house is a sandbox (D-0125): no ending. Its card's R and P would load a scene on one PC only and
            // take that player out of the party's world (D-0134).
            if (_ended || NetSession.InParty) return;
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
