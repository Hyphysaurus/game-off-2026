using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The project's own physics layers (D-0099). CarGuard: the Expressway's collision-only guards over its rails. They
    /// keep the cars on the road (the cars and everything else are on Default and still bump them), but nothing aimed or
    /// looked through should stop on a wall nobody can see: shots, darts, grenades, paint, sight lines, the camera and
    /// the grab ray all leave the layer out of their queries through <see cref="Seen"/>.
    /// </summary>
    public static class Layers
    {
        public const string CarGuardName = "CarGuard";
        private static int _carGuard = -1;

        /// <summary>The CarGuard layer's index, or -1 if the project has no such layer.</summary>
        public static int CarGuard
        {
            get { if (_carGuard < 0) _carGuard = LayerMask.NameToLayer(CarGuardName); return _carGuard; }   // asked again while missing: the layer can be added in the editor
        }

        /// <summary><paramref name="mask"/> without the layers nobody can see (the car guards).</summary>
        public static int Seen(int mask) => CarGuard < 0 ? mask : mask & ~(1 << CarGuard);
    }
}
