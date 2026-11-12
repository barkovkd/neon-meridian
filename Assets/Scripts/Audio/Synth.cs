using System;

namespace NeonMeridian
{
    public enum Wave { Sine, Square, Saw, Triangle }

    /// <summary>
    /// Tiny offline synthesiser. Every sound in the game is rendered into a float buffer with these building
    /// blocks at load time (there are no audio files). Pure math, no Unity API, so it can run on a worker thread.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 44100;
        const float TwoPi = 6.28318530718f;

        public static float[] Buffer(float seconds)
        {
            return new float[Math.Max(1, (int)(seconds * Rate))];
        }

        static float Osc(Wave wave, float phase)
        {
            // phase in cycles
            float p = phase - (float)Math.Floor(phase);
            switch (wave)
            {
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Saw: return 2f * p - 1f;
                case Wave.Triangle: return p < 0.5f ? 4f * p - 1f : 3f - 4f * p;
                default: return (float)Math.Sin(p * TwoPi);
            }
        }

        static float Env(float t, float attack, float decay)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return (float)Math.Exp(-(t - attack) / decay);
        }

        /// <summary>
        /// Adds an oscillator that glides from f0 to f1 over its duration (exponentially), shaped by an
        /// attack / exponential-decay envelope.
        /// </summary>
        public static void AddOsc(float[] d, Wave wave, float start, float duration, float f0, float f1, float amp, float attack, float decay)
        {
            int s0 = (int)(start * Rate), n = (int)(duration * Rate);
            float phase = 0f;
            float ratio = f1 / f0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float k = n > 1 ? i / (float)(n - 1) : 0f;
                float f = f0 * (float)Math.Pow(ratio, k);
                phase += f / Rate;
                if (idx < 0) continue;
                d[idx] += Osc(wave, phase) * amp * Env(t, attack, decay);
            }
        }

        /// <summary>Adds filtered noise: a low-pass that sweeps from lp0 to lp1 and an optional high-pass.</summary>
        public static void AddNoise(float[] d, Rng rng, float start, float duration, float amp, float lp0, float lp1, float hp, float attack, float decay)
        {
            int s0 = (int)(start * Rate), n = (int)(duration * Rate);
            float low = 0f, hpState = 0f;
            float hpA = hp > 0f ? 1f - (float)Math.Exp(-TwoPi * hp / Rate) : 0f;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= d.Length) break;
                float t = i / (float)Rate;
                float k = n > 1 ? i / (float)(n - 1) : 0f;
                float fc = lp0 * (float)Math.Pow(lp1 / lp0, k);
                float a = 1f - (float)Math.Exp(-TwoPi * fc / Rate);
                float x = rng.Value * 2f - 1f;
                low += a * (x - low);
                float y = low;
                if (hp > 0f)
                {
                    hpState += hpA * (y - hpState);
                    y -= hpState;
                }
                if (idx < 0) continue;
                d[idx] += y * amp * Env(t, attack, decay);
            }
        }

        public static void LowPass(float[] d, float cutoff)
        {
            float a = 1f - (float)Math.Exp(-TwoPi * cutoff / Rate);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                y += a * (d[i] - y);
                d[i] = y;
            }
        }

        public static void HighPass(float[] d, float cutoff)
        {
            float a = 1f - (float)Math.Exp(-TwoPi * cutoff / Rate);
            float y = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                y += a * (d[i] - y);
                d[i] -= y;
            }
        }

        /// <summary>Feedback delay, gives short sounds a bit of space.</summary>
        public static void Echo(float[] d, float delay, float feedback, float mix)
        {
            int n = (int)(delay * Rate);
            if (n <= 0) return;
            for (int i = n; i < d.Length; i++)
                d[i] += d[i - n] * feedback * mix;
        }

        /// <summary>Soft clipping - adds grit and keeps peaks in check.</summary>
        public static void Drive(float[] d, float gain)
        {
            for (int i = 0; i < d.Length; i++)
                d[i] = (float)Math.Tanh(d[i] * gain);
        }

        public static void Normalize(float[] d, float peak)
        {
            float max = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float a = Math.Abs(d[i]);
                if (a > max) max = a;
            }
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        /// <summary>Short fades at both ends so nothing clicks.</summary>
        public static void FadeEdges(float[] d, float seconds = 0.004f)
        {
            int n = Math.Min((int)(seconds * Rate), d.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                d[i] *= k;
                d[d.Length - 1 - i] *= k;
            }
        }

        /// <summary>Makes a buffer loop seamlessly by cross-fading its tail into its head (the result is shorter).</summary>
        public static float[] MakeLoop(float[] d, float crossfadeSeconds)
        {
            int n = Math.Min((int)(crossfadeSeconds * Rate), d.Length / 2);
            int len = d.Length - n;
            float[] result = new float[len];
            Array.Copy(d, result, len);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                result[i] = d[i] * k + d[len + i] * (1f - k);
            }
            return result;
        }

        public static float NoteHz(int midi)
        {
            return 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);
        }
    }
}
