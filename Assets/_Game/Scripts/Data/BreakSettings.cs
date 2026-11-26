using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Breakable props (D-0101): the pieces each prop's mesh was cut into (by Fraggo, made by Shift Jam / Breakables / Build
    /// Breakables), how they fly, how long they lie about, how they dissolve, and when the prop comes back. Read from
    /// Resources/Breakables. The piece meshes are made from licensed packs and stay out of git (_Imports/Generated).
    /// D-0240: how a prop that carries a Build Back piece builds itself back with Artificer instead of dissolving in (its
    /// off switch: Build Back On), and the backyard gate panel's own numbers, read by Shift Jam > House > Make Gate
    /// Breakable (D-0240).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Break Settings", fileName = "Breakables")]
    public sealed class BreakSettings : ScriptableObject
    {
        public const string ResourceName = "Breakables";

        /// <summary>Which pieces of a build back set off first (D-0240).</summary>
        public enum BuildOrder { BottomUp, TopDown, Random }

        [Tooltip("Every cut prop mesh, found by the mesh's name.")] public CarDamageFxSettings.Cut[] cuts = new CarDamageFxSettings.Cut[0];
        [Tooltip("The cut faces' material (raw wood / card inside).")] public Material inside;
        [Tooltip("The dissolve: a material on the URP Dissolve shader graph. Copied per piece, with the piece's own colour and texture.")] public Material dissolve;
        [Tooltip("The dissolve's glowing edge.")] [ColorUsage(false, true)] public Color dissolveEdge = new Color(1.6f, 1.1f, 0.4f);

        [Header("Pieces")]
        [Tooltip("How fast pieces fly from the hit, least and most, units per second on a prop 5 across.")] public Vector2 burst = new Vector2(4f, 8f);
        [Tooltip("Spin, radians per second at most.")] public float spin = 8f;
        [Tooltip("Seconds a piece lies about before it dissolves.")] public float pieceLife = 4f;
        [Tooltip("Seconds a dissolve takes (out, and the prop coming back in).")] public float dissolveSeconds = 0.8f;
        [Tooltip("Most pieces in the scene at once: past it, the oldest dissolve early.")] public int mostPieces = 120;

        [Header("Coming back")]
        [Tooltip("Seconds until a broken prop dissolves back in where it started (0 = never comes back). A prop's Breakable can have its own number (the gate panel's 30).")] public float comeBackAfter = 20f;
        [Tooltip("It waits while the player stands closer than this, in its own sizes.")] public float clearOfPlayer = 1.5f;

        [Header("Sound")]
        [Tooltip("One of these plays where a prop breaks.")] public AudioClip[] breakSounds = new AudioClip[0];
        [Tooltip("How loud the break sound plays.")] [Range(0f, 1f)] public float breakVolume = 0.8f;

        [Header("Building back (D-0240)")]
        [Tooltip("A prop that carries a Build Back piece (the backyard's gate panel) builds itself back with Artificer: its pieces fly in from round its place, turning and growing, and it is solid again when the last one lands. Off = it dissolves back in like every other prop.")]
        public bool buildBackOn = true;
        [Tooltip("How it is cut into pieces: its parts as modelled (the gate panel: each slat and rail), each patch of its texture, or each flat facet (most, smallest). A part whose model cannot be read in a built game (Read/Write off) flies in as the Fraggo pieces it broke into instead.")]
        public BuildFxSettings.Pieces buildBackPieces = BuildFxSettings.Pieces.Parts;
        [Min(0.05f), Tooltip("Seconds from the first piece setting off to the last one setting off.")]
        public float buildBackSeconds = 0.9f;
        [Tooltip("Seconds one piece takes to fly to its place: each a time between these two (smallest, largest).")]
        public Vector2 buildBackPieceSeconds = new Vector2(0.45f, 0.8f);
        [Tooltip("Which pieces set off first: the lowest (pieces level with each other go from the middle out), the highest, or at random.")]
        public BuildOrder buildBackOrder = BuildOrder.BottomUp;
        [Tooltip("The point the pieces gather round, in its sizes (the longest side of its box) from the middle of its foot: across, up, forward. Each piece starts out beyond its place, away from this point: at the foot they come in from above and the sides.")]
        public Vector3 buildBackFrom = Vector3.zero;
        [Tooltip("Stretches the way out from that point, per axis (across, up, forward): 1, 1, 1 = from all round; 1, 0, 1 = flat, from the sides; 0, 1, 0 = straight down.")]
        public Vector3 buildBackSpread = Vector3.one;
        [Tooltip("How far out a piece starts, in its sizes from its place (smallest, largest).")]
        public Vector2 buildBackDistance = new Vector2(0.5f, 1f);
        [Tooltip("The most a piece is turned when it sets off, degrees about each axis, unwinding as it lands.")]
        public Vector3 buildBackSpin = new Vector3(360f, 360f, 360f);
        [Range(0f, 1f), Tooltip("A piece's size as it sets off, as a share of its own: it grows to full as it lands.")]
        public float buildBackStartScale = 0.3f;
        [Tooltip("How a piece moves along its way, 0 to 1 over its flight (steep then flat = it snaps in and settles).")]
        public AnimationCurve buildBackLanding = new AnimationCurve(new Keyframe(0f, 0f, 2.2f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));

        [Header("The gate panel (D-0240): read by Shift Jam > House > Make Gate Breakable")]
        [Min(2), Tooltip("About how many pieces Fraggo cuts the panel's mesh into. The cut is made once; change this and run the maker again to cut it again.")]
        public int gatePieces = 10;
        [Min(1f), Tooltip("The panel's health, written onto its Damageable by the maker: the kid's punch takes 10, his kick 20 (Resources/PlayerMoves), a dart its weapon's damage (15 from the dart pistol).")]
        public float gateHealth = 60f;
        [Tooltip("Seconds until the broken panel builds itself back, written onto its Breakable by the maker as its own number (never while a player stands in its spot). 0 = it never comes back.")]
        public float gateComeBackAfter = 30f;
        [HideInInspector, Tooltip("The maker's note: the piece count the panel's current cut was asked for. A different Gate Pieces makes the next run cut again.")]
        public int gateCutAskedFor;

        private static BreakSettings _loaded;
        public static BreakSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<BreakSettings>(ResourceName);
            return _loaded;
        }

        public CarDamageFxSettings.Cut CutOf(string mesh)
        {
            if (string.IsNullOrEmpty(mesh) || cuts == null) return null;
            foreach (var c in cuts) if (c != null && c.mesh == mesh && c.pieces != null && c.pieces.Length > 0 && c.pieces[0] != null) return c;
            return null;
        }
    }
}
