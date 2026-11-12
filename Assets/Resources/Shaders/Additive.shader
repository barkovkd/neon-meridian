// Additive HDR glow: muzzle flashes, tracers, sparks, light cones, glow sprites, rain.
// Fades with the scene's exp2 fog so distant glows sink into the haze.
Shader "NeonMeridian/Additive"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        [HDR] _Color("Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Float) = 1
        _NearFade("Near Fade Distance", Float) = 0
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
        Cull Off

        Pass
        {
            Name "Additive"

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
                half _NearFade;
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
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                float viewDepth = output.positionCS.w;
                float f = unity_FogParams.x * viewDepth;
                half fade = saturate(exp2(-f * f));
                if (_NearFade > 0.0)
                    fade *= saturate(viewDepth / _NearFade);

                output.color = input.color;
                output.color.a *= fade;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half strength = tex.a * _Color.a * input.color.a * _Intensity;
                half3 rgb = tex.rgb * _Color.rgb * input.color.rgb * strength;
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
