using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A light with a switch and a shift core for a battery. Any size can press the switch (E on the body);
    /// a kid can also pick it up (E), aim it by looking, toggle it (Attack) and put it down (Crouch). In the bar it takes
    /// the hand's size (D-0138), so a toy holds a toy-sized one and can pick one up that is its size; its beam goes with
    /// its size: reach in step, brightness with the square (light falls off with the square of the distance), so it lights
    /// the same patch round the holder at either size. While on, the beam also shows in the air (BeamShaft, D-0140).
    /// Aim held with it in hand focuses the beam (D-0141, Alan Wake 2): narrower, brighter, further, the cone in the air
    /// brighter; aiming with it off switches it on for the aim and off again after.
    /// Switching it on wakes its core; off puts the core back to sleep. While on, its beam can wake other
    /// dormant cores through their LightSensor, which reads the list of lit flashlights kept here.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public sealed class Flashlight : MonoBehaviour, IInteractable
    {
        public static readonly List<Flashlight> Lit = new List<Flashlight>();

        [SerializeField, Tooltip("The spot light at the lens.")] private Light beam;
        [SerializeField, Tooltip("Optional. The core inside: woken when the light goes on, asleep when off.")] private ShiftSource core;
        [SerializeField, Min(0f), Tooltip("The beam's brightness at the size it was made at (a kid's); it replaces the light's own (D-0138). 0 = keep the light's own.")] private float beamIntensity = 3000f;
        [Header("The beam in the air (D-0140)")]
        [SerializeField, Tooltip("Show the beam as a soft glowing cone in the air while the light is on.")] private bool showShaft = true;
        [SerializeField, Range(0.05f, 1f), Tooltip("How long the cone is, as a share of the light's range.")] private float shaftLength = 0.45f;
        [SerializeField, Range(0f, 0.9f), Tooltip("How wide the cone starts at the lens, as a share of its far width.")] private float shaftLens = 0.06f;
        [SerializeField, Range(0f, 2f), Tooltip("How bright the cone is (0 = the shader's own, 0.22).")] private float shaftStrength = 0.22f;
        [Header("Focus while aiming (D-0141)")]
        [SerializeField, Range(0.1f, 1f), Tooltip("The cone's angle when focused, as a share of its own.")] private float focusAngle = 0.45f;
        [SerializeField, Min(1f), Tooltip("How much brighter when focused.")] private float focusBrightness = 2.5f;
        [SerializeField, Min(1f), Tooltip("How much further it reaches when focused.")] private float focusReach = 1.35f;
        [SerializeField, Min(1f), Tooltip("How much brighter the cone in the air is when focused.")] private float focusShaft = 1.8f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to focus once aim is held.")] private float focusIn = 0.15f;
        [SerializeField, Min(0.01f), Tooltip("Seconds to widen again once aim is let go.")] private float focusOut = 0.3f;
        [SerializeField] private bool startOn;
        [SerializeField, Tooltip("Off = the toy must push the physical switch; E does nothing and shows no prompt at toy size.")] private bool toyUsesE = false;
        [SerializeField] private UnityEvent onTurnedOn;
        [SerializeField] private UnityEvent onTurnedOff;

        private Carryable _carryable;
        private float _range, _intensity, _angle, _inner;      // the beam as made
        private float _fitShare = -1f, _fitFocus = -1f;        // the size and focus it was last fitted to
        private float _focus;                                  // 0 wide .. 1 focused, eased
        private int _aimFrame = -10;                           // the last frame aim was held with it in hand
        private bool _onForAim;                                // switched on by aiming: off again when the aim ends
        private BeamShaft _shaft;

        public bool IsOn { get; private set; }
        public Light Beam => beam;

        private void Awake()
        {
            _carryable = GetComponent<Carryable>();
            if (beam != null)
            {
                _range = beam.range;
                _intensity = beamIntensity > 0f ? beamIntensity : beam.intensity;
                _angle = beam.spotAngle;
                _inner = beam.innerSpotAngle;
            }
            FitBeam();
            if (showShaft && beam != null) _shaft = BeamShaft.Add(beam, shaftLength, shaftLens, shaftStrength);
            Set(startOn, silent: true);
        }

        private void OnEnable()
        {
            _carryable.Used += Toggle;
            FitBeam();
            if (IsOn && !Lit.Contains(this)) Lit.Add(this);
        }

        private void OnDisable()
        {
            // Stowed or gone mid-aim: back to wide, and off again if the aim had switched it on.
            _focus = 0f; _aimFrame = -10;
            if (_onForAim) { _onForAim = false; Set(false, silent: true); }
            _carryable.Used -= Toggle;
            Lit.Remove(this);
        }

        /// <summary>The Carrier says each frame whether aim is held with this in hand (D-0141).</summary>
        public void Aimed(bool aimed)
        {
            if (!aimed) return;
            bool already = _aimFrame >= Time.frameCount - 1;
            _aimFrame = Time.frameCount;
            if (!already && !IsOn) { Set(true, silent: false); _onForAim = true; }   // only as the aim starts: a click off mid-aim stays off
        }

        private void Update()
        {
            bool aimed = _aimFrame >= Time.frameCount - 1;
            if (!aimed && _onForAim) { _onForAim = false; Set(false, silent: false); }
            float want = aimed && IsOn ? 1f : 0f;
            if (_focus != want) _focus = Mathf.MoveTowards(_focus, want, Time.deltaTime / (want > _focus ? focusIn : focusOut));
            FitBeam();
        }

        /// <summary>The beam in step with the torch's size (D-0138): reach with the size, brightness with its square; and
        /// with the focus (D-0141): narrower, brighter, further.</summary>
        private void FitBeam()
        {
            if (beam == null || _carryable == null) return;
            float share = _carryable.SizeShare;
            if (Mathf.Approximately(share, _fitShare) && Mathf.Approximately(_focus, _fitFocus)) return;
            _fitShare = share; _fitFocus = _focus;
            float f = Mathf.SmoothStep(0f, 1f, _focus);
            beam.range = _range * share * Mathf.Lerp(1f, focusReach, f);
            beam.intensity = _intensity * share * share * Mathf.Lerp(1f, focusBrightness, f);
            beam.spotAngle = _angle * Mathf.Lerp(1f, focusAngle, f);
            beam.innerSpotAngle = Mathf.Min(beam.spotAngle, _inner * Mathf.Lerp(1f, focusAngle, f));
            if (_shaft != null) _shaft.Boost = Mathf.Lerp(1f, focusShaft, f);
        }

        public void Toggle()
        {
            _onForAim = false;   // switched by hand: it stays as left once the aim ends
            Set(!IsOn, silent: false);
        }

        public void Set(bool on, bool silent)
        {
            IsOn = on;
            if (beam != null) beam.enabled = on;
            if (core != null) { if (on) core.Awaken(); else core.Sleep(); }
            if (on) { if (!Lit.Contains(this)) Lit.Add(this); } else Lit.Remove(this);
            if (silent) return;
            if (on) onTurnedOn?.Invoke(); else onTurnedOff?.Invoke();
        }

        /// <summary>A kid picks up any flashlight (it grows to the kid's hand); a toy one its own size or smaller (D-0138).</summary>
        private bool CanTake(Interactor who)
        {
            Shifter s = who.Shifter;
            if (s == null) return false;
            if (s.Size == ShiftSize.Kid) return true;
            float hand = s.Scale / Mathf.Max(1e-4f, s.ScaleOf(ShiftSize.Kid));
            return _carryable.SizeShare <= hand * 1.01f;
        }

        public string PromptFor(Interactor who)
        {
            if (CanTake(who)) return "Pick up";
            if (!toyUsesE) return "";
            return IsOn ? "Switch off" : "Switch on";
        }

        public void Interact(Interactor who)
        {
            if (CanTake(who))
            {
                Carrier hands = who.GetComponent<Carrier>();
                if (hands != null && hands.Held == null) { hands.PickUp(_carryable); return; }
            }
            if (toyUsesE) Toggle();
        }
    }
}
