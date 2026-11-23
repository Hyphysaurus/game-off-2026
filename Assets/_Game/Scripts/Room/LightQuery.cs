using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The one question everything that reacts to light asks (D-0149): is a lit flashlight's beam on this point, within its
    /// reach and cone, with nothing solid between the lens and the point? Reach and cone are the beam's own as it is now,
    /// so a toy's small light has to be close (D-0138) and a focused one reaches further in a narrower cone (D-0141).
    /// Used by LightTarget (anything) and LightSensor (a core).
    /// </summary>
    public static class LightQuery
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>True when a lit flashlight's beam is on <paramref name="point"/>. <paramref name="focus"/> is the most
        /// focused of the beams on it (0 wide, 1 focused). <paramref name="self"/>: the thing being lit, never in the way.</summary>
        public static bool Lit(Vector3 point, Transform self, out float focus)
        {
            focus = 0f;
            bool lit = false;
            for (int i = 0; i < Flashlight.Lit.Count; i++)
            {
                Flashlight torch = Flashlight.Lit[i];
                if (!Shines(torch, point, self)) continue;
                lit = true;
                focus = Mathf.Max(focus, torch.Focus);
            }
            return lit;
        }

        /// <summary>True when this flashlight's beam is on <paramref name="point"/>.</summary>
        public static bool Shines(Flashlight torch, Vector3 point, Transform self)
        {
            Light beam = torch != null ? torch.Beam : null;
            if (beam == null || !beam.enabled || !beam.isActiveAndEnabled) return false;
            Vector3 origin = beam.transform.position;
            Vector3 to = point - origin;
            float distance = to.magnitude;
            if (distance < 1e-5f) return true;
            if (distance > beam.range) return false;
            float half = beam.type == LightType.Spot ? beam.spotAngle * 0.5f : 180f;
            if (Vector3.Angle(beam.transform.forward, to) > half) return false;
            // Nothing solid between the lens and the point, other than the thing itself or the torch.
            int count = Physics.RaycastNonAlloc(origin, to / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = Hits[i].transform;
                if ((self != null && t.IsChildOf(self)) || t.IsChildOf(torch.transform)) continue;
                return false;
            }
            return true;
        }
    }
}
