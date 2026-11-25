using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A small marker on a core (D-0221, "the refusals tell you where to go"): its place name (the words are on
    /// ObjectiveSet > Core Places under that name) and which size of player using it refills the charge, copied from the
    /// core's Mode by the helpers maker (Grow Only refills a toy, Shrink Only a kid, Toggle and Charge Only either). A
    /// "No charge" press names the nearest marked core that is awake, not spent and would refill that player.
    /// The maker adds one to every core in the open scene; without markers the old words stay.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ShiftSource))]
    public sealed class CoreMarker : MonoBehaviour
    {
        private static readonly List<CoreMarker> Marked = new List<CoreMarker>();

        [SerializeField, Tooltip("The name its place words are kept under on ObjectiveSet > Core Places (the maker writes the core's own object name).")]
        private string place = "";
        [SerializeField, Tooltip("Using this core refills a TOY-size player (it grows a toy, toggles, or only fills the charge). Set by the maker from the core's Mode.")]
        private bool refillsToy = true;
        [SerializeField, Tooltip("Using this core refills a KID-size player (it shrinks a kid, toggles, or only fills the charge). Set by the maker from the core's Mode.")]
        private bool refillsKid = true;

        private ShiftSource _core;

        public string Place => place ?? "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Marked.Clear();

        private void Awake() => _core = GetComponent<ShiftSource>();
        private void OnEnable() { if (!Marked.Contains(this)) Marked.Add(this); }
        private void OnDisable() => Marked.Remove(this);

        /// <summary>The maker's: the place name and the sizes it refills.</summary>
        public void Setup(string placeName, bool toy, bool kid)
        {
            place = placeName ?? "";
            refillsToy = toy;
            refillsKid = kid;
        }

        /// <summary>The nearest marked core to <paramref name="from"/> that is awake, not spent and would refill a player of
        /// <paramref name="size"/>, and has place words; null when none does. Walks the markers (a press, not every frame).</summary>
        public static CoreMarker Nearest(Vector3 from, ShiftSize size, ObjectiveSet set)
        {
            CoreMarker best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Marked.Count; i++)
            {
                CoreMarker m = Marked[i];
                if (m == null || m._core == null || !m._core.Usable) continue;
                if (size == ShiftSize.Toy ? !m.refillsToy : !m.refillsKid) continue;
                if (string.IsNullOrWhiteSpace(HelperWords.CorePlaceOf(set, m.Place))) continue;
                float sqr = (m.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                best = m;
                bestSqr = sqr;
            }
            return best;
        }
    }

    /// <summary>
    /// The words a refused shift shows (D-0221): "No charge" names the nearest core that can refill the player, "No room to
    /// grow" says where there is room (ObjectiveSet > Grow Rooms). Anything else, or nothing known, keeps the old words.
    /// Only the toast changes: GameEvents.ShiftRefused still carries the old words (the play log and the sounds read them).
    /// </summary>
    public static class RefusalPointer
    {
        /// <summary>What to show for <paramref name="refused"/> said to a player of <paramref name="size"/> at <paramref name="at"/>.</summary>
        public static string Point(string refused, Vector3 at, ShiftSize size)
        {
            if (string.IsNullOrEmpty(refused) || !Application.isPlaying) return refused;
            ObjectiveSet set = Objectives.Set;
            if (set == null || !set.refusalsPoint) return refused;
            if (refused == ShiftCharge.Empty)
            {
                CoreMarker core = CoreMarker.Nearest(at, size, set);
                if (core == null) return refused;
                return HelperWords.Format(set.emptyPointFormat, HelperWords.CorePlaceOf(set, core.Place));
            }
            if (refused == ShiftCharge.NoRoom)
            {
                string words = HelperWords.GrowWordsAt(set, at);
                return string.IsNullOrWhiteSpace(words) ? refused : words;
            }
            return refused;
        }
    }
}
