using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShiftJam
{
    /// <summary>
    /// The player's weapons. Carries up to a few (taken from WeaponPickups with E), shows the one in hand, fires it with
    /// Attack (tap, or hold for automatic), cycles with Previous / Next (1 / 2, or the mouse wheel while one is out), holsters or draws it with X and drops it
    /// with Z, and reloads a blaster with R (Reload keeps the time; an empty gun clicks and reloads by itself; a swap, a
    /// holster, a drop or a move stops it). A heavy gun out slows every step a little (Weapon > Move Speed). Holstered, the hands are empty (the player punches and kicks). Aim (right mouse) is held with a weapon
    /// drawn: the camera comes in and the body faces the aim. The kid's carry wins:
    /// while the Carrier holds something (the flashlight), Attack is the carried thing's and the weapon is put away; in
    /// first person, while the hands hold a prop, Attack throws it instead.
    /// Third person, a blaster (the test room's rule, numbers in WeaponHoldSettings): the gun is fixed in the right hand.
    /// At rest the arm is the animation's and the gun hangs with it. While aim is held, and for a moment after each
    /// shot, the arm comes up and points the gun at the crosshair (ArmAim), the body faces the aim, and a shot waits for
    /// the arm to be up so it leaves along the barrel. A long gun (Weapon.TwoHanded) has both hands on it the whole time
    /// it is out: carried low across the body, raised to the shoulder the same way. Melee (D-0106): the arm is raised
    /// through the Animator's Arms layer, like the Carrier; the weapon's handle sits in the closed fist along the hand's
    /// own directions (WeaponHoldSettings, a handle in the fist) and a swing turns the arm at the shoulder and the wrist,
    /// so the hand carries the weapon. A worn weapon (the foam finger) has the hand folded away inside it.
    /// First person (the test room's rule): a blaster sits low in front of the eye at its View Offset, the player's own
    /// body stands under the eye with its head hidden (ViewBody) and its hands on the gun (ArmAim.OnGun), and aim
    /// brings the gun's own sight to the middle of the view (Weapon.SightFit). Melee sits in front of the camera the
    /// same way, the swing turning it there, with the hand on its handle (no sight). Weapons are parented to the player
    /// so they shift size with it.
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
        private float _raise;                // 0 the arm is the animation's (a long gun: carried), 1 it is up and on the crosshair
        private float _hands;                // a long gun, and any gun in first person: 0 the arms are the animation's, 1 the hands are on the gun
        private ViewBody _viewBody;          // first person: the player's own body under the eye, head hidden
        private float _sight;                // first person: 0 the gun is at the hip, 1 its sight is at the eye
        private FingerCurl _fist, _support;  // the fingers of the gun hand, and of the hand under a long gun
        private float _grip;                 // 0 the gun hand is the animation's open hand, 1 it is closed round the grip
        private float _aimTime;              // seconds the arm stays up yet after a shot
        private bool _trigger;               // a click whose shot has not left: it waits for the arm
        private WeaponAmmoSettings _ammo;    // what an empty gun does, how a reload runs
        private readonly Reload _reload = new Reload();   // the reload under way, if any
        private float _reloadPose;           // 0 the normal hold, 1 the gun tipped in front of the chest for the reload
        private Interactor _interactor;      // what E / the pad's Y would use: that press is not a reload
        private PlayerMotor _motor;          // slowed a little by a heavy gun in the hands
        private FirstPersonBody _fpMotor;    // the same, for the first-person body
        private Vector3 _handScale = Vector3.one;   // the right hand bone's own scale, put back after a worn weapon
        private bool _folded;                // the right hand is folded away inside a worn weapon
        private Inventory _inventory;        // the slots (D-0108): how many it can carry, and the keys that choose

        /// <summary>False while the weapon is holstered (X): it is carried but put away, and the hands are empty.</summary>
        public bool Drawn { get; private set; } = true;
        /// <summary>True with a weapon drawn and in the hand: the camera goes over the shoulder, Attack fires.</summary>
        public bool Armed => isActiveAndEnabled && Drawn && Current != null && !HandsFull;
        /// <summary>True while a drawn weapon is aimed (Aim held): the camera comes in, the body faces the aim.</summary>
        public bool AimHeld => Armed && !Locked && !Reloading && input != null && input.AimHeld && Time.timeScale > 0f;
        /// <summary>True while the gun is being pointed: aim is held, or the arm is still up after a shot. The body faces the aim.</summary>
        public bool Aiming => AimHeld || (Armed && !Locked && !FirstPerson && (_aimTime > 0f || _trigger));
        /// <summary>How far the gun arm is up: 0 at rest, 1 on the crosshair.</summary>
        public float Raise => _raise;
        /// <summary>First person: how far the gun's sight is up to the eye, 0 at the hip to 1.</summary>
        public float Sight => _sight;
        /// <summary>First person: true once the sight is far enough up that the gun's own sight takes over from the dot.</summary>
        public bool DownTheSight => FirstPerson && _sight > _hold.dotHidesAt;
        /// <summary>First person: true while the player's own body is drawn with its hands on the gun.</summary>
        public bool ArmsInView => _viewBody != null && _viewBody.Shown;
        /// <summary>True while the gun in hand is being reloaded: it cannot fire or aim.</summary>
        public bool Reloading => _reload.Active;
        /// <summary>How far through the reload we are, 0 to 1 (0 when none runs).</summary>
        public float ReloadDone => _reload.Done;
        /// <summary>The player's moves set this while one is under way (a roll, a block, a hit, dead): no firing, the arm comes down.</summary>
        public bool Locked { get; set; }
        /// <summary>How fast the player moves with what is in the hands, as a share of normal: a heavy gun out is a
        /// little slower (the test room's rule); 1 with nothing out.</summary>
        public float MoveShare => Armed ? Current.MoveSpeed : 1f;
        /// <summary>The drawn weapon's own field of view while aimed in first person; 0 = it has none.</summary>
        public float SightFieldOfView => Armed ? Current.SightFieldOfView : 0f;

        public Weapon Current => _current >= 0 && _current < _weapons.Count ? _weapons[_current] : null;
        public int Count => _weapons.Count;
        public bool Full => _inventory != null ? _inventory.Full : _weapons.Count >= maxWeapons;
        /// <summary>First person with a prop in the hands: the wheel moves it, the keys are its.</summary>
        public bool HandsBusyInView => _fpHands != null && _fpHands.Held != null;
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
                    if (_hand != null) _handScale = _hand.localScale;
                }
                _layer = animator.GetLayerIndex(armsLayer);
            }
            _hold = WeaponHoldSettings.Load();
            _ammo = WeaponAmmoSettings.Load();
            _interactor = GetComponent<Interactor>();
            _motor = GetComponent<PlayerMotor>();
            _armAim = new ArmAim(animator, transform);   // here, before the Animator poses the arm: it reads the rest pose
            _viewBody = new ViewBody(animator, transform);
            _fist = new FingerCurl(animator, right: true);
            _support = new FingerCurl(animator, right: false);
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0.6f;
            // A player's holder also throws grenades with the free hand (its own piece; numbers in Resources/Grenades).
            if (input != null && !TryGetComponent(out GrenadeThrower _)) gameObject.AddComponent<GrenadeThrower>();
            // ...and hears and feels its own shots land on things with health (its own piece; numbers in Resources/WeaponAmmo).
            if (input != null && !TryGetComponent(out HitConfirm _)) gameObject.AddComponent<HitConfirm>();
            // ...and carries what it holds in slots (D-0108): weapons and hand tools in one bar.
            if (input != null && !TryGetComponent(out _inventory)) _inventory = gameObject.AddComponent<Inventory>();
        }

        private void Start() { if (input != null) WeaponHud.Create(this); }   // a player's holder shows its ammo on screen

        private void OnEnable()
        {
            if (input == null) return;
            input.UseHeld += OnAttack; input.Previous += OnPrevious; input.Next += OnNext; input.DropWeapon += OnDropWeapon; input.Drop += OnHolster; input.Reload += OnReload;
            ShowCurrent();
        }

        private void OnDisable()
        {
            if (input != null) { input.UseHeld -= OnAttack; input.Previous -= OnPrevious; input.Next -= OnNext; input.DropWeapon -= OnDropWeapon; input.Drop -= OnHolster; input.Reload -= OnReload; }
            if (Current != null) { Current.Cancel(); Current.gameObject.SetActive(false); }   // seated, or the player is off
            if (animator != null && _layer >= 0 && carrier != null && carrier.Held == null) animator.SetLayerWeight(_layer, 0f);
            _arm = 0f;
            _raise = 0f;
            _hands = 0f;
            _grip = 0f;
            _sight = 0f;
            _viewBody.Show(false);
            FoldHand(false);
            StandDown();
            EndReload();
            SlowBy(1f);   // seated, or the player is off: nothing in the hands slows anything
        }

        /// <summary>Tells whatever moves the player how much of its speed it keeps with this in the hands.</summary>
        private void SlowBy(float share)
        {
            if (_motor != null) _motor.SpeedShare = share;
            if (_fpMotor != null) _fpMotor.SpeedShare = share;
        }

        /// <summary>The weapon in hand changed or went away: a click still waiting for the arm is dropped, and the arm
        /// does not stay up for a shot that is no longer coming.</summary>
        private void StandDown() { _trigger = false; _aimTime = 0f; }

        /// <summary>A blaster in third person is pointed by the arm; melee, first person and a body without a
        /// humanoid arm are not.</summary>
        private bool ByArm(Weapon w) => w != null && w.AimsWithView && !FirstPerson && _armAim != null && _armAim.Ready;

        /// <summary>A long gun on a body that can hold one in both hands.</summary>
        private bool InBothHands(Weapon w) => ByArm(w) && w.TwoHanded && _armAim.BothHands;

        /// <summary>First person, a weapon out and a body whose arms can hold it: the body is drawn under the eye
        /// with its hand on the weapon (a blaster's grip, a melee weapon's handle).</summary>
        private bool InView(Weapon w) => w != null && FirstPerson && _armAim != null && _armAim.Ready && _viewBody.Ready;

        /// <summary>Third person, a melee weapon on a body with a humanoid arm: its handle is seated in the fist.</summary>
        private bool InFist(Weapon w) => w != null && !w.AimsWithView && !FirstPerson && _armAim != null && _armAim.Ready;

        /// <summary>Called by FirstPersonMode: hold the weapon in front of <paramref name="view"/>, or back in
        /// the hand with nulls.</summary>
        public void SetFirstPerson(Transform body, Transform view, FirstPersonLook look, PhysicsGrabber hands)
        {
            if (_fpMotor != null) _fpMotor.SpeedShare = 1f;
            _viewBody.Show(false);   // the body goes back as first person hid it; Update shows it again if a gun is out
            _sight = 0f;
            _hands = 0f;
            _grip = 0f;
            _fpBody = body; _fpView = view; _fpLook = look; _fpHands = hands;
            _fpMotor = body != null ? body.GetComponent<FirstPersonBody>() : null;
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
            if (Full && Current == null && _inventory != null) { _inventory.RoomForTool(); return null; }   // every slot holds a tool: nothing to swap
            if (Full && Current != null) Drop(Current);
            if (carrier != null && _inventory != null) carrier.Stow();   // a tool in the hand goes back in its slot
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
            if (_inventory != null) _inventory.Added(weapon);
            Drawn = true;   // a weapon just taken is in the hand
            ShowCurrent();
            Pose();
            GameEvents.RaiseAmmoChanged(this);
            return weapon;
        }

        /// <summary>Takes every weapon out of the hands without dropping it: switched off, unparented, nobody's, its rounds
        /// kept. A door between the house's rooms carries them (HouseCarry, D-0091) and hands them back with Equip.
        /// <paramref name="inHand"/> is the place of the one that was out.</summary>
        public List<Weapon> Stow(out int inHand)
        {
            inHand = _current;
            var taken = new List<Weapon>(_weapons);
            StandDown();
            EndReload();
            foreach (Weapon w in taken)
            {
                if (w == null) continue;
                w.Cancel();
                w.SetHolder(null);
                SetViewModel(w, false);
                w.transform.SetParent(null, false);
                w.gameObject.SetActive(false);
            }
            _weapons.Clear();
            _current = -1;
            if (_inventory != null) foreach (Weapon w in taken) _inventory.Removed(w);
            GameEvents.RaiseAmmoChanged(this);
            return taken;
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
            if (_inventory != null) _inventory.Removed(weapon);
            SetViewModel(weapon, false);
            weapon.gameObject.SetActive(true);
            weapon.transform.SetParent(null, true);
            if (_fpView != null) weapon.transform.position = _fpView.position + _fpView.forward * (0.6f * Scale);
            var looks = weapon.GetComponent<WeaponLooks>();
            if (looks != null) looks.SetLoose(true);
            if (weapon.TryGetComponent(out Rigidbody rb))
                rb.linearVelocity = Vector3.ProjectOnPlane(AimForward, Vector3.up).normalized * (dropToss * Scale) + Vector3.up * (dropToss * 0.5f * Scale);
            ShowCurrent();
            GameEvents.RaiseAmmoChanged(this);
        }

        private void OnDropWeapon() { if (KeysAct() && !HandsFull && Current != null && !Current.Busy) Drop(Current); }

        /// <summary>A key belongs to us (swap, drop, holster): the game is running, the player is in control and nothing
        /// is being typed. Unlike a click it does not need the mouse captured: a pad has no mouse, and in the editor the
        /// capture can be missing while the game still plays.</summary>
        private bool KeysAct() => isActiveAndEnabled && Time.timeScale > 0f && !Locked && GameMode.Current != GameMode.Mode.Build && !DebugConsole.IsOpen;

        /// <summary>True when a key may use the hands for something else (a grenade): the keys are ours, no move is
        /// under way, and the hands hold no prop (a weapon out is fine: the free hand does it).</summary>
        public bool FreeToAct => KeysAct() && !HandsFull && !(_fpHands != null && _fpHands.Held != null);

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
            GameEvents.RaiseAmmoChanged(this);
        }
        private void OnPrevious() { if (_inventory == null) Cycle(-1); }   // with slots, the Inventory chooses
        private void OnNext() { if (_inventory == null) Cycle(1); }

        /// <summary>Takes <paramref name="weapon"/> out (one we carry), as a slot key does.</summary>
        public void Select(Weapon weapon) { int i = _weapons.IndexOf(weapon); if (i >= 0) Select(i); }

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
            GameEvents.RaiseAmmoChanged(this);
        }

        // ---------- ammo and the reload ----------

        /// <summary>R (pad: Y). The pad's button is also Use: with something to use in reach, or the hands busy with a
        /// prop, that press was a use and nothing is reloaded.</summary>
        private void OnReload(bool sharedWithUse)
        {
            if (!KeysAct()) return;
            if (sharedWithUse && ((_interactor != null && _interactor.Target != null) || (_grab != null && (_grab.BusyThisFrame || !string.IsNullOrEmpty(_grab.Hint))) || (_fpHands != null && _fpHands.BusyThisFrame))) return;
            StartReload();
        }

        /// <summary>Starts reloading the gun in hand, as R does. False when it cannot: no blaster out, its magazine is
        /// full, nothing in reserve, a move is under way, or a reload is running already.</summary>
        public bool StartReload()
        {
            if (!Armed || Locked || Reloading || !(Current is BlasterWeapon gun) || !gun.gameObject.activeSelf) return false;
            if (_fpHands != null && _fpHands.Held != null) return false;   // first person with a prop in the hands: R turns the prop
            if (!_reload.Begin(gun, _ammo)) return false;
            StandDown();   // no shot waits through a reload, and the arm does not stay up for one
            PlayClip(_ammo.reloadStartClip, 1f);
            GameEvents.RaiseReloadStarted(this, gun);
            GameEvents.RaiseAmmoChanged(this);
            return true;
        }

        /// <summary>Stops the reload under way (of <paramref name="gun"/>, or whichever when null). The magazine goes
        /// back in the gun; rounds count only if the new one had already seated.</summary>
        public void CancelReload(Weapon gun = null)
        {
            if (_reload.Active && (gun == null || _reload.Weapon == gun)) EndReload();
        }

        private void EndReload()
        {
            if (!_reload.Active) return;
            BlasterWeapon gun = _reload.Weapon;
            bool loaded = _reload.Seated;
            _reload.Stop();
            _reloadPose = 0f;
            if (gun != null) gun.MagazinePart.Seat();
            GameEvents.RaiseReloadEnded(this, gun, loaded);
            GameEvents.RaiseAmmoChanged(this);
        }

        /// <summary>One frame of the reload: stopped when the gun is no longer out or a move takes the hands (a roll, a
        /// block, a hit); otherwise the magazine drops out and comes back, and the rounds count in when it seats.</summary>
        private void RunReload(Weapon inHand, bool visible)
        {
            if (!_reload.Active) return;
            if (!visible || Locked || _reload.Weapon != inHand) { EndReload(); return; }
            bool over = _reload.Tick(Time.deltaTime, _ammo, out bool seated);
            if (seated)
            {
                _reload.Weapon.Magazine.Load();
                PlayClip(_ammo.reloadSeatClip, 1f);
                GameEvents.RaiseAmmoChanged(this);
            }
            if (over) { EndReload(); return; }
            _reloadPose = _reload.Pose(_ammo);
            _reload.Weapon.MagazinePart.Set(_ammo.magazineDrop * _reload.MagazineOut(_ammo), _reload.MagazineHidden(_ammo));
        }

        /// <summary>How far <paramref name="w"/> is in its reload position: 0 unless it is the gun being reloaded.</summary>
        private float ReloadPose(Weapon w) => _reload.Active && _reload.Weapon == w ? _reloadPose : 0f;

        /// <summary>A blaster's trigger was pulled with nothing in the gun. A fresh pull clicks; then the reload starts
        /// by itself if there is anything to load (the test room's rule). Returns the seconds before the trigger
        /// answers again.</summary>
        public float Empty(BlasterWeapon gun, bool pressed)
        {
            if (pressed) { PlayClip(_ammo.dryClip, 1f); GameEvents.RaiseDryFired(this, gun); }
            if (_ammo.reloadWhenEmpty) StartReload();
            return _ammo.dryWait;
        }

        /// <summary>A gun we carry spent or gained rounds: whoever shows them is told.</summary>
        public void AmmoChanged() => GameEvents.RaiseAmmoChanged(this);

        /// <summary>An ammo box: every blaster carried gets this many magazines' worth of rounds into its reserve, up to
        /// the most it holds (0 = each reserve filled to the top). Returns the rounds gained in all: 0 means nothing
        /// was needed, and the box stays.</summary>
        public int AddAmmo(int magazines)
        {
            int gained = 0;
            foreach (Weapon w in _weapons) if (w is BlasterWeapon gun) gained += gun.Magazine.Add(magazines);
            if (gained > 0) { GameEvents.RaiseAmmoTaken(this, gained); GameEvents.RaiseAmmoChanged(this); }
            return gained;
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
            if (w == null || !Drawn || !w.gameObject.activeSelf || Locked || Reloading) return false;   // a click during a reload is dropped
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
            // The mouse wheel swaps weapons while one is out (the test room's rule): down for the next, up for the one
            // before. Not with a prop in the hands (the wheel moves or turns it), nor with the key sheet open.
            float wheel = input != null ? input.Scroll : 0f;
            if (wheel != 0f && _inventory == null && Drawn && !(_fpHands != null && _fpHands.Held != null) && !KeyHelp.IsOpen) Cycle(wheel < 0f ? 1 : -1);

            var w = Current;
            SlowBy(MoveShare);
            if (HandsFull) _handsFullFrame = Time.frameCount;
            bool visible = w != null && !HandsFull && Drawn;
            if (w != null && w.gameObject.activeSelf != visible) { if (!visible) w.Cancel(); w.gameObject.SetActive(visible); }
            RunReload(w, visible);
            bool held = input != null && input.AttackHeld;
            if (!held) _waitRelease = false;
            else if ((_fpHands != null && _fpHands.BusyThisFrame) || (_grab != null && _grab.BusyThisFrame)) _waitRelease = true;   // the press threw a prop: wait for a new one
            bool pulling = visible && held && !_waitRelease && CanAct() && !Reloading;

            // A blaster in third person: the arm is up while aim is held, the trigger is down or a click is waiting, and
            // for a moment after. The shot leaves once the arm is up.
            bool byArm = visible && ByArm(w);
            if (!byArm || Locked || Reloading) StandDown();
            else if (pulling || _trigger) _aimTime = _hold.lowerDelay;
            bool aiming = byArm && !Locked && (AimHeld || _aimTime > 0f);
            _aimTime = Mathf.Max(0f, _aimTime - Time.deltaTime);
            float seconds = aiming ? _hold.raiseTime : _hold.lowerTime;
            _raise = !byArm ? 0f : seconds > 0f ? Mathf.MoveTowards(_raise, aiming ? 1f : 0f, Time.deltaTime / seconds) : aiming ? 1f : 0f;
            // First person: the body is drawn under the eye while a blaster is out, and the gun's sight comes up to the
            // eye while aim is held (the test room's rule).
            bool inView = visible && InView(w);
            _viewBody.Show(inView);
            bool sighting = inView && AimHeld && w.AimsWithView;   // melee has no sight
            _sight = !inView ? 0f : _hold.sightTime > 0f ? Mathf.MoveTowards(_sight, sighting ? 1f : 0f, Time.deltaTime / _hold.sightTime) : sighting ? 1f : 0f;
            // A long gun: both hands come onto it as it is drawn and stay there; a move (a roll, a hit) takes them off.
            // In first person the hands are on any gun the same way.
            bool both = byArm && InBothHands(w);
            _hands = !both && !inView ? 0f : _hold.holdTime > 0f ? Mathf.MoveTowards(_hands, Locked ? 0f : 1f, Time.deltaTime / _hold.holdTime) : Locked ? 0f : 1f;
            bool up = !byArm || (_raise >= _hold.raisedAt && (!both || _hands >= _hold.raisedAt));
            // The fingers close round the grip or the handle while a weapon is in the hand.
            _grip = !byArm && !inView && !(visible && InFist(w)) ? 0f : _hold.gripTime > 0f ? Mathf.MoveTowards(_grip, 1f, Time.deltaTime / _hold.gripTime) : 1f;
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
            FoldHand(w != null && w.gameObject.activeSelf && w.Worn && _armAim != null && _armAim.Ready);
            if (w == null || !w.gameObject.activeSelf) return;
            float s = Scale;
            float reload = ReloadPose(w);
            if (_fpView != null)
            {
                Quaternion turn = w.ViewRotation; Vector3 at = w.ViewOffset * s;
                if (_sight > 0f)
                {
                    // Aim held: the gun's own sight comes to the middle of the view, in front of the eye by the gun's own
                    // Sight Distance (held further out, the hand on the grip comes up into view). A
                    // sight that stands at a slant on the gun (an offset sight) is used with the gun rolled over
                    // (its Sight Roll), so the sight stands upright.
                    w.SightFit(out Vector3 sight, out bool onMount);
                    Vector3 toSight = sight * w.transform.lossyScale.z + (onMount ? Vector3.up * (_hold.sightRise * s) : Vector3.zero);
                    Quaternion atEye = turn * Quaternion.AngleAxis(w.SightRoll, Vector3.forward);
                    at = Vector3.Lerp(at, Vector3.forward * (w.SightDistance(_hold) * s) - atEye * toSight, _sight);
                    turn = Quaternion.Slerp(turn, atEye, _sight);
                }
                if (reload > 0f)
                {
                    // Reloading: the gun comes to the middle of the view, barrel tipped up, magazine rolled round.
                    Quaternion tipped = Quaternion.AngleAxis(-_hold.reloadUpDegrees, Vector3.right) * Quaternion.AngleAxis(-_hold.reloadRollDegrees, Vector3.forward);
                    turn = Quaternion.Slerp(turn, tipped, reload);
                    at = Vector3.Lerp(at, (_hold.reloadViewPosition + tipped * _hold.gripFromHolder) * s, reload);
                }
                Quaternion rot = _fpView.rotation * turn * w.PoseRotation;
                Vector3 pos = _fpView.position + _fpView.rotation * at + rot * (w.PosePosition * s);
                w.transform.SetPositionAndRotation(pos, rot);
                if (_viewBody.Shown && _hands > 0f) HandsInView(w, pos, rot, reload, s);
                return;
            }
            if (ByArm(w))
            {
                // The gun is fixed in the fist the way the test room seats it: the middle of its grip at the fist's
                // centre, the barrel and the top set against the hand's own knuckles, thumb and palm.
                Quaternion fist = FistTurn(w);
                Vector3 inHand = _armAim.InHand(_hold.fistCentre);
                bool both = InBothHands(w);
                if (both ? _hands > 0f : _raise > 0f || reload > 0f)
                {
                    ArmAim.Hold hold = Grasp(w, fist, inHand, reload, both, s);
                    Vector3 target = _raise > 0f ? w.AimPoint() : transform.position + transform.forward * (100f * s);
                    if (both) _armAim.Shoulder2(target, Mathf.SmoothStep(0f, 1f, _raise), Mathf.SmoothStep(0f, 1f, _hands), hold, _hold, s);
                    else _armAim.Aim(target, Mathf.Max(Mathf.SmoothStep(0f, 1f, _raise), reload), hold, _hold, s);   // a reload needs the arm even when the gun hung at the side
                }
                Quaternion held = _hand.rotation * fist;
                w.transform.SetPositionAndRotation(_hand.TransformPoint(inHand) + held * (w.HandOffset * s), held);
                CloseFingers(w, both);
                return;
            }
            if (InFist(w))
            {
                // Melee: the Arms layer holds the arm out. A swing turns that arm at the shoulder and the wrist, and the
                // weapon is seated in the fist after it, so the hand carries the weapon round (it no longer spins about
                // the palm with the hand standing still).
                _armAim.Swing(w.PoseRotation, _hold.swingWrist);
                Quaternion held = _hand.rotation * HoldTurn(w);
                w.transform.SetPositionAndRotation(HandPoint(_armAim.InHand(HoldCentre(w))) + held * (w.HandOffset * s), held);
                CloseFingers(w, false);
                return;
            }
            // No humanoid arm.
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

        /// <summary>How a blaster is turned in the fist (gun rotation = right hand rotation x this).</summary>
        private Quaternion FistTurn(Weapon w) => _armAim.Grip(_hold.barrelInHand, _hold.topInHand) * Quaternion.Euler(_hold.gripEuler) * w.HandRotation;

        /// <summary>How any weapon is turned in the hand: a blaster by its grip (FistTurn); a melee weapon with its
        /// handle running through the fist, blade out past the thumb; a worn one along the hand's own fingers.</summary>
        private Quaternion HoldTurn(Weapon w)
        {
            if (w.AimsWithView) return FistTurn(w);
            Quaternion inHand = w.Worn ? _armAim.Grip(_hold.wornAlong, _hold.wornTop) : _armAim.Grip(_hold.handleInHand, _hold.handleTopInHand);
            return inHand * w.HandRotation;
        }

        /// <summary>Where the weapon's root sits from the wrist, in the hand's own directions (rig metres).</summary>
        private Vector3 HoldCentre(Weapon w) => w.AimsWithView ? _hold.fistCentre : w.Worn ? _hold.wornCentre : _hold.handleCentre;

        /// <summary>The right hand's size in the world as it would be unfolded (a worn weapon shrinks the bone itself).</summary>
        private Vector3 HandScale => !_folded ? _hand.lossyScale : (_hand.parent != null ? Vector3.Scale(_hand.parent.lossyScale, _handScale) : _handScale);

        /// <summary>A point in the right hand's own space (rig metres), in the world.</summary>
        private Vector3 HandPoint(Vector3 local) => _hand.position + _hand.rotation * Vector3.Scale(local, HandScale);

        /// <summary>A worn weapon (the foam finger): the right hand bone is shrunk to nothing, so the fist does not show
        /// through the cuff; put back as it was otherwise. The Animator never writes the bone's scale.</summary>
        private void FoldHand(bool fold)
        {
            if (_hand == null || fold == _folded) return;
            _folded = fold;
            _hand.localScale = fold ? _handScale * 0.02f : _handScale;
        }

        /// <summary>What the arms need to know about the gun they hold: how it sits in the fist, its kick this frame,
        /// where the support hand holds a long gun, and during a reload where the magazine is.</summary>
        private ArmAim.Hold Grasp(Weapon w, Quaternion fist, Vector3 inHand, float reload, bool both, float s)
        {
            var hold = new ArmAim.Hold
            {
                grip = fist,
                seat = Quaternion.Inverse(fist) * Vector3.Scale(inHand, HandScale) + w.HandOffset * s,
                muzzle = w.MuzzleOffset,
                kickTurn = w.PoseRotation,
                kickSlide = w.PosePosition * s,
                frame = _hold.gripFromHolder * s,
                reload = reload,
                palmRoll = w.SupportRoll(_hold),
            };
            // Measured in the weapon's own space; its size in the world is the weapon's scale.
            float size = w.transform.lossyScale.z;
            // A reload: where the free hand takes the magazine, as far out of the gun as it is now.
            if (reload > 0f && w is BlasterWeapon gun)
            {
                hold.magazineOut = _ammo.magazineDrop * _reload.MagazineOut(_ammo) * size;
                hold.magazine = gun.MagazinePart.Bottom * size + Vector3.down * hold.magazineOut;
            }
            if (both)
            {
                w.LongGunFit(_hold.longestSupportReach * s / Mathf.Max(1e-5f, size), out float stock, out Vector3 support);
                hold.stock = stock * size;
                hold.support = support * size;
            }
            return hold;
        }

        /// <summary>First person: the gun is where the view has it (<paramref name="root"/>, <paramref name="gun"/>).
        /// The chest turns and leans with the look, the body is stood under the eye, slid towards the gun if the arm
        /// cannot reach it, and the hands are put on the gun.</summary>
        private void HandsInView(Weapon w, Vector3 root, Quaternion gun, float reload, float s)
        {
            float weight = Mathf.SmoothStep(0f, 1f, _hands);
            bool both = w.TwoHanded && _armAim.BothHands;
            Vector3 look = _fpView.forward;
            float lookUp = Mathf.Max(0f, Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f)) * Mathf.Rad2Deg);
            _armAim.TurnChest((both ? _hold.aimChestTurn : 0f) * weight, lookUp * _hold.chestFollowsView * weight);
            _viewBody.Stand(_fpView.position, transform.forward, -look.y, _hold, s);
            ArmAim.Hold hold = Grasp(w, HoldTurn(w), _armAim.InHand(HoldCentre(w)), reload, both, s);
            _viewBody.Slide(_armAim.OutOfReach(root, gun, hold, _hold));
            _armAim.OnGun(root, gun, both, weight, hold, _hold, s);
            CloseFingers(w, both);
        }

        /// <summary>The fingers: the gun hand closes round the grip, the other curls under a long gun. How far is the
        /// gun's own (a thick grip, a fat fore-end), else the shared numbers.</summary>
        private void CloseFingers(Weapon w, bool both)
        {
            if (w.Worn) return;   // the hand is folded away inside it
            Vector3 curl = w.AimsWithView ? w.GripCurl(_hold) : w.HandleCurl(_hold);
            _fist.Close(Mathf.SmoothStep(0f, 1f, _grip), curl.x, curl.y, curl.z);
            float under = w.SupportCurl(_hold);
            if (both) _support.Close(Mathf.SmoothStep(0f, 1f, Mathf.Min(_grip, _hands)), under, under, 0f);
        }

        private static void SetViewModel(Weapon w, bool on)
        {
            if (w == null) return;
            foreach (var r in w.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = on ? ShadowCastingMode.Off : ShadowCastingMode.On;   // a floating gun casts a stray shadow
        }
    }
}
