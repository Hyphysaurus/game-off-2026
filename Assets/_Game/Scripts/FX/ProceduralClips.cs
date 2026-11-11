using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Sounds made from numbers at run time, so driving has a wind, a tyre skid and a landing thump without any audio
    /// file: filtered noise for the two loops (their ends are blended so they repeat without a click) and a falling
    /// low tone with a puff of noise for the thump. DrivingSound makes each once, when first needed, and frees them.
    /// The numbers below shape the sounds themselves (they are the "recording"); how loud and at what pitch they play
    /// is DrivingFeelSettings.
    /// </summary>
    public static class ProceduralClips
    {
        private const int Rate = 22050;             // samples a second: plenty for noise and a thump, half the memory
        private const float Peak = 0.9f;            // every clip is scaled so its loudest sample is this

        // wind: noise with the hiss and the rumble taken off, swelling slowly
        private const float WindSeconds = 2f, WindBlend = 0.2f;
        private const float WindHighCut = 900f, WindLowCut = 140f;   // Hz kept between
        private const float WindGust = 0.22f;                         // how much the slow swell moves the level

        // skid: noise ringing at two pitches, with a fast flutter (rubber chattering on the floor)
        private const float SkidSeconds = 1f, SkidBlend = 0.1f;
        private const float SkidToneA = 1500f, SkidToneB = 2300f, SkidRingA = 5f, SkidRingB = 7f, SkidMixB = 0.6f;
        private const int SkidFlutterPerLoop = 23;
        private const float SkidFlutter = 0.25f;

        // thump: a tone falling from ThumpFrom to ThumpTo Hz, dying fast, plus a short dull knock
        private const float ThumpSeconds = 0.3f;
        private const float ThumpFrom = 120f, ThumpTo = 45f, ThumpFall = 25f, ThumpDecay = 14f;
        private const float KnockCut = 400f, KnockDecay = 45f, KnockMix = 0.5f;

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
