using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The race in progress (D-0037): the race loop writes it, the driving HUD and the stadium TV read it. One race at a
    /// time. Times are seconds of play (scaled time).
    /// </summary>
    public static class RaceState
    {
        public static bool Running { get; private set; }
        /// <summary>The lap being driven, 1-based while running; after a finish, the number of laps run.</summary>
        public static int Lap { get; private set; }
        public static int Laps { get; private set; }
        /// <summary>Gates passed this lap, and how many there are (the finish line not counted).</summary>
        public static int Gate { get; private set; }
        public static int Gates { get; private set; }
        public static float LapTime { get; private set; }
        public static float TotalTime { get; private set; }
        /// <summary>The fastest lap so far this race, 0 when none yet.</summary>
        public static float BestLap { get; private set; }
        public static float LastLap { get; private set; }

        public static event Action Started, GatePassed, LapDone, Finished, Stopped;

        public static void Begin(int laps, int gates)
        {
            Running = true; Laps = Mathf.Max(1, laps); Gates = Mathf.Max(0, gates);
            Lap = 1; Gate = 0; LapTime = 0f; TotalTime = 0f; BestLap = 0f; LastLap = 0f;
            Started?.Invoke();
        }

        public static void Tick(float dt)
        {
            if (!Running) return;
            LapTime += dt; TotalTime += dt;
        }

        public static void PassGate() { if (!Running) return; Gate++; GatePassed?.Invoke(); }

        /// <summary>The finish line crossed with every gate passed: the lap counts. Returns true when that ended the race.</summary>
        public static bool CompleteLap()
        {
            if (!Running) return false;
            LastLap = LapTime;
            if (BestLap <= 0f || LapTime < BestLap) BestLap = LapTime;
            LapDone?.Invoke();
            if (Lap >= Laps) { Running = false; Finished?.Invoke(); return true; }
            Lap++; Gate = 0; LapTime = 0f;
            return false;
        }

        /// <summary>Abandoned (the driver got out, a reset): nothing more counts.</summary>
        public static void Stop()
        {
            if (!Running) return;
            Running = false;
            Stopped?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Running = false; Lap = Laps = Gate = Gates = 0; LapTime = TotalTime = BestLap = LastLap = 0f;
            Started = GatePassed = LapDone = Finished = Stopped = null;
        }
    }
}
