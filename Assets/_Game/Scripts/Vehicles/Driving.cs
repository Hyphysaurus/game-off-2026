using System;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The vehicle the player drives right now, or null (D-0037). The seats set it (CarSeat, ToyCar); the driving HUD and
    /// the race read it.
    /// </summary>
    public static class Driving
    {
        public static Transform Vehicle { get; private set; }
        public static event Action Changed;
        /// <summary>True once the game is quitting (in the editor: Play stopping). Seats skip putting the driver back then:
        /// Unity refuses a reparent while the car is being switched off.</summary>
        public static bool Quitting { get; private set; }

        public static void Set(Transform vehicle)
        {
            if (Vehicle == vehicle) return;
            Vehicle = vehicle;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Vehicle = null; Changed = null; Quitting = false;
            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;
        }

        private static void OnQuit() => Quitting = true;
    }
}
