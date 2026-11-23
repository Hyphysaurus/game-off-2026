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
    /// Reloading (Hold.reload above 0): the gun comes in front of the chest, barrel tipped up and the magazine rolled
    /// round towards the free hand, which goes to the magazine and follows it down and back up.
    /// First person (TurnChest, OutOfReach, OnGun): the gun is where the view has it and the arms are bent to it: the
    /// right hand on the grip, the left under a long gun's barrel, or on the magazine during a reload.
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
            /// <summary>A long gun: degrees the support palm sits round the barrel towards the support side (0 flat
            /// underneath, 90 flat against the gun's side). The gun's own, else the shared number.</summary>
            public float palmRoll;
            /// <summary>0 the normal hold, 1 the gun tipped in front of the chest for a reload.</summary>
            public float reload;
            /// <summary>Where the free hand takes the magazine: its bottom, from the root, as far out of the gun as it is now.</summary>
            public Vector3 magazine;
            /// <summary>How far the magazine has come down out of the gun.</summary>
            public float magazineOut;
            /// <summary>Keep the hand within a real wrist's roll and bend (a tool held in the fist): it then sits a little
            /// off the aim rather than folding over at the wrist.</summary>
            public bool limitWrist;
        }

        /// <summary>One arm: its bones, each bone's own turn against "y along the bone, z along the elbow's hinge", and
        /// how its hand is built (which way the knuckles and the thumb lie from the wrist, in the hand's own space).</summary>
        private sealed class Arm
        {
            public Transform upper, fore, hand, knuckle;
            public Quaternion upperFix, foreFix;
            /// <summary>How the hand sits on the forearm at rest (hand rotation = forearm rotation x this).</summary>
            public Quaternion handRest = Quaternion.identity;
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

        private readonly Transform _body, _hips, _chest, _neck, _head;
        private readonly Arm _right = new Arm(), _left = new Arm();
        private readonly Transform[] _spine;   // spine, chest, upper chest: the waist twist is shared over them (D-0141)
        private float _twistNow;   // the waist twist, eased, degrees (+ to the right)
        private float _twist;      // the twist the next Aim measures its limits from (set by TwistTorso, cleared by Aim)

        /// <summary>The chest's frame: the body's, turned by the waist twist this frame (D-0141). The one-handed limits and
        /// the chest check are measured in it, so a twist adds to the arm's own reach.</summary>
        private Quaternion Torso => _twist == 0f ? _body.rotation : Quaternion.AngleAxis(_twist, _body.up) * _body.rotation;

        /// <summary>False when the body has no humanoid right arm: nothing is posed.</summary>
        public bool Ready => _right.ok;
        /// <summary>True when the body can hold a gun in both hands (a left arm and a chest bone as well).</summary>
        public bool BothHands => _right.ok && _left.ok && _chest != null;
        /// <summary>True when the body has a humanoid left arm: the free arm can be posed on its own (a throw).</summary>
        public bool FreeArm => _left.ok;
        /// <summary>A direction or a point in the right hand's own space, from its parts along the hand's own
        /// directions: x along the knuckles (from the wrist), y towards the thumb's side, z the way the palm faces.</summary>
        public Vector3 InHand(Vector3 knucklesThumbPalm) => InHand(knucklesThumbPalm, false);
        /// <summary>The same in either hand (D-0144): the left hand's knuckles, thumb and palm are its own, so a number
        /// tuned for the right hand means the same grip in the left.</summary>
        public Vector3 InHand(Vector3 knucklesThumbPalm, bool left)
        {
            Arm a = left ? _left : _right;
            return a.fingers * knucklesThumbPalm.x + a.thumb * knucklesThumbPalm.y + a.palm * knucklesThumbPalm.z;
        }
        /// <summary>True when that arm is humanoid and can be posed.</summary>
        public bool ReadyFor(bool left) => left ? _left.ok : _right.ok;
        /// <summary>That hand's bone.</summary>
        public Transform HandOf(bool left) => left ? _left.hand : _right.hand;

        /// <summary>How a gun sits in the right hand (gun rotation = hand rotation x this), from where its barrel and its
        /// top point in the hand's own directions (knuckles, thumb, palm).</summary>
        public Quaternion Grip(Vector3 barrel, Vector3 top) => Grip(barrel, top, false);
        /// <summary>The same for either hand (D-0144).</summary>
        public Quaternion Grip(Vector3 barrel, Vector3 top, bool left)
        {
            Arm a = left ? _left : _right;
            Vector3 along = InHand(barrel, left), up = InHand(top, left);
            if (along.sqrMagnitude < 1e-8f) along = a.fingers;
            if (up.sqrMagnitude < 1e-8f) up = a.thumb;
            return Quaternion.LookRotation(along.normalized, up.normalized);
        }
        public Transform Hand => _right.hand;
        public Transform Shoulder => _right.upper;

        /// <summary>Reads the arms as they stand (call before the Animator has posed them: the rest pose tells which way
        /// each elbow folds).</summary>
        public ArmAim(Animator animator, Transform body)
        {
            _body = body;
            _spine = new Transform[0];
            if (animator == null || !animator.isHuman) return;
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            _chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_chest == null) _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_chest == null) _chest = animator.GetBoneTransform(HumanBodyBones.Spine);
            _neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (_neck == null) _neck = animator.GetBoneTransform(HumanBodyBones.Head);
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            var spine = new System.Collections.Generic.List<Transform>();
            foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            {
                Transform b = animator.GetBoneTransform(bone);
                if (b != null && !spine.Contains(b)) spine.Add(b);
            }
            _spine = spine.ToArray();
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
            arm.handRest = Quaternion.Inverse(arm.fore.rotation) * arm.hand.rotation;

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

        /// <summary>One-handed: bends the right arm (or the left, D-0144: the flashlight in the left hand while a gun is in
        /// the right) so the thing in the hand points at <paramref name="target"/>, as far as <paramref name="weight"/>
        /// (0 leaves the animation, 1 is the full aim). The left arm's limits are the right's mirrored.</summary>
        public void Aim(Vector3 target, float weight, in Hold hold, WeaponHoldSettings s, float scale, bool left = false)
        {
            Arm arm = left ? _left : _right;
            if (!arm.ok || weight <= 0f) { _twist = 0f; return; }
            arm.Remember();
            Vector3 elbowWay = left ? Mirror(s.elbowDirection) : s.elbowDirection;

            Vector3 shoulder = arm.upper.position;
            float length = Vector3.Distance(arm.fore.position, shoulder) + Vector3.Distance(arm.hand.position, arm.fore.position);
            Vector3 direction = Torso * Vector3.forward;
            if ((target - shoulder).sqrMagnitude > 1e-4f * scale * scale) direction = Limit((target - shoulder).normalized, s, oneHand: true, leftArm: left);
            Vector3 wrist = ClearOfBody(shoulder + direction * (length * s.reach), direction, s, scale);
            // The elbow and forearm stay out of the chest, the fist out of the face: the hand goes further forward instead.
            wrist = ClearOfTorso(arm, wrist, elbowWay, s, scale);

            // The gun: put on the wrist and turned until the barrel itself is on the target.
            Quaternion gun = BarrelOn(wrist, hold.seat, hold.muzzle, target, direction, s, oneHand: true, left: left);
            // The kick: the gun jumps back along its barrel and the barrel flips, the hand with it.
            wrist += gun * hold.kickSlide;
            gun *= hold.kickTurn;

            bool freeHand = !left && hold.reload > 0f && _left.ok;   // a reload is the right hand's gun
            if (hold.reload > 0f)
            {
                // Reloading: the gun goes from wherever the aim has it to its place in front of the chest (both measured
                // at the gun's reference point, as the carry is), and the free hand to the magazine.
                Vector3 at = wrist + gun * (hold.seat - hold.frame);
                ReloadPlace(s, scale, out Quaternion tipped, out Vector3 place);
                gun = Quaternion.Slerp(gun, tipped, hold.reload);
                Vector3 root = Vector3.Lerp(at, place, hold.reload) + gun * hold.frame;
                wrist = root - gun * hold.seat;
                if (freeHand) { _left.Remember(); HandToMagazine(root, gun, hold, s, scale); }
            }

            if (hold.limitWrist) Bend(arm, wrist, gun * Quaternion.Inverse(hold.grip), elbowWay, s.forearmRoll, s);
            else Bend(arm, wrist, gun * Quaternion.Inverse(hold.grip), elbowWay);
            if (weight < 1f) { arm.Blend(weight); if (freeHand) _left.Blend(weight); }
            _twist = 0f;   // the twist is this frame's, for this aim only
        }

        /// <summary>Where a gun is held for a reload: in front of the chest, barrel tipped up, rolled so the magazine
        /// comes round towards the free (left) hand. The place is the gun's reference point (Hold.frame back from its root).</summary>
        private void ReloadPlace(WeaponHoldSettings s, float scale, out Quaternion gun, out Vector3 at)
        {
            gun = _body.rotation * Quaternion.AngleAxis(-s.reloadUpDegrees, Vector3.right) * Quaternion.AngleAxis(-s.reloadRollDegrees, Vector3.forward);
            Vector3 chest = _chest != null ? _chest.position : _right.upper.position;
            at = chest + _body.rotation * (s.reloadPosition * scale);
        }

        /// <summary>A one-handed gun's reload: the free hand travels from where the animation has it to the bottom of
        /// the magazine and back, as far as Hold.reload. Its palm faces the gun from below and beside it.</summary>
        private void HandToMagazine(Vector3 root, Quaternion gun, in Hold hold, WeaponHoldSettings s, float scale)
        {
            Vector3 palm = MagazinePalm(root, gun, hold, s, scale);
            Vector3 wrist = LeftWrist(palm, (palm - _left.upper.position).normalized, gun, s, scale, s.palmRollDegrees, out Vector3 fingers, out Vector3 palmWay);
            Vector3 freeAt = _left.hand.position; Quaternion freeTurn = _left.hand.rotation;   // the animation's, before we move it
            Bend(_left, Vector3.Lerp(freeAt, wrist, hold.reload), Quaternion.Slerp(freeTurn, LeftHandTurn(fingers, palmWay), hold.reload), s.supportElbow, s.forearmRoll * hold.reload, s);
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
            if (hold.reload > 0f)
            {
                // Reloading: the gun comes in front of the chest, tipped, and the support hand goes to the magazine.
                ReloadPlace(s, scale, out Quaternion tipped, out Vector3 place);
                gun = Quaternion.Slerp(gun, tipped, hold.reload);
                at = Vector3.Lerp(at, place, hold.reload);
            }
            at += gun * hold.frame;   // from the reference point to the gun's root, where the hands are measured from

            // The right hand on the grip.
            Bend(_right, at - gun * hold.seat, gun * Quaternion.Inverse(hold.grip), s.longElbow);

            // The support hand: under the barrel, or on its way to the magazine during a reload. Out of reach, its
            // hold slides back along the barrel, only as the gun comes up to aim: the carry keeps the hold it has.
            SupportHand(at, gun, hold, s, scale, aim);

            if (weight < 1f)
            {
                // Parents before children: the chest, the head, then each arm.
                Quaternion chestNow = _chest.localRotation;
                _chest.localRotation = Quaternion.Slerp(chestWas, chestNow, weight);
                if (_neck != null) { Quaternion neckNow = _neck.localRotation; _neck.localRotation = Quaternion.Slerp(neckWas, neckNow, weight); }
                _right.Blend(weight); _left.Blend(weight);
            }
        }

        /// <summary>A long gun's support hand: under and beside the barrel, palm to the gun, fingers along it; during a
        /// reload it goes to the magazine instead (Hold.reload). <paramref name="root"/> and <paramref name="gun"/>
        /// are where the gun is. Out of the arm's reach the hand would hang in the air short of the gun: the hold
        /// slides back along the barrel towards the grip, by <paramref name="slide"/> of what it takes (0 = never).</summary>
        private void SupportHand(Vector3 root, Quaternion gun, in Hold hold, WeaponHoldSettings s, float scale, float slide)
        {
            Vector3 barrel = gun * Vector3.forward;
            Vector3 palm = root + gun * hold.support;
            if (hold.reload > 0f) palm = Vector3.Lerp(palm, MagazinePalm(root, gun, hold, s, scale), hold.reload);
            Vector3 reachFrom = _left.upper.position;
            // Round the barrel as this gun is held; on the way to the magazine, as every magazine is taken.
            float roll = Mathf.Lerp(hold.palmRoll, s.palmRollDegrees, hold.reload);
            Vector3 wrist = LeftWrist(palm, (palm - reachFrom).normalized, gun, s, scale, roll, out Vector3 fingers, out Vector3 palmWay);
            float back = hold.reload > 0f ? 0f : SlideToReach(wrist - reachFrom, barrel, hold.support.z - s.closestSupport * scale, LeftArmLength * MostStretch) * slide;
            if (back > 0f)
            {
                palm -= barrel * back;
                wrist = LeftWrist(palm, (palm - reachFrom).normalized, gun, s, scale, roll, out fingers, out palmWay);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                // Bend the arm, see which way the forearm ends up pointing, and line the hand up with it.
                Bend(_left, wrist, LeftHandTurn(fingers, palmWay), s.supportElbow, s.forearmRoll, s);
                wrist = LeftWrist(palm, (wrist - _left.fore.position).normalized, gun, s, scale, roll, out fingers, out palmWay);
            }
            Bend(_left, wrist, LeftHandTurn(fingers, palmWay), s.supportElbow, s.forearmRoll, s);
        }

        /// <summary>The left hand holding the gun at <paramref name="palm"/> from below and beside it: where its wrist
        /// goes, which way its fingers point (along the barrel, and along <paramref name="reachWay"/>, the way the arm
        /// comes to it) and which way its palm faces (towards the gun, from <paramref name="rollDegrees"/> round the
        /// barrel to the support side: 0 from straight underneath).</summary>
        private Vector3 LeftWrist(Vector3 palm, Vector3 reachWay, Quaternion gun, WeaponHoldSettings s, float scale, float rollDegrees, out Vector3 fingers, out Vector3 palmWay)
        {
            float roll = rollDegrees * Mathf.Deg2Rad;
            palmWay = (gun * Vector3.up * Mathf.Cos(roll) + gun * Vector3.right * Mathf.Sin(roll)).normalized;
            fingers = AcrossPalm(reachWay + gun * Vector3.forward * s.fingersAlongBarrel, palmWay);
            float palmLength = _left.knuckle != null ? Vector3.Distance(_left.knuckle.position, _left.hand.position) * PalmShare : 0.07f * scale;
            return palm - fingers * palmLength - palmWay * (s.palmDepth * scale);
        }

        /// <summary>The left arm's length, shoulder to wrist, as the bones stand.</summary>
        private float LeftArmLength => Vector3.Distance(_left.fore.position, _left.upper.position) + Vector3.Distance(_left.hand.position, _left.fore.position);

        /// <summary>A reload: where on the magazine the free hand's palm goes. Its bottom; but a long magazine pulled
        /// out of the gun hangs lower than the arm reaches, and a hand sent to a point it cannot reach stops in the
        /// air beside it. So the hold slides up the magazine, back towards the gun, as far as it takes for the arm to
        /// reach, never higher than the magazine has come out.</summary>
        private Vector3 MagazinePalm(Vector3 root, Quaternion gun, in Hold hold, WeaponHoldSettings s, float scale)
        {
            Vector3 palm = root + gun * hold.magazine;
            if (hold.magazineOut <= 0f) return palm;
            Vector3 shoulder = _left.upper.position;
            Vector3 wrist = LeftWrist(palm, (palm - shoulder).normalized, gun, s, scale, s.palmRollDegrees, out _, out _);
            return palm + gun * Vector3.up * SlideToReach(wrist - shoulder, gun * Vector3.down, hold.magazineOut, LeftArmLength * MostStretch);
        }

        /// <summary>First person: turns the chest to the right by <paramref name="turn"/> degrees (a long gun: the
        /// support shoulder comes forward) and leans it back by <paramref name="lean"/> degrees (looking up: the
        /// shoulders stay behind the gun). The neck is put back as it was. Call before the body is stood under the eye.</summary>
        public void TurnChest(float turn, float lean)
        {
            if (_chest == null || (turn == 0f && lean == 0f)) return;
            Quaternion neckFaces = _neck != null ? _neck.rotation : Quaternion.identity;
            _chest.rotation = Quaternion.AngleAxis(turn, _body.up) * Quaternion.AngleAxis(-lean, _body.right) * _chest.rotation;
            if (_neck != null) _neck.rotation = neckFaces;
        }

        /// <summary>First person: how far the gun hand's place is beyond the right arm's reach (kept a little bent,
        /// WeaponHoldSettings.reach) with the gun at <paramref name="root"/> turned <paramref name="gun"/>: the way from
        /// the shoulder to it, as long as the shortfall. Zero when the arm reaches. The body is slid by this.</summary>
        public Vector3 OutOfReach(Vector3 root, Quaternion gun, in Hold hold, WeaponHoldSettings s)
        {
            if (!Ready) return Vector3.zero;
            Vector3 line = root - gun * hold.seat - _right.upper.position;
            float length = Vector3.Distance(_right.fore.position, _right.upper.position) + Vector3.Distance(_right.hand.position, _right.fore.position);
            float over = line.magnitude - length * s.reach;
            return over > 0f ? line.normalized * over : Vector3.zero;
        }

        /// <summary>First person: the gun is where the view has it (<paramref name="root"/>, <paramref name="gun"/>, its
        /// kick already in) and the arms are bent to it. The right hand goes on the grip; the left under a long gun's
        /// barrel (<paramref name="bothHands"/>), and to the magazine during a reload, a pistol's too.
        /// <paramref name="weight"/> brings the hands onto the gun (0 leaves the animation).</summary>
        public void OnGun(Vector3 root, Quaternion gun, bool bothHands, float weight, in Hold hold, WeaponHoldSettings s, float scale)
        {
            if (!Ready || weight <= 0f) return;
            bool left = _left.ok && (bothHands || hold.reload > 0f);
            _right.Remember();
            if (left) _left.Remember();
            Bend(_right, root - gun * hold.seat, gun * Quaternion.Inverse(hold.grip), s.longElbow);
            if (left && bothHands) SupportHand(root, gun, hold, s, scale, 1f);
            else if (left) HandToMagazine(root, gun, hold, s, scale);
            if (weight < 1f) { _right.Blend(weight); if (left) _left.Blend(weight); }
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

        /// <summary>A melee swing: the right arm, as it stands this frame, turned at the shoulder by
        /// <paramref name="turn"/> (in the body's own axes: pitch lifts, yaw sweeps across), and the hand at the wrist by
        /// <paramref name="wristShare"/> of it again. Whatever is seated in the fist afterwards goes with the hand, so the
        /// arm swings the weapon instead of the weapon spinning round the palm.</summary>
        public void Swing(Quaternion turn, float wristShare)
        {
            if (!Ready) return;
            Quaternion inWorld(Quaternion q) => _body.rotation * q * Quaternion.Inverse(_body.rotation);
            _right.upper.rotation = inWorld(turn) * _right.upper.rotation;
            if (wristShare > 0f) _right.hand.rotation = inWorld(Quaternion.Slerp(Quaternion.identity, turn, wristShare)) * _right.hand.rotation;
        }

        /// <summary>The free (left) arm on its own, for a throw: its wrist goes to <paramref name="fromShoulder"/> (from
        /// the left shoulder, in the body's own space, world lengths), the elbow points along
        /// <paramref name="elbowWay"/>, and the hand keeps the turn it has. Blended in by <paramref name="weight"/> over
        /// whatever the arm was doing this frame (the animation, or a gun's support hold), so call it last.</summary>
        public void FreeHandTo(Vector3 fromShoulder, Vector3 elbowWay, float weight)
        {
            if (!_left.ok || weight <= 0f) return;
            _left.Remember();
            Quaternion hand = _left.hand.rotation;
            Bend(_left, _left.upper.position + _body.rotation * fromShoulder, hand, elbowWay);
            _left.Blend(Mathf.Clamp01(weight));
        }

        /// <summary>Bends one arm so its wrist reaches <paramref name="wrist"/>, then turns the hand. The upper arm and
        /// the forearm make a triangle with the line from shoulder to wrist; the elbow points along
        /// <paramref name="elbowWay"/> (the body's own space). <paramref name="rollShare"/> lets the forearm take up
        /// that share of the hand's roll, so the sleeve does not pinch at the cuff. With <paramref name="limits"/> the
        /// hand does not roll or bend at the wrist further than a real one could (its Wrist Roll and Wrist Bend), even
        /// if it then sits a little off where it was asked to be.</summary>
        private void Bend(Arm arm, Vector3 wrist, Quaternion handTurn, Vector3 elbowWay, float rollShare = 0f, WeaponHoldSettings limits = null)
        {
            Vector3 shoulder = arm.upper.position;
            Vector3 elbow = Elbow(arm, ref wrist, elbowWay, out Vector3 way, out Vector3 pole);
            Vector3 hinge = Vector3.Cross(elbow - shoulder, wrist - elbow);
            if (hinge.sqrMagnitude < 1e-12f) hinge = Vector3.Cross(way, pole);
            arm.upper.rotation = Frame(elbow - shoulder, hinge) * arm.upperFix;
            Quaternion foreFrame = Frame(wrist - elbow, hinge);
            if (rollShare > 0f || limits != null)
            {
                // How far the hand has turned from its rest fit on the forearm, seen in the forearm's frame (y along
                // its length): a roll about that length, and a bend at the wrist.
                Quaternion fore = foreFrame * arm.foreFix;
                Quaternion turned = arm.foreFix * (Quaternion.Inverse(fore) * handTurn * Quaternion.Inverse(arm.handRest)) * Quaternion.Inverse(arm.foreFix);
                if (turned.w < 0f) turned = new Quaternion(-turned.x, -turned.y, -turned.z, -turned.w);
                float roll = 2f * Mathf.Atan2(turned.y, turned.w) * Mathf.Rad2Deg;
                if (limits != null)
                {
                    Quaternion bendPart = turned * Quaternion.Inverse(Quaternion.AngleAxis(roll, Vector3.up));
                    if (bendPart.w < 0f) bendPart = new Quaternion(-bendPart.x, -bendPart.y, -bendPart.z, -bendPart.w);
                    bendPart.ToAngleAxis(out float bend, out Vector3 bendAxis);
                    roll = Mathf.Clamp(roll, -limits.wristRoll, limits.wristRoll);
                    Quaternion allowed = Quaternion.AngleAxis(Mathf.Min(bend, limits.wristBend), bendAxis) * Quaternion.AngleAxis(roll, Vector3.up);
                    handTurn = fore * (Quaternion.Inverse(arm.foreFix) * allowed * arm.foreFix) * arm.handRest;
                }
                foreFrame *= Quaternion.AngleAxis(roll * rollShare, Vector3.up);
            }
            arm.fore.rotation = foreFrame * arm.foreFix;
            arm.hand.rotation = handTurn;
        }

        /// <summary>Where the elbow goes for the wrist at <paramref name="wrist"/> (brought within the arm's reach, so it
        /// may move): the upper arm and the forearm make a triangle with the line from the shoulder, the elbow pointing
        /// along <paramref name="elbowWay"/> (the body's own space). <paramref name="way"/> is that line's direction,
        /// <paramref name="pole"/> the way the elbow stands off it.</summary>
        private Vector3 Elbow(Arm arm, ref Vector3 wrist, Vector3 elbowWay, out Vector3 way, out Vector3 pole)
        {
            Vector3 shoulder = arm.upper.position;
            float upperLength = Vector3.Distance(arm.fore.position, shoulder), foreLength = Vector3.Distance(arm.hand.position, arm.fore.position);
            Vector3 line = wrist - shoulder;
            float distance = Mathf.Clamp(line.magnitude, 0.05f * (upperLength + foreLength), (upperLength + foreLength) * MostStretch);
            way = line.sqrMagnitude > 1e-10f ? line.normalized : _body.forward;
            wrist = shoulder + way * distance;
            float along = (upperLength * upperLength - foreLength * foreLength + distance * distance) / (2f * distance);
            float out_ = Mathf.Sqrt(Mathf.Max(upperLength * upperLength - along * along, 0f));
            pole = Torso * elbowWay;
            pole -= way * Vector3.Dot(pole, way);
            pole = pole.sqrMagnitude > 1e-8f ? pole.normalized : -_body.up;
            return shoulder + way * along + pole * out_;
        }

        /// <summary>Keeps an arm out of the chest (D-0136) and its fist out of the face (D-0137). The chest seen from above
        /// is an oval round the chest bone (Chest Half Width and Depth), from the hips up to the neck; the head a ball Head
        /// Centre Up above the head bone, Head Clear wide. Where the outer half of the upper arm or any of the forearm
        /// would be inside the chest, or the forearm's far half or the fist inside the head's ball, the wrist goes
        /// forward by as much, a few rounds, so the arm passes in front instead of through.</summary>
        private Vector3 ClearOfTorso(Arm arm, Vector3 wrist, Vector3 elbowWay, WeaponHoldSettings s, float scale)
        {
            float width = s.chestHalfWidth * scale, depth = s.chestHalfDepth * scale;
            bool chest = _chest != null && width > 0f && depth > 0f;
            float headR = s.headClear * scale;
            bool head = _head != null && headR > 0f;
            if (!chest && !head) return wrist;
            Vector3 face = head ? _head.position + _body.up * (s.headCentreUp * scale) : Vector3.zero;
            float palm = arm.knuckle != null ? Vector3.Distance(arm.knuckle.position, arm.hand.position) * PalmShare : 0.07f * scale;
            if (!chest) width = depth = 1f;
            Quaternion toBody = Quaternion.Inverse(Torso);
            Vector3 middle = chest ? _chest.position : arm.upper.position;
            float bottom = _hips != null ? (toBody * (_hips.position - middle)).y : -3f * depth;
            float top = _neck != null ? (toBody * (_neck.position - middle)).y : 2f * depth;
            Vector3 forward = Torso * Vector3.forward;
            for (int pass = 0; pass < 4; pass++)
            {
                Vector3 at = wrist;
                Vector3 elbow = Elbow(arm, ref at, elbowWay, out _, out _);
                Vector3 shoulder = arm.upper.position;
                float push = 0f;
                Vector3 fist = at + (at - elbow).normalized * palm;
                for (int i = 0; i < 8; i++)
                {
                    // Three points down the upper arm's outer half (the shoulder itself sits at the chest's edge), four
                    // along the forearm to the wrist, and the middle of the fist.
                    Vector3 p = i < 3 ? Vector3.Lerp(shoulder, elbow, 0.5f + 0.25f * i) : i < 7 ? Vector3.Lerp(elbow, at, 0.25f * (i - 2)) : fist;
                    if (chest && i < 7)
                    {
                        Vector3 local = toBody * (p - middle);
                        if (local.y >= bottom && local.y <= top)
                        {
                            float u = local.x / width;
                            if (u * u < 1f)
                            {
                                float front = depth * Mathf.Sqrt(1f - u * u);
                                if (local.z < front) push = Mathf.Max(push, front - local.z);
                            }
                        }
                    }
                    if (head && i >= 4)
                    {
                        // How far forward takes the point out of the head's ball: |d + forward t| = r, t > 0.
                        Vector3 d = p - face;
                        float c = d.sqrMagnitude - headR * headR;
                        if (c < 0f)
                        {
                            float b = Vector3.Dot(d, forward);
                            push = Mathf.Max(push, -b + Mathf.Sqrt(b * b - c));
                        }
                    }
                }
                if (push <= 1e-4f * scale) break;
                wrist = at + forward * push;
            }
            return wrist;
        }

        /// <summary>One-handed, third person (D-0141, his "move the aim of the camera without rotating the character"):
        /// the body is not turned to the aim; the waist twists toward it instead, Twist Share of the way, at most Max Twist
        /// Across to the left and Max Twist Out to the right, shared over the spine bones, eased at Twist Rate. The arm then
        /// reaches from the twisted chest (its limits are measured from it), so the two add up. Past both the light stops
        /// at its limit. Call after the Animator has posed the body and before <see cref="Aim"/>, every frame the tool is
        /// up (<paramref name="weight"/> 0 eases the twist away).</summary>
        public void TwistTorso(Vector3 target, float weight, WeaponHoldSettings s, float dt)
        {
            _twist = 0f;
            if (!Ready || _spine.Length == 0) { _twistNow = 0f; return; }
            float want = 0f;
            Vector3 flat = Vector3.ProjectOnPlane(target - _right.upper.position, Vector3.up);
            if (weight > 0f && flat.sqrMagnitude > 1e-10f)
            {
                float off = Mathf.DeltaAngle(_body.eulerAngles.y, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg);   // + to the right
                want = Mathf.Clamp(off * s.twistShare, -s.maxTwistAcross, s.maxTwistOut) * weight;
            }
            _twistNow = Mathf.Lerp(_twistNow, want, 1f - Mathf.Exp(-Mathf.Max(0.01f, s.twistRate) * dt));
            if (Mathf.Abs(_twistNow) < 0.01f) return;
            float each = _twistNow / _spine.Length;
            foreach (Transform bone in _spine) bone.rotation = Quaternion.AngleAxis(each, _body.up) * bone.rotation;
            _twist = _twistNow;
        }

        /// <summary>No twist: the tool was put away or set down.</summary>
        public void ResetTwist() { _twistNow = 0f; _twist = 0f; }

        /// <summary>Puts the gun on <paramref name="anchor"/> (the wrist, or the stock's place at the shoulder) and turns
        /// it until the barrel itself is on the target. <paramref name="seat"/> is where the gun's root sits from the
        /// anchor, in the gun's axes; <paramref name="direction"/> is the way from the shoulder to the target. The barrel
        /// runs beside that line, so held parallel to it the gun would point past anything close; closer than a few
        /// times that distance the correction fades out, so something touching the gun cannot swing it round.</summary>
        private Quaternion BarrelOn(Vector3 anchor, Vector3 seat, Vector3 muzzle, Vector3 target, Vector3 direction, WeaponHoldSettings s, bool oneHand = false, bool left = false)
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
                    trueWay = Limit((target - from).normalized, s, oneHand, left);
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

        /// <summary>One-handed: how far across the body the right arm reaches when pointing <paramref name="pitch"/> degrees
        /// up: Max Turn Across when level (or down), narrowing to Max Turn Across Up at Max Pitch Up (D-0137).</summary>
        private static float Across(WeaponHoldSettings s, float pitch) =>
            Mathf.Lerp(s.maxTurnAcross, s.maxTurnAcrossUp, s.maxPitchUp > 0f ? Mathf.Clamp01(pitch / s.maxPitchUp) : 0f);

        /// <summary>Keeps the aim in front of the body and within reach of the shoulder. One-handed (the right arm alone)
        /// it reaches less far across the body than out to its own side (D-0136); a long gun turns the same both ways.</summary>
        private Vector3 Limit(Vector3 direction, WeaponHoldSettings s, bool oneHand = false, bool leftArm = false)
        {
            Quaternion torso = Torso;
            Vector3 local = Quaternion.Inverse(torso) * direction;
            if (leftArm) local.x = -local.x;   // the left arm: its own side is the body's left (D-0144)
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -s.maxPitchDown, s.maxPitchUp) * Mathf.Deg2Rad;
            float left = oneHand ? Across(s, pitch * Mathf.Rad2Deg) : s.maxTurn, right = oneHand ? s.maxTurnOut : s.maxTurn;
            float turn = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -left, right) * Mathf.Deg2Rad;
            var limited = new Vector3(Mathf.Sin(turn) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(turn) * Mathf.Cos(pitch));
            if (leftArm) limited.x = -limited.x;
            return torso * limited;
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

        /// <summary>A direction in the body's own space, seen in a mirror down the middle (right to left).</summary>
        private static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

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
