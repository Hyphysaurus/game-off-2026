using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// How shots land in a scene (D-0172, his "lets try the instant, TDM will go into the stadium scene"). Foam darts fly,
    /// fall and stick, and that is the charm in the house. In a versus scene a shot is decided at the click, on what is
    /// under the dot: no lead on a moving player, no drop, and online it is what was under the shooter's dot that decides
    /// the hit, not the lag. Since D-0176 (his "i want to keep ONE but i love the look of the flying dart") such a shot
    /// is still seen as a dart: it flies from the gun to its mark, fast and straight, sticks there, and the hit counts
    /// as it arrives. The guns are the same either way: the kind is the scene's, not the gun's. Drop one of these into a
    /// scene and pick the kind; a scene without one fires foam darts.
    /// </summary>
    public sealed class ShotRules : MonoBehaviour
    {
        public enum Kind { FoamDart, Instant }

        [SerializeField, Tooltip("Foam Dart: shots fly, fall and stick, as in the house. Instant: a shot is decided at the click, on what is under the dot; the dart is seen flying there from the gun, fast and straight, and the hit counts as it arrives (Resources/WeaponAmmo > Flight Speed).")]
        private Kind kind = Kind.Instant;

        private static ShotRules _scene;        // the rule of the scene in use (the last one switched on)
        private static bool _byHand;            // the debug key set it by hand
        private static bool _byHandInstant;

        /// <summary>True when a shot is decided at the click (and seen flying to its mark).</summary>
        public static bool Instant => _byHand ? _byHandInstant : _scene != null && _scene.kind == Kind.Instant;

        /// <summary>Flips the kind by hand (the debug console, F9) and says what it is now. It holds until a scene with a
        /// rule of its own loads, or the next flip.</summary>
        public static bool Flip()
        {
            _byHandInstant = !Instant;
            _byHand = true;
            return _byHandInstant;
        }

        private void OnEnable() { _scene = this; _byHand = false; }
        private void OnDisable() { if (_scene == this) _scene = null; }

        // Statics outlive a Play in the editor when the domain is not reloaded: start clean every time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void StartClean() { _scene = null; _byHand = false; _byHandInstant = false; }
    }
}
