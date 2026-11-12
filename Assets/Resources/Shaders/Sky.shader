// Night sky for an overcast megacity: dark zenith, city glow at the horizon, drifting clouds lit from below,
// plus a uniform flash term for lightning. The horizon colour is meant to match the fog colour.
Shader "NeonMeridian/Sky"
{
    Properties
    {
        _ZenithColor("Zenith", Color) = (0.01, 0.012, 0.03, 1)
        _HorizonColor("Horizon (match fog)", Color) = (0.07, 0.04, 0.12, 1)
        _GlowColor("City glow", Color) = (0.45, 0.12, 0.38, 1)
        _GlowColor2("City glow 2", Color) = (0.05, 0.3, 0.45, 1)
        _CloudColor("Cloud tint", Color) = (0.2, 0.1, 0.26, 1)
        _CloudScale("Cloud scale", Float) = 1.6
        _CloudSpeed("Cloud speed", Float) = 0.012
        _Flash("Lightning flash", Float) = 0
        _FlashDir("Lightning direction", Vector) = (0.3, 0.4, 0.8, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GlowColor;
                half4 _GlowColor2;
                half4 _CloudColor;
                float4 _FlashDir;
                half _CloudScale;
                half _CloudSpeed;
                half _Flash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.dir = input.positionOS.xyz;
                return output;
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += amp * Noise(p);
                    p = p * 2.03 + float2(17.1, 9.2);
                    amp *= 0.5;
                }
                return v;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d = normalize(input.dir);
                float up = d.y;

                // base gradient
                float h = saturate(up);
                half3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(h, 0.45));

                // city glow hugging the horizon, two tints on opposite sides
                float glow = exp(-max(up, 0.0) * 7.0);
                float side = 0.5 + 0.5 * d.x;
                col += lerp(_GlowColor2.rgb, _GlowColor.rgb, side) * glow * 0.5;

                // clouds: project the direction onto a plane overhead
                float2 cuv = d.xz / max(up + 0.18, 0.05) * _CloudScale;
                cuv += _Time.y * _CloudSpeed * float2(1.0, 0.35);
                float cloud = Fbm(cuv);
                cloud = smoothstep(0.38, 0.85, cloud);
                float cloudFade = saturate(up * 3.0 + 0.15);
                half3 cloudLit = _CloudColor.rgb * (0.35 + 0.9 * glow) + lerp(_GlowColor2.rgb, _GlowColor.rgb, side) * 0.12;
                col = lerp(col, col + cloudLit, cloud * cloudFade);

                // lightning: whole sky lifts, brightest around the strike direction
                float fd = saturate(dot(d, normalize(_FlashDir.xyz)));
                col += _Flash * (0.25 + 1.6 * pow(fd, 6.0)) * (0.5 + cloud) * half3(0.55, 0.6, 1.0);

                // below the horizon: just the fog colour
                col = lerp(col, _HorizonColor.rgb, saturate(-up * 8.0));

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
