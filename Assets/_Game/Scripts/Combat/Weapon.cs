using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Something a WeaponHolder carries and fires with Attack. The weapon's root is the grip: the holder puts it in the right
    /// hand (third person) or in front of the camera (first person), and the weapon adds its own motion on top (a swing, a
    /// recoil kick) through PoseOffset. Sizes are authored for a holder at scale 1 (the toy); the holder scales it.
    /// When dropped it becomes a loose physics prop that can be taken again with E.
    /// </summary>
    public abstract class Weapon : MonoBehaviour
    {
        [SerializeField] private string displayName = "Weapon";
        [Header("Hold")]
        [SerializeField, Tooltip("Extra turn of the weapon in the hand, on top of pointing along the aim.")] private Vector3 handEuler;
        [SerializeField, Tooltip("Slide from the palm, in the weapon's own axes, units at scale 1.")] private Vector3 handOffset;
        [SerializeField, Tooltip("First person: where the grip sits in camera space, units at scale 1.")] private Vector3 viewOffset = new Vector3(0.22f, -0.22f, 0.45f);
        [SerializeField, Tooltip("First person: extra turn in camera space.")] private Vector3 viewEuler;
        [SerializeField, Tooltip("Blasters point where the camera looks; melee weapons follow the body.")] private bool aimsWithView = true;

        public string DisplayName => displayName;
        public WeaponHolder Holder { get; private set; }
        public bool AimsWithView => aimsWithView;
        public Quaternion HandRotation => Quaternion.Euler(handEuler);
        public Vector3 HandOffset => handOffset;
        public Vector3 ViewOffset => viewOffset;
        public Quaternion ViewRotation => Quaternion.Euler(viewEuler);
        /// <summary>The weapon's own motion this frame (swing, recoil), applied in the hold frame.</summary>
        public virtual Quaternion PoseRotation => Quaternion.identity;
        /// <summary>The weapon's own slide this frame, hold-frame units at scale 1.</summary>
        public virtual Vector3 PosePosition => Vector3.zero;
        /// <summary>True while the weapon is mid-action and should not be swapped out.</summary>
        public virtual bool Busy => false;

        /// <summary>Attack was pressed.</summary>
        public abstract void TriggerPressed();
        /// <summary>Attack is held this frame (automatic fire).</summary>
        public virtual void TriggerHeld() { }
        /// <summary>Put away or dropped mid-action: stop cleanly.</summary>
        public virtual void Cancel() { }

        internal void SetHolder(WeaponHolder holder)
        {
            if (Holder == holder) return;
            if (holder == null) Cancel();
            Holder = holder;
        }

        protected float Scale => Holder != null ? Holder.Scale : Mathf.Max(0.001f, transform.lossyScale.y);
    }
}
