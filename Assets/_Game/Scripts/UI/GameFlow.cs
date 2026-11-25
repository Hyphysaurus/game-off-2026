using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The frame around the game: a start card that goes away on the first press (the title menu hides it: it shows only
    /// in a scene with no title), an end card with his title drawing over it that the last Goal
    /// raises, and its two choices: R (pad A) to play again, P (pad Y) to go to the playroom, the Workshop sandbox
    /// (D-0086). Lives on the HUD; the cards are plain objects it shows and hides.
    /// The end card also counts the gold stars (D-0220): a star and "n of 10 gold stars" (ObjectiveSet's End Card Format)
    /// under its title, the choices moved down to make room. Made in code the first time the card comes up; no line when
    /// the set has no stars or the format is empty.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] private GameObject startCard;
        [SerializeField] private GameObject endCard;
        [SerializeField, Tooltip("Found on the HUD if empty.")] private ScreenFade fade;
        [SerializeField] private float endFade = 1.2f;
        [Header("Gold stars on the end card (D-0220)")]
        [SerializeField, Tooltip("Where the star count sits on the end card: up from the card's middle (the title is at 60, "
                               + "the choices at -60 before they move).")] private float starsY = -30f;
        [SerializeField, Tooltip("The star count's letter size.")] private float starsSize = 48f;
        [SerializeField, Tooltip("How far the choices (the card's Sub line) move down to make room for the star count.")]
        private float choicesDrop = 60f;

        private bool _ended, _started;
        private TMP_Text _stars;
        private RectTransform _starsIcon;

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
            if (fade != null) fade.Cover(endFade, ShowEndCard);
            else ShowEndCard();
        }

        private void OnEnable() => Objectives.Changed += OnObjectives;
        private void OnDisable() => Objectives.Changed -= OnObjectives;

        /// <summary>A star earned with the card already up (the sill's own star lands with the Goal): count again.</summary>
        private void OnObjectives() { if (_stars != null && endCard != null && endCard.activeInHierarchy) CountStars(); }

        private void ShowEndCard()
        {
            if (endCard == null) return;
            endCard.SetActive(true);
            CountStars();
        }

        /// <summary>The end card's star line (D-0220): made under the title the first time, filled with the count now.</summary>
        private void CountStars()
        {
            ObjectiveSet set = Objectives.Set;
            int total = Objectives.StarTotal;
            if (total <= 0 || string.IsNullOrEmpty(set.endCardFormat)) { if (_stars != null) _stars.transform.parent.gameObject.SetActive(false); return; }
            if (_stars == null)
            {
                var title = endCard.transform.Find("Title") != null ? endCard.transform.Find("Title").GetComponent<TMP_Text>() : null;
                var line = new GameObject("Stars", typeof(RectTransform));
                var lineRt = (RectTransform)line.transform;
                lineRt.SetParent(endCard.transform, false);
                lineRt.anchorMin = lineRt.anchorMax = lineRt.pivot = new Vector2(0.5f, 0.5f);
                lineRt.anchoredPosition = new Vector2(0f, starsY); lineRt.sizeDelta = new Vector2(1400f, starsSize * 1.3f);
                var words = new GameObject("Words", typeof(RectTransform));
                var wordsRt = (RectTransform)words.transform;
                wordsRt.SetParent(lineRt, false);
                wordsRt.anchorMin = wordsRt.anchorMax = wordsRt.pivot = new Vector2(0.5f, 0.5f);
                var text = words.AddComponent<TextMeshProUGUI>();
                if (title != null) text.font = title.font;   // the card's own letters
                text.fontSize = starsSize; text.color = Color.white; text.raycastTarget = false;
                text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
                _stars = text;
                var icon = new GameObject("Star", typeof(RectTransform));
                _starsIcon = (RectTransform)icon.transform;
                _starsIcon.SetParent(lineRt, false);
                _starsIcon.anchorMin = _starsIcon.anchorMax = _starsIcon.pivot = new Vector2(0.5f, 0.5f);
                _starsIcon.sizeDelta = new Vector2(starsSize, starsSize);
                var img = icon.AddComponent<Image>(); img.sprite = ObjectiveHud.StarSprite; img.color = new Color32(0xff, 0xd1, 0x66, 0xff); img.raycastTarget = false;
                // The choices move down under it.
                var sub = endCard.transform.Find("Sub") as RectTransform;
                if (sub != null) sub.anchoredPosition += new Vector2(0f, -choicesDrop);
            }
            _stars.transform.parent.gameObject.SetActive(true);
            _stars.text = Objectives.Format(set.endCardFormat, Objectives.StarCount, total);
            // The star stands just left of the words, the pair centred.
            float wide = _stars.GetPreferredValues(_stars.text).x, gap = starsSize * 0.3f, all = starsSize + gap + wide;
            ((RectTransform)_stars.transform).sizeDelta = new Vector2(wide + 4f, starsSize * 1.3f);
            ((RectTransform)_stars.transform).anchoredPosition = new Vector2(-all * 0.5f + starsSize + gap + wide * 0.5f, 0f);
            _starsIcon.anchoredPosition = new Vector2(-all * 0.5f + starsSize * 0.5f, 0f);
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
