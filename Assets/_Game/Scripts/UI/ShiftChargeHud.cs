using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The carried shift on screen (D-0045): one small battery per charge at the bottom centre (lit = ready, dark =
    /// spent), a line above it when a press is refused ("No room to grow"), and the one-time hint the first time a save
    /// gets a charge. Hidden outside Play mode and under menus. Made by ShiftCharge; built in code like the other HUDs.
    /// </summary>
    public sealed class ShiftChargeHud : MonoBehaviour
    {
        private static readonly Color Lit = new Color(0.35f, 0.9f, 1f, 0.95f), Dark = new Color(1f, 1f, 1f, 0.15f);
        private const float NoticeSeconds = 1.6f, HintSeconds = 7f;

        private ShiftCharge _charge;
        private CanvasGroup _group;
        private RectTransform _row;
        private Image[] _cells = new Image[0];
        private TextMeshProUGUI _notice, _hint;
        private Febucci.TextAnimatorForUnity.TextMeshPro.TextAnimator_TMP _noticeFx;
        private CanvasGroup _noticeGroup;   // the notice fades by its group: Text Animator owns the letters' colours
        private float _noticeUntil, _hintUntil;

        public static ShiftChargeHud Create(ShiftCharge charge)
        {
            var go = new GameObject("ShiftChargeHud");
            if (charge.GetComponent<NetPlayer>() != null) DontDestroyOnLoad(go);   // an online player outlives scene loads (it was made on the title, before its world): so does its HUD (D-0126)
            var hud = go.AddComponent<ShiftChargeHud>();
            hud._charge = charge;
            hud.Build();
            hud.Refresh();
            return hud;
        }

        /// <summary>Redraws the batteries for the charge count (and the cap, if it changed).</summary>
        public void Refresh()
        {
            if (_charge == null) return;
            int max = _charge.Max;
            if (_cells.Length != max)
            {
                foreach (var cell in _cells) if (cell != null) Destroy(cell.gameObject);
                _cells = new Image[max];
                for (int i = 0; i < max; i++)
                {
                    float x = (i - (max - 1) * 0.5f) * 34f;
                    var rt = MenuKit.Rect("Charge " + (i + 1), _row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(24f, 38f));
                    _cells[i] = rt.gameObject.AddComponent<Image>();
                    _cells[i].raycastTarget = false;
                    var cap = MenuKit.Rect("Cap", rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 3f), new Vector2(10f, 5f));
                    var capImg = cap.gameObject.AddComponent<Image>(); capImg.raycastTarget = false; capImg.color = new Color(1f, 1f, 1f, 0.35f);
                }
            }
            for (int i = 0; i < _cells.Length; i++) _cells[i].color = i < _charge.Charges ? Lit : Dark;
        }

        public void ShowNotice(string text) { TextFx.Set(_noticeFx, _notice, TextFx.Shaken(text)); _noticeUntil = Time.unscaledTime + NoticeSeconds; }   // shakes in (D-0098)
        public void ShowHint(string text) { _hint.text = text; _hintUntil = Time.unscaledTime + HintSeconds; UiAudio.Play(UiAudio.Kind.Toast); }

        private void Update()
        {
            if (_charge == null) { Destroy(gameObject); return; }   // the player is gone (its scene, or a party left): as the other HUDs do
            bool show = _charge != null && _charge.isActiveAndEnabled && GameMode.Current == GameMode.Mode.Play && !PauseMenu.IsOpen && !TitleMenu.IsOpen;
            _group.alpha = show ? 1f : 0f;
            _noticeGroup.alpha = Mathf.Clamp01((_noticeUntil - Time.unscaledTime) / 0.4f);
            _hint.alpha = Mathf.Clamp01((_hintUntil - Time.unscaledTime) / 0.8f);
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
            _row = MenuKit.Rect("Charges", canvasGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(300f, 44f));
            _notice = MenuKit.Text("Notice", canvasGo.transform, "", 30, new Vector2(0.5f, 0f), new Vector2(1000f, 44f));
            _notice.rectTransform.anchoredPosition = new Vector2(0f, 100f);
            _noticeGroup = _notice.gameObject.AddComponent<CanvasGroup>(); _noticeGroup.alpha = 0f;
            _noticeFx = TextFx.On(_notice);
            _hint = MenuKit.Text("Hint", canvasGo.transform, "", 26, new Vector2(0.5f, 0f), new Vector2(1200f, 44f));
            _hint.rectTransform.anchoredPosition = new Vector2(0f, 140f); _hint.alpha = 0f;
        }
    }
}
