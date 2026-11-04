using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The safety net: if the player falls below a height, or a toy lands from too high (the fall rule, D-0016),
    /// put them back at the last checkpoint with no momentum. Lives on the Player beside the motor.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class Respawn : MonoBehaviour
    {
        [SerializeField, Tooltip("Below this world height the player is put back. Pack units.")] private float killHeight = -20f;
        [Header("Fall rule")]
        [SerializeField, Tooltip("A toy that lands from higher than this (in its own units) goes back to the checkpoint. 0 = off.")] private float fatalFallHeight = 12f;
        [SerializeField, Tooltip("Must match MovementSettings.gravity: the landing speed is turned back into a height.")] private float gravity = 25f;
        [SerializeField, Tooltip("The kid never takes fall damage.")] private bool kidImmune = true;
        [SerializeField] private UnityEngine.Events.UnityEvent onFell;
        [SerializeField, Tooltip("Found on the HUD if empty. A short black cut covers every reset.")] private ScreenFade fade;
        [SerializeField] private float cutOut = 0.12f, cutIn = 0.25f;

        private PlayerMotor _motor;
        private Shifter _shifter;
        private Vector3 _point;
        private Quaternion _facing;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _shifter = GetComponent<Shifter>();
            SetPoint(transform.position, transform.rotation);
            if (fade == null) fade = ScreenFade.Find();
        }

        private void Put()
        {
            if (fade != null) fade.Cut(cutOut, () => _motor.Warp(_point, _facing), cutIn);
            else _motor.Warp(_point, _facing);
        }

        private void OnEnable() { _motor.Landed += OnLanded; }
        private void OnDisable() { _motor.Landed -= OnLanded; }

        private void OnLanded(float fallSpeed)
        {
            if (fatalFallHeight <= 0f) return;
            if (kidImmune && _shifter != null && _shifter.Size == ShiftSize.Kid) return;
            float height = fallSpeed * fallSpeed / (2f * Mathf.Max(0.01f, gravity));   // the drop that gives this landing speed
            if (height < fatalFallHeight) return;
            Put();
            onFell?.Invoke();
        }

        /// <summary>Back to the checkpoint right now: hazards call this.</summary>
        public void Reset() => Put();

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
