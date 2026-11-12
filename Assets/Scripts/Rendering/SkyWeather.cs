using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonMeridian
{
    /// <summary>
    /// Night sky, fog, ambient light, the environment reflection, rain that follows the viewer and the occasional
    /// lightning strike.
    /// </summary>
    public sealed class SkyWeather : MonoBehaviour
    {
        public static SkyWeather Instance { get; private set; }

        public static readonly Color FogColor = new Color(0.075f, 0.05f, 0.125f);
        public const float FogDensity = 0.003f;

        /// <summary>Raised when a lightning strike starts; the argument is the delay until the thunder should be heard.</summary>
        public event Action<float> Lightning;

        static readonly Color AmbientSky = new Color(0.09f, 0.085f, 0.17f);
        static readonly Color AmbientEquator = new Color(0.1f, 0.065f, 0.13f);
        static readonly Color AmbientGround = new Color(0.03f, 0.025f, 0.045f);
        const float MoonIntensity = 0.2f;

        Material sky;
        Light moon;
        Transform viewer;
        Transform rain, splashes;
        float nextStrike, strikeTime = -10f;
        Rng rng;

        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static readonly int FlashDirId = Shader.PropertyToID("_FlashDir");

        public void Setup(Transform view, int seed)
        {
            Instance = this;
            viewer = view;
            rng = new Rng(seed * 17 + 3);

            sky = Mats.Sky();
            sky.SetColor("_HorizonColor", FogColor);
            sky.SetColor("_ZenithColor", new Color(0.012f, 0.014f, 0.035f));
            sky.SetColor("_GlowColor", new Color(0.42f, 0.1f, 0.36f));
            sky.SetColor("_GlowColor2", new Color(0.04f, 0.26f, 0.4f));
            sky.SetColor("_CloudColor", new Color(0.17f, 0.09f, 0.24f));

            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;

            // glossy surfaces mirror this instead of Unity's default daylight probe
            Cubemap reflection = ProcTextures.SkyReflection(
                new Color(0.01f, 0.012f, 0.03f), new Color(0.05f, 0.03f, 0.09f),
                new Color(0.3f, 0.07f, 0.26f), new Color(0.03f, 0.18f, 0.28f), new Color(0.012f, 0.01f, 0.02f));
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = 1f;

            GameObject moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(transform, false);
            moonGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.45f, 0.55f, 0.9f);
            moon.intensity = MoonIntensity;
            moon.shadows = LightShadows.None;
            RenderSettings.sun = moon;

            rain = BuildRain();
            splashes = BuildSplashes();
            nextStrike = Time.time + rng.Range(9f, 20f);
        }

        public void SetViewer(Transform view)
        {
            viewer = view;
        }

        Transform BuildRain()
        {
            GameObject go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.62f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.66f, 0.8f, 1f, 0.5f);
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 3000f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 0.5f, 44f);

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-2.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-27f);
            vel.z = new ParticleSystem.MinMaxCurve(-1.2f);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.024f;
            r.lengthScale = 1f;
            r.sharedMaterial = Mats.Additive(Mats.StreakTex, new Color(0.7f, 0.85f, 1f), 0.45f, 1.5f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            return go.transform;
        }

        Transform BuildSplashes()
        {
            GameObject go = new GameObject("RainSplashes");
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.26f);
            main.startColor = new Color(0.62f, 0.78f, 1f, 0.5f);
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 480f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.01f, 30f);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = Mats.Additive(Mats.RingTex, new Color(0.7f, 0.85f, 1f), 0.5f, 0f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;

            ps.Play();
            return go.transform;
        }

        void LateUpdate()
        {
            if (viewer != null)
            {
                Vector3 p = viewer.position;
                rain.position = new Vector3(p.x, p.y + 15f, p.z) + viewer.forward * 6f;
                Vector3 f = viewer.forward;
                f.y = 0f;
                splashes.position = new Vector3(p.x, 0.04f, p.z) + f.normalized * 9f;
            }

            // lightning: a double flash, the sky and the moonlight jump together
            float t = Time.time;
            if (t >= nextStrike)
            {
                nextStrike = t + rng.Range(16f, 44f);
                strikeTime = t;
                Vector3 dir = new Vector3(rng.Range(-1f, 1f), rng.Range(0.25f, 0.6f), rng.Range(-1f, 1f)).normalized;
                sky.SetVector(FlashDirId, dir);
                Lightning?.Invoke(rng.Range(0.8f, 2.6f));
            }

            float age = t - strikeTime;
            float flash = 0f;
            if (age < 1.2f)
            {
                flash = Mathf.Exp(-age * 14f);
                if (age > 0.16f) flash = Mathf.Max(flash, 0.8f * Mathf.Exp(-(age - 0.16f) * 17f));
            }
            sky.SetFloat(FlashId, flash * 0.9f);
            moon.intensity = MoonIntensity + flash * 1.5f;
            float lift = 1f + flash * 2.5f;
            RenderSettings.ambientSkyColor = AmbientSky * lift;
            RenderSettings.ambientEquatorColor = AmbientEquator * lift;
        }
    }
}
