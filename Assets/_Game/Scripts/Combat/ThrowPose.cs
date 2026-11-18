using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Swings the free (left) arm forward for a throw, after the test room's ThrowPose. It only moves that arm, and it
    /// fades in and out, so it sits on top of whatever the body and the other arm are doing (a gun held in both hands:
    /// the free hand leaves it for the throw and comes back). The arm is bent by the same code that holds a gun
    /// (ArmAim). Not a component: the GrenadeThrower owns one, sets how far the swing is, and applies it after the
    /// Animator and the WeaponHolder have posed the body. Numbers in Resources/Grenades > The arm.
    /// </summary>
    public sealed class ThrowPose
    {
        private readonly ArmAim _arms;

        /// <summary>0 the hand is wound up behind the shoulder, 1 it is thrown forward.</summary>
        public float Swing { get; private set; }
        /// <summary>How much of the arm is the throw's: 0 the animation's, 1 all the throw's.</summary>
        public float Influence { get; private set; }
        /// <summary>False when the body has no humanoid left arm: nothing is posed (the grenade is thrown all the same).</summary>
        public bool Ready => _arms.FreeArm;

        /// <summary>Make it before the Animator has posed the body: the rest pose tells which way the elbow folds.</summary>
        public ThrowPose(Animator animator, Transform body) { _arms = new ArmAim(animator, body); }

        /// <summary>Sets the pose for a swing that is <paramref name="done"/> (0 to 1) of the way through: up quickly,
        /// forward through the release, then the arm is let go.</summary>
        public void Set(float done, GrenadeSettings s)
        {
            Swing = Smooth(s.swingShares.x, s.releaseAt + s.swingShares.y, done);
            Influence = Smooth(0f, s.armInBy, done) * (1f - Smooth(s.armOutFrom, 1f, done));
        }

        /// <summary>No throw under way: the arm is the animation's.</summary>
        public void Rest() { Swing = 0f; Influence = 0f; }

        /// <summary>Bends the arm for this frame. Call from LateUpdate, after everything else that poses the body.</summary>
        public void Apply(GrenadeSettings s, float scale)
        {
            if (Influence <= 0f || !Ready) return;
            _arms.FreeHandTo(LeftIsMinusX(Vector3.Lerp(s.windUp, s.release, Swing)) * scale, LeftIsMinusX(s.elbowWay), Influence);
        }

        // The settings count x towards the free-hand side; in the body's own space left is -x.
        private static Vector3 LeftIsMinusX(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        private static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));
    }
}
