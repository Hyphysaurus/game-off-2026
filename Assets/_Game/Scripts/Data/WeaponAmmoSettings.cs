using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Ammo and reloading as numbers, the same for every gun: what an empty gun does, how a reload runs through its
    /// length, the read-out on the HUD, the hit marker, the puff at the barrel, and the sounds. After the test room.
    /// Ours, not the test room's: what the shooter hears and feels when a shot lands on something with health, and the
    /// burst when a target goes down (the thud there is the test room's). Each gun's own numbers (magazine size,
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

        [Header("The hit marker (four ticks round the crosshair when a shot lands on something with health)")]
        [Tooltip("Its colour for a hit.")]
        public Color hitColour = Color.white;
        [Tooltip("Its colour for the hit that finishes something off.")]
        public Color downColour = new Color(1f, 0.4f, 0.2f);
        [Tooltip("How far from the middle the ticks start, how long they are and how thick, on a 1080-high screen (the test room: 8, 9 and 3 on a 648-high one).")]
        public Vector3 markerTicks = new Vector3(13f, 15f, 5f);
        [Tooltip("Seconds a hit's flash lasts; the finishing hit's lasts twice as long (the test room: 0.2).")]
        public float markerSeconds = 0.2f;
        [Tooltip("How much bigger the ticks start: for a hit, and for the finishing hit (the test room: 1.25 and 1.7). They come back to their size as they fade.")]
        public Vector2 markerScales = new Vector2(1.25f, 1.7f);

        [Header("A shot leaving")]
        [Min(0), Tooltip("Small bits thrown from the barrel along the shot, so it is seen leaving (the test room: 6). 0 = none.")]
        public int puffAmount = 6;
        [Tooltip("Their colour.")]
        public Color puffColour = new Color(1f, 0.97f, 0.85f);
        [Tooltip("How fast the fastest flies, units per second at scale 1 (the test room: 2.5).")]
        public float puffSpeed = 2.5f;
        [Tooltip("A bit's radius, units at scale 1 (the test room: 0.018).")]
        public float puffSize = 0.018f;
        [Tooltip("Seconds a bit lasts (the test room: 0.2).")]
        public float puffSeconds = 0.2f;
        [Min(1f), Tooltip("An ammo's own firing sound plays up to this many times higher or lower each shot (the test room: 1.1). 1 = always the same.")]
        public float firePitchSpread = 1.1f;

        [Header("A shot landing on something with health: at the shooter's ear (ours, not the test room's)")]
        [Tooltip("One of these plays at the player's ear (not placed in the room) when one of the player's shots lands on something with health. Licensed clips under _Imports: with none, or the clips gone, nothing plays.")]
        public AudioClip[] landedClips = System.Array.Empty<AudioClip>();
        [Range(0f, 1f), Tooltip("How loud that is. 0 = off.")]
        public float landedVolume = 0.5f;
        [Tooltip("One of these plays instead for the hit that knocks it down.")]
        public AudioClip[] downedClips = System.Array.Empty<AudioClip>();
        [Range(0f, 1f), Tooltip("How loud that is. 0 = off.")]
        public float downedVolume = 0.8f;
        [Min(1f), Tooltip("Each of them plays up to this many times higher or lower. 1 = always the same.")]
        public float landedPitchSpread = 1.06f;
        [Min(0f), Tooltip("Seconds: hits that land closer together than this make one sound, so a shotgun's eight pellets are heard once, not eight times. 0 = every hit sounds.")]
        public float landedGap = 0.05f;
        [Range(0f, 1f), Tooltip("Third person: how hard a knock-down nudges the camera, as a share of the strongest shake (Resources/VehicleCamera > Shake: its tilt and travel). 0 = no nudge.")]
        public float downNudge = 0.3f;
        [Min(0f), Tooltip("Seconds that nudge takes to die away. 0 = no nudge.")]
        public float downNudgeSeconds = 0.2f;
        [Min(0f), Tooltip("First person: how far a knock-down kicks the view up, degrees; it springs back like a shot's kick (a pistol shot kicks 1.2). 0 = no kick.")]
        public float downViewPunch = 1.5f;

        [Header("A target going down (practice targets, toy soldiers)")]
        [Min(0), Tooltip("Ours: bits thrown up from where the last hit landed. 0 = none.")]
        public int downBits = 16;
        [Tooltip("Ours: their colour.")]
        public Color downBitsColour = new Color(1f, 0.85f, 0.35f);
        [Tooltip("Ours: how fast the fastest flies, units per second for a target of scale 1.")]
        public float downBitsSpeed = 6f;
        [Tooltip("Ours: a bit's radius, units for a target of scale 1.")]
        public float downBitsSize = 0.08f;
        [Tooltip("Ours: how fast they fall, units per second squared for a target of scale 1.")]
        public float downBitsGravity = 9f;
        [Tooltip("Ours: seconds a bit lasts.")]
        public float downBitsSeconds = 0.5f;
        [Range(0f, 0.5f), Tooltip("Ours: the hit that knocks it down squashes it this much, in place of its usual squash (a bear's is 0.15, a soldier's 0.1). 0 = its usual squash.")]
        public float downSquash = 0.3f;
        [Min(0.01f), Tooltip("Ours: seconds that squash takes to spring back.")]
        public float downSquashSeconds = 0.3f;
        [Tooltip("The test room's: the thud where a toy soldier goes down (its dummy's). Licensed, under _Imports.")]
        public AudioClip figureDownClip;
        [Tooltip("The test room's: the thud where a practice target goes down (its target crate's). Licensed, under _Imports.")]
        public AudioClip targetDownClip;
        [Range(0f, 1f), Tooltip("How loud the thud is. 0 = off.")]
        public float downVolume = 1f;

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
