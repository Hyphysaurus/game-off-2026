using Playtest;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The monsters under the bed in the play log (D-0228), beside PlayLogEvents and in the same way: it listens to the
    /// monsters' GameEvents and writes one plain line each, with the monster's name: spotted the toy (and whether it was
    /// sleepy in the starlight), a bump that landed, dizzy in a beam, knocked down (how many of how many), all of them down,
    /// and a frozen one tipped over. Bumps and knock-downs are also counted in the play's row. Spawns itself whenever a play
    /// is being recorded; nothing to place in a scene.
    /// </summary>
    public sealed class MonsterPlayLog : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!PlayLog.On || FindFirstObjectByType<MonsterPlayLog>() != null) return;
            var go = new GameObject("MonsterPlayLog"); DontDestroyOnLoad(go); go.AddComponent<MonsterPlayLog>();
        }

        private void OnEnable()
        {
            GameEvents.MonsterSpotted += OnSpotted; GameEvents.MonsterBumped += OnBumped; GameEvents.MonsterDizzy += OnDizzy;
            GameEvents.MonsterDown += OnDown; GameEvents.MonstersAllDown += OnAllDown; GameEvents.MonsterTipped += OnTipped;
        }

        private void OnDisable()
        {
            GameEvents.MonsterSpotted -= OnSpotted; GameEvents.MonsterBumped -= OnBumped; GameEvents.MonsterDizzy -= OnDizzy;
            GameEvents.MonsterDown -= OnDown; GameEvents.MonstersAllDown -= OnAllDown; GameEvents.MonsterTipped -= OnTipped;
        }

        private static string Name(DarkMonster m) => m == null ? "?" : m.name;

        private void OnSpotted(DarkMonster m) => PlayLog.Tell("monster spotted the toy: " + Name(m) + (m != null && m.Sleepy ? " (sleepy, starlight)" : " (dark)"));
        private void OnBumped(DarkMonster m) { PlayLog.Tell("monster bumped the toy: " + Name(m)); PlayLog.Count("monster bumps"); }
        private void OnDizzy(DarkMonster m) => PlayLog.Tell("monster dizzy in the beam: " + Name(m));
        private void OnDown(DarkMonster m) { PlayLog.Tell("monster knocked down: " + Name(m) + " (" + DarkMonster.DownCount + " of " + DarkMonster.RoamCount + " down)"); PlayLog.Count("monsters down"); }
        private void OnAllDown(int count) => PlayLog.Tell("ALL " + count + " monsters under the bed knocked down");
        private void OnTipped(DarkMonster m) => PlayLog.Tell("frozen monster tipped over (wall switch's light): " + Name(m));
    }
}
