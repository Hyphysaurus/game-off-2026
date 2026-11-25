using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The opening's numbers (D-0097): New Game's few seconds in the toy box: dark, the flaps open and light comes in, a
    /// spark (a tiny shift orb) drifts down onto the lying toy and strikes, and the toy gets up. Read from
    /// Resources/OpeningSettings; without the asset these defaults play. Seconds are real time (the slow-motion does not
    /// stretch them); distances are in the toy's own heights unless they say otherwise. Also the toy's first words once it
    /// is up (D-0213, said by WakeLine), and the second line after them that names the goal (D-0220).
    /// </summary>
    [CreateAssetMenu(menuName = "Shift Jam/Opening Settings", fileName = "OpeningSettings")]
    public sealed class OpeningSettings : ScriptableObject
    {
        [Header("Dark")]
        [Tooltip("Seconds of black before anything shows.")] public float blackHold = 0.5f;
        [Tooltip("Seconds the black takes to lift.")] public float fadeIn = 1.4f;

        [Header("The lid")]
        [Tooltip("Seconds from the start until the flaps begin to open.")] public float lidAt = 1.3f;
        [Tooltip("Seconds each flap takes to open.")] public float lidSeconds = 1.3f;
        [Tooltip("Seconds between one flap and the next.")] public float lidStagger = 0.18f;
        [Tooltip("The light that pours in from the open top: its colour.")] public Color lightColor = new Color(1f, 0.88f, 0.68f);
        [Tooltip("Its brightness once fully in.")] public float lightIntensity = 6f;
        [Tooltip("Seconds it takes to come in, from the lid starting.")] public float lightRamp = 1.6f;
        [Tooltip("Its cone, degrees.")] public float lightAngle = 55f;
        [Tooltip("Seconds it takes to fade once the player has control.")] public float lightFadeAfter = 3f;
        [Tooltip("Dust drifting in the light, motes at once (0 = none).")] public int dustMotes = 60;

        [Header("The spark")]
        [Tooltip("Seconds from the start until the spark appears at the open top.")] public float sparkAt = 3f;
        [Tooltip("Seconds it takes to drift down onto the toy.")] public float sparkFall = 1.8f;
        [Tooltip("Its size, toy heights across.")] public float sparkSize = 0.22f;
        [Tooltip("How wide it swirls on the way down, toy heights.")] public float sparkSwirl = 0.6f;
        [Tooltip("Turns of the swirl on the way down.")] public float sparkTurns = 1.25f;
        [Tooltip("Its own glow light: colour (the orb's cyan).")] public Color sparkLightColor = new Color(0.35f, 0.9f, 1f);
        [Tooltip("Its own glow light: brightness.")] public float sparkLightIntensity = 3f;
        [Tooltip("Its own glow light: reach, toy heights.")] public float sparkLightRange = 2.5f;

        [Header("The strike")]
        [Tooltip("How white the screen flashes (0..1).")] public float flash = 0.6f;
        [Tooltip("Seconds the flash takes to fade.")] public float flashSeconds = 0.5f;
        [Tooltip("Game speed during the slow-motion (1 = none).")] public float slowScale = 0.3f;
        [Tooltip("Seconds of slow-motion, then it eases back over as long again.")] public float slowSeconds = 0.35f;
        [Tooltip("Camera shake: degrees.")] public float shakeDegrees = 1.6f;
        [Tooltip("Camera shake: seconds.")] public float shakeSeconds = 0.45f;

        [Header("Camera")]
        [Tooltip("Where the camera lies beside the toy while it is down (toy heights: right, up, forward of its feet).")]
        public Vector3 lowShot = new Vector3(0.9f, 0.3f, 0.7f);
        [Tooltip("How much the low shot looks up at the open top rather than at the toy (0..1).")] public float lookUp = 0.65f;
        [Tooltip("Where the camera rises to while the toy gets up (toy heights: right, up, forward; negative forward = behind).")]
        public Vector3 riseShot = new Vector3(0.55f, 0.95f, -1.4f);
        [Tooltip("Seconds the hand-over to the player's own camera takes.")] public float handOver = 0.8f;

        [Header("Skip")]
        [Tooltip("Seconds a key or button is held to skip (Esc skips at once).")] public float skipHold = 0.6f;

        [Header("The wake-up line (D-0213)")]
        [Tooltip("What the toy says once it is up (his words). A speech sticker at the top of the screen, typed out. Shown "
               + "on every New Game (New Game clears the slot) and at most once per save slot; never on Continue, a "
               + "respawn or in a sandbox. Empty = no line.")]
        [TextArea(2, 4)] public string wakeLine = "Geez, things got shifty last night... How did I get here? Am I dreaming?";
        [Tooltip("One word of the line that wobbles (its first match, any case). The line itself stays plain text. Empty "
               + "or not in the line = no wobble.")] public string wakeLineWobble = "shifty";
        [Tooltip("The Text Animator effect that wobbles it, by its tag: wave, wiggle, swing, pend, dangle, rot, shake, "
               + "bounce. Empty = no wobble.")] public string wakeLineWobbleEffect = "wave";
        [Tooltip("Seconds after the toy starts to get up (or after a skip) before the line drops in: the strike's flash "
               + "and the skip's fade from black are gone by then. Real time.")] public float wakeLineDelay = 0.6f;
        [Tooltip("Letters typed a second.")] public float wakeLineLettersPerSecond = 28f;
        [Tooltip("The beat the typing waits after a sentence ends (\". \", \"... \", \"? \"), seconds.")] public float wakeLinePause = 0.3f;
        [Tooltip("Seconds the whole line stays up once typed, before it fades.")] public float wakeLineHold = 4.5f;
        [Tooltip("Its sound as it drops in, one of the UI sounds (Resources/UiSounds). None = silent.")]
        public UiAudio.Kind wakeLineSound = UiAudio.Kind.Toast;

        [Header("The first-minute goal (D-0220)")]
        [Tooltip("A second line the toy says once the wake-up line has gone: the whole game's goal in the first minute. "
               + "PLACEHOLDER wording, his to rewrite. Typed like the wake-up line (its letters a second, pause and sound), "
               + "said with it (same New Games, same once per slot). Empty = no second line.")]
        [TextArea(2, 4)] public string goalLine = "I need to get back up on that window sill.";
        [Tooltip("Seconds between the wake-up line leaving and this one dropping in. Real time. With no wake-up line it "
               + "drops in after Wake Line Delay instead.")] public float goalLineGap = 0.4f;
        [Tooltip("Seconds this line stays up once typed, before it fades.")] public float goalLineHold = 4f;

        // The speech sticker's own drop-in before its letters start, and its fade at the end (ToastHud: half of 0.35, and 0.35).
        private const float SayLandSeconds = 0.175f, SayOutSeconds = 0.35f;

        /// <summary>The wake-up line's look and timing, for Toasts.Say.</summary>
        public SayStyle WakeLineStyle() => new SayStyle
        {
            lettersPerSecond = wakeLineLettersPerSecond, pauseSeconds = wakeLinePause, holdSeconds = wakeLineHold,
            delay = wakeLineDelay, wobbleWord = wakeLineWobble, wobbleTag = wakeLineWobbleEffect, sound = wakeLineSound,
        };

        /// <summary>The goal line's look and timing (D-0220): typed like the wake-up line, no wobble, dropping in Goal Line
        /// Gap after the wake-up line has gone (<paramref name="afterWakeLine"/>), or after Wake Line Delay alone.</summary>
        public SayStyle GoalLineStyle(bool afterWakeLine) => new SayStyle
        {
            lettersPerSecond = wakeLineLettersPerSecond, pauseSeconds = wakeLinePause, holdSeconds = goalLineHold,
            delay = afterWakeLine ? WakeLineSeconds() + Mathf.Max(0f, goalLineGap) : wakeLineDelay,
            wobbleWord = "", wobbleTag = "", sound = wakeLineSound,
        };

        /// <summary>Seconds from the wake-up line being asked for until its sticker has gone: its delay, the drop-in, a
        /// letter at a time (and the beat after each sentence, as the speech sticker types it), the hold, the fade.</summary>
        public float WakeLineSeconds()
        {
            string line = wakeLine ?? "";
            float step = 1f / Mathf.Max(1f, wakeLineLettersPerSecond), pause = Mathf.Max(0f, wakeLinePause), typing = 0f;
            for (int i = 0; i + 1 < line.Length; i++)   // up to the last letter's time, as the sticker counts it
            {
                typing += step;
                char ch = line[i];
                if ((ch == '.' || ch == '?' || ch == '!') && line[i + 1] == ' ') typing += pause;
            }
            return Mathf.Max(0f, wakeLineDelay) + SayLandSeconds + typing + Mathf.Max(0f, wakeLineHold) + SayOutSeconds;
        }

        private static OpeningSettings _loaded;

        /// <summary>Resources/OpeningSettings, or the defaults.</summary>
        public static OpeningSettings Load()
        {
            if (_loaded == null) _loaded = Resources.Load<OpeningSettings>("OpeningSettings");
            if (_loaded == null) _loaded = CreateInstance<OpeningSettings>();
            return _loaded;
        }
    }
}
