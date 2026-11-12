using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// Front door of the audio system: one-shot effects (2D and positional), attached loops, ambience and the
    /// two-layer soundtrack (a calm layer that always plays and a combat layer faded in by danger).
    /// </summary>
    public static class Sound
    {
        public static float Master = 0.9f;
        public static float MusicVolume = 0.5f;
        public static float SfxVolume = 0.9f;

        static SoundManager manager;

        public static bool Ready => manager != null && manager.SfxReady;

        public static void Init(Transform parent, int seed)
        {
            if (manager != null) return;
            GameObject go = new GameObject("Sound");
            go.transform.SetParent(parent, false);
            manager = go.AddComponent<SoundManager>();
            manager.Begin(seed);
        }

        public static IEnumerator WaitReady()
        {
            while (manager != null && !manager.SfxReady) yield return null;
        }

        /// <summary>Non-positional sound (interface, the player's own gun).</summary>
        public static void Play(Sfx id, float volume = 1f, float pitch = 1f)
        {
            if (manager != null) manager.Play2D(id, volume, pitch);
        }

        public static void PlayAt(Sfx id, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 70f)
        {
            if (manager != null) manager.Play3D(id, position, volume, pitch, maxDistance);
        }

        /// <summary>Looping positional source parented to something that moves (drone, engine). Destroy it with its owner.</summary>
        public static AudioSource AttachLoop(Sfx id, Transform parent, float volume, float maxDistance = 30f)
        {
            return manager != null ? manager.AttachLoop(id, parent, volume, maxDistance) : null;
        }

        /// <summary>0 = calm, 1 = full combat. Drives the combat layer of the soundtrack.</summary>
        public static void SetCombat(float level)
        {
            if (manager != null) manager.combatTarget = Mathf.Clamp01(level);
        }

        public static void PlayDelayed(Sfx id, float delay, float volume = 1f)
        {
            if (manager != null) manager.StartCoroutine(manager.Delayed(id, delay, volume));
        }

        /// <summary>Mutes or restores everything except the interface (used by the pause menu).</summary>
        /// <summary>Peak and loudness figures of every generated clip, one per line (used by the automated run).</summary>
        public static string Describe() => manager != null ? manager.Describe() : "";

        public static bool MusicReady => manager != null && manager.MusicReady;

        public static void SetPaused(bool paused)
        {
            AudioListener.pause = paused;
        }
    }

    public sealed class SoundManager : MonoBehaviour
    {
        public bool SfxReady { get; private set; }
        [System.NonSerialized] public float combatTarget;

        readonly AudioClip[] clips = new AudioClip[(int)Sfx.Count];
        AudioSource[] pool2D, pool3D;
        int next2D, next3D;
        AudioSource rain, city, musicCalm, musicCombat;
        float combat;

        public void Begin(int seed)
        {
            pool2D = new AudioSource[10];
            for (int i = 0; i < pool2D.Length; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true; // interface sounds keep working in the pause menu
                pool2D[i] = s;
            }

            pool3D = new AudioSource[24];
            for (int i = 0; i < pool3D.Length; i++)
            {
                GameObject go = new GameObject("Voice");
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                Setup3D(s, 70f);
                pool3D[i] = s;
            }

            rain = MakeAmbient();
            city = MakeAmbient();
            musicCalm = MakeAmbient();
            musicCombat = MakeAmbient();
            StartCoroutine(Generate(seed));
        }

        static void Setup3D(AudioSource s, float maxDistance)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2.5f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
        }

        AudioSource MakeAmbient()
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.loop = true;
            s.volume = 0f;
            return s;
        }

        IEnumerator Generate(int seed)
        {
            // the soundtrack renders on a worker thread while the effects are made here, a few per frame
            Task<float[]> calmTask = Task.Run(() => Music.Render(false, seed));
            Task<float[]> combatTask = Task.Run(() => Music.Render(true, seed));

            Rng rng = new Rng(seed * 53 + 11);
            float budget = Time.realtimeSinceStartup;
            for (int i = 0; i < (int)Sfx.Count; i++)
            {
                Sfx id = (Sfx)i;
                float[] data = SfxBank.Render(id, rng);
                AudioClip clip = AudioClip.Create(id.ToString(), data.Length, 1, Synth.Rate, false);
                clip.SetData(data, 0);
                clips[i] = clip;
                if (Time.realtimeSinceStartup - budget > 0.012f)
                {
                    yield return null;
                    budget = Time.realtimeSinceStartup;
                }
            }
            SfxReady = true;

            rain.clip = clips[(int)Sfx.RainLoop];
            rain.Play();
            city.clip = clips[(int)Sfx.CityLoop];
            city.Play();

            while (!calmTask.IsCompleted || !combatTask.IsCompleted) yield return null;
            if (calmTask.IsFaulted || combatTask.IsFaulted)
            {
                Debug.LogWarning("Soundtrack synthesis failed: " + (calmTask.Exception ?? combatTask.Exception));
                yield break;
            }

            musicCalm.clip = MakeStereo("MusicCalm", calmTask.Result);
            yield return null;
            musicCombat.clip = MakeStereo("MusicCombat", combatTask.Result);
            yield return null;
            double start = AudioSettings.dspTime + 0.3;
            musicCalm.PlayScheduled(start);
            musicCombat.PlayScheduled(start);
        }

        static AudioClip MakeStereo(string name, float[] interleaved)
        {
            AudioClip clip = AudioClip.Create(name, interleaved.Length / 2, 2, Synth.Rate, false);
            clip.SetData(interleaved, 0);
            return clip;
        }

        public bool MusicReady => musicCalm != null && musicCalm.clip != null && musicCombat.clip != null;

        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < clips.Length; i++) Describe(sb, clips[i], ((Sfx)i).ToString());
            Describe(sb, musicCalm.clip, "MusicCalm");
            Describe(sb, musicCombat.clip, "MusicCombat");
            return sb.ToString();
        }

        static void Describe(System.Text.StringBuilder sb, AudioClip clip, string name)
        {
            if (clip == null)
            {
                sb.AppendLine("  " + name.PadRight(16) + " MISSING");
                return;
            }
            float[] data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            double sum = 0.0, dc = 0.0;
            float peak = 0f;
            int bad = 0, clipped = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i];
                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    bad++;
                    continue;
                }
                float a = Mathf.Abs(v);
                if (a > peak) peak = a;
                if (a >= 0.999f) clipped++;
                sum += v * v;
                dc += v;
            }
            int n = Mathf.Max(1, data.Length);
            sb.AppendLine("  " + name.PadRight(16) + " " + clip.length.ToString("0.00").PadLeft(6) + " s  ch " + clip.channels
                          + "  peak " + peak.ToString("0.00") + "  rms " + System.Math.Sqrt(sum / n).ToString("0.000")
                          + "  dc " + (dc / n).ToString("0.000") + "  clipped " + clipped + "  bad " + bad);
        }

        public void Play2D(Sfx id, float volume, float pitch)
        {
            AudioClip clip = clips[(int)id];
            if (clip == null) return;
            AudioSource s = pool2D[next2D];
            next2D = (next2D + 1) % pool2D.Length;
            s.clip = clip;
            s.pitch = pitch;
            s.volume = volume * Sound.SfxVolume * Sound.Master;
            s.Play();
        }

        public void Play3D(Sfx id, Vector3 position, float volume, float pitch, float maxDistance)
        {
            AudioClip clip = clips[(int)id];
            if (clip == null) return;
            AudioSource s = pool3D[next3D];
            next3D = (next3D + 1) % pool3D.Length;
            s.transform.position = position;
            s.clip = clip;
            s.pitch = pitch;
            s.maxDistance = maxDistance;
            s.volume = volume * Sound.SfxVolume * Sound.Master;
            s.Play();
        }

        public AudioSource AttachLoop(Sfx id, Transform parent, float volume, float maxDistance)
        {
            AudioClip clip = clips[(int)id];
            if (clip == null) return null;
            GameObject go = new GameObject("Loop_" + id);
            go.transform.SetParent(parent, false);
            AudioSource s = go.AddComponent<AudioSource>();
            Setup3D(s, maxDistance);
            s.clip = clip;
            s.loop = true;
            s.volume = volume * Sound.SfxVolume * Sound.Master;
            s.time = Random.Range(0f, clip.length * 0.9f);
            s.Play();
            return s;
        }

        public IEnumerator Delayed(Sfx id, float delay, float volume)
        {
            yield return new WaitForSeconds(delay);
            Play2D(id, volume, 1f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            combat = Mathf.MoveTowards(combat, combatTarget, dt * (combatTarget > combat ? 0.9f : 0.22f));
            float m = Sound.MusicVolume * Sound.Master;
            if (musicCalm.clip != null)
            {
                musicCalm.volume = Mathf.MoveTowards(musicCalm.volume, m * (0.85f - 0.25f * combat), dt * 0.4f);
                musicCombat.volume = m * combat;
            }
            if (SfxReady)
            {
                rain.volume = Mathf.MoveTowards(rain.volume, 0.3f * Sound.SfxVolume * Sound.Master, dt * 0.3f);
                city.volume = Mathf.MoveTowards(city.volume, 0.5f * Sound.SfxVolume * Sound.Master, dt * 0.3f);
            }
        }
    }

    /// <summary>
    /// Renders the soundtrack: eight bars in A minor at 100 BPM, as interleaved stereo. The calm layer is pads, a
    /// sub drone and sparse bells; the combat layer adds drums, bass and an arpeggio on the same harmony, so the
    /// two can be cross-faded at any moment.
    /// </summary>
    public static class Music
    {
        const int Rate = Synth.Rate;
        const float Bpm = 100f;
        const int Bars = 8;

        // Am - F - Dm - E, two bars each (root in octave 2, then the triad)
        static readonly int[][] Chords =
        {
            new[] { 45, 57, 60, 64 },
            new[] { 41, 53, 57, 60 },
            new[] { 38, 50, 53, 57 },
            new[] { 40, 52, 56, 59 }
        };

        public static float[] Render(bool combat, int seed)
        {
            float beat = 60f / Bpm;
            float bar = beat * 4f;
            int frames = (int)(bar * Bars * Rate);
            float[] left = new float[frames], right = new float[frames];
            Rng rng = new Rng(seed * 97 + (combat ? 7 : 3));

            for (int c = 0; c < 4; c++)
            {
                int[] chord = Chords[c];
                float t0 = c * 2f * bar;
                float len = 2f * bar;

                if (!combat)
                {
                    // pads: two detuned saws per note, one a little left, one a little right
                    for (int n = 1; n < 4; n++)
                    {
                        float f = Synth.NoteHz(chord[n]);
                        Voice(left, right, Wave.Saw, t0, len + 0.8f, f * 0.996f, 0.07f, 0.7f, 1.2f, 0.9f, 0.35f);
                        Voice(left, right, Wave.Saw, t0, len + 0.8f, f * 1.004f, 0.07f, 0.7f, 1.2f, 0.35f, 0.9f);
                        Voice(left, right, Wave.Triangle, t0, len + 0.8f, f * 2f, 0.03f, 1.2f, 1.2f, 0.6f, 0.6f);
                    }
                    // sub drone
                    Voice(left, right, Wave.Sine, t0, len + 0.3f, Synth.NoteHz(chord[0] - 12), 0.3f, 0.4f, 0.6f, 1f, 1f);
                    // sparse bells
                    for (int k = 0; k < 4; k++)
                    {
                        if (!rng.Chance(0.75f)) continue;
                        float at = t0 + (k * 2f + (rng.Chance(0.5f) ? 0.5f : 1.5f)) * beat;
                        float f = Synth.NoteHz(chord[rng.Range(1, 4)] + 12 + (rng.Chance(0.3f) ? 12 : 0));
                        bool l = rng.Chance(0.5f);
                        Voice(left, right, Wave.Triangle, at, 1.1f, f, 0.09f, 0.004f, 0.35f, l ? 1f : 0.3f, l ? 0.3f : 1f);
                        Voice(left, right, Wave.Sine, at, 0.7f, f * 2.01f, 0.03f, 0.004f, 0.2f, l ? 1f : 0.3f, l ? 0.3f : 1f);
                    }
                }
                else
                {
                    for (int b = 0; b < 8; b++)
                    {
                        float bt = t0 + b * beat;
                        // kick on every beat
                        Voice(left, right, Wave.Sine, bt, 0.24f, 150f, 0.75f, 0.002f, 0.09f, 1f, 1f, 45f);
                        Noise(left, right, rng, bt, 0.012f, 0.25f, 4000f, 0f, 0.004f);
                        // snare on 2 and 4
                        if (b % 2 == 1)
                        {
                            Noise(left, right, rng, bt, 0.2f, 0.4f, 5200f, 900f, 0.07f);
                            Voice(left, right, Wave.Sine, bt, 0.12f, 220f, 0.22f, 0.002f, 0.05f, 1f, 1f, 150f);
                        }
                        // hats on the off-beats, sixteenths in the last two bars
                        int hats = c == 3 ? 4 : 2;
                        for (int h = 0; h < hats; h++)
                        {
                            float ht = bt + (h + (hats == 2 ? 0.5f : 0f)) * beat / (hats == 2 ? 1f : 4f);
                            if (hats == 2 && h == 1) continue;
                            Noise(left, right, rng, ht, 0.05f, hats == 4 && h % 2 == 1 ? 0.07f : 0.13f, 12000f, 6000f, 0.018f);
                        }
                        // bass: eighth notes, octave jump on the "and" of 2 and 4
                        for (int e = 0; e < 2; e++)
                        {
                            float et = bt + e * beat * 0.5f;
                            int note = chord[0] - 12 + (e == 1 && b % 2 == 1 ? 12 : 0);
                            Voice(left, right, Wave.Saw, et, beat * 0.45f, Synth.NoteHz(note), 0.26f, 0.004f, 0.16f, 1f, 1f);
                            Voice(left, right, Wave.Square, et, beat * 0.45f, Synth.NoteHz(note) * 0.5f, 0.14f, 0.004f, 0.16f, 1f, 1f);
                        }
                        // arpeggio: sixteenths running up and down the triad
                        for (int s = 0; s < 4; s++)
                        {
                            int step = (b * 4 + s) % 6;
                            int idx = step < 4 ? step : 6 - step;
                            int note = idx < 3 ? chord[1 + idx] + 12 : chord[1] + 24;
                            float st = bt + s * beat * 0.25f;
                            bool l = (b * 4 + s) % 2 == 0;
                            Voice(left, right, Wave.Square, st, beat * 0.22f, Synth.NoteHz(note), 0.07f, 0.002f, 0.07f, l ? 1f : 0.45f, l ? 0.45f : 1f);
                        }
                    }
                }
            }

            // tone shaping and a wrap-around echo so the loop point is inaudible
            float cutoff = combat ? 5200f : 1700f;
            LowPassWrapped(left, cutoff);
            LowPassWrapped(right, cutoff);
            float delay = beat * 0.75f;
            EchoWrapped(left, delay, combat ? 0.22f : 0.38f);
            EchoWrapped(right, delay * 1.01f, combat ? 0.22f : 0.38f);

            float peak = 0f;
            for (int i = 0; i < frames; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(left[i]));
                peak = Mathf.Max(peak, Mathf.Abs(right[i]));
            }
            float gain = peak > 1e-5f ? 0.8f / peak : 1f;
            float[] interleaved = new float[frames * 2];
            for (int i = 0; i < frames; i++)
            {
                interleaved[i * 2] = left[i] * gain;
                interleaved[i * 2 + 1] = right[i] * gain;
            }
            return interleaved;
        }

        /// <summary>One note. Indices wrap around the loop, so tails that run past the end reappear at the start.</summary>
        static void Voice(float[] l, float[] r, Wave wave, float start, float duration, float freq, float amp, float attack, float release,
            float panL, float panR, float endFreq = 0f)
        {
            int len = l.Length;
            int s0 = (int)(start * Rate), n = (int)(duration * Rate);
            float phase = 0f;
            float ratio = endFreq > 0f ? endFreq / freq : 1f;
            float hold = duration - release;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = ratio == 1f ? freq : freq * Mathf.Pow(ratio, i / (float)n);
                phase += f / Rate;
                float p = phase - Mathf.Floor(phase);
                float x;
                switch (wave)
                {
                    case Wave.Square: x = p < 0.5f ? 1f : -1f; break;
                    case Wave.Saw: x = 2f * p - 1f; break;
                    case Wave.Triangle: x = p < 0.5f ? 4f * p - 1f : 3f - 4f * p; break;
                    default: x = Mathf.Sin(p * 6.2831853f); break;
                }
                float env = t < attack ? t / attack : 1f;
                if (t > hold) env *= Mathf.Max(0f, 1f - (t - hold) / release);
                // plucked notes (short attack) also decay while held
                if (attack < 0.01f) env *= Mathf.Exp(-t / Mathf.Max(release, 0.02f));
                int idx = (s0 + i) % len;
                float v = x * amp * env;
                l[idx] += v * panL;
                r[idx] += v * panR;
            }
        }

        static void Noise(float[] l, float[] r, Rng rng, float start, float duration, float amp, float lowpass, float highpass, float decay)
        {
            int len = l.Length;
            int s0 = (int)(start * Rate), n = (int)(duration * Rate);
            float a = 1f - Mathf.Exp(-6.2831853f * lowpass / Rate);
            float ha = highpass > 0f ? 1f - Mathf.Exp(-6.2831853f * highpass / Rate) : 0f;
            float low = 0f, hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = rng.Value * 2f - 1f;
                low += a * (x - low);
                float y = low;
                if (highpass > 0f)
                {
                    hp += ha * (y - hp);
                    y -= hp;
                }
                float v = y * amp * Mathf.Exp(-(i / (float)Rate) / decay);
                int idx = (s0 + i) % len;
                l[idx] += v;
                r[idx] += v;
            }
        }

        static void LowPassWrapped(float[] d, float cutoff)
        {
            float a = 1f - Mathf.Exp(-6.2831853f * cutoff / Rate);
            // warm the filter up on the tail so the state matches at the loop point
            float y = 0f;
            for (int i = d.Length - 2048; i < d.Length; i++) y += a * (d[i] - y);
            for (int i = 0; i < d.Length; i++)
            {
                y += a * (d[i] - y);
                d[i] = y;
            }
        }

        static void EchoWrapped(float[] d, float delay, float feedback)
        {
            int n = (int)(delay * Rate);
            int len = d.Length;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < len; i++)
                    d[i] += d[(i - n + len) % len] * feedback * (pass == 0 ? 1f : 0.35f);
        }
    }
}
