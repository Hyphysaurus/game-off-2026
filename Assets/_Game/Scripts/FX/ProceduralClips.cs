using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Sounds made from numbers at run time, so driving has an engine hum, a wind, a tyre skid and a landing thump without
    /// any audio file: filtered noise for the two loops (their ends are blended so they repeat without a click) and a falling
    /// low tone with a puff of noise for the thump. DrivingSound makes each once, when first needed, and frees them.
    /// The numbers below shape the sounds themselves (they are the "recording"); how loud and at what pitch they play
    /// is DrivingFeelSettings.
    /// </summary>
    public static class ProceduralClips
    {
        private const int Rate = 22050;             // samples a second: plenty for noise and a thump, half the memory
        private const float Peak = 0.9f;            // every clip is scaled so its loudest sample is this

        // wind: a thin, steady rush of air. The first version kept the low band (140 to 900 Hz) and swelled slowly, which
        // with no engine under it sounded like surf (his ear, D-0055): the band sits higher now and barely swells.
        private const float WindSeconds = 2f, WindBlend = 0.2f;
        private const float WindHighCut = 2600f, WindLowCut = 500f;  // Hz kept between
        private const float WindGust = 0.04f;                         // how much the slow swell moves the level

        // skid: noise ringing at two pitches, with a fast flutter (rubber chattering on the floor)
        private const float SkidSeconds = 1f, SkidBlend = 0.1f;
        private const float SkidToneA = 1500f, SkidToneB = 2300f, SkidRingA = 5f, SkidRingB = 7f, SkidMixB = 0.6f;
        private const int SkidFlutterPerLoop = 23;
        private const float SkidFlutter = 0.25f;

        // thump: a tone falling from ThumpFrom to ThumpTo Hz, dying fast, plus a short dull knock
        private const float ThumpSeconds = 0.3f;
        private const float ThumpFrom = 120f, ThumpTo = 45f, ThumpFall = 25f, ThumpDecay = 14f;
        private const float KnockCut = 400f, KnockDecay = 45f, KnockMix = 0.5f;

        // engine: a low hum with an uneven edge. Every part is a whole number of Hz, so one second of it closes on itself.
        // (The pack's motor loops have nothing under 1 kHz: they whirr, and no pitch makes them hum. Measured, D-0056.)
        private const int EngineBase = 80;            // Hz at pitch 1: the car plays it from 0.7 (idling) to 1.6 and beyond
        private const float EngineGrit = 1.5f;        // how hard the sum is pressed into a soft clip: more = rougher
        private const float EngineRumble = 0.08f;     // low noise mixed under the tone
        private static readonly (float multiple, float level)[] EngineParts =
        {
            (0.5f, 0.35f),   // half the firing rate: the lope that makes it an engine and not a tone
            (1f, 1f), (1.5f, 0.22f), (2f, 0.6f), (3f, 0.4f), (4f, 0.26f), (5f, 0.15f), (6f, 0.1f), (8f, 0.05f),
        };
        private static AudioClip _engine;

        /// <summary>A one-second loop of a small engine humming, shared by every car (made once, kept).</summary>
        public static AudioClip Engine()
        {
            if (_engine != null) return _engine;
            int n = Rate;   // one second
            var data = new float[n];
            float total = 0f; foreach (var part in EngineParts) total += part.level;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, sum = 0f;
                for (int k = 0; k < EngineParts.Length; k++)
                    sum += EngineParts[k].level * Mathf.Sin(2f * Mathf.PI * EngineBase * EngineParts[k].multiple * t + k * 0.9f);
                data[i] = (float)System.Math.Tanh(EngineGrit * sum / total);
            }
            // a little low rumble under it, looped without a click like the wind
            int blend = Mathf.RoundToInt(0.1f * Rate);
            var raw = new float[n + blend];
            var random = new System.Random(11);
            float cut = OnePole(260f), low = 0f;
            for (int i = 0; i < raw.Length; i++) { low += cut * ((float)random.NextDouble() * 2f - 1f - low); raw[i] = low; }
            var rumble = Loop(raw, n, blend);
            float most = 1e-6f; for (int i = 0; i < n; i++) most = Mathf.Max(most, Mathf.Abs(rumble[i]));
            for (int i = 0; i < n; i++) data[i] += EngineRumble * rumble[i] / most;
            _engine = Clip("Engine (generated)", data);
            return _engine;
        }

        /// <summary>A two-second loop of rushing air.</summary>
        public static AudioClip Wind()
        {
            int n = Mathf.RoundToInt(WindSeconds * Rate), blend = Mathf.RoundToInt(WindBlend * Rate);
            var raw = new float[n + blend];
            var random = new System.Random(7);
            float high = OnePole(WindHighCut), low = OnePole(WindLowCut), a = 0f, b = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                a += high * (white - a);   // hiss off
                b += low * (a - b);        // what is left under the low cut...
                raw[i] = a - b;            // ...taken away
            }
            var data = Loop(raw, n, blend);
            for (int i = 0; i < n; i++)
            {
                float turn = 2f * Mathf.PI * i / n;   // whole cycles per loop, so the swell loops too
                data[i] *= 1f - WindGust + WindGust * (0.6f * Mathf.Sin(turn) + 0.4f * Mathf.Sin(3f * turn + 1.3f));
            }
            return Clip("Wind (generated)", data);
        }

        /// <summary>A one-second loop of tyres scrubbing sideways.</summary>
        public static AudioClip Skid()
        {
            int n = Mathf.RoundToInt(SkidSeconds * Rate), blend = Mathf.RoundToInt(SkidBlend * Rate);
            var raw = new float[n + blend];
            var random = new System.Random(11);
            float fa = 2f * Mathf.Sin(Mathf.PI * SkidToneA / Rate), fb = 2f * Mathf.Sin(Mathf.PI * SkidToneB / Rate);
            float lowA = 0f, bandA = 0f, lowB = 0f, bandB = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                // two ringing band filters (state variable): each keeps a narrow band round its tone
                lowA += fa * bandA; bandA += fa * (white - lowA - bandA / SkidRingA);
                lowB += fb * bandB; bandB += fb * (white - lowB - bandB / SkidRingB);
                raw[i] = bandA + SkidMixB * bandB;
            }
            var data = Loop(raw, n, blend);
            for (int i = 0; i < n; i++)
                data[i] *= 1f - SkidFlutter + SkidFlutter * Mathf.Sin(2f * Mathf.PI * SkidFlutterPerLoop * i / n);
            return Clip("Skid (generated)", data);
        }

        /// <summary>A short low thud for a landing.</summary>
        public static AudioClip Thump()
        {
            int n = Mathf.RoundToInt(ThumpSeconds * Rate), tail = Rate / 100;   // the last hundredth fades to nothing
            var data = new float[n];
            var random = new System.Random(3);
            float cut = OnePole(KnockCut), knock = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                phase += 2f * Mathf.PI * (ThumpTo + (ThumpFrom - ThumpTo) * Mathf.Exp(-ThumpFall * t)) / Rate;
                knock += cut * ((float)random.NextDouble() * 2f - 1f - knock);
                float v = Mathf.Sin(phase) * Mathf.Exp(-ThumpDecay * t) + KnockMix * knock * Mathf.Exp(-KnockDecay * t);
                if (i >= n - tail) v *= (float)(n - 1 - i) / tail;
                data[i] = v;
            }
            return Clip("Thump (generated)", data);
        }

        // drift cues (D-0058): a bright buzzing whirr for a charging slide (whole Hz, so one second loops on itself), a
        // two-partial bell for a banked level, and a short soft blip for "you can drift here"
        private const int ChargeBase = 330;            // Hz at pitch 1 (the cue plays it from 0.8 to 1.6 as the charge builds)
        private const int ChargeWobble = 12;           // times a second the whirr pulses
        private const float ChimeSeconds = 0.6f, ChimeTone = 1318.5f, ChimeDecay = 7f;   // E6, ringing out
        private const float TickSeconds = 0.07f, TickTone = 2200f, TickDecay = 60f;

        /// <summary>A one-second loop of a bright, pulsing whirr: a slide charging.</summary>
        public static AudioClip DriftCharge()
        {
            int n = Rate;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, sum = 0f;
                for (int k = 1; k <= 6; k++) sum += Mathf.Sin(2f * Mathf.PI * ChargeBase * k * t) / k;   // a soft saw
                float pulse = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * ChargeWobble * t);
                data[i] = (float)System.Math.Tanh(1.2f * sum) * pulse;
            }
            return Clip("Drift charge (generated)", data);
        }

        /// <summary>A short bell: a drift level banked.</summary>
        public static AudioClip Chime()
        {
            int n = Mathf.RoundToInt(ChimeSeconds * Rate), tail = Rate / 100;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float attack = Mathf.Clamp01(t * 400f);   // a few milliseconds in, so it does not click
                float v = Mathf.Sin(2f * Mathf.PI * ChimeTone * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * ChimeTone * 2.76f * t) * Mathf.Exp(-6f * t);
                v *= attack * Mathf.Exp(-ChimeDecay * t);
                if (i >= n - tail) v *= (float)(n - 1 - i) / tail;
                data[i] = v;
            }
            return Clip("Drift chime (generated)", data);
        }

        /// <summary>A tiny soft blip: a drift could start here.</summary>
        public static AudioClip Tick()
        {
            int n = Mathf.RoundToInt(TickSeconds * Rate), tail = Rate / 200;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float v = Mathf.Sin(2f * Mathf.PI * TickTone * t * (1f - 2f * t)) * Mathf.Clamp01(t * 2000f) * Mathf.Exp(-TickDecay * t);
                if (i >= n - tail) v *= (float)(n - 1 - i) / tail;
                data[i] = v;
            }
            return Clip("Drift tick (generated)", data);
        }

        /// <summary>The smoothing step of a simple low-pass filter that starts cutting at <paramref name="hz"/>.</summary>
        private static float OnePole(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * hz / Rate);

        /// <summary>Makes <paramref name="raw"/> (n + blend samples) into n samples that repeat without a click: the start
        /// is cross-faded with what came after the end, at equal loudness.</summary>
        private static float[] Loop(float[] raw, int n, int blend)
        {
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i >= blend) { data[i] = raw[i]; continue; }
                float k = (float)i / blend;
                data[i] = raw[i] * Mathf.Sqrt(k) + raw[n + i] * Mathf.Sqrt(1f - k);
            }
            return data;
        }

        private static AudioClip Clip(string name, float[] data)
        {
            float most = 1e-6f;
            for (int i = 0; i < data.Length; i++) most = Mathf.Max(most, Mathf.Abs(data[i]));
            float gain = Peak / most;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
