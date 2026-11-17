using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The arms holding a gun, after the test room's AimPose. Given where the crosshair lands, it works out where the gun
    /// should be, then bends the arms so the hands are on it. It writes the bones after the Animator has, blended in by
    /// a weight, so at 0 the animation is untouched. The gun itself is not moved here: the holder keeps it fixed in the
    /// right hand.
    /// One-handed (a pistol): the right hand goes out along the aim and the barrel is turned onto the target.
    /// Two-handed (a long gun): both hands stay on the gun while it is out. Carried, it sits low in front of the chest,
    /// barrel down and across the body; aimed, its stock sits in the right shoulder and the barrel is on the target. The
    /// chest turns so the support shoulder comes forward; the head stays as it was.
    /// Not a component: the WeaponHolder owns one and calls it from its LateUpdate.
    /// </summary>
    public sealed class ArmAim
    {
        /// <summary>What is held, in the gun's own axes (z along the barrel, y its top, x its right), lengths in world units.</summary>
        public struct Hold
        {
            /// <summary>The gun in the hand: gun rotation = right hand rotation x this.</summary>
            public Quaternion grip;
            /// <summary>From the right wrist to the gun's root (the grip).</summary>
            public Vector3 seat;
            /// <summary>From the gun's root to the end of the barrel.</summary>
            public Vector3 muzzle;
            /// <summary>The weapon's own motion this frame (the recoil): a turn and a slide.</summary>
            public Quaternion kickTurn;
            public Vector3 kickSlide;
            /// <summary>From the point the carry and the shoulder positions are measured to (the test room's hand prop
            /// bone) to the gun's root.</summary>
            public Vector3 frame;
            /// <summary>A long gun: from the root back to the end of the stock.</summary>
            public float stock;
            /// <summary>A long gun: where the support hand holds, from the root.</summary>
            public Vector3 support;
        }

        /// <summary>One arm: its bones, each bone's own turn against "y along the bone, z along the elbow's hinge", and
        /// how its hand is built (which way the knuckles and the thumb lie from the wrist, in the hand's own space).</summary>
        private sealed class Arm
        {
            public Transform upper, fore, hand, knuckle;
            public Quaternion upperFix, foreFix;
            public Vector3 fingers = Vector3.up, thumb = Vector3.forward, palm = Vector3.right;
            public Quaternion upperWas, foreWas, handWas;
            public bool ok;

            public void Remember() { upperWas = upper.localRotation; foreWas = fore.localRotation; handWas = hand.localRotation; }
            public void Blend(float weight)
            {
                Quaternion upperNow = upper.localRotation, foreNow = fore.localRotation, handNow = hand.localRotation;
                upper.localRotation = Quaternion.Slerp(upperWas, upperNow, weight);
                fore.localRotation = Quaternion.Slerp(foreWas, foreNow, weight);
                hand.localRotation = Quaternion.Slerp(handWas, handNow, weight);
            }
        }

        private const float MostStretch = 0.985f;   // the furthest a wrist is put from its shoulder, as a share of the arm
        private const float PalmShare = 0.75f;      // the palm's middle: this far from the wrist to the middle knuckle

        private readonly Transform _body, _hips, _chest, _neck;
        private readonly Arm _right = new Arm(), _left = new Arm();

        /// <summary>False when the body has no humanoid right arm: nothing is posed.</summary>
        public bool Ready => _right.ok;
        /// <summary>True when the body can hold a gun in both hands (a left arm and a chest bone as well).</summary>
        public bool BothHands => _right.ok && _left.ok && _chest != null;
        /// <summary>A direction or a point in the right hand's own space, from its parts along the hand's own
        /// directions: x along the knuckles (from the wrist), y towards the thumb's side, z the way the palm faces.</summary>
        public Vector3 InHand(Vector3 knucklesThumbPalm) => _right.fingers * knucklesThumbPalm.x + _right.thumb * knucklesThumbPalm.y + _right.palm * knucklesThumbPalm.z;

        /// <summary>How a gun sits in the right hand (gun rotation = hand rotation x this), from where its barrel and its
        /// top point in the hand's own directions (knuckles, thumb, palm).</summary>
        public Quaternion Grip(Vector3 barrel, Vector3 top)
        {
            Vector3 along = InHand(barrel), up = InHand(top);
            if (along.sqrMagnitude < 1e-8f) along = _right.fingers;
            if (up.sqrMagnitude < 1e-8f) up = _right.thumb;
            return Quaternion.LookRotation(along.normalized, up.normalized);
        }
        public Transform Hand => _right.hand;
        public Transform Shoulder => _right.upper;

        /// <summary>Reads the arms as they stand (call before the Animator has posed them: the rest pose tells which way
        /// each elbow folds).</summary>
        public ArmAim(Animator animator, Transform body)
        {
            _body = body;
            if (animator == null || !animator.isHuman) return;
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_chest == null) _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_chest == null) _chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (_neck == null) _neck = animator.GetBoneTransform(HumanBodyBones.Head);
            Measure(_right, animator, body, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightThumbProximal);
            Measure(_left, animator, body, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftThumbProximal);
            // The way each palm faces: fingers x thumb on a right hand, the other way round on a left.
            _right.palm = Vector3.Cross(_right.fingers, _right.thumb).normalized;
            _left.palm = Vector3.Cross(_left.thumb, _left.fingers).normalized;
        }

        private static void Measure(Arm arm, Animator animator, Transform body, HumanBodyBones upper, HumanBodyBones fore, HumanBodyBones hand, HumanBodyBones knuckle, HumanBodyBones thumb)
        {
            arm.upper = animator.GetBoneTransform(upper); arm.fore = animator.GetBoneTransform(fore); arm.hand = animator.GetBoneTransform(hand);
            if (arm.upper == null || arm.fore == null || arm.hand == null) return;
            Vector3 upperLine = arm.fore.position - arm.upper.position, foreLine = arm.hand.position - arm.fore.position;
            if (upperLine.sqrMagnitude < 1e-10f || foreLine.sqrMagnitude < 1e-10f) return;
            // The slight bend of the rest pose is the elbow's hinge. A straight arm has none: an elbow folds forward.
            Vector3 hinge = Vector3.Cross(upperLine, foreLine);
            if (Vector3.Angle(upperLine, foreLine) < 3f) hinge = Vector3.Cross(upperLine, body.forward);
            if (hinge.sqrMagnitude < 1e-12f) hinge = -body.up;
            arm.upperFix = Quaternion.Inverse(Frame(upperLine, hinge)) * arm.upper.rotation;
            arm.foreFix = Quaternion.Inverse(Frame(foreLine, hinge)) * arm.fore.rotation;

            // The hand: which way the knuckles point from the wrist, and which side the thumb is on.
            arm.knuckle = animator.GetBoneTransform(knuckle);
            Transform thumbBone = animator.GetBoneTransform(thumb);
            Quaternion fromWorld = Quaternion.Inverse(arm.hand.rotation);
            Vector3 along = arm.knuckle != null ? fromWorld * (arm.knuckle.position - arm.hand.position) : fromWorld * foreLine;
            Vector3 top = thumbBone != null ? fromWorld * (thumbBone.position - arm.hand.position) : fromWorld * body.forward;
            along.Normalize();
            top -= along * Vector3.Dot(top, along);
            if (top.sqrMagnitude < 1e-10f) top = fromWorld * body.up - along * Vector3.Dot(fromWorld * body.up, along);
            arm.fingers = along; arm.thumb = top.normalized;
            arm.ok = true;
        }

        /// <summary>One-handed: bends the right arm so the gun in the hand points at <paramref name="target"/>, as far as
        /// <paramref name="weight"/> (0 leaves the animation, 1 is the full aim).</summary>
        public void Aim(Vector3 target, float weight, in Hold hold, WeaponHoldSettings s, float scale)
        {
            if (!Ready || weight <= 0f) return;
            _right.Remember();

            Vector3 shoulder = _right.upper.position;
            float length = Vector3.Distance(_right.fore.position, shoulder) + Vector3.Distance(_right.hand.position, _right.fore.position);
            Vector3 direction = _body.forward;
            if ((target - shoulder).sqrMagnitude > 1e-4f * scale * scale) direction = Limit((target - shoulder).normalized, s);
            Vector3 wrist = ClearOfBody(shoulder + direction * (length * s.reach), direction, s, scale);

            // The gun: put on the wrist and turned until the barrel itself is on the target.
            Quaternion gun = BarrelOn(wrist, hold.seat, hold.muzzle, target, direction, s);
            // The kick: the gun jumps back along its barrel and the barrel flips, the hand with it.
            wrist += gun * hold.kickSlide;
            gun *= hold.kickTurn;

            Bend(_right, wrist, gun * Quaternion.Inverse(hold.grip), s.elbowDirection);
            if (weight < 1f) _right.Blend(weight);
        }

        /// <summary>Two-handed: both hands on a long gun. <paramref name="aim"/> moves it from the carry (0) up to the
        /// shoulder and onto <paramref name="target"/> (1); <paramref name="weight"/> brings the hands onto the gun (0
        /// leaves the animation).</summary>
        public void Shoulder2(Vector3 target, float aim, float weight, in Hold hold, WeaponHoldSettings s, float scale)
        {
            if (!BothHands || weight <= 0f) return;
            _right.Remember(); _left.Remember();
            Quaternion chestWas = _chest.localRotation, neckWas = _neck != null ? _neck.localRotation : Quaternion.identity;

            // The chest turns so the support shoulder comes forward; the head is put back as it was.
            Quaternion neckFaces = _neck != null ? _neck.rotation : Quaternion.identity;
            _chest.rotation = Quaternion.AngleAxis(Mathf.Lerp(s.carryChestTurn, s.aimChestTurn, aim), _body.up) * _chest.rotation;
            if (_neck != null) _neck.rotation = neckFaces;

            // Aimed: the stock sits in the right shoulder and the barrel is on the target.
            Vector3 shoulder = _right.upper.position;
            Vector3 direction = _body.forward;
            if ((target - shoulder).sqrMagnitude > 1e-4f * scale * scale) direction = Limit((target - shoulder).normalized, s);
            // Everything below places the gun's reference point (hold.frame back from its root): the carry and the
            // shoulder pocket are measured to it, as in the test room.
            Vector3 muzzle = hold.frame + hold.muzzle;
            Vector3 pocket = s.shoulderPocket * scale + new Vector3(0f, 0f, hold.stock - hold.frame.z);
            Quaternion aimedTurn = BarrelOn(shoulder, pocket, muzzle, target, direction, s);
            Vector3 aimedAt = shoulder + aimedTurn * pocket;
            // Pointed steeply down, the grip would swing back into the belly: the whole gun slides forward instead (the
            // stock leaves the shoulder) and is lined up again from there. A few rounds settle it.
            Vector3 anchor = shoulder;
            for (int pass = 0; pass < 3; pass++)
            {
                Vector3 grip = ClearOfBody(aimedAt, aimedTurn * Vector3.forward, s, scale);
                if ((grip - aimedAt).sqrMagnitude < 1e-10f) break;
                anchor += grip - aimedAt;
                aimedTurn = BarrelOn(anchor, pocket, muzzle, target, aimedTurn * Vector3.forward, s);
                aimedAt = anchor + aimedTurn * pocket;
            }

            // Carried: low in front of the chest, barrel down and across the body.
            Quaternion carriedTurn = Quaternion.LookRotation(_body.rotation * (Quaternion.Euler(s.carryDownDegrees, -s.carryLeftDegrees, 0f) * Vector3.forward), Vector3.up);
            Vector3 carriedAt = _chest.position + _body.rotation * (s.carryPosition * scale);

            Quaternion gun = Quaternion.Slerp(carriedTurn, aimedTurn, aim);
            Vector3 at = Vector3.Lerp(carriedAt, aimedAt, aim);
            at += gun * hold.kickSlide;
            gun *= hold.kickTurn;
            at += gun * hold.frame;   // from the reference point to the gun's root, where the hands are measured from

            // The right hand on the grip.
            Bend(_right, at - gun * hold.seat, gun * Quaternion.Inverse(hold.grip), s.longElbow);

            // The support hand: under and beside the barrel, palm to the gun, fingers along it.
            float roll = s.palmRollDegrees * Mathf.Deg2Rad;
            Vector3 barrel = gun * Vector3.forward;
            Vector3 palmWay = (gun * Vector3.up * Mathf.Cos(roll) + gun * Vector3.right * Mathf.Sin(roll)).normalized;
            float palmLength = _left.knuckle != null ? Vector3.Distance(_left.knuckle.position, _left.hand.position) * PalmShare : 0.07f * scale;
            float palmDepth = s.palmDepth * scale;
            Vector3 palm = at + gun * hold.support;
            Vector3 reachFrom = _left.upper.position;
            Vector3 fingers = AcrossPalm((palm - reachFrom).normalized + barrel * s.fingersAlongBarrel, palmWay);
            Vector3 wrist = palm - fingers * palmLength - palmWay * palmDepth;
            // Out of reach, the hand would hang in the air short of the gun: the hold slides back along the barrel. Only
            // as the gun comes up to aim: the carry keeps the hold it has.
            float armLength = Vector3.Distance(_left.fore.position, reachFrom) + Vector3.Distance(_left.hand.position, _left.fore.position);
            float back = SlideToReach(wrist - reachFrom, barrel, hold.support.z - s.closestSupport * scale, armLength * MostStretch) * aim;
            if (back > 0f)
            {
                palm -= barrel * back;
                fingers = AcrossPalm((palm - reachFrom).normalized + barrel * s.fingersAlongBarrel, palmWay);
                wrist = palm - fingers * palmLength - palmWay * palmDepth;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                // Bend the arm, see which way the forearm ends up pointing, and line the hand up with it.
                Bend(_left, wrist, LeftHandTurn(fingers, palmWay), s.supportElbow);
                fingers = AcrossPalm((wrist - _left.fore.position).normalized + barrel * s.fingersAlongBarrel, palmWay);
                wrist = palm - fingers * palmLength - palmWay * palmDepth;
            }
            Bend(_left, wrist, LeftHandTurn(fingers, palmWay), s.supportElbow);

            if (weight < 1f)
            {
                // Parents before children: the chest, the head, then each arm.
                Quaternion chestNow = _chest.localRotation;
                _chest.localRotation = Quaternion.Slerp(chestWas, chestNow, weight);
                if (_neck != null) { Quaternion neckNow = _neck.localRotation; _neck.localRotation = Quaternion.Slerp(neckWas, neckNow, weight); }
                _right.Blend(weight); _left.Blend(weight);
            }
        }

        /// <summary>The middle of the support hand's palm, in the world, as the bones stand now.</summary>
        public Vector3 SupportPalm => _left.ok && _left.knuckle != null ? Vector3.Lerp(_left.hand.position, _left.knuckle.position, PalmShare) : _left.ok ? _left.hand.position : Vector3.zero;

        /// <summary>The left hand's turn with its fingers along <paramref name="fingers"/> and its palm facing
        /// <paramref name="palmWay"/>. On a left hand the thumb is on the side fingers x palm.</summary>
        private Quaternion LeftHandTurn(Vector3 fingers, Vector3 palmWay)
        {
            Vector3 thumb = Vector3.Cross(fingers, palmWay);
            return Quaternion.LookRotation(fingers, thumb) * Quaternion.Inverse(Quaternion.LookRotation(_left.fingers, _left.thumb));
        }

        /// <summary>Bends one arm so its wrist reaches <paramref name="wrist"/>, then turns the hand. The upper arm and
        /// the forearm make a triangle with the line from shoulder to wrist; the elbow points along
        /// <paramref name="elbowWay"/> (the body's own space).</summary>
        private void Bend(Arm arm, Vector3 wrist, Quaternion handTurn, Vector3 elbowWay)
        {
            Vector3 shoulder = arm.upper.position;
            float upperLength = Vector3.Distance(arm.fore.position, shoulder), foreLength = Vector3.Distance(arm.hand.position, arm.fore.position);
            Vector3 line = wrist - shoulder;
            float distance = Mathf.Clamp(line.magnitude, 0.05f * (upperLength + foreLength), (upperLength + foreLength) * MostStretch);
            Vector3 way = line.sqrMagnitude > 1e-10f ? line.normalized : _body.forward;
            wrist = shoulder + way * distance;
            float along = (upperLength * upperLength - foreLength * foreLength + distance * distance) / (2f * distance);
            float out_ = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0f));
            Vector3 pole = _body.rotation * elbowWay;
            pole -= way * Vector3.Dot(pole, way);
            pole = pole.sqrMagnitude > 1e-8f ? pole.normalized : -_body.up;
            Vector3 elbow = shoulder + way * along + pole * out_;
            Vector3 hinge = Vector3.Cross(elbow - shoulder, wrist - elbow);
            if (hinge.sqrMagnitude < 1e-12f) hinge = Vector3.Cross(way, pole);
            arm.upper.rotation = Frame(elbow - shoulder, hinge) * arm.upperFix;
            arm.fore.rotation = Frame(wrist - elbow, hinge) * arm.foreFix;
            arm.hand.rotation = handTurn;
        }

        /// <summary>Puts the gun on <paramref name="anchor"/> (the wrist, or the stock's place at the shoulder) and turns
        /// it until the barrel itself is on the target. <paramref name="seat"/> is where the gun's root sits from the
        /// anchor, in the gun's axes; <paramref name="direction"/> is the way from the shoulder to the target. The barrel
        /// runs beside that line, so held parallel to it the gun would point past anything close; closer than a few
        /// times that distance the correction fades out, so something touching the gun cannot swing it round.</summary>
        private Quaternion BarrelOn(Vector3 anchor, Vector3 seat, Vector3 muzzle, Vector3 target, Vector3 direction, WeaponHoldSettings s)
        {
            Vector3 beside = seat + muzzle; beside.z = 0f;
            float near = beside.magnitude * s.nearestTrueAim;
            float trust = near > 1e-6f ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(near * 0.5f, near, Vector3.Distance(target, anchor))) : 1f;
            if (trust > 0f)
            {
                Vector3 trueWay = direction;
                for (int pass = 0; pass < 3; pass++)
                {
                    Vector3 from = anchor + Quaternion.LookRotation(trueWay, Vector3.up) * beside;
                    if ((target - from).sqrMagnitude < 1e-10f) break;
                    trueWay = Limit((target - from).normalized, s);
                }
                direction = Vector3.Slerp(direction, trueWay, trust).normalized;
            }
            return Quaternion.LookRotation(direction, Vector3.up);
        }

        /// <summary>How far back along the barrel the support hand's hold has to slide for the wrist to be within the
        /// arm's reach: 0 if it already is, at most <paramref name="room"/>.</summary>
        private static float SlideToReach(Vector3 fromShoulder, Vector3 barrel, float room, float longest)
        {
            if (fromShoulder.magnitude <= longest || room <= 0f) return 0f;
            // Slid back by t, the wrist is |fromShoulder - t * barrel| from the shoulder: the first t within reach, or
            // the nearest it ever gets.
            float along = Vector3.Dot(fromShoulder, barrel);
            float inside = along * along - fromShoulder.sqrMagnitude + longest * longest;
            return Mathf.Clamp(inside > 0f ? along - Mathf.Sqrt(inside) : along, 0f, room);
        }

        /// <summary>The part of <paramref name="way"/> that lies flat in the palm: the direction the fingers point.</summary>
        private static Vector3 AcrossPalm(Vector3 way, Vector3 palmWay)
        {
            Vector3 flat = way - palmWay * Vector3.Dot(way, palmWay);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.Cross(palmWay, Vector3.up).normalized;
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
