using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A step or a star for a part that only raises a UnityEvent (D-0220): a core's On Used, a doorway's On Used, a Flat
    /// Zone's On All Found, a squad's On Routed, a skittle's or soldier's On Died, a ring's On Boost, the lights' On Lights
    /// On, the star lamp's On Woken. The objectives maker (Shift Jam > Objectives > Build Objectives) puts one hook per
    /// rule under Greybox's "ObjectiveHooks (D-0220)" and wires those events to it as persistent listeners, so the parts'
    /// own scripts never change. A rule over several parts (three skittles, four soldiers, three rings) calls
    /// <see cref="FirePart"/> with each part's number: the hook counts DIFFERENT parts until Needed have fired, in this
    /// visit to the scene (a reload starts the count again; a star already earned stays earned).
    /// OFF SWITCH: untick this script (a switched-off hook ignores its calls), or delete the hooks' object.
    /// </summary>
    public sealed class ObjectiveHook : MonoBehaviour
    {
        public enum Kind { Step, Star }

        [SerializeField, Tooltip("The step's or star's id in the ObjectiveSet (Resources/ObjectiveSet).")] private string id = "";
        [SerializeField, Tooltip("A main step (marks it and every step before it) or a gold star (earns it).")] private Kind kind = Kind.Star;
        [SerializeField, Min(1), Tooltip("How many DIFFERENT parts must fire (Fire Part's number) before it counts. 1 = the first call.")]
        private int needed = 1;

        private int _fired, _count;   // the parts that have fired this visit (one bit each), and how many

        public string Id => id;
        public Kind What => kind;
        public int Needed => needed;

        /// <summary>The maker's: what this hook marks, and how many different parts it waits for.</summary>
        public void Setup(string stepOrStarId, Kind stepOrStar, int partsNeeded)
        {
            id = stepOrStarId; kind = stepOrStar; needed = Mathf.Max(1, partsNeeded);
        }

        /// <summary>Marks the step or earns the star now.</summary>
        public void Fire()
        {
            if (!isActiveAndEnabled) return;
            if (kind == Kind.Step) Objectives.MarkStep(id);
            else Objectives.AwardStar(id);
        }

        /// <summary>Part number <paramref name="part"/> (0..31) has fired: once Needed different parts have, it counts.</summary>
        public void FirePart(int part)
        {
            if (!isActiveAndEnabled) return;
            if (needed <= 1) { Fire(); return; }
            if (part < 0 || part > 31) return;
            int bit = 1 << part;
            if ((_fired & bit) != 0) return;
            _fired |= bit; _count++;
            if (_count >= needed) Fire();
        }
    }
}
