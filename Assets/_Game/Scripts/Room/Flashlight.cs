using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ShiftJam
{
    /// <summary>
    /// A light with a switch and a shift core for a battery. Any size can press the switch (E on the body);
    /// a kid can also pick it up (E), aim it by looking, toggle it (Attack) and put it down (Crouch).
    /// Switching it on wakes its core; off puts the core back to sleep. While on, its beam can wake other
    /// dormant cores through their LightSensor, which reads the list of lit flashlights kept here.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public sealed class Flashlight : MonoBehaviour, IInteractable
    {
        public static readonly List<Flashlight> Lit = new List<Flashlight>();

        [SerializeField, Tooltip("The spot light at the lens.")] private Light beam;
        [SerializeField, Tooltip("Optional. The core inside: woken when the light goes on, asleep when off.")] private ShiftSource core;
        [SerializeField] private bool startOn;
        [SerializeField, Tooltip("Off = the toy must push the physical switch; E does nothing and shows no prompt at toy size.")] private bool toyUsesE = false;
        [SerializeField] private UnityEvent onTurnedOn;
        [SerializeField] private UnityEvent onTurnedOff;

        private Carryable _carryable;

        public bool IsOn { get; private set; }
        public Light Beam => beam;

        private void Awake()
        {
            _carryable = GetComponent<Carryable>();
            Set(startOn, silent: true);
        }

        private void OnEnable()
        {
            _carryable.Used += Toggle;
            if (IsOn && !Lit.Contains(this)) Lit.Add(this);
        }

        private void OnDisable()
        {
            _carryable.Used -= Toggle;
            Lit.Remove(this);
        }

        public void Toggle() => Set(!IsOn, silent: false);

        public void Set(bool on, bool silent)
        {
            IsOn = on;
            if (beam != null) beam.enabled = on;
            if (core != null) { if (on) core.Awaken(); else core.Sleep(); }
            if (on) { if (!Lit.Contains(this)) Lit.Add(this); } else Lit.Remove(this);
            if (silent) return;
            if (on) onTurnedOn?.Invoke(); else onTurnedOff?.Invoke();
        }

        private static bool IsKid(Interactor who) => who.Shifter != null && who.Shifter.Size == ShiftSize.Kid;

        public string PromptFor(Interactor who)
        {
            if (IsKid(who)) return "Pick up";
            if (!toyUsesE) return "";
            return IsOn ? "Switch off" : "Switch on";
        }

        public void Interact(Interactor who)
        {
            if (IsKid(who))
            {
                Carrier hands = who.GetComponent<Carrier>();
                if (hands != null && hands.Held == null) { hands.PickUp(_carryable); return; }
            }
            if (toyUsesE) Toggle();
        }
    }
}
