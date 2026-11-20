using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The opening seconds: the figure lies in the box and gets up. The pack has no get-up clip, so the
    /// Animator's Wake state plays the idle death clip backwards; this script starts it part-way through
    /// the lying stretch and hands control back when the figure is on its feet. The motor is off meanwhile
    /// (no walking, no gravity), the camera still looks around.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class WakeUp : MonoBehaviour
    {
        private static readonly int WakeHash = Animator.StringToHash("Wake");
        private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

        [SerializeField] private Animator animator;
        [SerializeField, Range(0f, 1f), Tooltip("Where in the death clip the get-up starts, 1 = fully lying. 0.55 skips the long still stretch at the end.")]
        private float startAt = 0.55f;
        [SerializeField, Tooltip("Seconds until control returns. The Wake state runs at speed -1.5, so 0.55 of a 4.58 s clip takes about 1.7 s.")]
        private float duration = 1.7f;
        [SerializeField, Tooltip("Blend into Locomotion at the end, seconds.")] private float blend = 0.3f;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private UnityEvent onAwake;

        private PlayerMotor _motor;
        private float _timer = -1f;

        public bool Awake_ { get; private set; }

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            if (playOnStart && !HouseCarry.Arriving) Begin();   // through a door from another room the toy is up already (D-0091)
        }

        /// <summary>Lies the figure down and starts the get-up.</summary>
        public void Begin()
        {
            if (animator == null) { Finish(); return; }
            _motor.enabled = false;
            animator.Play(WakeHash, 0, startAt);
            _timer = duration;
        }

        private void Update()
        {
            if (_timer < 0f) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Finish();
        }

        private void Finish()
        {
            _timer = -1f;
            if (animator != null) animator.CrossFadeInFixedTime(LocomotionHash, blend, 0);
            _motor.enabled = true;
            Awake_ = true;
            onAwake?.Invoke();
        }
    }
}
