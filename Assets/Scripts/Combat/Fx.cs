using UnityEngine;
using UnityEngine.Rendering;

namespace NeonMeridian
{
    /// <summary>Camera shake accumulator. Anything can add trauma; the player camera consumes it.</summary>
    public static class ScreenShake
    {
        public static float Trauma { get; private set; }

        public static void Add(float amount)
        {
            Trauma = Mathf.Clamp01(Trauma + amount);
        }

        /// <summary>Shake that weakens with distance from the listener.</summary>
        public static void AddAt(Vector3 position, float amount, float radius)
        {
            Camera cam = GameDirector.Instance != null ? GameDirector.Instance.Cam : null;
            if (cam == null) return;
            float d = Vector3.Distance(cam.transform.position, position);
            if (d < radius) Add(amount * (1f - d / radius));
        }

        public static void Decay(float dt)
        {
            Trauma = Mathf.MoveTowards(Trauma, 0f, dt * 1.6f);
        }
    }

    /// <summary>
    /// All transient effects - tracers, sparks, smoke, flashes, shockwaves - run through a few shared, pooled
    /// systems so a firefight never allocates or spawns objects.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        static Fx instance;

        ParticleSystem sparks, smoke;

        struct TracerSlot
        {
            public LineRenderer line;
            public float life, maxLife;
            public Color color;
        }

        struct FlashSlot
        {
            public Light light;
            public float life, maxLife, intensity;
        }

        struct RingSlot
        {
            public Transform transform;
            public Material material;
            public float life, maxLife, size;
            public Color color;
        }

        readonly TracerSlot[] tracers = new TracerSlot[56];
        readonly FlashSlot[] flashes = new FlashSlot[12];
        readonly RingSlot[] rings = new RingSlot[10];
        int tracerNext, flashNext, ringNext;

        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static void Init(Transform parent)
        {
            if (instance != null) return;
            GameObject go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            instance = go.AddComponent<Fx>();
            instance.Build();
        }

        void Build()
        {
            sparks = MakeSystem("Sparks", Mats.Additive(Mats.StreakTex, Color.white, 2.2f), 1500, true, 1.3f);
            smoke = MakeSystem("Smoke", Mats.Alpha(Mats.SmokeTex, Color.white, 0.6f), 500, false, -0.04f);

            Material tracerMat = Mats.Additive(Mats.BeamTex, Color.white, 2.6f);
            for (int i = 0; i < tracers.Length; i++)
            {
                GameObject go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.sharedMaterial = tracerMat;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.View;
                lr.enabled = false;
                tracers[i].line = lr;
            }

            for (int i = 0; i < flashes.Length; i++)
            {
                GameObject go = new GameObject("Flash");
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.enabled = false;
                flashes[i].light = l;
            }

            for (int i = 0; i < rings.Length; i++)
            {
                Material m = Mats.Additive(Mats.RingTex, Color.white, 2f);
                GameObject go = Prims.Make(Prims.Quad, transform, Vector3.zero, Vector3.one, m, "Ring");
                go.SetActive(false);
                rings[i].transform = go.transform;
                rings[i].material = m;
            }
        }

