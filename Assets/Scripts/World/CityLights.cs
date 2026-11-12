using System.Collections.Generic;
using UnityEngine;

namespace NeonMeridian
{
    /// <summary>
    /// The city owns several hundred lights; only the ones near the camera are switched on, fading in and out
    /// with distance so nothing pops. Also runs the neon flicker.
    /// </summary>
    public sealed class CityLights : MonoBehaviour
    {
        public int maxActive = 56;
        public float radius = 120f;

        City city;
        Transform viewer;
        float timer;
        readonly List<CityLight> active = new List<CityLight>();
        readonly List<Candidate> candidates = new List<Candidate>();

        struct Candidate
        {
            public CityLight light;
            public float score;
        }

        public void Init(City c, Transform view)
        {
            city = c;
            viewer = view;
            timer = 0f;
        }

        public void SetViewer(Transform view)
        {
            viewer = view;
        }

        void Update()
        {
            if (city == null || viewer == null) return;

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                timer = 0.2f;
                Reselect();
            }

            Vector3 vp = viewer.position;
            float t = Time.time;
            for (int i = 0; i < active.Count; i++)
            {
                CityLight cl = active[i];
                float d = Vector3.Distance(cl.position, vp);
                float fade = Mathf.Clamp01((radius - d) / (radius * 0.25f));
                float flick = 1f;
                if (cl.flicker > 0f)
                {
                    // slow breathing plus the occasional hard drop-out of a tired tube
                    float n = Mathf.PerlinNoise(t * 7f + cl.phase, cl.phase * 1.7f);
                    float drop = n > 0.66f ? 1f : 0f;
                    flick = 1f - cl.flicker * (0.35f * Mathf.PerlinNoise(t * 1.3f, cl.phase) + 0.9f * drop);
                    if (flick < 0.05f) flick = 0.05f;
                }
                cl.light.intensity = cl.baseIntensity * fade * flick;
            }
        }

        void Reselect()
        {
            Vector3 vp = viewer.position;
            Vector3 fwd = viewer.forward;
            float r2 = radius * radius;

            candidates.Clear();
            List<CityLight> all = city.lights;
            for (int i = 0; i < all.Count; i++)
            {
                CityLight cl = all[i];
                Vector3 to = cl.position - vp;
                float d2 = to.sqrMagnitude;
                if (d2 > r2) continue;
                // lights well behind the camera cannot reach anything on screen
                float score = d2;
                if (d2 > 30f * 30f && Vector3.Dot(to, fwd) < 0f) score *= 5f;
                candidates.Add(new Candidate { light = cl, score = score });
            }
            candidates.Sort((a, b) => a.score.CompareTo(b.score));

            for (int i = 0; i < active.Count; i++) active[i].light.enabled = false;
            active.Clear();
            int count = Mathf.Min(maxActive, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                CityLight cl = candidates[i].light;
                cl.light.enabled = true;
                active.Add(cl);
            }
        }
    }

    /// <summary>Cycles the two shared signal materials: green, amber, red for north-south, the opposite for east-west.</summary>
    public sealed class TrafficSignals : MonoBehaviour
    {
        public static TrafficSignals Instance { get; private set; }

        public const float GreenTime = 9f, AmberTime = 2f;
        public const float Cycle = (GreenTime + AmberTime) * 2f;

        static readonly Color Green = new Color(0.15f, 3.2f, 0.9f);
        static readonly Color Amber = new Color(3.2f, 2.1f, 0.2f);
        static readonly Color Red = new Color(3.4f, 0.2f, 0.18f);

        Material ns, ew;
        int lastPhase = -1;

        public void Init(Material signalNS, Material signalEW)
        {
            Instance = this;
            ns = signalNS;
            ew = signalEW;
        }

        /// <summary>0 = NS green, 1 = NS amber, 2 = EW green, 3 = EW amber.</summary>
        public int Phase
        {
            get
            {
                float t = Mathf.Repeat(Time.time, Cycle);
                if (t < GreenTime) return 0;
                if (t < GreenTime + AmberTime) return 1;
                if (t < GreenTime * 2f + AmberTime) return 2;
                return 3;
            }
        }

        /// <summary>May a vehicle travelling along Z (north-south) or along X enter the junction right now?</summary>
        public bool CanGo(bool alongZ)
        {
            int p = Phase;
            return alongZ ? p == 0 : p == 2;
        }

        void Update()
        {
            int p = Phase;
            if (p == lastPhase || ns == null) return;
            lastPhase = p;
            Mats.SetGlow(ns, p == 0 ? Green : p == 1 ? Amber : Red);
            Mats.SetGlow(ew, p == 2 ? Green : p == 3 ? Amber : Red);
        }
    }
}
