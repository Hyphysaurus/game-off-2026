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
        public enum Mode { Toggle, GrowOnly, ShrinkOnly }

        [SerializeField] private Mode mode = Mode.Toggle;
        [SerializeField, Tooltip("Shown when the player is the wrong size for this source (\"Too big to hold\"). Empty hides the prompt.")]
        private string wrongSizePrompt = "";
        [Header("Power")]
        [SerializeField, Tooltip("Dark and unusable until Awaken() is called (by a Flashlight or a LightSensor).")] private bool startDormant;
        [SerializeField, Tooltip("Shown while dormant. Empty hides the prompt.")] private string dormantPrompt = "Dark";
        [SerializeField, Tooltip("Shifts this core gives before it is spent. 0 = unlimited.")] private int uses = 0;
        [SerializeField, Tooltip("Shown once spent. Empty hides the prompt.")] private string spentPrompt = "";
        [Header("Looks")]
        [SerializeField, Tooltip("Active only while awake and not spent: the glow, the sparks.")] private GameObject[] awakeOnly;
        [SerializeField, Tooltip("Active only while dormant: a dim ember so the player can find it in the dark.")] private GameObject[] dormantOnly;
        [SerializeField, Tooltip("Optional. The core's solid body: gets the lit material when awake, the dark one otherwise.")] private Renderer marble;
        [SerializeField] private Material marbleAwake;
        [SerializeField] private Material marbleDark;
        [Header("Events")]
        [SerializeField, Tooltip("Hook Feel feedbacks here: the flash, the punch, the sound.")] private UnityEvent onUsed;
        [SerializeField] private UnityEvent onAwaken;
        [SerializeField] private UnityEvent onSleep;
        [SerializeField] private UnityEvent onSpent;

        private int _used;

        public bool Dormant { get; private set; }
        public bool Spent => uses > 0 && _used >= uses;
        public bool Usable => !Dormant && !Spent;

        private void Awake()
        {
            Dormant = startDormant;
            ApplyLooks();
        }

        /// <summary>Power on: the core lights up and can be used. Nothing happens if already awake.</summary>
        public void Awaken()
        {
            if (!Dormant) return;
            Dormant = false;
            ApplyLooks();
            onAwaken?.Invoke();
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

        public string PromptFor(Interactor who)
        {
            Shifter shifter = who.Shifter;
            if (shifter == null || shifter.Shifting) return "";
            if (Spent) return spentPrompt;
            if (Dormant) return dormantPrompt;
            bool toy = shifter.Size == ShiftSize.Toy;
            switch (mode)
            {
                case Mode.GrowOnly: return toy ? "Shift: grow" : wrongSizePrompt;
                case Mode.ShrinkOnly: return toy ? wrongSizePrompt : "Shift: shrink";
                default: return toy ? "Shift: grow" : "Shift: shrink";
            }
        }

        public void Interact(Interactor who)
        {
            Shifter shifter = who.Shifter;
            if (shifter == null || shifter.Shifting || !Usable) return;
            ShiftSize before = shifter.Size;
            switch (mode)
            {
                case Mode.GrowOnly: shifter.ShiftTo(ShiftSize.Kid); break;
                case Mode.ShrinkOnly: shifter.ShiftTo(ShiftSize.Toy); break;
                default: shifter.Toggle(); break;
            }
            if (shifter.Size == before) return;
            onUsed?.Invoke();
            _used++;
            if (Spent)
            {
                ApplyLooks();
                onSpent?.Invoke();
            }
        }
    }
}
