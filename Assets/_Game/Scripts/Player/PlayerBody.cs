using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Which body the player wears. The Player has the Playroom Male and Female as children; before anything else wakes,
    /// this switches on the one the Options panel picked (PlayerOptions.Body) and switches the other off, so every script
    /// that looks for "the Animator in my children" finds the right one. It also hands out what belongs to that body:
    /// its Animator, its measured numbers, its palm. A change of the option shows when a scene next loads.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public sealed class PlayerBody : MonoBehaviour
    {
        [SerializeField, Tooltip("Resources/PlayerBody is loaded if empty.")] private PlayerBodySettings settings;

        /// <summary>The body in use; null when the Player has none of the settings' bodies.</summary>
        public PlayerBodySettings.Body Profile { get; private set; }
        public PlayerBodySettings Settings => settings;
        /// <summary>The Animator of the body in use.</summary>
        public Animator Animator { get; private set; }
        /// <summary>Which body is on: its place in the settings' list (what PlayerOptions.Body asked for, if it exists).</summary>
        public int Index { get; private set; }

        /// <summary>How many bodies there are to pick from (the Options panel asks).</summary>
        public static int Count
        {
            get { var s = PlayerBodySettings.Load(); return s != null ? s.bodies.Length : 0; }
        }

        /// <summary>The name of body <paramref name="index"/> for the Options panel.</summary>
        public static string NameOf(int index)
        {
            var s = PlayerBodySettings.Load();
            var body = s != null ? s.At(index) : null;
            return body != null ? body.name : "-";
        }

        private void Awake()
        {
            if (settings == null) settings = PlayerBodySettings.Load();
            if (settings == null || settings.bodies.Length == 0) { Animator = GetComponentInChildren<Animator>(); return; }
            Index = Mathf.Clamp(PlayerOptions.Body, 0, settings.bodies.Length - 1);
            if (transform.Find(settings.bodies[Index].child) == null) Index = FirstPresent();
            for (int i = 0; i < settings.bodies.Length; i++)
            {
                Transform child = transform.Find(settings.bodies[i].child);
                if (child == null) continue;
                child.gameObject.SetActive(i == Index);
                if (i != Index) continue;
                Profile = settings.bodies[i];
                Animator = child.GetComponentInChildren<Animator>(true);
            }
            if (Animator == null) Animator = GetComponentInChildren<Animator>();
        }

        private int FirstPresent()
        {
            for (int i = 0; i < settings.bodies.Length; i++)
                if (transform.Find(settings.bodies[i].child) != null) return i;
            return 0;
        }

        /// <summary>The palm centre in the right hand bone's space for this body, or <paramref name="fallback"/> without one.</summary>
        public Vector3 Palm(Vector3 fallback) => Profile != null ? Profile.palm : fallback;
    }
}
