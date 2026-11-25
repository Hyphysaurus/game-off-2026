using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What the rail camera needs from a Flat Zone (D-0198): the lane's plane, the stretch of it the camera may travel,
    /// the floor and ceiling it frames between, and the numbers. The camera looks along -normal (the normal points to the
    /// camera's side); "along" runs to the camera's right.
    /// </summary>
    public sealed class FlatRail
    {
        public Vector3 point;          // on the lane's plane (world)
        public Vector3 normal;         // flat, unit: towards the camera
        public float start, end;       // how far along (from point) the camera's middle may go, world units
        public float floor, ceiling;   // world heights the frame stays between where it can (ceiling <= floor: no limit)
        public FlatViewSettings view;

        /// <summary>The way along the lane, to the camera's right.</summary>
        public Vector3 Along => Vector3.Cross(Vector3.up, -normal).normalized;
    }
}
