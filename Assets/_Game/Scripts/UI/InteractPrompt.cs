using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>Shows "E  Shift: grow" style text for whatever the Interactor has in reach. Blank when nothing. Through
    /// Text Animator (D-0098): a new line pops in, "Charge full" bounces, and E refused shakes the line for a moment.</summary>
    public sealed class InteractPrompt : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string keyName = "E";
        [SerializeField, Tooltip("Optional. Shown when nothing is in reach but something is carried.")] private Carrier carrier;
        [SerializeField] private string heldHint = "LMB  Light     X  Put down";

        private const float ShakeSeconds = 0.6f;
        private Febucci.TextAnimatorForUnity.TextMeshPro.TextAnimator_TMP _fx;
        private string _shown;
        private float _shakeUntil = -1f;
        private bool _shaking;

        private void Awake() { _fx = TextFx.On(label); }
        private void OnEnable() { GameEvents.UseRefused += OnUseRefused; }
        private void OnDisable() { GameEvents.UseRefused -= OnUseRefused; }

        private void OnUseRefused(Interactor who, string why)
        {
            if (who != interactor) return;
            _shakeUntil = Time.unscaledTime + ShakeSeconds;
            _shown = null;   // shown again, shaking
        }

        /// <summary>Sets the line only when it changes, so its pop plays once.</summary>
        private void Show(string text)
        {
            bool shake = Time.unscaledTime < _shakeUntil && !string.IsNullOrEmpty(text);
            if (text == _shown && shake == _shaking) return;
            _shown = text; _shaking = shake;
            string tagged = shake ? TextFx.Shaken(text)
                : text != null && text.EndsWith(ShiftSource.ChargeFullPrompt) ? TextFx.Bounced(text)
                : TextFx.Popped(text);
            TextFx.Set(_fx, label, tagged);
        }

        private float _nextFind;

        private void Update()
        {
            // The scene's player was removed (online: OnlineWorld, D-0125) or never was (the Stadium's copied HUD): follow
            // this PC's own player, found by name (the online owner's copy is "Player"), twice a second until found.
            if (interactor == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 0.5f;
                var own = GameObject.Find("Player");
                if (own != null) { own.TryGetComponent(out interactor); if (carrier == null) own.TryGetComponent(out carrier); }
            }
            if (interactor == null || label == null) return;
            // The grab first: what it holds, or the prop under the aim. Then whatever is in reach. Then why the aimed-at
            // prop can't be grabbed ("Too big"), then the hand-carry hint.
            var grab = interactor.Grab;
            bool grabOn = grab != null && grab.isActiveAndEnabled;
            if (grabOn && !string.IsNullOrEmpty(grab.Hint)) { Show(grab.Hint); return; }
            IInteractable target = interactor.Target;
            string words = target != null ? target.PromptFor(interactor) : "";
            if (!string.IsNullOrEmpty(words)) Show(target is IHintOnly ? words : $"{keyName}  {words}");   // a hint has no key (D-0046)
            else if (grabOn && !string.IsNullOrEmpty(grab.Why)) Show(grab.Why);
            else Show(carrier != null && carrier.Held != null ? heldHint : "");
        }
    }
}
