using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShiftJam
{
    /// <summary>
    /// One corner of the playroom that has gone grey: the five-minute colouring test. It adds up the colour on everything
    /// in it, brightens the whole place as the colour comes back (screen saturation and the room lights), times the run,
    /// and calls it done at 100%: the soldiers give up, a burst goes up over every toy, the best time is kept. Enter
    /// then starts it over (the scene's reset: everything grey again, all paint gone, the soldiers back at it).
    /// Ported from the Godot test room's colour_corner.gd; the numbers are in PaintSettings.
    /// </summary>
    public sealed class ColourCorner : MonoBehaviour, IResettable
    {
        private const string BestKey = "ShiftJam.ColourCorner.Best";

        [SerializeField, Tooltip("Everything here that has lost its colour.")] private Colourable[] things = Array.Empty<Colourable>();
        [SerializeField, Tooltip("The soldiers that rub the colour off: told to give up when the corner is done.")] private MischiefSoldier[] soldiers = Array.Empty<MischiefSoldier>();
        [SerializeField, Tooltip("A global Volume with Color Adjustments: its saturation follows the colour.")] private Volume grade;
        [SerializeField, Tooltip("The room's lights: their strength follows the colour.")] private Light[] lights = Array.Empty<Light>();
        [SerializeField, Tooltip("The scene's reset (F9): Enter after the end runs it. Without one, only the corner itself starts over.")] private WorkshopReset reset;
        [SerializeField, Tooltip("The scene's paint, for the bursts and sounds. Found (or made) if empty.")] private PaintSplats paint;

        private PaintSettings _settings;
        private ColorAdjustments _adjust;
        private float[] _lightBase;
        private bool _started;

        /// <summary>The share of all the colour that is back, 0 to 1.</summary>
        public float Share { get; private set; }
        public bool Finished { get; private set; }
        /// <summary>Seconds since the first paint landed (stops at the end).</summary>
        public float Seconds { get; private set; }
        /// <summary>The best finished time kept on this machine; 0 if there is none yet.</summary>
        public float Best { get; private set; }
        /// <summary>Everything in the corner that has lost its colour.</summary>
        public System.Collections.Generic.IReadOnlyList<Colourable> Things => things;
        /// <summary>The share, the clock's state or the end changed.</summary>
        public event Action<ColourCorner> Changed;

        private void Awake()
        {
            _settings = PaintSettings.Load();
            Best = PlayerPrefs.GetFloat(BestKey, 0f);
            if (grade != null && grade.profile.TryGet(out _adjust)) _adjust.active = true;   // .profile is this scene's own copy
            _lightBase = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) _lightBase[i] = lights[i] != null ? lights[i].intensity : 0f;
        }

        private void OnEnable()
        {
            foreach (Colourable thing in things) if (thing != null) { thing.Changed += OnChanged; thing.Painted += OnPainted; }
        }

        private void OnDisable()
        {
            foreach (Colourable thing in things) if (thing != null) { thing.Changed -= OnChanged; thing.Painted -= OnPainted; }
        }

        private void Start() => Refresh();

        private void Update()
        {
            if (_started && !Finished) Seconds += Time.deltaTime;
            if (!Finished || Time.timeScale <= 0f) return;
            var keys = Keyboard.current;
            if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)) Restart();
        }

        /// <summary>Starts the corner over: everything grey again, the paint taken away, the soldiers woken.</summary>
        public void Restart()
        {
            if (reset != null) { reset.ResetAll(); return; }   // every IResettable in the scene, this one too
            foreach (Colourable thing in things) if (thing != null) thing.ResetState();
            foreach (MischiefSoldier soldier in soldiers) if (soldier != null) soldier.ResetState();
            Splats.Clear();
            ResetState();
        }

        /// <summary>The reset key: the clock and the end are forgotten. (The toys, the paint and the soldiers reset themselves.)</summary>
        public void ResetState()
        {
            Finished = false; _started = false; Seconds = 0f;
            foreach (MischiefSoldier soldier in soldiers) if (soldier != null) soldier.Wake();
            Refresh();
        }

        private PaintSplats Splats => paint != null ? paint : (paint = PaintSplats.InScene());

        private void OnChanged(Colourable thing) => Refresh();

        private void OnPainted(Colourable thing, bool full)
        {
            if (Finished) return;
            _started = true;   // the clock starts with the first paint
            if (!full) return;
            Vector3 top = thing.transform.position + Vector3.up * thing.Height;
            Splats.Play(_settings.restoreClip, thing.transform.position);
            Burst(top, _settings.restoredBurst);
        }

        /// <summary>Adds the colour up and brightens the place to match; ends the run when it is all back.</summary>
        private void Refresh()
        {
            float total = 0f; int count = 0;
            foreach (Colourable thing in things) if (thing != null) { total += thing.Colour; count++; }
            Share = count > 0 ? total / count : 0f;
            // The whole place brightens with the colour, not just the things.
            if (_adjust != null) _adjust.saturation.Override((Mathf.Lerp(_settings.saturation.x, _settings.saturation.y, Share) - 1f) * 100f);
            float strength = Mathf.Lerp(_settings.light.x, _settings.light.y, Share);
            for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = _lightBase[i] * strength;
            if (!Finished && count > 0 && Share >= _settings.doneShare) Finish();
            Changed?.Invoke(this);
        }

        private void Finish()
        {
            Finished = true;
            foreach (MischiefSoldier soldier in soldiers) if (soldier != null) soldier.WindDown();
            Splats.Play(_settings.finishedClip, transform.position);
            foreach (Colourable thing in things)
                if (thing != null) Burst(thing.transform.position + Vector3.up * thing.Height, _settings.finishedBurst);
            if (Best <= 0f || Seconds < Best)
            {
                Best = Seconds;
                PlayerPrefs.SetFloat(BestKey, Best);
                PlayerPrefs.Save();
            }
        }

        /// <summary>A burst of confetti: bits, speed, size, seconds.</summary>
        private void Burst(Vector3 at, Vector4 burst)
        {
            if (_settings.confetti.Length == 0) return;
            Color colour = _settings.confetti[UnityEngine.Random.Range(0, _settings.confetti.Length)];
            Splats.Puff(at, Vector3.up, colour, Mathf.RoundToInt(burst.x), burst.y, burst.z, _settings.burstGravity, burst.w);
        }
    }
}
