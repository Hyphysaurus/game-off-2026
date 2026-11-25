using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A sign that shows only to a player who has no flashlight (D-0215): the hallway's spare light's tape sign. The bedroom
    /// gives the toy its own light (under the bed); the spare by the darts stays as a safety net, and its sign speaks up only
    /// when the player comes without one. "Has one" is what the light key would take out (Inventory.UsableLight: a
    /// flashlight in a slot the player's size can use) or a flashlight in the hands. Looked at a few times a second; the
    /// sign's children are shown or hidden (this part stays on), so whatever draws or reveals the words keeps working.
    /// </summary>
    public sealed class NoLightSign : MonoBehaviour
    {
        [SerializeField, Min(0.05f), Tooltip("Seconds between looks at what the player carries.")] private float lookEvery = 0.25f;
        [SerializeField, Tooltip("Off = the sign always shows (as before D-0215).")] private bool onlyWithoutLight = true;

        private Inventory _bag;
        private Carrier _hands;
        private bool _found, _shown = true;
        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + lookEvery;
            if (!_found)
            {
                GameObject own = GameObject.Find("Player");   // this PC's own player, as the prompt HUD finds it
                if (own == null) { Show(true); return; }
                own.TryGetComponent(out _bag);
                own.TryGetComponent(out _hands);
                _found = true;
            }
            Show(!onlyWithoutLight || !HasLight());
        }

        private bool HasLight()
        {
            if (_bag != null && _bag.UsableLight() != null) return true;
            return _hands != null && _hands.Held != null && _hands.Held.TryGetComponent(out Flashlight _);
        }

        private void Show(bool show)
        {
            if (show == _shown) return;
            _shown = show;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(show);
        }
    }
}
