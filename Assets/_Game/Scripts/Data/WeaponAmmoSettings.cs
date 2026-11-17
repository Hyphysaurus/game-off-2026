using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Ammo and reloading as numbers, the same for every gun: what an empty gun does, how a reload runs through its
    /// length, the read-out on the HUD, and the sounds. After the test room. Each gun's own numbers (magazine size,
    /// reserve, reload seconds) are on its prefab (BlasterWeapon > Magazine); where the gun is held during a reload is
    /// in WeaponHold > A reload. Lives at Resources/WeaponAmmo; without the asset these defaults are used.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Weapon Ammo Settings", fileName = "WeaponAmmo")]
    public sealed class WeaponAmmoSettings : ScriptableObject
    {
        [Header("An empty gun")]
        [Tooltip("Seconds before the trigger answers again after a dry click (the test room: 0.25).")]
        public float dryWait = 0.25f;
        [Tooltip("On: pulling the trigger on an empty gun clicks once, then the reload starts by itself if there are rounds in reserve (the test room's rule). Off: it only clicks; reload with the key.")]
        public bool reloadWhenEmpty = true;

        [Header("A reload, as shares of its length (0 = the start, 1 = the end)")]
        [Tooltip("No reload is shorter than this, seconds, whatever the gun says.")]
        public float shortestReload = 0.2f;
        [Range(0.01f, 1f), Tooltip("The gun has reached its reload position by this share (the test room: 0.2). Higher: it tips over more slowly.")]
        public float poseInBy = 0.2f;
        [Range(0f, 0.99f), Tooltip("The gun starts back to the normal hold from this share (the test room: 0.82).")]
        public float poseOutFrom = 0.82f;
        [Tooltip("The old magazine slides out between these two shares (the test room: 0.25 to 0.45).")]
        public Vector2 magazineOut = new Vector2(0.25f, 0.45f);
        [Tooltip("The new magazine slides in between these two shares; at the second the rounds count in and the seat sound plays (the test room: 0.55 to 0.75). A reload cancelled before then loads nothing.")]
        public Vector2 magazineBack = new Vector2(0.55f, 0.75f);
        [Tooltip("The magazine is hidden between these two shares, while one is swapped for the other (the test room: 0.44 to 0.56).")]
        public Vector2 magazineGone = new Vector2(0.44f, 0.56f);
        [Tooltip("How far the magazine drops out of the gun, in the player's own units (the test room: 0.2 m). Higher: the free hand pulls it further down.")]
        public float magazineDrop = 0.198f;

        [Header("The read-out")]
        [Range(0f, 1f), Tooltip("The count turns to the low colour when the rounds in the gun are at or under this share of a full magazine (the test room: a quarter).")]
        public float lowShare = 0.25f;
        [Tooltip("The count's colour with plenty in the gun.")]
        public Color countColour = Color.white;
        [Tooltip("The count's colour when the magazine is nearly empty.")]
        public Color lowColour = new Color(1f, 0.35f, 0.3f);
        [Tooltip("What the count shows while a reload runs.")]
        public string reloadingText = "RELOADING";
        [Tooltip("The size of the count (\"12 / 48\"), on a 1080-high screen.")]
        public float countSize = 53f;
        [Tooltip("The size of the gun's and the ammo's name under it.")]
        public float nameSize = 27f;
        [Tooltip("Where the read-out's bottom left corner sits from the screen's bottom left, on a 1920 x 1080 screen.")]
        public Vector2 corner = new Vector2(40f, 72f);
        [Tooltip("The crosshair: the side of its dark square and of the white dot in it, on a 1080-high screen (the test room: 10 and 4 on a 648-high one).")]
        public Vector2 dotSizes = new Vector2(17f, 7f);

        [Header("Sounds (licensed clips under _Imports: a project without them plays nothing)")]
        [Tooltip("The trigger pulled on an empty gun.")]
        public AudioClip dryClip;
        [Tooltip("A reload starts.")]
        public AudioClip reloadStartClip;
        [Tooltip("The new magazine seats.")]
        public AudioClip reloadSeatClip;
        [Tooltip("An ammo box is taken.")]
        public AudioClip pickupClip;

        private static WeaponAmmoSettings _defaults;

        /// <summary>Resources/WeaponAmmo, or the defaults above when the asset is missing.</summary>
        public static WeaponAmmoSettings Load()
        {
            var asset = Resources.Load<WeaponAmmoSettings>("WeaponAmmo");
            if (asset != null) return asset;
            if (_defaults == null) { _defaults = CreateInstance<WeaponAmmoSettings>(); _defaults.hideFlags = HideFlags.HideAndDontSave; }
            return _defaults;
        }
    }
}
