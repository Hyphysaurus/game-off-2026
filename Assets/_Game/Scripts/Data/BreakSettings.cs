using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Breakable props (D-0101): the pieces each prop's mesh was cut into (by Fraggo, made by Shift Jam / Breakables / Build
    /// Breakables), how they fly, how long they lie about, how they dissolve, and when the prop comes back. Read from
    /// Resources/Breakables. The piece meshes are made from licensed packs and stay out of git (_Imports/Generated).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Break Settings", fileName = "Breakables")]
    public sealed class BreakSettings : ScriptableObject
    {
        public const string ResourceName = "Breakables";

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
        [Tooltip("Seconds until a broken prop dissolves back in where it started (0 = never comes back).")] public float comeBackAfter = 20f;
        [Tooltip("It waits while the player stands closer than this, in its own sizes.")] public float clearOfPlayer = 1.5f;

        [Header("Sound")]
        public AudioClip[] breakSounds = new AudioClip[0];
        [Range(0f, 1f)] public float breakVolume = 0.8f;

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
