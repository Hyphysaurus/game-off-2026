#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>Shows one toast of each kind in Play, to look over the toast cards (D-0162).</summary>
    public static class ToastSamples
    {
        [MenuItem("Shift Jam/UI/Show Sample Toasts (Play)")]
        private static void Show()
        {
            if (!Application.isPlaying) { Debug.Log("[ToastSamples] Enter Play first: the toasts draw in the game view."); return; }
            Toasts.Show("<b>L</b> light   <b>Right click</b> aim and focus", ToastKind.Got, "Flashlight!");
            Toasts.Show("Too small to reach. Grow first.", ToastKind.Refused);
            Toasts.Show("Focus the light on a soldier to blind him.", ToastKind.Tip);
            Toasts.Show("Low on darts", ToastKind.Warning);
            Toasts.Show("The core woke up. Use it to shift (E).", ToastKind.Core);
        }
    }
}
#endif
