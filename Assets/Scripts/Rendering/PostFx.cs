using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeonMeridian
{
    /// <summary>
    /// Owns the global post-processing volume: the base night-city grade plus gameplay driven effects
    /// (damage, low health, scanner, time dilation, dash).
    /// </summary>
    public sealed class PostFx : MonoBehaviour
    {
        public static PostFx Instance { get; private set; }

        /// <summary>1 = healthy, 0 = dead. Low values redden and desaturate the picture.</summary>
        [NonSerialized] public float healthFraction = 1f;

        /// <summary>0..1 strength of the scanner look.</summary>
        [NonSerialized] public float scanner;

        /// <summary>0..1 strength of the time-dilation look.</summary>
        [NonSerialized] public float focus;

        /// <summary>0..1 extra darkening used by menus and fades.</summary>
        [NonSerialized] public float dim;

        Volume volume;
        VolumeProfile profile;
        Bloom bloom;
        Tonemapping tonemapping;
        ColorAdjustments color;
        Vignette vignette;
        ChromaticAberration chroma;
        FilmGrain grain;
        LensDistortion lens;
        SplitToning split;
        WhiteBalance balance;

        float hitPulse, dashPulse, scannerSmooth, focusSmooth, dimSmooth;

        const float BaseExposure = 0.3f, BaseContrast = 14f, BaseSaturation = 10f;
        const float BaseVignette = 0.3f, BaseChroma = 0.1f, BaseGrain = 0.2f;

        public void Setup()
        {
            Instance = this;
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            VolumeProfile asset = Resources.Load<VolumeProfile>("GameVolumeProfile");
            if (asset != null)
            {
                volume.sharedProfile = asset;
                profile = volume.profile; // deep copy, the asset itself is never modified
            }
            else
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile = profile;
            }

            bloom = Get<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.96f, 1.04f));
            bloom.highQualityFiltering.Override(false);
            bloom.maxIterations.Override(6);

            tonemapping = Get<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            color = Get<ColorAdjustments>();
            color.postExposure.Override(BaseExposure);
            color.contrast.Override(BaseContrast);
            color.saturation.Override(BaseSaturation);
            color.colorFilter.Override(Color.white);

            vignette = Get<Vignette>();
            vignette.intensity.Override(BaseVignette);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);

            chroma = Get<ChromaticAberration>();
            chroma.intensity.Override(BaseChroma);

            grain = Get<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(BaseGrain);
            grain.response.Override(0.8f);

            lens = Get<LensDistortion>();
            lens.intensity.Override(0f);

            split = Get<SplitToning>();
            split.shadows.Override(new Color(0.42f, 0.5f, 0.6f));
            split.highlights.Override(new Color(0.6f, 0.47f, 0.52f));
            split.balance.Override(-10f);

            balance = Get<WhiteBalance>();
            balance.temperature.Override(-6f);
            balance.tint.Override(6f);

            ConfigureReflections();
        }

        T Get<T>() where T : VolumeComponent
        {
            if (!profile.TryGet(out T component)) component = profile.Add<T>(true);
            component.active = true;
            return component;
        }

        /// <summary>
        /// Screen space reflections ship in this URP version behind a scripting define, so the settings type is looked
        /// up by name. If it is not there the game simply runs without them.
        /// </summary>
        void ConfigureReflections()
        {
            Type type = typeof(Bloom).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceReflectionVolumeSettings");
            if (type == null) return;

            if (!profile.TryGet(type, out VolumeComponent ssr)) ssr = profile.Add(type, true);
            ssr.active = true;
            SetParam(ssr, "reflectionStrength", 1f);
            SetParam(ssr, "minimumSmoothness", 0.3f);
            SetParam(ssr, "smoothnessFadeStart", 0.5f);
            SetParam(ssr, "screenEdgeFadeDistance", 0.12f);
            SetParam(ssr, "clampReflectedColor", true);
            SetParam(ssr, "maxColorValue", 6f);
            SetParam(ssr, "maxRaySteps", 48);
        }

        static void SetParam(VolumeComponent component, string field, object value)
        {
            try
            {
                FieldInfo fi = component.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                if (fi == null) return;
                if (!(fi.GetValue(component) is VolumeParameter p)) return;
                // walk up to the VolumeParameter<T> that declares "value" so overrides cannot make the lookup ambiguous
                Type t = p.GetType();
                PropertyInfo pi = null;
                while (t != null && pi == null)
                {
                    pi = t.GetProperty("value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    t = t.BaseType;
                }
                if (pi == null) return;
                pi.SetValue(p, value);
                p.overrideState = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("PostFx: could not set " + field + ": " + e.Message);
            }
        }

        public void Hit(float strength)
        {
            hitPulse = Mathf.Max(hitPulse, Mathf.Clamp01(strength));
        }

        public void Dash()
        {
            dashPulse = 1f;
        }

        void Update()
        {
            if (profile == null) return;
            float dt = Time.unscaledDeltaTime;
            hitPulse = Mathf.MoveTowards(hitPulse, 0f, dt * 2.6f);
            dashPulse = Mathf.MoveTowards(dashPulse, 0f, dt * 4.5f);
            scannerSmooth = Mathf.MoveTowards(scannerSmooth, scanner, dt * 6f);
            focusSmooth = Mathf.MoveTowards(focusSmooth, focus, dt * 5f);
            dimSmooth = Mathf.MoveTowards(dimSmooth, dim, dt * 3f);

            float hurt = Mathf.Clamp01((0.4f - healthFraction) / 0.4f);
            float beat = hurt > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f) : 0f;

            Color filter = Color.white;
            filter = Color.Lerp(filter, new Color(0.72f, 1f, 1f), scannerSmooth * 0.8f);
            filter = Color.Lerp(filter, new Color(1f, 0.9f, 0.76f), focusSmooth * 0.7f);
            filter = Color.Lerp(filter, new Color(1f, 0.72f, 0.72f), hurt * 0.5f);
            color.colorFilter.value = filter;
            color.saturation.value = BaseSaturation - 28f * scannerSmooth - 14f * focusSmooth - 40f * hurt;
            color.contrast.value = BaseContrast + 10f * focusSmooth + 6f * scannerSmooth;
            color.postExposure.value = BaseExposure - 1.6f * dimSmooth;

            vignette.intensity.value = Mathf.Clamp01(BaseVignette + 0.12f * scannerSmooth + 0.1f * focusSmooth + 0.2f * hurt * (0.6f + 0.4f * beat) + 0.16f * hitPulse);
            vignette.color.value = Color.Lerp(Color.black, new Color(0.55f, 0f, 0.02f), Mathf.Max(hurt, hitPulse));

            chroma.intensity.value = Mathf.Clamp01(BaseChroma + 0.3f * scannerSmooth + 0.45f * focusSmooth + 0.7f * hitPulse + 0.6f * dashPulse);
            grain.intensity.value = Mathf.Clamp01(BaseGrain + 0.2f * scannerSmooth);
            lens.intensity.value = -0.18f * focusSmooth - 0.32f * dashPulse;
        }
    }
}
