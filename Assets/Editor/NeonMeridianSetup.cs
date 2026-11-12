using System;
using System.IO;
using NeonMeridian;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Everything the project needs besides the scripts is generated here: the shader-variant template materials, the
/// post-processing profile, render pipeline settings and the (nearly empty) Main scene.
///   Menu: Neon Meridian > Setup Project / Build macOS App
///   Batch: -executeMethod NeonMeridianSetup.BatchSetup | NeonMeridianSetup.BatchBuild
/// </summary>
public static class NeonMeridianSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string MaterialFolder = "Assets/Resources/Materials";
    const string ProfilePath = "Assets/Resources/GameVolumeProfile.asset";
    const string PipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string BuildPath = "Builds/Neon Meridian.app";

    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(MaterialFolder + "/" + MatTemplate.LitOpaque + ".mat") || !File.Exists(ScenePath)) Setup();
        };
    }

    [MenuItem("Neon Meridian/Setup Project")]
    public static void Setup()
    {
        EnsureMaterials();
        EnsureVolumeProfile();
        ConfigurePipeline();
        ConfigurePlayer();
        EnsureScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Setup] Neon Meridian project is ready.");
    }

    [MenuItem("Neon Meridian/Build macOS App")]
    public static void BuildMac()
    {
        Setup();
        Build();
    }

    public static void BatchSetup()
    {
        RunBatch(Setup);
    }

    public static void BatchBuild()
    {
        RunBatch(() =>
        {
            Setup();
            Build();
        });
    }

    static void RunBatch(Action action)
    {
        try
        {
            action();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[Setup] FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }

    // ---------------------------------------------------------------------- materials

    static void EnsureMaterials()
    {
        Directory.CreateDirectory(MaterialFolder);
        AssetDatabase.Refresh();

        string flatPath = MaterialFolder + "/FlatNormal.asset";
        Texture2D flat = AssetDatabase.LoadAssetAtPath<Texture2D>(flatPath);
        if (flat == null)
        {
            flat = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "FlatNormal" };
            Color32[] px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(128, 128, 255, 255);
            flat.SetPixels32(px);
            flat.Apply();
            AssetDatabase.CreateAsset(flat, flatPath);
        }

        foreach (MatTemplate t in Enum.GetValues(typeof(MatTemplate)))
        {
            string path = MaterialFolder + "/" + t + ".mat";
            Shader shader = Shader.Find(MaterialTemplates.ShaderFor(t));
            if (shader == null) throw new Exception("Shader not found: " + MaterialTemplates.ShaderFor(t));

            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = t.ToString() };
                MaterialTemplates.Configure(m, t, flat);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = shader;
            }
            MaterialTemplates.Configure(m, t, flat);
            EditorUtility.SetDirty(m);
            Debug.Log("[Setup] template " + t + " keywords: [" + string.Join(" ", m.shaderKeywords) + "] queue " + m.renderQueue);
        }
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------------- post-processing profile

    static void EnsureVolumeProfile()
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) != null) AssetDatabase.DeleteAsset(ProfilePath);

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        Type[] types =
        {
            typeof(Bloom), typeof(Tonemapping), typeof(ColorAdjustments), typeof(Vignette), typeof(ChromaticAberration),
            typeof(FilmGrain), typeof(LensDistortion), typeof(SplitToning), typeof(WhiteBalance),
            typeof(Bloom).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceReflectionVolumeSettings")
        };
        foreach (Type type in types)
        {
            if (type == null) continue;
            VolumeComponent component = profile.Add(type, true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        // the values are set from code at runtime (PostFx); a visible base keeps the profile meaningful in the editor
        if (profile.TryGet(out Bloom bloom)) bloom.intensity.value = 0.9f;
        if (profile.TryGet(out Tonemapping tone)) tone.mode.value = TonemappingMode.ACES;
        if (profile.TryGet(out FilmGrain grain)) grain.intensity.value = 0.2f;
        EditorUtility.SetDirty(profile);
    }

    // ---------------------------------------------------------------------- render pipeline

    static void ConfigurePipeline()
    {
        UniversalRenderPipelineAsset urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (urp == null) throw new Exception("URP asset missing at " + PipelineAssetPath);

        SerializedObject so = new SerializedObject(urp);
        SetInt(so, "m_MSAA", 1);
        SetFloat(so, "m_RenderScale", 1f);
        SetBool(so, "m_SupportsHDR", true);
        SetBool(so, "m_RequireDepthTexture", true);
        SetBool(so, "m_RequireOpaqueTexture", false);
        SetBool(so, "m_MainLightShadowsSupported", false);
        SetBool(so, "m_AdditionalLightShadowsSupported", false);
        SetBool(so, "m_SoftShadowsSupported", false);
        SetBool(so, "m_SupportsLightCookies", false);
        SetBool(so, "m_SupportsLightLayers", false);
        SetBool(so, "m_SupportDataDrivenLensFlare", false);
        SetBool(so, "m_SupportScreenSpaceLensFlare", false);
        SetBool(so, "m_UseAdaptivePerformance", false);
        SetInt(so, "m_ColorGradingMode", 1);          // HDR grading
        SetInt(so, "m_ColorGradingLutSize", 32);
        SetInt(so, "m_AdditionalLightsRenderingMode", 1); // per pixel
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urp);

        UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null) throw new Exception("Renderer asset missing at " + RendererPath);

        // Screen space reflections exist in this URP version behind the URP_SCREEN_SPACE_REFLECTION define
        // (set in Player Settings). Add the renderer feature when the type is compiled in.
        Type ssrType = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceReflectionRendererFeature");
        if (ssrType == null)
        {
            Debug.LogWarning("[Setup] Screen space reflection feature is not available; wet streets will only mirror the sky probe.");
        }
        else
        {
            bool present = false;
            foreach (ScriptableRendererFeature f in renderer.rendererFeatures)
                if (f != null && f.GetType() == ssrType) present = true;

            if (!present)
            {
                ScriptableRendererFeature feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssrType);
                feature.name = "ScreenSpaceReflection";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

                SerializedObject rso = new SerializedObject(renderer);
                SerializedProperty features = rso.FindProperty("m_RendererFeatures");
                SerializedProperty map = rso.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
                Debug.Log("[Setup] added the screen space reflection renderer feature");
            }
        }

        AssetDatabase.SaveAssets();
    }

    static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) Debug.LogWarning("[Setup] property not found: " + name);
        else p.boolValue = value;
    }

    static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) Debug.LogWarning("[Setup] property not found: " + name);
        else p.intValue = value;
    }

    static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) Debug.LogWarning("[Setup] property not found: " + name);
        else p.floatValue = value;
    }

    // ---------------------------------------------------------------------- player settings

    static void ConfigurePlayer()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "ARM64");
    }

    // ---------------------------------------------------------------------- scene

    static void EnsureScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject go = new GameObject("GameDirector");
        go.AddComponent<GameDirector>();

        // Fog is switched on in the saved scene too so the fog shader variants are kept in player builds.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = SkyWeather.FogDensity;
        RenderSettings.fogColor = SkyWeather.FogColor;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.09f, 0.085f, 0.17f);
        RenderSettings.ambientEquatorColor = new Color(0.1f, 0.065f, 0.13f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.025f, 0.045f);

        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save " + ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        // leftovers of the project template
        AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
        AssetDatabase.DeleteAsset("Assets/Settings/SampleSceneProfile.asset");
    }

    // ---------------------------------------------------------------------- build

    static void Build()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
            throw new Exception("Build " + summary.result + ", errors: " + summary.totalErrors);
        Debug.Log("[Build] OK: " + summary.outputPath + " (" + (summary.totalSize / (1024 * 1024)) + " MB, " + summary.totalTime + ")");
    }
}
