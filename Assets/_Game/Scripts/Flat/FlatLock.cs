using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// A Flat Zone's hold on the body (D-0198): the plane the body keeps to while a side-on stretch is on. The stick's
    /// depth part, any sideways speed along the normal and any drift off the plane are taken away by the motor (and the
    /// rope's stick read), so the 3D body plays as a 2D one. The zone hands the motor a fresh one every frame it holds
    /// the body, so the numbers (FlatView) can be tuned while playing.
    /// Its plane is also the one the 2D aim lives in (D-0216, FlatAim): Right is the screen's right, Meet finds where
    /// the cursor's ray lands on it, OnPlane puts a point on it.
    /// </summary>
    public struct FlatLock
    {
        /// <summary>Any point on the plane (world).</summary>
        public Vector3 point;
        /// <summary>The plane's normal (world, flat on the ground, unit length): the depth axis that is locked.</summary>
        public Vector3 normal;
        /// <summary>Share of the drift off the plane taken away per second (the body is eased back, never snapped).</summary>
        public float snapRate;
        /// <summary>The fastest the body is slid back onto the plane, world units a second. 0 = no limit.</summary>
        public float snapSpeed;
        /// <summary>Degrees the stick may move and still mean the way it was going when the hold came on or went off
        /// (the camera turns a quarter round then, and the stick is read against it). 0 = the stick is not kept.</summary>
        public float keepStick;
        /// <summary>Who holds the body (the Flat Zone). A second zone leaves a body alone while the first has it, and a
        /// zone only takes its own hold away. Empty, or gone: nobody's.</summary>
        public Object owner;

        public FlatLock(Vector3 point, Vector3 normal, float snapRate, float snapSpeed = 0f, float keepStick = 0f, Object owner = null)
        {
            normal.y = 0f;
            this.point = point;
            this.normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.forward;
            this.snapRate = Mathf.Max(0f, snapRate);
            this.snapSpeed = Mathf.Max(0f, snapSpeed);
            this.keepStick = Mathf.Clamp(keepStick, 0f, 90f);
            this.owner = owner;
        }

        /// <summary>The vector with its part along the normal taken away.</summary>
        public Vector3 Flatten(Vector3 v) => v - normal * Vector3.Dot(v, normal);

        /// <summary>How far this point stands off the plane, along the normal.</summary>
        public float Off(Vector3 position) => Vector3.Dot(position - point, normal);

        /// <summary>The move this step that eases a body at <paramref name="position"/> back toward the plane: a share
        /// of what is left, never faster than Snap Speed.</summary>
        public Vector3 Correction(Vector3 position, float dt)
        {
            float step = Off(position) * Mathf.Clamp01(snapRate * dt);
            if (snapSpeed > 0f) step = Mathf.Clamp(step, -snapSpeed * dt, snapSpeed * dt);
            return -normal * step;
        }

        /// <summary>The way along the lane (flat, unit length) that is nearest <paramref name="hint"/>. A hint with no
        /// part along the lane (straight into the depth, or nothing): the way to the camera's right.</summary>
        public Vector3 Along(Vector3 hint)
        {
            Vector3 right = Right;
            return Vector3.Dot(hint, right) < 0f ? -right : right;
        }

        /// <summary>The way along the lane to the side camera's right (flat, unit length): the camera stands on the
        /// normal's side and looks along -normal. The screen's right in the side view (D-0216).</summary>
        public Vector3 Right => Vector3.Cross(Vector3.up, -normal);

        /// <summary>The point on the plane straight across from <paramref name="position"/> (its depth taken away).</summary>
        public Vector3 OnPlane(Vector3 position) => position - normal * Off(position);

        /// <summary>Where <paramref name="ray"/> meets the plane, from the camera's side (D-0216: the mouse cursor's ray
        /// in the side view). False when it runs along the plane, away from it, or meets it behind its start: a camera
        /// still swinging in from behind the player, or one on the wrong side.</summary>
        public bool Meet(Ray ray, out Vector3 point)
        {
            point = default;
            float facing = Vector3.Dot(ray.direction, normal);
            if (facing > -0.05f) return false;   // along the plane or away from it: no useful meeting
            float distance = Off(ray.origin) / -facing;
            if (distance <= 0f) return false;
            point = ray.origin + ray.direction * distance;
            return true;
        }
    }
}
