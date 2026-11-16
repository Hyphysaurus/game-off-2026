using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Marks a soft thing (cloth, a plush toy): paint soaks into it. A shell needs more speed to break on it, its splats
    /// are smaller and blotchy, it throws fewer drops and paint does not run down it. Put it on the thing or any parent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintSoft : MonoBehaviour
    {
        /// <summary>True if what was hit, or anything it is part of, is marked soft.</summary>
        public static bool Is(Collider struck) => struck != null && struck.GetComponentInParent<PaintSoft>() != null;
    }
}
