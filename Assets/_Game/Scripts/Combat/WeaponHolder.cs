using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// The player's weapons. Carries up to a few (taken from WeaponPickups with E), shows the one in hand, fires it with
    /// Attack (tap, or hold for automatic), cycles with Previous / Next (1 / 2), holsters or draws it with X and drops it
    /// with Z. Holstered, the hands are empty (the player punches and kicks). Aim (right mouse) is held with a weapon
    /// drawn: the camera comes in and the body faces the aim. The kid's carry wins:
    /// while the Carrier holds something (the flashlight), Attack is the carried thing's and the weapon is put away; in
    /// first person, while the hands hold a prop, Attack throws it instead.
    /// Third person, a blaster (the test room's rule, numbers in WeaponHoldSettings): the gun is fixed in the right hand.
    /// At rest the arm is the animation's and the gun hangs with it. While aim is held, and for a moment after each
    /// shot, the arm comes up and points the gun at the crosshair (ArmAim), the body faces the aim, and a shot waits for
    /// the arm to be up so it leaves along the barrel. Melee: the arm is raised through the Animator's Arms layer, like
    /// the Carrier, and the weapon follows it.
    /// First person: it floats in front of the camera as a view model. Weapons are parented to the player so they shift
    /// size with it.
    /// </summary>
    [DefaultExecutionOrder(100)]   // after the Carrier's arm weight and the cameras, so the weapon never lags a frame
    public sealed class WeaponHolder : MonoBehaviour
    {
        [SerializeField, Tooltip("Found on this object if empty.")] private PlayerInput input;
        [SerializeField, Tooltip("Found on this object if empty.")] private Shifter shifter;
        [SerializeField, Tooltip("Found on this object if empty.")] private Carrier carrier;
        [SerializeField, Tooltip("Found in children if empty.")] private Animator animator;
        [SerializeField, Tooltip("Aims along this camera. Main Camera if empty.")] private Camera aimCamera;
        [SerializeField, Min(1)] private int maxWeapons = 2;
        [SerializeField, Tooltip("Animator layer that raises the arm; shared with the Carrier.")] private string armsLayer = "Arms";
        [SerializeField, Tooltip("Palm centre in the hand bone's space (rig metres).")] private Vector3 palm = new Vector3(0f, 0.08f, 0f);
        [SerializeField, Tooltip("Chest height above the feet, units at scale 1: melee and the muzzle check start here.")] private float chestHeight = 1.15f;
        [SerializeField, Tooltip("Speed a dropped weapon is tossed forward, units per second at scale 1.")] private float dropToss = 3f;

        private readonly List<Weapon> _weapons = new List<Weapon>();
        private int _current = -1;
        private Transform _hand, _shoulder;
        private int _layer = -1;
        private float _arm;
        private AudioSource _audio;
        private Transform _fpBody, _fpView;
        private FirstPersonLook _fpLook;
        private PhysicsGrabber _fpHands;
        private ThirdPersonGrab _grab;   // the third-person grab: a held prop fills the hands, and its throw is not a shot
        private bool _waitRelease;   // Attack is still down from a click the hands used (a throw): no automatic fire yet
        private PlayerBody _wears;   // the body in use: its palm replaces the one measured for the old rig
        private int _handsFullFrame = -10;   // the last frame the hands held something: that press put it down, it does not holster
        private WeaponHoldSettings _hold;    // how a blaster is held in third person
        private ArmAim _armAim;              // bends the right arm to point the gun
        private float _raise;                // 0 the arm is the animation's, 1 it is up and on the crosshair
        private float _aimTime;              // seconds the arm stays up yet after a shot
        private bool _trigger;               // a click whose shot has not left: it waits for the arm

        /// <summary>False while the weapon is holstered (X): it is carried but put away, and the hands are empty.</summary>
        public bool Drawn { get; private set; } = true;
        /// <summary>True with a weapon drawn and in the hand: the camera goes over the shoulder, Attack fires.</summary>
        public bool Armed => isActiveAndEnabled && Drawn && Current != null && !HandsFull;
        /// <summary>True while a drawn weapon is aimed (Aim held): the camera comes in, the body faces the aim.</summary>
        public bool AimHeld => Armed && !Locked && input != null && input.AimHeld && Time.timeScale > 0f;
        /// <summary>True while the gun is being pointed: aim is held, or the arm is still up after a shot. The body faces the aim.</summary>
        public bool Aiming => AimHeld || (Armed && !Locked && !FirstPerson && (_aimTime > 0f || _trigger));
        /// <summary>How far the gun arm is up: 0 at rest, 1 on the crosshair.</summary>
        public float Raise => _raise;
        /// <summary>The player's moves set this while one is under way (a roll, a block, a hit, dead): no firing, the arm comes down.</summary>
        public bool Locked { get; set; }
        /// <summary>The drawn weapon's own field of view while aimed in first person; 0 = it has none.</summary>
        public float SightFieldOfView => Armed ? Current.SightFieldOfView : 0f;

        public Weapon Current => _current >= 0 && _current < _weapons.Count ? _weapons[_current] : null;
        public int Count => _weapons.Count;
        public bool Full => _weapons.Count >= maxWeapons;
        public float Scale => shifter != null ? shifter.Scale : Mathf.Max(0.001f, transform.lossyScale.y);
        public bool FirstPerson => _fpView != null;
        /// <summary>The first-person body standing in for the player, when there is one: never shot by our own shots.</summary>
        public Transform FirstPersonBodyRoot => _fpBody;
        private Transform View => _fpView != null ? _fpView : (aimCamera != null ? aimCamera.transform : (Camera.main != null ? Camera.main.transform : null));
        /// <summary>Where the aim ray starts: the camera.</summary>
        public Vector3 AimOrigin => View != null ? View.position : AttackOrigin;
        public Vector3 AimForward => View != null ? View.forward : transform.forward;
        /// <summary>Where melee and the muzzle check start: the eyes in first person, the chest in third.</summary>
        public Vector3 AttackOrigin => _fpView != null ? _fpView.position : transform.position + Vector3.up * (chestHeight * Scale);
        /// <summary>Which way melee hits: the view in first person, the body's facing in third.</summary>
        public Vector3 AttackForward => _fpView != null ? _fpView.forward : transform.forward;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (shifter == null) shifter = GetComponent<Shifter>();
            if (carrier == null) carrier = GetComponent<Carrier>();
            _grab = GetComponent<ThirdPersonGrab>();
            _wears = GetComponent<PlayerBody>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (animator.isHuman)
                {
                    _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    _shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                }
                _layer = animator.GetLayerIndex(armsLayer);
            }
            _hold = WeaponHoldSettings.Load();
            _armAim = new ArmAim(animator, transform);   // here, before the Animator poses the arm: it reads the rest pose
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.6f;
        }

        private void OnEnable()
        {
            if (input == null) return;
            input.UseHeld += OnAttack; input.Previous += OnPrevious; input.Next += OnNext; input.DropWeapon += OnDropWeapon; input.Drop += OnHolster;
            ShowCurrent();
        }

        private void OnDisable()
        {
            if (input != null) { input.UseHeld -= OnAttack; input.Previous -= OnPrevious; input.Next -= OnNext; input.DropWeapon -= OnDropWeapon; input.Drop -= OnHolster; }
            if (Current != null) { Current.Cancel(); Current.gameObject.SetActive(false); }   // seated, or the player is off
            if (animator != null && _layer >= 0 && carrier != null && carrier.Held == null) animator.SetLayerWeight(_layer, 0f);
            _arm = 0f;
            _raise = 0f;
            StandDown();
        }

        /// <summary>The weapon in hand changed or went away: a click still waiting for the arm is dropped, and the arm
        /// does not stay up for a shot that is no longer coming.</summary>
        private void StandDown() { _trigger = false; _aimTime = 0f; }

        /// <summary>A blaster in third person is pointed by the arm; melee, first person and a body without a
        /// humanoid arm are not.</summary>
        private bool ByArm(Weapon w) => w != null && w.AimsWithView && !FirstPerson && _armAim != null && _armAim.Ready;

        /// <summary>Called by FirstPersonMode: hold the weapon as a view model in front of <paramref name="view"/>, or back in
        /// the hand with nulls.</summary>
        public void SetFirstPerson(Transform body, Transform view, FirstPersonLook look, PhysicsGrabber hands)
        {
            _fpBody = body; _fpView = view; _fpLook = look; _fpHands = hands;
            foreach (var w in _weapons) SetViewModel(w, view != null);
        }

        /// <summary>True for anything that is part of us (never a target).</summary>
        public bool IsSelf(Transform t) => t.IsChildOf(transform) || (_fpBody != null && t.IsChildOf(_fpBody));

        /// <summary>A recoil or impact kick for the first-person view; nothing in third person.</summary>
        public void Punch(Vector2 degrees) { if (_fpLook != null) _fpLook.Punch(degrees); }

        public void PlayClip(AudioClip clip, float pitch)
        {
            if (clip == null || _audio == null) return;
            _audio.pitch = pitch;
            _audio.PlayOneShot(clip);
        }

        /// <summary>Takes a weapon: a prefab is copied, a loose one in the world is picked up. A full holder drops the
        /// weapon in hand to make room. Returns the weapon now in hand.</summary>
        public Weapon Equip(Weapon source)
        {
            if (source == null) return null;
            Weapon weapon = source.gameObject.scene.IsValid() ? source : Instantiate(source);
            if (weapon.Holder != null && weapon.Holder != this) return null;
            if (_weapons.Contains(weapon)) { Select(_weapons.IndexOf(weapon)); return weapon; }
            if (Full && Current != null) Drop(Current);
            if (Current != null) { Current.Cancel(); Current.gameObject.SetActive(false); }

            var looks = weapon.GetComponent<WeaponLooks>();
            if (looks == null) looks = weapon.gameObject.AddComponent<WeaponLooks>();
            looks.SetLoose(false);
            weapon.transform.SetParent(transform, false);
            weapon.transform.localScale = looks.BaseScale;   // under the player: toy size at scale 1, grows with a shift
            weapon.SetHolder(this);
            SetViewModel(weapon, FirstPerson);
            _weapons.Add(weapon);
            _current = _weapons.Count - 1;
            Drawn = true;   // a weapon just taken is in the hand
            ShowCurrent();
            Pose();
            return weapon;
        }

        /// <summary>Lets go of a weapon: it falls out of the hand as a loose prop that E takes again.</summary>
        public void Drop(Weapon weapon)
        {
            int index = _weapons.IndexOf(weapon);
            if (index < 0) return;
            weapon.Cancel();
            if (weapon == Current) StandDown();
            _weapons.RemoveAt(index);
            if (_current >= _weapons.Count) _current = _weapons.Count - 1;
            else if (index < _current) _current--;
            weapon.SetHolder(null);
            SetViewModel(weapon, false);
            weapon.gameObject.SetActive(true);
            weapon.transform.SetParent(null, true);
            if (_fpView != null) weapon.transform.position = _fpView.position + _fpView.forward * (0.6f * Scale);
            var looks = weapon.GetComponent<WeaponLooks>();
            if (looks != null) looks.SetLoose(true);
            if (weapon.TryGetComponent(out Rigidbody rb))
                rb.linearVelocity = Vector3.ProjectOnPlane(AimForward, Vector3.up).normalized * (dropToss * Scale) + Vector3.up * (dropToss * 0.5f * Scale);
            ShowCurrent();
        }

        private void OnDropWeapon() { if (KeysAct() && !HandsFull && Current != null && !Current.Busy) Drop(Current); }

        /// <summary>A key belongs to us (swap, drop, holster): the game is running, the player is in control and nothing
        /// is being typed. Unlike a click it does not need the mouse captured: a pad has no mouse, and in the editor the
        /// capture can be missing while the game still plays.</summary>
        private bool KeysAct() => isActiveAndEnabled && Time.timeScale > 0f && !Locked && GameMode.Current != GameMode.Mode.Build && !DebugConsole.IsOpen;

        /// <summary>X with empty hands: put the weapon away, or take it out again. The same key puts down what is carried;
        /// a press that did that (the hands were full this frame or the last) does not also holster.</summary>
        private void OnHolster()
        {
            if (HandsFull || _handsFullFrame >= Time.frameCount - 1) return;
            if (Current == null || Current.Busy || !KeysAct()) return;   // a key, not a click
            SetDrawn(!Drawn);
        }

        /// <summary>Holsters (false) or draws (true) the weapon in hand.</summary>
        public void SetDrawn(bool drawn)
        {
            if (drawn == Drawn) return;
            Drawn = drawn;
            StandDown();
            if (!drawn && Current != null) Current.Cancel();
            ShowCurrent();
        }
        private void OnPrevious() => Cycle(-1);
        private void OnNext() => Cycle(1);

        private void Cycle(int step)
        {
            if (_weapons.Count < 2 || !KeysAct() || HandsFull || (Current != null && Current.Busy)) return;   // 1 / 2 are keys, not clicks
            Select((_current + step + _weapons.Count) % _weapons.Count);
        }

        private void Select(int index)
        {
            if (index == _current) return;
            if (Current != null) { Current.Cancel(); Current.gameObject.SetActive(false); }
            StandDown();
            _current = index;
            Drawn = true;   // picking a weapon takes it out
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            for (int i = 0; i < _weapons.Count; i++)
                if (_weapons[i] != null) _weapons[i].gameObject.SetActive(i == _current && isActiveAndEnabled && !HandsFull && Drawn);
        }

        private bool HandsFull => (carrier != null && carrier.Held != null) || (_grab != null && _grab.Holding);

        /// <summary>Attack (a click) belongs to us: on foot, in Play or first person, mouse captured, hands free.</summary>
        private bool CanAct()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || HandsFull || Locked) return false;
            if (GameMode.Current == GameMode.Mode.Build) return false;
            if (Cursor.lockState != CursorLockMode.Locked) return false;   // that click was to capture the mouse
            if (_fpHands != null && _fpHands.BusyThisFrame) return false;    // the click threw the held prop
            if (_grab != null && _grab.BusyThisFrame) return false;
            return true;
        }

        private void OnAttack()
        {
            var w = Current;
            if (w == null || !Drawn || !CanAct()) return;
            PullTrigger();
        }

        /// <summary>One pull of the trigger on the weapon in hand, as a click makes it (the click's own checks are
        /// OnAttack's). The body turns to the aim; a blaster on the arm fires once the arm is up, anything else at once.
        /// False with no weapon out.</summary>
        public bool PullTrigger()
        {
            var w = Current;
            if (w == null || !Drawn || !w.gameObject.activeSelf || Locked) return false;
            FaceAim();
            if (ByArm(w)) _trigger = true;   // the shot waits for the arm to be up (Update), so it leaves along the barrel
            else w.TriggerPressed();
            return true;
        }

        /// <summary>Third person: turn the body to the camera's heading so the attack goes where you look.</summary>
        private void FaceAim()
        {
            if (FirstPerson) return;
            Vector3 flat = Vector3.ProjectOnPlane(AimForward, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        private void Update()
        {
            var w = Current;
            if (HandsFull) _handsFullFrame = Time.frameCount;
            bool visible = w != null && !HandsFull && Drawn;
            if (w != null && w.gameObject.activeSelf != visible) { if (!visible) w.Cancel(); w.gameObject.SetActive(visible); }
            bool held = input != null && input.AttackHeld;
            if (!held) _waitRelease = false;
            else if ((_fpHands != null && _fpHands.BusyThisFrame) || (_grab != null && _grab.BusyThisFrame)) _waitRelease = true;   // the press threw a prop: wait for a new one
            bool pulling = visible && held && !_waitRelease && CanAct();

            // A blaster in third person: the arm is up while aim is held, the trigger is down or a click is waiting, and
            // for a moment after. The shot leaves once the arm is up.
            bool byArm = visible && ByArm(w);
            if (!byArm || Locked) StandDown();
            else if (pulling || _trigger) _aimTime = _hold.lowerDelay;
            bool aiming = byArm && !Locked && (AimHeld || _aimTime > 0f);
            _aimTime = Mathf.Max(0f, _aimTime - Time.deltaTime);
            float seconds = aiming ? _hold.raiseTime : _hold.lowerTime;
            _raise = !byArm ? 0f : seconds > 0f ? Mathf.MoveTowards(_raise, aiming ? 1f : 0f, Time.deltaTime / seconds) : aiming ? 1f : 0f;
            bool up = !byArm || _raise >= _hold.raisedAt;
            if (_trigger && up && Time.timeScale > 0f)
            {
                _trigger = false;
                w.TriggerPressed();
            }
            else if (pulling && up) w.TriggerHeld();

            // Melee: the arm reaches out through the Arms layer while the weapon is in hand. A blaster's arm is ArmAim's.
            // The Carrier writes its own weight earlier this frame: keep the larger.
            if (animator != null && _layer >= 0)
            {
                _arm = Mathf.MoveTowards(_arm, visible && !byArm && !FirstPerson && !Locked ? 1f : 0f, Time.deltaTime / 0.15f);
                if (_arm > 0f) animator.SetLayerWeight(_layer, Mathf.Max(animator.GetLayerWeight(_layer), _arm));
            }
        }

        private void LateUpdate() => Pose();

        private void Pose()
        {
            var w = Current;
            if (w == null || !w.gameObject.activeSelf) return;
            float s = Scale;
            if (_fpView != null)
            {
                Quaternion rot = _fpView.rotation * w.ViewRotation * w.PoseRotation;
                Vector3 pos = _fpView.position + _fpView.rotation * (w.ViewOffset * s) + rot * (w.PosePosition * s);
                w.transform.SetPositionAndRotation(pos, rot);
                return;
            }
            if (ByArm(w))
            {
                // The gun is fixed in the fist: barrel along the knuckles, top towards the thumb, plus the tuned turns.
                Quaternion fist = _armAim.NaturalGrip * Quaternion.Euler(_hold.gripEuler) * w.HandRotation;
                Vector3 inHand = _wears != null ? _wears.Palm(palm) : palm;
                if (_raise > 0f)
                {
                    var hold = new ArmAim.Hold
                    {
                        grip = fist,
                        seat = Quaternion.Inverse(fist) * Vector3.Scale(inHand, _hand.lossyScale) + w.HandOffset * s,
                        muzzle = w.MuzzleOffset,
                        kickTurn = w.PoseRotation,
                        kickSlide = w.PosePosition * s,
                    };
                    _armAim.Aim(w.AimPoint(), Mathf.SmoothStep(0f, 1f, _raise), hold, _hold, s);
                }
                Quaternion held = _hand.rotation * fist;
                w.transform.SetPositionAndRotation(_hand.TransformPoint(inHand) + held * (w.HandOffset * s), held);
                return;
            }
            Quaternion frame;
            if (w.AimsWithView) frame = Quaternion.LookRotation(AimForward, Vector3.up);   // no humanoid arm: the gun points with the view
            else
            {
                // Melee follows the arm (shoulder to hand) on the body's heading, so the blade reads as held out.
                Vector3 reach = _hand != null && _shoulder != null ? _hand.position - _shoulder.position : transform.forward;
                Vector3 flat = Vector3.ProjectOnPlane(reach, Vector3.up);
                if (flat.sqrMagnitude < 1e-6f) flat = transform.forward;
                frame = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }
            Quaternion r = frame * w.PoseRotation * w.HandRotation;
            Vector3 grip = _hand != null ? _hand.TransformPoint(_wears != null ? _wears.Palm(palm) : palm) : transform.position + Vector3.up * (chestHeight * s) + transform.right * (0.3f * s) + transform.forward * (0.3f * s);
            w.transform.SetPositionAndRotation(grip + r * ((w.HandOffset + w.PosePosition) * s), r);
        }

        /// <summary>Third person with a blaster: a small crosshair where the shots go (first person draws its own).</summary>
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || FirstPerson || !(Current is BlasterWeapon) || !Current.gameObject.activeSelf) return;
            if (Cursor.lockState != CursorLockMode.Locked || GameMode.Current != GameMode.Mode.Play) return;
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(cx - 8f, cy - 1f, 5f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3f, cy - 1f, 5f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 8f, 2f, 5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 3f, 2f, 5f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void SetViewModel(Weapon w, bool on)
        {
            if (w == null) return;
            foreach (var r in w.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = on ? ShadowCastingMode.Off : ShadowCastingMode.On;   // a floating gun casts a stray shadow
        }
    }
}