        ParticleSystem MakeSystem(string name, Material material, int max, bool stretched, float gravity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            if (stretched)
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            else
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = stretched
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f))
                : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.45f, 1f, 1.6f));

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (stretched)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f;
                r.lengthScale = 1f;
            }
            else
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
            }

            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ public API

        public static void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.04f, float life = 0.07f)
        {
            if (instance == null) return;
            ref TracerSlot s = ref instance.tracers[instance.tracerNext];
            instance.tracerNext = (instance.tracerNext + 1) % instance.tracers.Length;
            s.life = s.maxLife = life;
            s.color = color;
            s.line.enabled = true;
            s.line.SetPosition(0, from);
            s.line.SetPosition(1, to);
            s.line.widthMultiplier = width;
            s.line.startColor = color;
            s.line.endColor = color;
        }

        public static void Sparks(Vector3 position, Vector3 normal, Color color, int count, float speed = 7f, float life = 0.4f, float size = 0.05f)
        {
            if (instance == null) return;
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (normal + Random.insideUnitSphere * 0.9f).normalized;
                ep.position = position + normal * 0.03f;
                ep.velocity = dir * speed * Random.Range(0.35f, 1f);
                ep.startLifetime = life * Random.Range(0.5f, 1.2f);
                ep.startSize = size * Random.Range(0.6f, 1.3f);
                ep.startColor = color;
                instance.sparks.Emit(ep, 1);
            }
        }

        public static void Smoke(Vector3 position, Vector3 velocity, Color color, int count, float size, float life = 1.6f, float spread = 0.3f)
        {
            if (instance == null) return;
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = position + Random.insideUnitSphere * spread;
                ep.velocity = velocity + Random.insideUnitSphere * 0.5f;
                ep.startLifetime = life * Random.Range(0.7f, 1.3f);
                ep.startSize = size * Random.Range(0.7f, 1.4f);
                ep.startColor = color;
                ep.rotation = Random.Range(0f, 360f);
                instance.smoke.Emit(ep, 1);
            }
        }

        public static void Flash(Vector3 position, Color color, float intensity, float range, float life = 0.09f)
        {
            if (instance == null) return;
            ref FlashSlot s = ref instance.flashes[instance.flashNext];
            instance.flashNext = (instance.flashNext + 1) % instance.flashes.Length;
            s.life = s.maxLife = life;
            s.intensity = intensity;
            s.light.transform.position = position;
            s.light.color = color;
            s.light.range = range;
            s.light.intensity = intensity;
            s.light.enabled = true;
        }

        /// <summary>Expanding ring lying in the plane perpendicular to <paramref name="normal"/>.</summary>
        public static void Ring(Vector3 position, Vector3 normal, Color color, float size, float life = 0.35f)
        {
            if (instance == null) return;
            ref RingSlot s = ref instance.rings[instance.ringNext];
            instance.ringNext = (instance.ringNext + 1) % instance.rings.Length;
            s.life = s.maxLife = life;
            s.size = size;
            s.color = color;
            s.transform.position = position;
            s.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);
            s.transform.localScale = Vector3.one * 0.1f;
            s.transform.gameObject.SetActive(true);
        }

        /// <summary>Bullet hitting a surface.</summary>
        public static void Impact(Vector3 point, Vector3 normal, Color color, bool heavy = false)
        {
            Sparks(point, normal, Color.Lerp(color, Color.white, 0.5f), heavy ? 14 : 6, heavy ? 9f : 6f, 0.32f);
            Smoke(point + normal * 0.1f, normal * 0.6f, new Color(0.5f, 0.5f, 0.56f, 0.35f), heavy ? 2 : 1, 0.35f, 0.9f, 0.05f);
            if (heavy) Flash(point + normal * 0.2f, color, 26f, 5f, 0.06f);
        }

        public static void Explosion(Vector3 position, float radius, Color color)
        {
            Flash(position + Vector3.up * 0.5f, Color.Lerp(color, Color.white, 0.4f), 900f, radius * 3.2f, 0.22f);
            Sparks(position, Vector3.up, Color.Lerp(color, Color.white, 0.3f), 46, radius * 2.4f, 0.7f, 0.09f);
            Smoke(position + Vector3.up * 0.5f, Vector3.up * 1.6f, new Color(0.16f, 0.15f, 0.18f, 0.8f), 12, radius * 0.6f, 2.4f, radius * 0.25f);
            Smoke(position + Vector3.up * 0.3f, Vector3.up * 0.4f, new Color(color.r, color.g * 0.8f, color.b * 0.6f, 0.5f), 5, radius * 0.4f, 0.5f, radius * 0.15f);
            Ring(position + Vector3.up * 0.15f, Vector3.up, color, radius * 2.4f, 0.4f);
            ScreenShake.AddAt(position, 0.75f, radius * 5f);
            Crowd.Scare(position, 45f);
            Sound.PlayAt(Sfx.Explosion, position, 1f, Random.Range(0.9f, 1.1f), 140f);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < tracers.Length; i++)
            {
                if (tracers[i].life <= 0f) continue;
                tracers[i].life -= dt;
                if (tracers[i].life <= 0f)
                {
                    tracers[i].line.enabled = false;
                    continue;
                }
                Color c = tracers[i].color;
                c.a *= tracers[i].life / tracers[i].maxLife;
                tracers[i].line.startColor = c;
                tracers[i].line.endColor = c;
            }

            for (int i = 0; i < flashes.Length; i++)
            {
                if (flashes[i].life <= 0f) continue;
                flashes[i].life -= dt;
                if (flashes[i].life <= 0f)
                {
                    flashes[i].light.enabled = false;
                    continue;
                }
                flashes[i].light.intensity = flashes[i].intensity * (flashes[i].life / flashes[i].maxLife);
            }

            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i].life <= 0f) continue;
                rings[i].life -= dt;
                if (rings[i].life <= 0f)
                {
                    rings[i].transform.gameObject.SetActive(false);
                    continue;
                }
                float k = 1f - rings[i].life / rings[i].maxLife;
                rings[i].transform.localScale = Vector3.one * Mathf.Lerp(0.1f, rings[i].size, 1f - (1f - k) * (1f - k));
                Color c = rings[i].color * (1f - k);
                rings[i].material.SetColor(ColorId, c);
            }
        }
    }
}
