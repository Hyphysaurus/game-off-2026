using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// One key puts the Workshop's toys back: every IResettable in the scene (practice targets, toy soldiers, cars, health)
    /// returns to its start and every flying shot is cleared. Loose props are left where they are. Put one in the Workshop.
    /// </summary>
    public sealed class WorkshopReset : MonoBehaviour
    {
        [SerializeField] private Key resetKey = Key.F9;

        private void Update()
        {
            var k = Keyboard.current;
            if (k != null && resetKey != Key.None && k[resetKey].wasPressedThisFrame) ResetAll();
        }

        public void ResetAll()
        {
            Projectile.ClearAll();
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (b is IResettable r) r.ResetState();   // a health reset twice (by its owner too) is harmless
            }
        }
    }
}
