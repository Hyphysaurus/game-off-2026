using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// The Colour Corner's readout at the top of the screen: the share of colour that is back and the clock, the ammo
    /// that is loaded (T changes it), and at the end the time, the best time and how to play again. It builds its own
    /// canvas at start and only listens: the corner and the ammo belt do not know it exists.
    /// </summary>
    public sealed class ColourCornerHud : MonoBehaviour
    {
        private const string KeyColour = "#8FE3FF";

        [SerializeField] private ColourCorner corner;
        [SerializeField, Tooltip("The player's ammo belt: its loaded kind is shown under the colour. None = no ammo line.")] private AmmoBelt ammo;
        [SerializeField] private float fontSize = 30f;

        private TextMeshProUGUI _text;
        private int _shownPercent = -1, _shownSecond = -1;
        private bool _shownFinished;
        private string _shownAmmo;

        private void Awake()
        {
            var canvasGo = new GameObject("ColourCornerCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            var box = BuildUiKit.Rect("Progress", canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(760f, 108f));
            BuildUiKit.Panel(box, new Color(0f, 0f, 0f, 0.45f)).raycastTarget = false;
            _text = BuildUiKit.Label(box, "", fontSize);
            _text.richText = true;
        }

        private void Update()
        {
            if (corner == null) return;
            int percent = Mathf.FloorToInt(corner.Share * 100f), second = (int)corner.Seconds;
            string loaded = ammo != null ? ammo.CurrentName : null;
            if (percent == _shownPercent && second == _shownSecond && corner.Finished == _shownFinished && loaded == _shownAmmo) return;
            _shownPercent = percent; _shownSecond = second; _shownFinished = corner.Finished; _shownAmmo = loaded;
            if (corner.Finished)
                _text.text = $"Corner restored in {Clock(corner.Seconds)}   (best {Clock(corner.Best)})\n<color={KeyColour}>Enter</color>  play again";
            else
                _text.text = $"Colour {percent}%   {Clock(corner.Seconds)}" +
                             (string.IsNullOrEmpty(loaded) ? "" : $"\n<size=70%><color={KeyColour}>T</color>  {loaded}</size>");
        }

        private static string Clock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";
    }
}
