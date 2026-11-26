using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The carried shift on screen (D-0045): one small battery per charge at the bottom centre (lit = ready, dark =
    /// spent). Hidden outside Play mode and under menus. Made by ShiftCharge; built in code like the other HUDs. Its
    /// lines (a refused press, the hints) are toasts now (D-0162): this HUD existing is what marks the local player.
    /// THE PIXEL LOOK (D-0243, PixelKit > Pixel On): the charge is the SHIFT row of the bottom-right status panel, which
    /// HealthHud draws for a player that has health; this HUD then shows nothing (it still marks the local player). A
    /// player with no health HUD gets the row here, on a status panel of its own in the same corner.
    /// </summary>
    public sealed class ShiftChargeHud : MonoBehaviour
    {
        private static readonly Color Lit = new Color(0.35f, 0.9f, 1f, 0.95f), Dark = new Color(1f, 1f, 1f, 0.15f);
        // Height above the screen's bottom (1920 x 1080 units) of the batteries' row middle. While the inventory bar
        // shows it goes up so the row sits just above the bar, not across its slots (D-0132).
        private const float RowY = 46f, RowHeight = 44f;
        private const float RaisedRowY = HotbarHud.Top + 8f + RowHeight * 0.5f;

        private Inventory _inventory;
        private float _lift = -1f;

        private ShiftCharge _charge;
        private CanvasGroup _group;
        private RectTransform _row;
        private Image[] _cells = new Image[0];
        // The pixel look (D-0243): a one-row status panel, used only when no health HUD draws the row.
        private bool _pixel;
        private RectTransform _status;
        private PixelKit.StatusRow _pixelRow;
        private int _shownMax = -1;

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
            if (_pixel) { RefreshPixel(); return; }
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

        /// <summary>The pixel row's cells: each charge an equal share of the bar; the icon and the label dim at none.</summary>
        private void RefreshPixel()
        {
            int max = _charge.Max, charges = Mathf.Clamp(_charge.Charges, 0, Mathf.Max(0, max));
            int each = max > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)PixelKit.BarCells / max)) : 1;
            if (max != _shownMax) { _shownMax = max; _pixelRow.Bar.Resize(Mathf.Max(1, max * each)); }
            _pixelRow.Bar.Show(charges * each, PixelKit.Cyan, PixelKit.EmptyCell);
            _pixelRow.Label.color = charges > 0 ? PixelKit.Cyan : PixelKit.QuietText;
            _pixelRow.Icon.color = new Color(1f, 1f, 1f, charges > 0 ? 1f : 0.55f);
        }

        public void ShowNotice(string text) => Toasts.Show(text, ToastKind.Refused);   // a refused press: the card shakes in (D-0098, D-0162)
        public void ShowHint(string text, ToastKind kind = ToastKind.Tip, string title = null) => Toasts.Show(text, kind, title);

        private void Update()
        {
            if (_charge == null) { Destroy(gameObject); return; }   // the player is gone (its scene, or a party left): as the other HUDs do
            bool show = _charge != null && _charge.isActiveAndEnabled && GameMode.Current == GameMode.Mode.Play && !PauseMenu.IsOpen && !TitleMenu.IsOpen;
            if (_pixel)
            {
                // The health HUD's panel carries the row when there is one (and tells the kit where it is): nothing here.
                if (HealthHud.DrawsShift(_charge)) { if (_group.alpha != 0f) _group.alpha = 0f; return; }
                _group.alpha = show ? 1f : 0f;
                float wide = show ? PixelKit.StatusWidth + PixelKit.Edge : 0f, top = show ? PixelKit.Edge + _status.sizeDelta.y : 0f;
                if (PixelKit.StatusWide != wide) PixelKit.StatusWide = wide;
                if (PixelKit.StatusTop != top) PixelKit.StatusTop = top;
                return;
            }
            _group.alpha = show ? 1f : 0f;
            // The inventory bar shows while anything is carried (HotbarHud): sit above it then.
            if (_inventory == null) _inventory = _charge.GetComponent<Inventory>();
            float lift = _inventory != null && _inventory.isActiveAndEnabled && _inventory.Items.Count > 0 ? RaisedRowY - RowY : 0f;
            if (lift != _lift) { _lift = lift; _row.anchoredPosition = new Vector2(0f, RowY + lift); }
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;   // by screen height (D-0133)
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
            if (PixelKit.On)
            {
                _pixel = true;
                _group.alpha = 0f;
                _status = PixelKit.StatusPanel(canvasGo.transform, 1);
                _pixelRow = new PixelKit.StatusRow(_status, 0, IconSet.Shift, PixelKit.ShiftLabel, PixelKit.Cyan, PixelKit.BarCells);
                return;
            }
            _row = MenuKit.Rect("Charges", canvasGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, RowY), new Vector2(300f, RowHeight));
        }
    }
}
