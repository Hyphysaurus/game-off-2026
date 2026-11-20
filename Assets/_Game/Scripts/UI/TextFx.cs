using Febucci.TextAnimatorCore.Time;
using Febucci.TextAnimatorForUnity;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Text Animator on the HUD's lines (D-0098). Effects are tags in the text: {incr} grows a line in as it appears,
    /// &lt;shake&gt; rattles it, &lt;bounce&gt; hops it (the effect names are the plugin's, Assets/Plugins/Febucci/.../Effects).
    /// The animation runs on real time, so it plays under a menu and through slow motion.
    /// </summary>
    public static class TextFx
    {
        public const string Pop = "{incr}", PopEnd = "{/incr}";

        /// <summary>The label's animator, added once.</summary>
        public static TextAnimator_TMP On(TMP_Text label)
        {
            if (label == null) return null;
            if (!label.TryGetComponent(out TextAnimator_TMP anim))
            {
                anim = label.gameObject.AddComponent<TextAnimator_TMP>();
                var s = anim.localSettings;
                s.timeScale = TimeScale.Unscaled;
                anim.localSettings = s;
            }
            return anim;
        }

        /// <summary>Shows the text with its tags (empty clears the line).</summary>
        public static void Set(TextAnimator_TMP anim, TMP_Text label, string tagged)
        {
            if (anim != null) anim.SetText(tagged ?? "");
            else if (label != null) label.text = tagged ?? "";
        }

        /// <summary>A plain line, popping in.</summary>
        public static string Popped(string text) => string.IsNullOrEmpty(text) ? "" : Pop + text + PopEnd;
        /// <summary>A refusal: pops in shaking.</summary>
        public static string Shaken(string text) => string.IsNullOrEmpty(text) ? "" : Pop + "<shake>" + text + "</shake>" + PopEnd;
        /// <summary>Good news: pops in with a bounce.</summary>
        public static string Bounced(string text) => string.IsNullOrEmpty(text) ? "" : Pop + "<bounce>" + text + "</bounce>" + PopEnd;
    }
}
