using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// A black cover over the screen for cuts: fades out, does something while the screen is black (a warp,
    /// a scene load), fades back in. One per HUD; anyone finds it with Find(). Uses unscaled time so a Feel
    /// time dip never stalls it.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFade : MonoBehaviour
    {
        private Image _image;
        private Coroutine _running;

        public static ScreenFade Find() => FindFirstObjectByType<ScreenFade>();

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
            SetAlpha(0f);
        }

        /// <summary>Cover the screen over outTime, call atBlack, uncover over inTime.</summary>
        public void Cut(float outTime, Action atBlack, float inTime)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(outTime, atBlack, inTime, 0f));
        }

        /// <summary>Cover the screen and stay covered (for an end card drawn above it).</summary>
        public void Cover(float outTime, Action atBlack = null)
        {
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Run(outTime, atBlack, 0f, 1f));
        }

        private IEnumerator Run(float outTime, Action atBlack, float inTime, float endAlpha)
        {
            float a = _image.color.a;
            for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime) { SetAlpha(Mathf.Lerp(a, 1f, t / outTime)); yield return null; }
            SetAlpha(1f);
            atBlack?.Invoke();
            if (inTime > 0f)
            {
                for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime) { SetAlpha(Mathf.Lerp(1f, endAlpha, t / inTime)); yield return null; }
            }
            SetAlpha(endAlpha);
            _running = null;
        }

        private void SetAlpha(float a) { Color c = _image.color; c.a = a; _image.color = c; }
    }
}
