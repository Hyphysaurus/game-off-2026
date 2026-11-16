using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A race with a field (D-0061): every car in it, the countdown, who leads and how each one finished. One at a time,
    /// like RaceState, which stays what it was: the PLAYER's own lap, gates and times. The grid (RaceGrid) opens the
    /// field, counts it down and closes it; the finish line (RaceLoop) and the gates count the cars through; the HUD
    /// only reads. A race begun the old way, by driving over the line alone, has no field and nothing here runs.
    /// Times are seconds of play from GO.
    /// </summary>
    public static class RaceField
    {
        public enum Stage
        {
            /// <summary>No race with a field.</summary>
            None,
            /// <summary>Lined up on the grid, held still, the lights counting.</summary>
            Countdown,
            /// <summary>GO has been given and the player is still racing.</summary>
            Racing,
            /// <summary>The player has finished: the results are up while the rest come home.</summary>
            Done,
        }

        private static readonly List<Racer> List = new List<Racer>();
        private static readonly List<Racer> Order = new List<Racer>();
        private static readonly Comparison<Racer> ByStanding = Compare;
        private static float _clock, _lap;
        private static int _home;   // cars over the line at the end so far

        public static Stage Now { get; private set; }
        /// <summary>The number on the lights during the countdown (3, 2, 1), 0 once GO is given.</summary>
        public static int Count { get; private set; }
        /// <summary>Every car in the race, in grid order.</summary>
        public static IReadOnlyList<Racer> Racers => List;
        /// <summary>The same cars, leader first (after <see cref="Refresh"/>).</summary>
        public static IReadOnlyList<Racer> Standings => Order;
        public static Racer Player { get; private set; }
        /// <summary>The finish line this race is counted at.</summary>
        public static RaceLoop Line { get; private set; }
        public static RacePath Path { get; private set; }
        public static int Laps { get; private set; }
        /// <summary>Seconds since GO. While the player races it is the player's own total time, to the frame.</summary>
        public static float Clock => RaceState.Running && Now == Stage.Racing ? RaceState.TotalTime : _clock;
        /// <summary>Unscaled time at which GO was given (the HUD shows GO! for a moment).</summary>
        public static float WentAt { get; private set; }

        /// <summary>Opened: the cars are on the grid. Counted: the lights changed. Went: GO. Settled: the player finished
        /// and the results are known. Closed: over, the cars are going home.</summary>
        public static event Action Opened, Counted, Went, Settled, Closed;
        /// <summary>A car crossed the line and it counted (the start of lap one, or a lap done).</summary>
        public static event Action<Racer> Crossed;
        /// <summary>A car was put back on the path.</summary>
        public static event Action<Racer> Rescued;

        public static void Open(List<Racer> racers, Racer player, RaceLoop line, RacePath path, int laps)
        {
            List.Clear(); List.AddRange(racers);
            Order.Clear(); Order.AddRange(racers);
            Player = player; Line = line; Path = path; Laps = Mathf.Max(1, laps);
            _lap = path != null ? path.Length : 0f;
            _clock = 0f; Count = 0; _home = 0;
            Now = Stage.Countdown;
            Refresh();
            Opened?.Invoke();
        }

        public static void Show(int count)
        {
            if (Now != Stage.Countdown) return;
            Count = count;
            Counted?.Invoke();
        }

        public static void Go()
        {
            if (Now != Stage.Countdown) return;
            Now = Stage.Racing; Count = 0; _clock = 0f;
            WentAt = Time.unscaledTime;
            Went?.Invoke();
        }

        /// <summary>Runs the clock once the player's own has stopped (the rest are still coming home).</summary>
        public static void Tick(float dt)
        {
            if (Now == Stage.Racing && RaceState.Running) _clock = RaceState.TotalTime;
            else if (Now == Stage.Racing || Now == Stage.Done) _clock += dt;
        }

        /// <summary>The car a collider belongs to, if it is in the race.</summary>
        public static Racer Find(Collider other)
        {
            if (other == null || Now == Stage.None) return null;
            Rigidbody body = other.attachedRigidbody;
            if (body == null) return null;
            for (int i = 0; i < List.Count; i++)
                if (List[i].Car != null && List[i].Car.Body == body) return List[i];
            return null;
        }

        /// <summary>A crossing of the line that counts (the line has checked the gates): the first starts lap one, each
        /// one after ends a lap, and the last lap's ends that car's race. Null is ignored (a race with no field).</summary>
        public static void Cross(Racer racer)
        {
            if (racer == null || racer.Finished || (Now != Stage.Racing && Now != Stage.Done)) return;
            float now = Clock;
            racer.Crossings++;
            if (racer.Crossings > 1) racer.LapTimes.Add(now - racer.LapStart);
            racer.LapStart = now;
            racer.Gate = 0;
            if (racer.LapsDone >= Laps)
            {
                racer.Finished = true; racer.Estimated = false;
                racer.FinishTime = now;
                racer.FinishRank = ++_home;
            }
            Refresh();
            Crossed?.Invoke(racer);
        }

        /// <summary>The player has finished (RaceState says so): the clock carries on by itself, every car still out is
        /// given the time it is on course for, and the results are known.</summary>
        public static void Settle()
        {
            if (Now != Stage.Racing) return;
            _clock = RaceState.TotalTime;
            Now = Stage.Done;
            for (int i = 0; i < List.Count; i++)
            {
                Racer r = List[i];
                if (r.Finished) continue;
                // from the line (not the grid) at the pace it has held so far
                float come = Mathf.Max(1f, r.Standing(_lap)), left = Mathf.Max(0f, Laps * _lap - come);
                r.FinishTime = _clock + left * (_clock / come);
                r.Estimated = true;
            }
            Refresh();
            Settled?.Invoke();
        }

        public static void TellRescued(Racer racer) => Rescued?.Invoke(racer);

        /// <summary>Over. The cars and the results stay readable until the next race opens.</summary>
        public static void Close()
        {
            if (Now == Stage.None) return;
            Now = Stage.None; Count = 0;
            Line = null;
            Closed?.Invoke();
        }

        /// <summary>Works out who leads: finished cars in the order they crossed the line, then the rest by how far they
        /// have come.</summary>
        public static void Refresh()
        {
            Order.Sort(ByStanding);
            for (int i = 0; i < Order.Count; i++) Order[i].Place = i + 1;
        }

        private static int Compare(Racer a, Racer b)
        {
            if (a.Finished != b.Finished) return a.Finished ? -1 : 1;
            if (a.Finished) return a.FinishRank.CompareTo(b.FinishRank);
            float sa = a.Standing(_lap), sb = b.Standing(_lap);
            return sa != sb ? sb.CompareTo(sa) : a.Slot.CompareTo(b.Slot);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            List.Clear(); Order.Clear();
            Player = null; Line = null; Path = null;
            Now = Stage.None; Count = 0; Laps = 0; _clock = _lap = 0f; WentAt = 0f; _home = 0;
            Opened = Counted = Went = Settled = Closed = null;
            Crossed = Rescued = null;
        }
    }
}
