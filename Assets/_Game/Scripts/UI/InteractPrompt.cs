using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>Shows "E  Shift: grow" style text for whatever the Interactor has in reach. Blank when nothing. Through
    /// Text Animator (D-0098): a new line pops in, "Charge full" bounces, and E refused shakes the line for a moment.
    /// THE PIXEL LOOK (D-0243, PixelKit > Pixel On): the same words as his mockup's key prompt footer at the bottom middle
    /// of the screen: each key as its Kenney glyph (or a cream key cap), its words in bold capitals, gold dashes between,
    /// on a dark pixel panel; the scene's label is left empty. The footer keeps clear of the hotbar (bottom left) and the
    /// status panel (bottom right): it sits between them, or over them when there is no room between. A new line pops,
    /// "Charge full" bounces, a refusal shakes, as before. It hides under the menus, the key sheet and the console.</summary>
    public sealed class InteractPrompt : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string keyName = "E";
        [SerializeField, Tooltip("Optional. Shown when nothing is in reach but something is carried.")] private Carrier carrier;
        [SerializeField] private string heldHint = "LMB  Light     X  Put down";

        private const float ShakeSeconds = 0.6f;
        // The pixel footer (D-0243): its canvas's sorting (over the HUD's panels, under the toasts), its padding inside the
        // frame (sides, top and bottom), the gap kept to the hotbar and the status panel, and its pop, bounce and shake.
        private const int FooterSorting = 38;
        private const float PadX = 20f, PadY = 12f, Clear = 12f, PopSeconds = 0.18f, PopSwell = 0.12f, BounceSwell = 0.16f, ShakeWidth = 6f;
        private Febucci.TextAnimatorForUnity.TextMeshPro.TextAnimator_TMP _fx;
        private string _shown;
        private float _shakeUntil = -1f;
        private bool _shaking;
        // The pixel footer.
        private GameObject _footer;
        private RectTransform _canvasRt, _panel;
        private CanvasGroup _footerGroup;
        private PixelKit.KeyRow _row;
        private float _changedAt = float.NegativeInfinity;
        private bool _bounce;
        private Vector2 _placed = new Vector2(float.NaN, float.NaN);

        private void Awake()
        {
            if (PixelKit.On) BuildFooter();
            else _fx = TextFx.On(label);
        }

        private void OnEnable() { GameEvents.UseRefused += OnUseRefused; }
        private void OnDisable() { GameEvents.UseRefused -= OnUseRefused; if (_footerGroup != null) _footerGroup.alpha = 0f; }
        private void OnDestroy() { if (_footer != null) Destroy(_footer); }

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
            if (_footer != null) { ShowFooter(text); return; }
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
            if (interactor == null || (label == null && _footer == null)) { if (_footer != null) Show(""); return; }
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

        private void LateUpdate()
        {
            if (_footer != null) Tick();
        }

        // ---------- the pixel footer (D-0243) ----------

        /// <summary>The footer's own canvas (a root object of this scene, removed with this prompt), its panel and row; the
        /// scene's label is emptied (its words are the footer's now).</summary>
        private void BuildFooter()
        {
            if (label != null) { label.text = ""; label.enabled = false; }
            _footer = new GameObject("PixelPrompt", typeof(RectTransform));
            _footerGroup = PixelKit.MakeCanvas(_footer, FooterSorting);
            _footerGroup.alpha = 0f;
            _canvasRt = (RectTransform)_footer.transform;
            _panel = BuildUiKit.Rect("Footer", _canvasRt, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(960f, PixelKit.Edge), new Vector2(100f, PixelKit.Glyph + PadY * 2f));
            PixelKit.Panel(_panel, PixelKit.Frame.Footer);
            _row = new PixelKit.KeyRow(_panel, "Keys", PixelKit.Body, PixelKit.Cream);
            _panel.gameObject.SetActive(false);
        }

        /// <summary>New words for the footer: laid out once, then popped (bounced for "Charge full", shaken when refused).</summary>
        private void ShowFooter(string text)
        {
            bool any = !string.IsNullOrWhiteSpace(text);
            if (!any) { _row.Clear(); _panel.gameObject.SetActive(false); return; }
            _panel.gameObject.SetActive(true);   // first: the row's new words are made awake, so they can be measured
            _row.Prompt(text);
            float wide = Mathf.Round(_row.Width + PadX * 2f);
            _panel.sizeDelta = new Vector2(wide, PixelKit.Glyph + PadY * 2f);
            _row.Root.anchoredPosition = new Vector2(-Mathf.Round(_row.Width * 0.5f), 0f);
            _panel.gameObject.SetActive(true);
            _bounce = text.EndsWith(ShiftSource.ChargeFullPrompt);
            _changedAt = Time.unscaledTime;
            _placed = new Vector2(float.NaN, float.NaN);   // placed afresh for its new width
        }

        /// <summary>Each frame while it is made: hidden under the menus; placed clear of the hotbar and the status panel; its
        /// pop, bounce or shake. Nothing is made here.</summary>
        private void Tick()
        {
            bool covered = PauseMenu.IsOpen || TitleMenu.IsOpen || KeyHelp.IsOpen || DebugConsole.IsOpen;
            float alpha = covered || !_panel.gameObject.activeSelf ? 0f : 1f;
            if (_footerGroup.alpha != alpha) _footerGroup.alpha = alpha;
            if (alpha <= 0f) return;

            // Between the hotbar's right edge and the status panel's left edge, as near the middle as it fits; over both
            // when the room between is too narrow for it.
            float screen = _canvasRt.rect.width, wide = _panel.sizeDelta.x;
            float left = PixelKit.HotbarRight > 0f ? PixelKit.HotbarRight + Clear : PixelKit.Edge;
            float right = screen - (PixelKit.StatusWide > 0f ? PixelKit.StatusWide + Clear : PixelKit.Edge);
            float x, y;
            if (right - left >= wide)
            {
                x = Mathf.Clamp(screen * 0.5f, left + wide * 0.5f, right - wide * 0.5f);
                y = PixelKit.Edge;
            }
            else
            {
                x = Mathf.Clamp(screen * 0.5f, PixelKit.Edge + wide * 0.5f, Mathf.Max(PixelKit.Edge + wide * 0.5f, screen - PixelKit.Edge - wide * 0.5f));
                y = Mathf.Max(PixelKit.HotbarTop, PixelKit.StatusTop, PixelKit.Edge) + Clear;
            }
            var at = new Vector2(Mathf.Round(x), Mathf.Round(y));
            float t = Time.unscaledTime - _changedAt;
            float shake = _shaking && t < ShakeSeconds ? Mathf.Sin(t * 60f) * ShakeWidth * (1f - t / ShakeSeconds) : 0f;
            Vector2 now = new Vector2(at.x + Mathf.Round(shake), at.y);
            if (now != _placed) { _placed = now; _panel.anchoredPosition = now; }

            float swell = 1f;
            if (_bounce && t < ShakeSeconds) swell = 1f + BounceSwell * Mathf.Abs(Mathf.Sin(t / ShakeSeconds * Mathf.PI * 3f)) * (1f - t / ShakeSeconds);
            else if (t < PopSeconds) swell = 1f + PopSwell * (1f - t / PopSeconds);
            if (_panel.localScale.x != swell) _panel.localScale = new Vector3(swell, swell, 1f);
        }
    }
}
