using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The glowing thing in the room that changes the player's size when used. A core is a battery: it can
    /// start dormant (dark, unusable) until something wakes it (a flashlight switched on, a beam held on it),
    /// and it can be spent after a number of uses. The rule lives here, on the thing, so a dormant core on a
    /// bed and a core inside a flashlight behave the same way.
    /// </summary>
    public sealed class ShiftSource : MonoBehaviour, IInteractable
    {
        /// <summary>ChargeOnly (D-0090): no shift at all, E fills the charge the player carries (the hallway's lesson core).</summary>
        public enum Mode { Toggle, GrowOnly, ShrinkOnly, ChargeOnly }

        [SerializeField] private Mode mode = Mode.Toggle;
        [SerializeField, Tooltip("Shown when the player is the wrong size for this source (\"Too big to hold\"). Empty hides the prompt.")]
        private string wrongSizePrompt = "";
        [Header("Power")]
        [SerializeField, Tooltip("Dark and unusable until Awaken() is called (by a Flashlight or a LightSensor).")] private bool startDormant;
        [SerializeField, Tooltip("Shown while dormant. Empty hides the prompt.")] private string dormantPrompt = "Dark";
        [SerializeField, Tooltip("Shifts this core gives before it is spent. 0 = unlimited.")] private int uses = 0;
        [SerializeField, Tooltip("Shown once spent. Empty hides the prompt.")] private string spentPrompt = "";
        [Header("Where it is used from")]
        [SerializeField, Min(0f), Tooltip("Above 0: the player must stand up where the core is (on the bed, on the sill) to use it: feet no more than this far below the core's middle (world units), and on the ground. From the floor below it reads Climb Prompt and does nothing. 0 = usable from anywhere in reach.")]
        private float standWithin;
        [SerializeField, Tooltip("Shown to a player below the core when Stand Within is set.")] private string climbPrompt = "Climb up to reach it";
        [Header("Looks")]
        [SerializeField, Tooltip("Active only while awake and not spent: the glow, the sparks.")] private GameObject[] awakeOnly;
        [SerializeField, Tooltip("Active only while dormant: a dim ember so the player can find it in the dark.")] private GameObject[] dormantOnly;
        [SerializeField, Tooltip("Optional. The core's solid body: gets the lit material when awake, the dark one otherwise.")] private Renderer marble;
        [SerializeField] private Material marbleAwake;
        [SerializeField] private Material marbleDark;
        [Header("Events")]
        [SerializeField, Tooltip("Hook Feel feedbacks here: the flash, the punch, the sound.")] private UnityEvent onUsed;
        [SerializeField, Tooltip("A toast when it wakes (D-0162). Empty: none.")] private string awakeToast = "The core woke up. Use it to shift (E).";
        [SerializeField] private UnityEvent onAwaken;
        [SerializeField] private UnityEvent onSleep;
        [SerializeField] private UnityEvent onSpent;

        private int _used;

        /// <summary>A ChargeOnly core's prompts (placeholder wording, his to change).</summary>
        public const string ChargePrompt = "Take the shift charge", ChargeFullPrompt = "Charge full";

        /// <summary>Raised each time this core shifts the player (after the size has changed). The orb's strike listens here.</summary>
        public event System.Action Used;

        public bool Dormant { get; private set; }
        public bool Spent => uses > 0 && _used >= uses;
        public bool Usable => !Dormant && !Spent;

        private void Awake()
        {
            Dormant = startDormant;
            ApplyLooks();
        }

        private void Start()
        {
            // Every core wears the shift orb (D-0062). One that was not built with it gets it here, so the course,
            // the Workshop and the Pocket Core need no rebuild.
            ShiftOrbLink.Dress(this);
        }

        /// <summary>Hands the looks to something else (the orb): the old glow, sparks, ember and marble are switched
        /// off and no longer follow this core's state.</summary>
        public void RetireLooks()
        {
            if (awakeOnly != null) foreach (var go in awakeOnly) if (go != null) go.SetActive(false);
            if (dormantOnly != null) foreach (var go in dormantOnly) if (go != null) go.SetActive(false);
            awakeOnly = null;
            dormantOnly = null;
            marble = null;
        }

        /// <summary>Power on: the core lights up and can be used. Nothing happens if already awake.</summary>
        public void Awaken()
        {
            if (!Dormant) return;
            Dormant = false;
            ApplyLooks();
            if (!string.IsNullOrEmpty(awakeToast)) Toasts.Show(awakeToast, ToastKind.Core);
            onAwaken?.Invoke();
            GameEvents.RaiseSourceAwakened(this);
        }

        /// <summary>Power off: back to dark. A spent core stays spent.</summary>
        public void Sleep()
        {
            if (Dormant) return;
            Dormant = true;
            ApplyLooks();
            onSleep?.Invoke();
        }

        private void ApplyLooks()
        {
            bool awake = !Dormant && !Spent;
            bool ember = Dormant && !Spent;
            if (awakeOnly != null) foreach (var go in awakeOnly) if (go != null) go.SetActive(awake);
            if (dormantOnly != null) foreach (var go in dormantOnly) if (go != null) go.SetActive(ember);
            if (marble != null)
            {
                Material want = awake ? marbleAwake : marbleDark;
                if (want != null) marble.sharedMaterial = want;
            }
        }

        /// <summary>Whether the player's feet are up where the core is (Stand Within). The reach of a kid's hand is
        /// 56 ahead and 60 up, so a core on the bed or the window sill was used from the floor and the climb was skipped
        /// (walk check 19 Nov, D-0083).</summary>
        private bool HighEnough(Interactor who) => standWithin <= 0f || transform.position.y - who.transform.position.y <= standWithin;

        public string PromptFor(Interactor who)
        {
            Shifter shifter = who.Shifter;
            if (shifter == null || shifter.Shifting) return "";
            if (Spent) return spentPrompt;
            if (Dormant) return dormantPrompt;
            if (mode == Mode.ChargeOnly)
            {
                var charge = shifter.GetComponent<ShiftCharge>();
                if (charge == null) return "";
                return charge.Charges >= charge.Max ? ChargeFullPrompt : ChargePrompt;
            }
            bool toy = shifter.Size == ShiftSize.Toy;
            string shift;
            switch (mode)
            {
                case Mode.GrowOnly: shift = toy ? "Shift: grow" : null; break;
                case Mode.ShrinkOnly: shift = toy ? null : "Shift: shrink"; break;
                default: shift = toy ? "Shift: grow" : "Shift: shrink"; break;
            }
            if (shift == null) return wrongSizePrompt;
            return HighEnough(who) ? shift : climbPrompt;
        }

        public void Interact(Interactor who)
        {
            Shifter shifter = who.Shifter;
            if (shifter == null || shifter.Shifting) return;
            if (!Usable) { GameEvents.RaiseUseRefused(who, Spent ? spentPrompt : dormantPrompt); return; }
            if (standWithin > 0f)
            {
                // Up where the core is, and standing: a kid jumping beside the bed must not shrink in mid-air.
                if (!HighEnough(who)) { GameEvents.RaiseUseRefused(who, climbPrompt); return; }
                var motor = who.GetComponent<PlayerMotor>();   // plain form: Fast Script Reload breaks `TryGetComponent(out ...) &&`
                if (motor != null && motor.isActiveAndEnabled && !motor.Grounded) { GameEvents.RaiseUseRefused(who, climbPrompt); return; }
            }
            if (mode == Mode.ChargeOnly)
            {
                // A battery and nothing else: the charge fills, the size stays. Full already: nothing to take.
                var battery = shifter.GetComponent<ShiftCharge>();
                if (battery == null || battery.Charges >= battery.Max) { GameEvents.RaiseUseRefused(who, ChargeFullPrompt); return; }
                battery.Refill();
                UiAudio.Play(UiAudio.Kind.Toast);   // the charge filled: a charge-only core makes no shift sound of its own (D-0100)
                onUsed?.Invoke();
                Used?.Invoke();
                _used++;
                if (Spent) { ApplyLooks(); onSpent?.Invoke(); }
                return;
            }
            ShiftSize before = shifter.Size;
            switch (mode)
            {
                case Mode.GrowOnly: shifter.ShiftTo(ShiftSize.Kid); break;
                case Mode.ShrinkOnly: shifter.ShiftTo(ShiftSize.Toy); break;
                default: shifter.Toggle(); break;
            }
            if (shifter.Size == before) { GameEvents.RaiseUseRefused(who, wrongSizePrompt); return; }
            // A core is a battery: using one also refills the shift the player carries (D-0045).
            if (shifter.TryGetComponent(out ShiftCharge charge)) charge.Refill();
            onUsed?.Invoke();
            Used?.Invoke();
            _used++;
            if (Spent)
            {
                ApplyLooks();
                onSpent?.Invoke();
            }
        }
    }
}
