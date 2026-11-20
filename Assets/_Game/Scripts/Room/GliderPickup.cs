using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The paper plane lying on the playroom's desk beside core B (D-0095): E takes it (a toy or a kid) and the glider is
    /// yours (Glider.Give: the hint says how to fly it). Its model is hidden while the player has a glider, so coming back
    /// through a door with one, the desk is bare. Needs a trigger collider for the Interactor; CourseBuilder (LandGlider)
    /// places it.
    /// </summary>
    public sealed class GliderPickup : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("Shown next to E. Placeholder wording.")] private string prompt = "Take the paper plane";
        [SerializeField, Tooltip("The plane's model on the desk: hidden while the player has a glider.")] private GameObject display;

        private Glider _player;   // the scene's player, found once
        private float _nextLook;

        public string PromptFor(Interactor who)
        {
            Glider glider = who != null ? who.GetComponent<Glider>() : null;
            return glider == null || glider.Owned ? null : prompt;
        }

        public void Interact(Interactor who)
        {
            Glider glider = who != null ? who.GetComponent<Glider>() : null;
            if (glider == null || glider.Owned) return;
            glider.Give();
            _player = glider;
            Show();
        }

        private void Update()
        {
            if (_player == null && Time.unscaledTime >= _nextLook) { _nextLook = Time.unscaledTime + 1f; _player = FindFirstObjectByType<Glider>(); }
            Show();
        }

        private void Show()
        {
            bool show = _player == null || !_player.Owned;
            if (display != null && display.activeSelf != show) display.SetActive(show);
        }
    }
}
