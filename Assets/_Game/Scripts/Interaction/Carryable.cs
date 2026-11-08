using System;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// Something a kid-sized player can pick up, aim and put down. It knows nothing about hands: the Carrier
    /// on the Player moves it. It only says how to hold it (which way is its front, where the hands close) and tells
    /// listeners when it is picked up, used (the Attack button while held) or put down.
    /// Grips (D-0042) are in the thing's own space, so they follow its size: a child named "Grip" is where one hand closes,
    /// children "GripR" and "GripL" are where two hands hold it. Without them the grips come from the thing's own box: a
    /// quarter of the length in from the handle end (the end away from a flashlight's beam), or for a plank a quarter in
    /// from each end, held in both hands.
    /// </summary>
    public sealed class Carryable : MonoBehaviour
    {
        [SerializeField, Tooltip("Yaw-only rotation used when the thing is put down, so its front faces the way the player faces.")]
        private Vector3 holdRotation = Vector3.zero;
        [Header("Grip (in the right hand)")]
        [SerializeField, Tooltip("Where the palm centre is, in the hand bone's own space (rig metres): the fingers run along the hand's +Y. The thing's grip (a Grip child, or the handle end) is put here.")]
        private Vector3 gripPalm = new Vector3(0f, 0.08f, 0f);
        [SerializeField, Tooltip("Extra turn of the thing in the hand. Zero = the thing's +Z points the way the arm reaches (shoulder to hand).")]
        private Vector3 gripRotation = Vector3.zero;
        [SerializeField, Tooltip("When put down, lie along the ground: the far end finds its own support, so a ruler laid from a table edge toward a box becomes a ramp. Planks are carried flat in both hands.")]
        private bool plank;
        [SerializeField] private UnityEvent onPickedUp;
        [SerializeField] private UnityEvent onPutDown;

        private bool _measured;
        private Bounds _local;
        private Transform _grip, _gripR, _gripL;

        public Quaternion HoldRotation => Quaternion.Euler(holdRotation);
        public Vector3 GripPalm => gripPalm;
        public Quaternion GripRotation => Quaternion.Euler(gripRotation);
        public bool Plank => plank;
        [SerializeField, Tooltip("Carried in the hand (Carrier) instead of grabbed (ThirdPersonGrab). Planks and flashlights always are.")]
        private bool keepInHand;
        /// <summary>True for tools held in the hand: the flashlight (Attack uses it) and planks (laid along the ground).
        /// Everything else is grabbed and set down with the third-person grab.</summary>
        public bool KeepInHand => keepInHand || plank || GetComponent<Flashlight>() != null;
        public bool IsCarried { get; private set; }
        /// <summary>Raised when the Attack button is pressed while this is held. The flashlight toggles on it.</summary>
        public event Action Used;

        /// <summary>The thing's box in its own space (its meshes), worked out once.</summary>
        public Bounds LocalBounds { get { Measure(); return _local; } }

        /// <summary>Held in both hands: a plank, or anything with GripR and GripL children.</summary>
        public bool TwoHanded { get { Measure(); return plank || AuthoredTwoHandGrips; } }

        /// <summary>True when GripR and GripL children say where the two hands go (and which is the right).</summary>
        public bool AuthoredTwoHandGrips { get { Measure(); return _gripR != null && _gripL != null; } }

        /// <summary>Where one hand closes, in the thing's own space: the Grip child, or a quarter of the length in from the
        /// handle end (away from a flashlight's beam; otherwise the -end of the longest side).</summary>
        public Vector3 GripLocal
        {
            get
            {
                Measure();
                if (_grip != null) return transform.InverseTransformPoint(_grip.position);
                Vector3 head = LongAxis();
                if (TryGetComponent(out Flashlight torch) && torch.Beam != null)
                {
                    Vector3 toLens = transform.InverseTransformPoint(torch.Beam.transform.position) - _local.center;
                    head = Dominant(toLens);
                }
                return _local.center - head * (Along(head) * 0.5f);
            }
        }

        /// <summary>Where two hands hold it, in the thing's own space: GripR and GripL children, or a quarter in from each end
        /// along its longest side (<paramref name="a"/> the + end, <paramref name="b"/> the - end).</summary>
        public void TwoHandGrips(out Vector3 a, out Vector3 b)
        {
            Measure();
            if (_gripR != null && _gripL != null)
            {
                a = transform.InverseTransformPoint(_gripR.position);
                b = transform.InverseTransformPoint(_gripL.position);
                return;
            }
            Vector3 axis = LongAxis();
            float quarter = Along(axis) * 0.5f;
            a = _local.center + axis * quarter;
            b = _local.center - axis * quarter;
        }

        /// <summary>The thing's thinnest side in its own space (a plank lies on it), as a unit axis.</summary>
        public Vector3 FlatAxis()
        {
            Measure();
            Vector3 s = _local.size;
            if (s.x <= s.y && s.x <= s.z) return Vector3.right;
            return s.y <= s.z ? Vector3.up : Vector3.forward;
        }

        internal void SetCarried(bool carried)
        {
            if (IsCarried == carried) return;
            IsCarried = carried;
            if (carried) onPickedUp?.Invoke(); else onPutDown?.Invoke();
        }

        internal void Use() => Used?.Invoke();

        private Vector3 LongAxis()
        {
            Vector3 s = _local.size;
            if (s.x >= s.y && s.x >= s.z) return Vector3.right;
            return s.y >= s.z ? Vector3.up : Vector3.forward;
        }

        /// <summary>Half the box's size along a unit axis of its own space.</summary>
        private float Along(Vector3 axis) => Mathf.Abs(Vector3.Dot(_local.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));

        /// <summary>The ± axis of the thing's own space closest to <paramref name="v"/>.</summary>
        private static Vector3 Dominant(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            return a.y >= a.z ? new Vector3(0f, Mathf.Sign(v.y), 0f) : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        /// <summary>Reads the box from the meshes (in the thing's own space, so it never changes) and finds Grip children.</summary>
        private void Measure()
        {
            if (_measured) return;
            _measured = true;
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            bool any = false;
            Bounds box = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; } else box.Encapsulate(p);
                }
            }
            _local = box;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (_grip == null && t.name == "Grip") _grip = t;
                else if (_gripR == null && t.name == "GripR") _gripR = t;
                else if (_gripL == null && t.name == "GripL") _gripL = t;
            }
        }
    }
}
