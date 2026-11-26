using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The safety net: if the player falls below a height, or a toy lands too hard (the fall rule, D-0016), put them back
    /// at the last checkpoint with no momentum. Lives on the Player beside the motor.
    /// The fall rule reads the landing speed (D-0096): a toy that comes down as fast as a free fall from Fatal Fall Height
    /// would (under the motor's own gravity) goes back. Every free fall breaks exactly where it always did; a glide, or
    /// anything else that slowed the fall, lands soft by itself.
    /// The reset itself (D-0230): the toy comes apart where it fell and builds itself at the checkpoint (RebuildOnReset,
    /// added here at the start); with that off, or when it cannot play, the short black cut as before.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class Respawn : MonoBehaviour
    {
        [SerializeField, Tooltip("Below this world height the player is put back. Pack units.")] private float killHeight = -20f;
        [Header("Fall rule")]
        [SerializeField, Tooltip("A toy that lands as fast as a free fall from this high (in its own units) goes back to the checkpoint. 0 = off.")] private float fatalFallHeight = 12f;
        [SerializeField, Tooltip("The kid never takes fall damage.")] private bool kidImmune = true;
        [SerializeField] private UnityEngine.Events.UnityEvent onFell;
        [SerializeField, Tooltip("Found on the HUD if empty. A short black cut covers every reset the rebuild does not (D-0230: BuildFx > Rebuild On off, or no body to copy).")] private ScreenFade fade;
        [SerializeField] private float cutOut = 0.12f, cutIn = 0.25f;
        [SerializeField, Tooltip("Start at the last save point when a save exists (SaveGame). Off = always the scene's spawn.")] private bool loadSave = true;

        private PlayerMotor _motor;
        private Shifter _shifter;
        private Vector3 _point;
        private Quaternion _facing;
        private float _busyUntil;   // a put is under way until then (unscaled time, like the fade)
        private RebuildOnReset _rebuild;   // the toy comes apart and builds itself at the checkpoint (D-0230)
        private System.Action _warpNow;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _shifter = GetComponent<Shifter>();
            SetPoint(transform.position, transform.rotation);
            if (fade == null) fade = ScreenFade.Find();
            if (!TryGetComponent(out _rebuild)) _rebuild = gameObject.AddComponent<RebuildOnReset>();   // D-0230
            _warpNow = WarpNow;
        }

        private void Put()
        {
            if (_motor == null) return;   // Unity also sends Reset() in the editor (adding the component, the Reset menu)
            // One put at a time. The kill-height check asks every frame, and a new Cut restarts the fade before it
            // darkens, so the warp would never come. After the cut the player is back up and the check stops asking.
            if (Busy) return;
            // D-0230: the toy comes apart here and builds itself at the checkpoint (Busy while it plays); when it does
            // not play, the short black cut.
            bool rebuilt = _rebuild != null && _rebuild.Play(_warpNow);
            if (!rebuilt) _busyUntil = Time.unscaledTime + cutOut + cutIn + 0.1f;
            GameEvents.RaisePlayerReset(this);
            if (rebuilt) return;
            if (fade != null) fade.Cut(cutOut, _warpNow, cutIn);
            else WarpNow();
        }

        private void WarpNow() => _motor.Warp(_point, _facing);

        private Vector3 _spawn; private Quaternion _spawnFacing;

        private void Start()
        {
            _spawn = _point; _spawnFacing = _facing;
            // With a title menu in the scene, it decides (Continue / New Game); without one, continue the active slot.
            // Through a door from another room (D-0091), the door puts the player down instead.
            if (loadSave && !HouseCarry.Arriving && FindFirstObjectByType<TitleMenu>() == null) LoadSlot(SaveGame.Slot);
        }

        /// <summary>Puts the player at the slot's save point with its size. False if the slot is empty.</summary>
        public bool LoadSlot(int slot)
        {
            if (!SaveGame.TryLoad(slot, out var d)) return false;
            if (_shifter != null) _shifter.SetSizeNow(d.size);
            SetPoint(d.position, Quaternion.Euler(0f, d.yaw, 0f));
            _motor.Warp(d.position + Vector3.up * 0.2f, _facing);
            if (TryGetComponent(out ShiftCharge charge)) charge.Refill();   // a loaded save starts charged (D-0045)
            if (TryGetComponent(out Glider glider)) glider.SetOwned(false);   // the glider is not saved (D-0095)
            Debug.Log("Loaded slot " + (slot + 1) + ": " + d.room + " as " + d.size);
            return true;
        }

        /// <summary>Back to the scene's own spawn (the toy box): New Game.</summary>
        public void ToStart()
        {
            if (_shifter != null) _shifter.SetSizeNow(ShiftSize.Toy);
            if (TryGetComponent(out ShiftCharge charge)) charge.Drain();   // New Game: the box core gives the first charge
            if (TryGetComponent(out Glider glider)) glider.SetOwned(false);   // and the desk gives the glider
            SetPoint(_spawn, _spawnFacing);
            _motor.Warp(_spawn, _spawnFacing);
        }

        private void OnEnable() { _motor.Landed += OnLanded; }
        private void OnDisable() { _motor.Landed -= OnLanded; }

        /// <summary>The landing speed a free fall from Fatal Fall Height reaches under the motor's own gravity, own units a
        /// second (about 26.5 for 14 at gravity 25). 0 with the rule off.</summary>
        public float FatalLandingSpeed => fatalFallHeight > 0f && _motor != null ? Mathf.Sqrt(2f * Mathf.Max(0.01f, _motor.OwnGravity) * fatalFallHeight) : 0f;

        private void OnLanded(float landingSpeed)
        {
            if (fatalFallHeight <= 0f) return;
            if (kidImmune && _shifter != null && _shifter.Size == ShiftSize.Kid) return;
            if (landingSpeed < FatalLandingSpeed) return;   // softer than a fall from Fatal Fall Height (D-0096)
            Put();
            onFell?.Invoke();
        }

        /// <summary>Back to the checkpoint right now: hazards call this.</summary>
        public void Reset() => Put();

        /// <summary>True while a reset's black cut, or its rebuild (D-0230), is under way: another Reset asked for now is
        /// not done (one put at a time). Who must not be skipped waits for this to pass (the start of a team match, D-0182).</summary>
        public bool Busy => Time.unscaledTime < _busyUntil || (_rebuild != null && _rebuild.Playing);

        /// <summary>Where the next respawn lands. Checkpoints call this.</summary>
        public void SetPoint(Vector3 position, Quaternion rotation)
        {
            _point = position;
            _facing = rotation;
        }

        private void Update()
        {
            if (transform.position.y < killHeight) Put();
        }
    }
}
