using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// On a menu button that lies over a slab of a MenuModel (D-0196): it shows the button's state on the slab.
    /// Disabled (the button cannot be used), selected (the mouse is on it, or the keys or the stick chose it: one
    /// state for all three, and the mouse coming onto a button chooses it), active (the owner says so: the save slot in
    /// use), else normal; a little larger while selected, a little smaller while pressed. On real time: a menu holds
    /// the game's.
    /// </summary>
    public sealed class MenuSlab : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler
    {
        private MenuModel _model;
        private MenuModel.Control _control;
        private Selectable _button;
        private bool _selected, _pressed;
        private float _size = 1f;

        /// <summary>The owner's own mark on it (the save slot in use).</summary>
        public bool Active { get; set; }

        public void Bind(MenuModel model, MenuModel.Control control)
        {
            _model = model; _control = control;
            TryGetComponent(out _button);
            Show(true);
        }

        public void OnSelect(BaseEventData e) => _selected = true;
        public void OnDeselect(BaseEventData e) { _selected = false; _pressed = false; }
        public void OnPointerEnter(PointerEventData e)
        {
            if (_button != null && _button.IsInteractable() && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
        public void OnPointerDown(PointerEventData e) => _pressed = true;
        public void OnPointerUp(PointerEventData e) => _pressed = false;

        private void OnEnable() { _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject; _pressed = false; }
        private void OnDisable() { _selected = false; _pressed = false; }

        private void Update() => Show(false);

        private void Show(bool atOnce)
        {
            if (_model == null || _control == null) return;
            bool usable = _button == null || _button.IsInteractable();
            MenuModel.State state = !usable ? MenuModel.State.Disabled : _selected ? MenuModel.State.Selected : Active ? MenuModel.State.Active : MenuModel.State.Normal;
            float want = !usable ? 1f : _pressed ? _model.PressedSize : _selected ? _model.SelectedSize : 1f;
            _size = atOnce ? want : Mathf.MoveTowards(_size, want, Time.unscaledDeltaTime / _model.SizeSeconds * 0.1f);
            _model.Paint(_control, state, _size);
        }
    }
}
