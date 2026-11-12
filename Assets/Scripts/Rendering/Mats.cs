using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NeonMeridian
{
    /// <summary>
    /// Shader-variant templates. A player build only contains the shader variants that some material asset uses, so
    /// every keyword combination the game needs exists as a small material asset in Resources/Materials (created by
    /// the editor setup script). Runtime materials are always clones of one of these.
    /// </summary>
    public enum MatTemplate
    {
        LitOpaque,
        LitEmissive,
        LitSmoothA,          // smoothness read from albedo alpha
        LitEmissiveSmoothA,
        LitNormalSmoothA,    // + normal map
        LitTransparent,
        UnlitOpaque,
        UnlitCutout
    }

    public static class MaterialTemplates
    {
        public const string LitShader = "Universal Render Pipeline/Lit";
        public const string UnlitShader = "Universal Render Pipeline/Unlit";
        public const string ResourceFolder = "Materials/";

        public static string ShaderFor(MatTemplate t) => t >= MatTemplate.UnlitOpaque ? UnlitShader : LitShader;

        /// <summary>
        /// Puts a fresh Lit/Unlit material into the state the template stands for. Properties and keywords are kept
        /// consistent with each other because URP re-derives keywords from properties when it validates materials.
        /// </summary>
        public static void Configure(Material m, MatTemplate t, Texture flatNormal)
        {
            m.SetColor("_BaseColor", Color.white);
            switch (t)
            {
                case MatTemplate.LitEmissive:
                    Emissive(m);
                    break;
                case MatTemplate.LitSmoothA:
                    SmoothFromAlbedoAlpha(m);
                    break;
                case MatTemplate.LitEmissiveSmoothA:
                    Emissive(m);
                    SmoothFromAlbedoAlpha(m);
                    break;
                case MatTemplate.LitNormalSmoothA:
                    SmoothFromAlbedoAlpha(m);
                    m.SetTexture("_BumpMap", flatNormal);
                    m.EnableKeyword("_NORMALMAP");
                    break;
                case MatTemplate.LitTransparent:
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_BlendModePreserveSpecular", 1f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.One);
                    m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)RenderQueue.Transparent;
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.SetShaderPassEnabled("ShadowCaster", false);
                    break;
                case MatTemplate.UnlitCutout:
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.3f);
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                    m.EnableKeyword("_ALPHATEST_ON");
                    break;
            }
        }

        static void Emissive(Material m)
        {
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            m.EnableKeyword("_EMISSION");
        }

        static void SmoothFromAlbedoAlpha(Material m)
        {
            m.SetFloat("_SmoothnessTextureChannel", 1f);
            m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        }
    }

    /// <summary>Runtime material factory. Everything visible in the game gets its material from here.</summary>
    public static class Mats
    {
        static readonly Material[] templates = new Material[8];
        static Shader additive, alphaBlend, hologram, screen, sky;
        static bool ready;

        static readonly Dictionary<long, Material> litCache = new Dictionary<long, Material>();
        static readonly Dictionary<long, Material> glowCache = new Dictionary<long, Material>();

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int NearFadeId = Shader.PropertyToID("_NearFade");

        public static Texture2D GlowTex { get; private set; }
        public static Texture2D RingTex { get; private set; }
        public static Texture2D StreakTex { get; private set; }
        public static Texture2D SmokeTex { get; private set; }
        public static Texture2D ConeTex { get; private set; }
        public static Texture2D FlareTex { get; private set; }
        public static Texture2D BeamTex { get; private set; }

        public static void Init()
        {
            if (ready) return;
            ready = true;

            Texture2D flat = Resources.Load<Texture2D>(MaterialTemplates.ResourceFolder + "FlatNormal");
            int missing = 0;
            for (int i = 0; i < templates.Length; i++)
            {
                MatTemplate t = (MatTemplate)i;
                Material m = Resources.Load<Material>(MaterialTemplates.ResourceFolder + t);
                if (m == null)
                {
                    // Editor fallback: all shader variants are available there, so the template can be made on the fly.
                    missing++;
                    Shader s = Shader.Find(MaterialTemplates.ShaderFor(t));
                    if (s == null)
                    {
                        Debug.LogError("Shader not found: " + MaterialTemplates.ShaderFor(t));
                        s = Shader.Find("Hidden/InternalErrorShader");
                    }
                    m = new Material(s) { name = t.ToString() };
                    if (flat == null)
                    {
                        flat = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
                        Color32[] px = new Color32[16];
                        for (int k = 0; k < px.Length; k++) px[k] = new Color32(128, 128, 255, 255);
                        flat.SetPixels32(px);
                        flat.Apply();
                    }
                    MaterialTemplates.Configure(m, t, flat);
                }
                templates[i] = m;
            }
            if (missing > 0)
                Debug.LogWarning("Material templates missing in Resources/Materials (" + missing + "). Run 'Neon Meridian > Setup Project' before building a player.");

            additive = LoadShader("Additive");
            alphaBlend = LoadShader("AlphaBlend");
            hologram = LoadShader("Hologram");
            screen = LoadShader("Screen");
            sky = LoadShader("Sky");

            GlowTex = ProcTextures.GlowDot();
            RingTex = ProcTextures.Ring();
            StreakTex = ProcTextures.Streak();
            SmokeTex = ProcTextures.Smoke(11);
            ConeTex = ProcTextures.LightCone();
            FlareTex = ProcTextures.Flare();
            BeamTex = ProcTextures.Beam();
        }

        static Shader LoadShader(string name)
        {
            Shader s = Resources.Load<Shader>("Shaders/" + name);
            if (s == null) s = Shader.Find("NeonMeridian/" + name);
            if (s == null) Debug.LogError("Shader missing: NeonMeridian/" + name);
            return s;
        }

        static Material Clone(MatTemplate t, string name)
        {
            return new Material(templates[(int)t]) { name = name };
        }

        static long Key(Color c, float a, float b)
        {
            long r = Mathf.Clamp(Mathf.RoundToInt(c.r * 63f), 0, 1023);
            long g = Mathf.Clamp(Mathf.RoundToInt(c.g * 63f), 0, 1023);
            long bl = Mathf.Clamp(Mathf.RoundToInt(c.b * 63f), 0, 1023);
            long al = Mathf.Clamp(Mathf.RoundToInt(c.a * 63f), 0, 1023);
            long ka = Mathf.Clamp(Mathf.RoundToInt(a * 100f), 0, 1023);
            long kb = Mathf.Clamp(Mathf.RoundToInt(b * 100f), 0, 1023);
            return r | (g << 10) | (bl << 20) | (al << 30) | (ka << 40) | (kb << 50);
        }

        // ------------------------------------------------------------------ lit

        /// <summary>Plain lit surface. Cached, so identical requests share one material.</summary>
        public static Material Lit(Color color, float metallic = 0f, float smoothness = 0.4f)
        {
            long key = Key(color, metallic, smoothness);
            if (litCache.TryGetValue(key, out Material m) && m != null) return m;
            m = Clone(MatTemplate.LitOpaque, "Lit");
            m.SetColor(BaseColorId, color);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, smoothness);
            litCache[key] = m;
            return m;
        }

        /// <summary>Lit surface with an HDR emission colour. Not cached - callers usually animate it.</summary>
        public static Material Emissive(Color baseColor, Color emission, float metallic = 0f, float smoothness = 0.4f)
        {
            Material m = Clone(MatTemplate.LitEmissive, "Emissive");
            m.SetColor(BaseColorId, baseColor);
            m.SetColor(EmissionColorId, emission);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, smoothness);
            return m;
        }

        public static void SetEmission(Material m, Color emission)
        {
            m.SetColor(EmissionColorId, emission);
        }

        public static Material Glass(Color tint, float alpha, float smoothness = 0.93f)
        {
            Material m = Clone(MatTemplate.LitTransparent, "Glass");
            tint.a = alpha;
            m.SetColor(BaseColorId, tint);
            m.SetFloat(MetallicId, 0f);
            m.SetFloat(SmoothnessId, smoothness);
            return m;
        }

        public static Material Facade(FacadeTextures tex, Color tint, Color emission, float metallic)
        {
            Material m = Clone(MatTemplate.LitEmissiveSmoothA, tex.albedo.name);
            m.SetTexture(BaseMapId, tex.albedo);
            m.SetColor(BaseColorId, tint);
            m.SetTexture(EmissionMapId, tex.emission);
            m.SetColor(EmissionColorId, emission);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, 1f);
            return m;
        }

        /// <summary>Tiled ground: albedo with smoothness in alpha, plus a normal map.</summary>
        public static Material Ground(Texture2D albedo, Texture2D normal, Color tint, float normalScale = 1f, float smoothnessScale = 1f)
        {
            Material m = Clone(MatTemplate.LitNormalSmoothA, albedo.name);
            m.SetTexture(BaseMapId, albedo);
            m.SetColor(BaseColorId, tint);
            m.SetTexture(BumpMapId, normal);
            m.SetFloat(BumpScaleId, normalScale);
            m.SetFloat(MetallicId, 0f);
            m.SetFloat(SmoothnessId, smoothnessScale);
            return m;
        }

        public static Material Textured(Texture2D albedo, Color tint, float metallic = 0f, float smoothnessScale = 1f)
        {
            Material m = Clone(MatTemplate.LitSmoothA, albedo.name);
            m.SetTexture(BaseMapId, albedo);
            m.SetColor(BaseColorId, tint);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(SmoothnessId, smoothnessScale);
            return m;
        }

        // ------------------------------------------------------------------ unlit / glow

        /// <summary>Opaque unlit HDR colour - neon tubes, lamp heads, LEDs. Cached.</summary>
        public static Material Glow(Color hdrColor)
        {
            long key = Key(hdrColor * 0.1f, hdrColor.maxColorComponent * 0.05f, 0f);
            if (glowCache.TryGetValue(key, out Material m) && m != null) return m;
            m = Clone(MatTemplate.UnlitOpaque, "Glow");
            m.SetColor(BaseColorId, hdrColor);
            glowCache[key] = m;
            return m;
        }

        /// <summary>Own (uncached) glow material for things that change colour.</summary>
        public static Material GlowInstance(Color hdrColor)
        {
            Material m = Clone(MatTemplate.UnlitOpaque, "GlowInstance");
            m.SetColor(BaseColorId, hdrColor);
            return m;
        }

        public static void SetGlow(Material m, Color hdrColor)
        {
            m.SetColor(BaseColorId, hdrColor);
        }

        /// <summary>Opaque unlit textured surface (baked sign text on a black panel).</summary>
        public static Material Sign(Texture texture, float intensity)
        {
            Material m = Clone(MatTemplate.UnlitOpaque, "Sign");
            m.SetTexture(BaseMapId, texture);
            m.SetColor(BaseColorId, new Color(intensity, intensity, intensity, 1f));
            return m;
        }

        public static Material SignCutout(Texture texture, float intensity)
        {
            Material m = Clone(MatTemplate.UnlitCutout, "SignCutout");
            m.SetTexture(BaseMapId, texture);
            m.SetColor(BaseColorId, new Color(intensity, intensity, intensity, 1f));
            return m;
        }

        // ------------------------------------------------------------------ custom shaders

        public static Material Additive(Texture texture, Color hdrColor, float intensity = 1f, float nearFade = 0f)
        {
            Material m = new Material(additive) { name = "Additive" };
            if (texture != null) m.SetTexture(MainTexId, texture);
            m.SetColor(ColorId, hdrColor);
            m.SetFloat(IntensityId, intensity);
            m.SetFloat(NearFadeId, nearFade);
            return m;
        }

        public static Material Alpha(Texture texture, Color color, float nearFade = 0f)
        {
            Material m = new Material(alphaBlend) { name = "AlphaBlend" };
            if (texture != null) m.SetTexture(MainTexId, texture);
            m.SetColor(ColorId, color);
            m.SetFloat(NearFadeId, nearFade);
            return m;
        }

        public static Material Holo(Texture texture, Color hdrColor, float intensity = 1f, float scanDensity = 14f, float glitch = 0.2f, float edgeFade = 0f,
            bool twoSided = true)
        {
            Material m = new Material(hologram) { name = "Hologram" };
            if (texture != null) m.SetTexture(MainTexId, texture);
            m.SetColor(ColorId, hdrColor);
            m.SetFloat(IntensityId, intensity);
            m.SetFloat("_ScanDensity", scanDensity);
            m.SetFloat("_Glitch", glitch);
            m.SetFloat("_EdgeFade", edgeFade);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            m.SetFloat("_Seed", Random.value * 10f);
            return m;
        }

        /// <summary>Animated LED billboard, see Screen.shader for the styles.</summary>
        public static Material Screen(Texture text, Color colorA, Color colorB, int style, float aspect, Vector2 ledGrid, Vector4 texRect,
            float intensity = 2.2f, float speed = 1f, float seed = 0f)
        {
            Material m = new Material(screen) { name = "Screen" };
            if (text != null) m.SetTexture(MainTexId, text);
            m.SetColor("_ColorA", colorA);
            m.SetColor("_ColorB", colorB);
            m.SetFloat("_Style", style);
            m.SetFloat("_Speed", speed);
            m.SetFloat("_Seed", seed);
            m.SetFloat(IntensityId, intensity);
            m.SetVector("_Pixels", new Vector4(ledGrid.x, ledGrid.y, aspect, 0f));
            m.SetVector("_TexRect", texRect);
            return m;
        }

        public static Material Sky()
        {
            return new Material(sky) { name = "Sky" };
        }
    }
}
