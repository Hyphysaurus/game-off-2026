using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The right arm holding a gun, after the test room's AimPose (one-handed). Given where the crosshair lands, it works
    /// out where the gun should be (the hand out along the aim, the barrel on the target), then bends the upper arm and
    /// the forearm so the hand is there. It writes the three bones after the Animator has, blended in by a weight, so at
    /// 0 the animation is untouched. The gun itself is not moved here: the holder keeps it fixed in the hand.
    /// Not a component: the WeaponHolder owns one and calls it from its LateUpdate.
    /// </summary>
    public sealed class ArmAim
    {
        /// <summary>What is held, in the gun's own axes (z along the barrel, y its top), lengths in world units.</summary>
        public struct Hold
        {
            /// <summary>The gun in the hand: gun rotation = hand rotation x this.</summary>
            public Quaternion grip;
            /// <summary>From the wrist to the gun's root (the grip), in the gun's axes.</summary>
            public Vector3 seat;
            /// <summary>From the gun's root to the end of the barrel, in the gun's axes.</summary>
            public Vector3 muzzle;
            /// <summary>The weapon's own motion this frame (the recoil): a turn and a slide in the gun's axes.</summary>
            public Quaternion kickTurn;
            public Vector3 kickSlide;
        }

        private const float MostStretch = 0.985f;   // the furthest a wrist is put from its shoulder, as a share of the arm

        private readonly Transform _body, _upper, _fore, _hand, _hips;
        private readonly Quaternion _upperFix, _foreFix;   // each bone's own turn against "y along the bone, z along the elbow's hinge"

        /// <summary>False when the body has no humanoid right arm: nothing is posed.</summary>
        public bool Ready { get; }
        /// <summary>The hold worked out from the hand's own bones: barrel along the knuckles, top towards the thumb.</summary>
        public Quaternion NaturalGrip { get; }
        public Transform Hand => _hand;
        public Transform Shoulder => _upper;

        /// <summary>Reads the arm as it stands (call before the Animator has posed it: the rest pose tells which way the
        /// elbow folds).</summary>
        public ArmAim(Animator animator, Transform body)
        {
            _body = body;
            NaturalGrip = Quaternion.identity;
            if (animator == null || !animator.isHuman) return;
            _upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _fore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (_upper == null || _fore == null || _hand == null) return;

            Vector3 upperLine = _fore.position - _upper.position, foreLine = _hand.position - _fore.position;
            if (upperLine.sqrMagnitude < 1e-10f || foreLine.sqrMagnitude < 1e-10f) return;
            // The slight bend of the rest pose is the elbow's hinge. A straight arm has none: a right elbow folds forward.
            Vector3 hinge = Vector3.Cross(upperLine, foreLine);
            if (Vector3.Angle(upperLine, foreLine) < 3f) hinge = Vector3.Cross(upperLine, body.forward);
            if (hinge.sqrMagnitude < 1e-12f) hinge = -body.up;
            _upperFix = Quaternion.Inverse(Frame(upperLine, hinge)) * _upper.rotation;
            _foreFix = Quaternion.Inverse(Frame(foreLine, hinge)) * _fore.rotation;

            // The fist: the barrel runs the way the knuckles point from the wrist, the gun's top is on the thumb's side.
            Transform knuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform thumb = animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            Quaternion fromWorld = Quaternion.Inverse(_hand.rotation);
            Vector3 along = knuckle != null ? fromWorld * (knuckle.position - _hand.position) : fromWorld * foreLine;
            Vector3 top = thumb != null ? fromWorld * (thumb.position - _hand.position) : fromWorld * body.forward;
            along.Normalize();
            top -= along * Vector3.Dot(top, along);
            if (top.sqrMagnitude < 1e-10f) top = fromWorld * body.up - along * Vector3.Dot(fromWorld * body.up, along);
            NaturalGrip = Quaternion.LookRotation(along, top.normalized);
            Ready = true;
        }

        /// <summary>Bends the arm so the gun in the hand points at <paramref name="target"/>, as far as
        /// <paramref name="weight"/> (0 leaves the animation, 1 is the full aim).</summary>
        public void Aim(Vector3 target, float weight, in Hold hold, WeaponHoldSettings s, float scale)
        {
            if (!Ready || weight <= 0f) return;
            Quaternion upperWas = _upper.localRotation, foreWas = _fore.localRotation, handWas = _hand.localRotation;

            Vector3 shoulder = _upper.position;
            float upperLength = Vector3.Distance(_fore.position, shoulder), foreLength = Vector3.Distance(_hand.position, _fore.position);
            Vector3 direction = _body.forward;
            if ((target - shoulder).sqrMagnitude > 1e-4f * scale * scale) direction = Limit((target - shoulder).normalized, s);
            Vector3 wrist = ClearOfBody(shoulder + direction * ((upperLength + foreLength) * s.reach), direction, s, scale);

            // The gun: put on the wrist and turned until the barrel itself is on the target. The barrel runs beside the
            // line from the shoulder, so held parallel to it the gun would point past anything close.
            Vector3 beside = hold.seat + hold.muzzle; beside.z = 0f;
            float near = beside.magnitude * s.nearestTrueAim;
            float trust = near > 1e-6f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(near * 0.5f, near, Vector3.Distance(target, wrist))) : 1f;
            if (trust > 0f)
            {
                Vector3 trueWay = direction;
                for (int pass = 0; pass < 3; pass++)
                {
                    Vector3 from = wrist + Quaternion.LookRotation(trueWay, Vector3.up) * beside;
                    if ((target - from).sqrMagnitude < 1e-10f) break;
                    trueWay = Limit((target - from).normalized, s);
                }
                direction = Vector3.Slerp(direction, trueWay, trust).normalized;
            }
            Quaternion gun = Quaternion.LookRotation(direction, Vector3.up);
            // The kick: the gun jumps back along its barrel and the barrel flips, the hand with it.
            wrist += gun * hold.kickSlide;
            gun *= hold.kickTurn;

            // The arm: the upper arm and the forearm make a triangle with the line from shoulder to wrist.
            Vector3 line = wrist - shoulder;
            float distance = Mathf.Clamp(line.magnitude, 0.05f * (upperLength + foreLength), (upperLength + foreLength) * MostStretch);
            Vector3 way = line.sqrMagnitude > 1e-10f ? line.normalized : direction;
            wrist = shoulder + way * distance;
            float along = (upperLength * upperLength - foreLength * foreLength + distance * distance) / (2f * distance);
            float out_ = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0f));
            Vector3 pole = _body.rotation * s.elbowDirection;
            pole -= way * Vector3.Dot(pole, way);
            pole = pole.sqrMagnitude > 1e-8f ? pole.normalized : -_body.up;
            Vector3 elbow = shoulder + way * along + pole * out_;
            Vector3 hinge = Vector3.Cross(elbow - shoulder, wrist - elbow);
            if (hinge.sqrMagnitude < 1e-12f) hinge = Vector3.Cross(way, pole);

            _upper.rotation = Frame(elbow - shoulder, hinge) * _upperFix;
            _fore.rotation = Frame(wrist - elbow, hinge) * _foreFix;
            _hand.rotation = gun * Quaternion.Inverse(hold.grip);

            if (weight < 1f)
            {
                Quaternion upperAimed = _upper.localRotation, foreAimed = _fore.localRotation, handAimed = _hand.localRotation;
                _upper.localRotation = Quaternion.Slerp(upperWas, upperAimed, weight);
                _fore.localRotation = Quaternion.Slerp(foreWas, foreAimed, weight);
                _hand.localRotation = Quaternion.Slerp(handWas, handAimed, weight);
            }
        }

        /// <summary>Keeps the aim in front of the body and within reach of the shoulder.</summary>
        private Vector3 Limit(Vector3 direction, WeaponHoldSettings s)
        {
            Vector3 local = Quaternion.Inverse(_body.rotation) * direction;
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -s.maxPitchDown, s.maxPitchUp) * Mathf.Deg2Rad;
            float turn = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -s.maxTurn, s.maxTurn) * Mathf.Deg2Rad;
            return _body.rotation * new Vector3(Mathf.Sin(turn) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(turn) * Mathf.Cos(pitch));
        }

        /// <summary>Keeps the gun hand out of the body. Inside the body's oval (seen from above, round the hips), the
        /// point slides out along the way the gun points: a gun pointed steeply down sits in front of the belly.</summary>
        private Vector3 ClearOfBody(Vector3 point, Vector3 way, WeaponHoldSettings s, float scale)
        {
            float width = s.bodyHalfWidth * scale, depth = s.bodyHalfDepth * scale;
            if (width <= 0f || depth <= 0f) return point;
            Vector3 middle = _hips != null ? _hips.position : _body.position;
            Vector3 local = Quaternion.Inverse(_body.rotation) * (point - middle);
            var at = new Vector2(local.x / width, local.z / depth);
            if (at.sqrMagnitude >= 1f) return point;
            Vector3 wayLocal = Quaternion.Inverse(_body.rotation) * way;
            var along = new Vector2(wayLocal.x, wayLocal.z);
            along = along.sqrMagnitude > 1e-4f ? along.normalized : Vector2.up;
            // The step t along `along` that lands on the oval's edge: |at + t * step| = 1, t > 0.
            var step = new Vector2(along.x / width, along.y / depth);
            float a = step.sqrMagnitude, b = 2f * Vector2.Dot(at, step), c = at.sqrMagnitude - 1f;
            float t = (-b + Mathf.Sqrt(Mathf.Max(b * b - 4f * a * c, 0f))) / (2f * a);
            return point + _body.rotation * new Vector3(along.x, 0f, along.y) * t;
        }

        /// <summary>A turn with y along a bone and z along the elbow's hinge.</summary>
        private static Quaternion Frame(Vector3 along, Vector3 hinge)
        {
            Vector3 y = along.normalized;
            Vector3 z = hinge - y * Vector3.Dot(hinge, y);
            if (z.sqrMagnitude < 1e-12f) z = Vector3.Cross(y, Mathf.Abs(y.y) < 0.9f ? Vector3.up : Vector3.right);
            return Quaternion.LookRotation(z.normalized, y);
        }
    }
}
