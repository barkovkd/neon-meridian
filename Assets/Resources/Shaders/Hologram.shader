// Additive hologram: world-space scanlines, a slow sweep bar, flicker and occasional horizontal glitch.
// Used for floating holo-ads, energy barriers, scanner highlights and waypoint beams.
Shader "NeonMeridian/Hologram"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (0.2, 0.9, 1, 1)
        _Intensity("Intensity", Float) = 1
        _ScanDensity("Scanlines per metre", Float) = 14
        _ScanSpeed("Scanline speed", Float) = 1.5
        _Flicker("Flicker amount", Range(0, 1)) = 0.15
        _Glitch("Glitch amount", Range(0, 1)) = 0.2
        _EdgeFade("Edge fade (uv)", Range(0, 0.5)) = 0.0
        _Seed("Seed", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One One
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            Name "Hologram"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _ScanDensity;
                half _ScanSpeed;
                half _Flicker;
                half _Glitch;
                half _EdgeFade;
                half _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 rawUv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.rawUv = input.uv;

                float f = unity_FogParams.x * output.positionCS.w;
                output.color = input.color;
                output.color.a *= saturate(exp2(-f * f));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _Time.y;

                // horizontal glitch: every so often a band of rows is shifted sideways
                float2 uv = input.uv;
                float band = floor(input.rawUv.y * 24.0);
                float tick = floor(t * 9.0 + _Seed * 13.0);
                float g = Hash11(band * 7.13 + tick * 3.71 + _Seed);
                float burst = step(0.82, Hash11(tick * 0.37 + _Seed * 5.0));
                uv.x += (g - 0.5) * 0.12 * _Glitch * burst * step(0.6, g);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                half scan = 0.62 + 0.38 * sin(input.positionWS.y * _ScanDensity * 6.2831853 - t * _ScanSpeed * 6.2831853);
                half sweep = smoothstep(0.92, 1.0, frac(input.rawUv.y * 0.5 - t * 0.11 + _Seed)) * 0.9;
                half flick = 1.0 - _Flicker * step(0.72, Hash11(floor(t * 17.0) + _Seed * 3.0)) * Hash11(floor(t * 31.0) + _Seed);

                half edge = 1.0;
                if (_EdgeFade > 0.0)
                {
                    float2 e = min(input.rawUv, 1.0 - input.rawUv);
                    edge = saturate(min(e.x, e.y) / _EdgeFade);
                }

                half strength = tex.a * _Color.a * input.color.a * _Intensity * flick * edge;
                half3 rgb = tex.rgb * _Color.rgb * input.color.rgb * strength * (scan + sweep);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
