using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The player's health and shield (D-0112), the same in the course, the sandboxes and online. Resources/PlayerHealth
    /// is used when it exists (Create > Shift Jam > Player Health Settings, in a Resources folder, to tune); without
    /// one these defaults play. The shield takes hits first and comes back by itself a while after the last hit;
    /// health does not come back by itself.
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Player Health Settings", fileName = "PlayerHealth")]
    public sealed class PlayerHealthSettings : ScriptableObject
    {
        [Header("Health")]
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f), Tooltip("Seconds after a hit during which further hits are ignored (a shotgun's pellets count once).")] public float invulnerability = 0.1f;
        [Min(0f), Tooltip("Seconds nothing can hurt a player who just came back after being knocked out.")] public float respawnProtection = 2f;

        [Header("Back in play")]
        [Min(0f), Tooltip("Seconds a knocked-out player must lie before Jump or Fire brings them straight back (the full wait is PlayerMoves' respawnAfter).")] public float skipAfter = 0.6f;
        [Tooltip("Coming back after a knock-out fills every blaster's magazine, its starting reserve and the grenades.")] public bool restock = true;

        [Header("Shield")]
        [Min(0f), Tooltip("Shield points, taken before health. 0 = no shield.")] public float shieldMax = 50f;
        [Min(0f), Tooltip("Seconds after the last hit before the shield starts coming back.")] public float shieldDelay = 4f;
        [Min(0f), Tooltip("Shield points back a second once it is coming back.")] public float shieldRate = 25f;

        [Header("On screen")]
        [Tooltip("Which screen corner the bars sit in (0,0 bottom left, 1,0 bottom right).")] public Vector2 hudCorner = new Vector2(1f, 0f);
        [Tooltip("From that corner, in a 1920 x 1080 screen's pixels (negative x goes left from the right edge).")] public Vector2 hudOffset = new Vector2(-40f, 40f);
        [Min(40f)] public float barWidth = 320f;
        [Min(4f)] public float barHeight = 16f;
        public Color healthColor = new Color(1f, 0.42f, 0.3f);
        public Color shieldColor = new Color(0.35f, 0.85f, 1f);
        [Range(0f, 1f), Tooltip("At or under this share of health the bar pulses.")] public float lowHealthShare = 0.3f;

        private static PlayerHealthSettings _loaded;

        /// <summary>Resources/PlayerHealth, or the defaults.</summary>
        public static PlayerHealthSettings Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<PlayerHealthSettings>("PlayerHealth");
            if (_loaded == null) _loaded = CreateInstance<PlayerHealthSettings>();
            return _loaded;
        }
    }
}
