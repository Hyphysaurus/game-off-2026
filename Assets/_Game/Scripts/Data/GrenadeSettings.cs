using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Grenades as numbers, after the test room's thrower: how many are carried, how the throw leaves the body, how the
    /// free arm swings, and the count on screen. What a grenade does once it has left (its fuse, its blast, its bounce)
    /// is on its own prefab (Prefabs/Combat/Grenade, component Grenade). Lives at Resources/Grenades; without the asset
    /// these defaults are used, and with no grenade prefab set nothing is thrown.
    /// Lengths on the body are the test room's metres x 0.99 (D-0068); the throw's speed is the test room's number
    /// for number, like every shot's flight (D-0071). All of them are at toy size and grow with the thrower.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Grenade Settings", fileName = "Grenades")]
    public sealed class GrenadeSettings : ScriptableObject
    {
        [Header("What is carried")]
        [Min(0), Tooltip("Grenades carried at the start, and after the reset key (the test room: 3).")]
        public int carried = 3;
        [Min(0), Tooltip("The most that can be carried (the test room: 3). A supply crate fills up to this.")]
        public int mostCarried = 3;

        [Header("The throw")]
        [Tooltip("What is thrown: the Grenade prefab (Prefabs/Combat/Grenade). Empty: nothing is thrown.")]
        public Grenade grenade;
        [Tooltip("How fast it leaves the hand, units per second at toy size (the test room: 16). Higher: it lands further away.")]
        public float throwSpeed = 16f;
        [Tooltip("How much the throw is tilted up above where you are looking (the test room: 0.35). 0 = straight along the look; higher: a higher arc.")]
        public float loft = 0.35f;
        [Tooltip("Where it leaves the body at toy size, from the feet: x to the free-hand (left) side, y up, z ahead. Just in front of the chest (the test room: 0.2, 1.45, 0.55 m).")]
        public Vector3 releasePoint = new Vector3(0.198f, 1.4355f, 0.5445f);
        [Min(0f), Tooltip("Seconds between throws (the test room: 0.8).")]
        public float cooldown = 0.8f;
        [Min(0f), Tooltip("The most it spins as it leaves, turns in radians a second about each axis; each throw picks its own (the test room: 6).")]
        public float spin = 6f;
        [Tooltip("The sound of a throw. A licensed clip under _Imports: a project without it throws silently.")]
        public AudioClip throwClip;

        [Header("The arm")]
        [Min(0.01f), Tooltip("Seconds the free arm takes to swing (the test room: 0.4).")]
        public float swingSeconds = 0.4f;
        [Range(0f, 1f), Tooltip("The grenade leaves the hand this far through the swing (the test room: 0.45). Lower: it leaves sooner after the key.")]
        public float releaseAt = 0.45f;
        [Tooltip("Where the hand is wound up, from the free shoulder, at toy size: x to the free-hand side, y up, z ahead (the test room: 0.12, 0.3, -0.12 m).")]
        public Vector3 windUp = new Vector3(0.1188f, 0.297f, -0.1188f);
        [Tooltip("Where the hand ends, thrown forward, from the free shoulder (the test room: 0.02, 0.22, 0.45 m).")]
        public Vector3 release = new Vector3(0.0198f, 0.2178f, 0.4455f);
        [Tooltip("Which way the elbow points during the swing: x to the free-hand side, y up, z ahead (the test room: out, down and a little back).")]
        public Vector3 elbowWay = new Vector3(0.7f, -0.6f, -0.3f);
        [Tooltip("The hand starts forward at the first share of the swing and is fully forward this much after the release (the test room: 0.15 and 0.1).")]
        public Vector2 swingShares = new Vector2(0.15f, 0.1f);
        [Range(0.01f, 1f), Tooltip("The arm has left the animation for the throw by this share of the swing (the test room: 0.2).")]
        public float armInBy = 0.2f;
        [Range(0f, 0.99f), Tooltip("The arm starts back to the animation from this share of the swing (the test room: 0.7).")]
        public float armOutFrom = 0.7f;

        [Header("The throw arc while G is held (D-0148)")]
        [Tooltip("Hold G (pad: the right stick held in) to see where the throw goes; let go to throw.")]
        public bool showArc = true;
        [Min(0f), Tooltip("The arc line's width, units at toy size (grows with the thrower).")]
        public float arcWidth = 0.08f;
        [Min(0f), Tooltip("Dashes along the arc, per line width (0 = a solid line).")]
        public float arcDashes = 0.35f;
        public Color arcColour = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("The landing ring when the blast would not reach the thrower.")]
        public Color ringColour = new Color(1f, 0.85f, 0.3f, 0.9f);
        [Tooltip("The landing ring when the thrower stands inside the blast.")]
        public Color dangerColour = new Color(1f, 0.25f, 0.2f, 0.95f);

        [Header("The count on screen (a line on the weapon read-out)")]
        [Tooltip("What the line says; {0} is the number carried.")]
        public string countText = "Grenades  {0}";
        [Tooltip("The size of the line, on a 1080-high screen.")]
        public float countSize = 27f;
        [Tooltip("The line's colour with grenades left.")]
        public Color countColour = Color.white;
        [Tooltip("The line's colour with none left (the test room: white at 0.45).")]
        public Color emptyColour = new Color(1f, 1f, 1f, 0.45f);

        private static GrenadeSettings _defaults;

        /// <summary>Resources/Grenades, or the defaults above when the asset is missing.</summary>
        public static GrenadeSettings Load()
        {
            var asset = Resources.Load<GrenadeSettings>("Grenades");
            if (asset != null) return asset;
            if (_defaults == null) { _defaults = CreateInstance<GrenadeSettings>(); _defaults.hideFlags = HideFlags.HideAndDontSave; }
            return _defaults;
        }
    }
}
