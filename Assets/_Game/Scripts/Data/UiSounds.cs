using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The menus' and toasts' sounds (D-0100), all from the Playroom pack's UI set so they sound like one game. Read from
    /// Resources/UiSounds (made and filled by Shift Jam / Audio / Build UI Sounds); without it the UI is silent. Swap any
    /// clip or volume here.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/UI Sounds", fileName = "UiSounds")]
    public sealed class UiSounds : ScriptableObject
    {
        [Header("Menus")]
        [Tooltip("Moving between items: the mouse onto one, or arrows / stick to the next.")] public CourseSounds.Cue move;
        [Tooltip("A button pressed.")] public CourseSounds.Cue click;
        [Tooltip("A choice that starts something: New Game, Continue, Resume, Back to Title.")] public CourseSounds.Cue confirm;
        [Tooltip("Back out: Esc, Back, closing Options.")] public CourseSounds.Cue back;
        [Tooltip("Picking one of a set: a save slot.")] public CourseSounds.Cue select;
        [Tooltip("A greyed-out button pressed (Continue on an empty slot).")] public CourseSounds.Cue denied;
        [Tooltip("A menu opening over the game (the pause).")] public CourseSounds.Cue open;

        [Header("Toasts")]
        [Tooltip("A line on the HUD's hint row, a core filling the shift charge, a lap done.")] public CourseSounds.Cue toast;
        [Tooltip("Good news: a best lap, a race won or finished.")] public CourseSounds.Cue win;

        [Tooltip("Moves closer together than this (seconds) play once: a quick scroll is not a machine gun.")] public float moveGap = 0.05f;

        private static UiSounds _loaded;
        private static bool _looked;

        /// <summary>Resources/UiSounds, or null.</summary>
        public static UiSounds Load()
        {
            if (!_looked) { _loaded = Resources.Load<UiSounds>("UiSounds"); _looked = true; }
            return _loaded;
        }
    }
}
