using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShiftJam
{
    /// <summary>
    /// The kinds of ammo a player carries for its blasters, and which one is loaded: T (pad: D-pad up) goes to the next.
    /// The first kind is the blaster's own (foam darts); each other kind is a shot prefab every blaster fires instead
    /// (paint balls; a third can follow by adding a row). Sits beside the WeaponHolder; a blaster asks it what to fire.
    /// Without one, blasters fire their own darts as before.
    /// </summary>
    public sealed class AmmoBelt : MonoBehaviour
    {
        /// <summary>One kind of ammo.</summary>
        [Serializable]
        public sealed class Kind
        {
            public string name = "Darts";
            [Tooltip("What every blaster fires with this loaded. None = each blaster's own shot.")] public Projectile shot;
            [Tooltip("Its firing sounds. None = the blaster's own.")] public AudioClip[] fireClips = Array.Empty<AudioClip>();
        }

        [SerializeField] private Kind[] kinds = { new Kind() };
        [SerializeField, Tooltip("The kind loaded when the scene starts.")] private int startWith;
        [SerializeField, Tooltip("The key that loads the next kind. None to disable.")] private Key nextKey = Key.T;
        [SerializeField, Tooltip("The pad's D-pad up loads the next kind too.")] private bool padUp = true;

        /// <summary>Which kind is loaded.</summary>
        public int Index { get; private set; }
        public int Count => kinds.Length;
        /// <summary>The loaded kind's name ("" when the belt is empty).</summary>
        public string CurrentName => kinds.Length > 0 ? kinds[Index].name : "";
        /// <summary>Another kind was loaded.</summary>
        public event Action<AmmoBelt> Changed;

        private void Awake() => Index = kinds.Length > 0 ? Mathf.Clamp(startWith, 0, kinds.Length - 1) : 0;

        /// <summary>What a blaster fires now: the loaded kind's shot, or <paramref name="own"/> when that kind has none.</summary>
        public Projectile Shot(Projectile own) => kinds.Length > 0 && kinds[Index].shot != null ? kinds[Index].shot : own;

        /// <summary>One of the loaded kind's firing sounds; null when it has none (the blaster plays its own).</summary>
        public AudioClip FireClip()
        {
            if (kinds.Length == 0) return null;
            AudioClip[] clips = kinds[Index].fireClips;
            return clips != null && clips.Length > 0 ? clips[UnityEngine.Random.Range(0, clips.Length)] : null;
        }

        /// <summary>Loads a kind by its place in the list.</summary>
        public void Select(int index)
        {
            if (kinds.Length == 0) return;
            index = ((index % kinds.Length) + kinds.Length) % kinds.Length;
            if (index == Index) return;
            Index = index;
            Changed?.Invoke(this);
        }

        public void Next() => Select(Index + 1);

        private void Update()
        {
            if (kinds.Length < 2 || Time.timeScale <= 0f) return;                             // paused, or a menu is up
            if (GameMode.Current == GameMode.Mode.Build || DebugConsole.IsOpen) return;   // T is typed or builds there
            var keys = Keyboard.current; var pad = Gamepad.current;
            bool pressed = (keys != null && nextKey != Key.None && keys[nextKey].wasPressedThisFrame)
                        || (padUp && pad != null && pad.dpad.up.wasPressedThisFrame);
            if (pressed) Next();
        }
    }
}
