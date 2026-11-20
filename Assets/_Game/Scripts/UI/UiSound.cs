using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A menu control's sounds (D-0100): a soft move when the mouse comes onto it or the arrows / stick land on it, its own
    /// press sound (Click unless told otherwise: Confirm, Back, Select), and a soft no when it is pressed greyed out.
    /// Added by the menus' button makers (MenuKit.Button, TitleMenu).
    /// </summary>
    public sealed class UiSound : MonoBehaviour, ISelectHandler, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private UiAudio.Kind press = UiAudio.Kind.Click;
        private Selectable _selectable;

        /// <summary>Gives <paramref name="control"/> its sounds (once) and sets what its press sounds like.</summary>
        public static UiSound On(Selectable control, UiAudio.Kind pressKind = UiAudio.Kind.Click)
        {
            if (control == null) return null;
            if (!control.TryGetComponent(out UiSound s))
            {
                s = control.gameObject.AddComponent<UiSound>();
                if (control is Button b) b.onClick.AddListener(s.Pressed);
            }
            s.press = pressKind;
            return s;
        }

        /// <summary>What its press sounds like from now on.</summary>
        public UiAudio.Kind Press { get => press; set => press = value; }

        private void Awake() { _selectable = GetComponent<Selectable>(); }

        private bool Live => _selectable == null || _selectable.IsInteractable();

        public void OnSelect(BaseEventData e) { if (Live) UiAudio.Play(UiAudio.Kind.Move); }
        public void OnPointerEnter(PointerEventData e) { if (Live && EventSystem.current?.currentSelectedGameObject != gameObject) UiAudio.Play(UiAudio.Kind.Move); }
        // a Button's own click arrives through onClick (Pressed); greyed out, it never fires, so the no is played here
        public void OnPointerClick(PointerEventData e) { if (!Live) UiAudio.Play(UiAudio.Kind.Denied); }
        public void OnSubmit(BaseEventData e) { if (!Live) UiAudio.Play(UiAudio.Kind.Denied); }

        private void Pressed() => UiAudio.Play(press);
    }
}
