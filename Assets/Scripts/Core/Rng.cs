using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// Small deterministic PRNG (xorshift32). The city, signs and encounters are all generated from seeds,
    /// so the same seed always produces the same district.
    /// </summary>
    public sealed class Rng
    {
        uint state;

        public Rng(int seed)
        {
            state = (uint)seed * 747796405u + 2891336453u;
            if (state == 0) state = 0x9E3779B9u;
            Next();
            Next();
            Next();
        }

        public uint Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value => (Next() & 0xFFFFFF) / 16777216f;

        public float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>Integer in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(Next() % (uint)(maxExclusive - min));
        }

        public bool Chance(float probability) => Value < probability;

        public float Sign => (Next() & 1) == 0 ? -1f : 1f;

        public T Pick<T>(T[] items) => items[Range(0, items.Length)];

        public T Pick<T>(IList<T> items) => items[Range(0, items.Count)];

        public Rng Fork() => new Rng((int)Next());

        public Vector2 InsideCircle()
        {
            float a = Value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        public Color Hue(float saturation, float value) => Color.HSVToRGB(Value, saturation, value);

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Stable hash of two integers into [0, 1) - handy for per-cell decisions without an Rng instance.</summary>
        public static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
