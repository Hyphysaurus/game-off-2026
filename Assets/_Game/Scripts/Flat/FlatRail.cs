using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// What the rail camera needs from a Flat Zone (D-0198): the lane's plane, the stretch of it the camera may travel,
    /// the floor and ceiling it frames between, and the numbers. The camera looks along -normal (the normal points to the
    /// camera's side); "along" runs to the camera's right.
    /// It also holds the rail camera's sums, with no state: where the camera stands, how it is turned and what it frames
    /// for a given place along the lane and a given height. ShoulderCamera.RailView uses exactly these every frame
    /// (it only adds the easing in between), and they can be asked in the editor without Play (FlatZone.ViewFor), so
    /// the framing can be checked against numbers on paper.
    /// </summary>
    public sealed class FlatRail
    {
        public Vector3 point;          // on the lane's plane (world)
        public Vector3 normal;         // flat, unit: towards the camera
        public float start, end;       // how far along (from point) the camera's middle may go, world units
        public float floor, ceiling;   // world heights the frame stays between where it can (ceiling <= floor: no limit)
        public FlatViewSettings view;

        /// <summary>The numbers in use: the zone's, or Resources/FlatView (its defaults when there is none).</summary>
        public FlatViewSettings View => view != null ? view : (view = FlatViewSettings.Load());

        /// <summary>The way along the lane, to the camera's right.</summary>
        public Vector3 Along => Vector3.Cross(Vector3.up, -normal).normalized;

        /// <summary>How far along the lane (from its point) a place in the world is.</summary>
        public float AlongOf(Vector3 world) => Vector3.Dot(world - point, Along);

        /// <summary>World units of the lane the screen shows top to bottom, for a player of this size.</summary>
        public float Frame(float scale) => Mathf.Max(0.1f, View.frameHeight * scale);

        /// <summary>The rail camera's field of view, degrees.</summary>
        public float FieldOfView => Mathf.Clamp(View.fieldOfView, 1f, 120f);

        /// <summary>How the rail camera is turned: looking at the lane from the normal's side, tipped by the pitch.</summary>
        public Quaternion Rotation => Quaternion.LookRotation(-normal, Vector3.up) * Quaternion.Euler(View.pitch, 0f, 0f);

        /// <summary>How far the camera stands from the point it looks at, so the frame fills the screen at the lane.</summary>
        public float Distance(float scale) => Frame(scale) * 0.5f / Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>The height the middle of the frame wants for feet at <paramref name="feetY"/> (before the floor and
        /// the ceiling have their say).</summary>
        public float HeightFor(float feetY, float scale) => feetY + Frame(scale) * (0.5f - View.feetInFrame);

        /// <summary>That place along the lane, kept between the rail's ends.</summary>
        public float ClampAlong(float along) => Mathf.Clamp(along, Mathf.Min(start, end), Mathf.Max(start, end));

        /// <summary>That height for the middle of the frame, kept so the frame shows nothing under the floor or over the
        /// ceiling; a frame taller than the room sits in its middle. No floor and ceiling given: unchanged.</summary>
        public float ClampHeight(float height, float scale)
        {
            if (ceiling <= floor) return height;
            float half = Frame(scale) * 0.5f, bottom = floor + half, top = ceiling - half;
            return bottom <= top ? Mathf.Clamp(height, bottom, top) : (floor + ceiling) * 0.5f;
        }

        /// <summary>The point in the world the camera looks at (the middle of the screen, on the lane) for a place along
        /// the lane and a height, both after the rail's limits.</summary>
        public Vector3 Centre(float along, float height, float scale)
        {
            Vector3 centre = point + Along * ClampAlong(along);
            centre.y = ClampHeight(height, scale);
            return centre;
        }

        /// <summary>Where the camera stands and how it is turned to look at that point.</summary>
        public void Pose(float along, float height, float scale, out Vector3 position, out Quaternion rotation)
        {
            rotation = Rotation;
            position = Centre(along, height, scale) - rotation * Vector3.forward * Distance(scale);
        }

        /// <summary>The view once it has settled on a body whose feet stand at <paramref name="feet"/>:
        /// <paramref name="lead"/> is how far ahead along the lane it looks (0 standing still; the settings' lead, own
        /// units times the size, running to the camera's right; minus that running left).</summary>
        public void Settled(Vector3 feet, float lead, float scale, out Vector3 position, out Quaternion rotation)
            => Pose(AlongOf(feet) + lead, HeightFor(feet.y, scale), scale, out position, out rotation);
    }
}
