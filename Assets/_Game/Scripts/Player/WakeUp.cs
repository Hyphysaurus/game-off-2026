using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// The opening seconds: the figure lies in the box and gets up. The pack has no get-up clip, so the
    /// Animator's Wake state plays the idle death clip backwards; this script starts it part-way through
    /// the lying stretch and hands control back when the figure is on its feet. The motor is off meanwhile
    /// (no walking, no gravity), the camera still looks around.
    /// It plays by itself only when no title card is up; with the title, New Game's opening (Opening, D-0097) lies the
    /// figure down (Lie) and starts the get-up when the spark reaches it (Begin). Continue starts standing.
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
        /// <summary>Lying still, waiting for Begin (the opening holds it here).</summary>
        public bool Lying { get; private set; }
        /// <summary>Getting up right now.</summary>
        public bool Rising => _timer >= 0f;
        /// <summary>Seconds the get-up takes.</summary>
        public float Duration => duration;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            // Through a door from another room the toy is up already (D-0091); with the title up, New Game's opening plays
            // it and Continue starts standing (D-0097). The title is made before any Start (AfterSceneLoad / sceneLoaded).
            if (playOnStart && !HouseCarry.Arriving && FindFirstObjectByType<TitleMenu>() == null) Begin();
        }

        /// <summary>Lies the figure down and holds it there, still, until Begin.</summary>
        public void Lie()
        {
            if (animator == null) return;
            _motor.enabled = false;
            _timer = -1f;
            Awake_ = false;
            Lying = true;
            animator.Play(WakeHash, 0, startAt);
            animator.speed = 0f;
        }

        /// <summary>Lies the figure down (if Lie has not) and starts the get-up.</summary>
        public void Begin()
        {
            if (animator == null) { Finish(); return; }
            _motor.enabled = false;
            if (!Lying) animator.Play(WakeHash, 0, startAt);
            animator.speed = 1f;
            Lying = false;
            Awake_ = false;
            _timer = duration;
        }

        /// <summary>On its feet now (a skipped opening).</summary>
        public void FinishNow()
        {
            if (Lying || Rising) Finish(0f);
        }

        private void Update()
        {
            if (_timer < 0f) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Finish();
        }

        private void Finish() => Finish(blend);

        private void Finish(float blendSeconds)
        {
            _timer = -1f;
            Lying = false;
            if (animator != null)
            {
                animator.speed = 1f;
                if (blendSeconds > 0f) animator.CrossFadeInFixedTime(LocomotionHash, blendSeconds, 0);
                else animator.Play(LocomotionHash, 0, 0f);
            }
            _motor.enabled = true;
            Awake_ = true;
            onAwake?.Invoke();
        }
    }
}
