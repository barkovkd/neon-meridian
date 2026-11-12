// Opaque animated LED screen / billboard. Everything is procedural: a baked text texture is combined with one of
// several animated backgrounds. Opaque on purpose (with depth passes) so screens show up in screen space reflections.
//   _Style: 0 ticker, 1 logo + pulse rings, 2 equalizer, 3 synth sun + grid, 4 hazard stripes, 5 data rain
Shader "NeonMeridian/Screen"
{
    Properties
    {
        _MainTex("Text / logo (alpha = mask)", 2D) = "black" {}
        [HDR] _ColorA("Primary colour", Color) = (0.2, 0.9, 1, 1)
        [HDR] _ColorB("Secondary colour", Color) = (1, 0.2, 0.6, 1)
        _Intensity("Intensity", Float) = 2
        _Style("Style", Float) = 0
        _Speed("Speed", Float) = 1
        _Seed("Seed", Float) = 0
        _Pixels("LED grid (cols, rows, aspect, 0)", Vector) = (128, 64, 2, 0)
        _TexRect("Text uv scale (xy) / offset (zw)", Vector) = (1, 1, 0, 0)
        _Flicker("Flicker", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "Unlit"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _ColorA;
            half4 _ColorB;
            float4 _Pixels;
            float4 _TexRect;
            half _Intensity;
            half _Style;
            half _Speed;
            half _Seed;
            half _Flicker;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fog : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float f = unity_FogParams.x * output.positionCS.w;
                output.fog = saturate(exp2(-f * f));
                return output;
            }

            half TextMask(float2 cellUv, float2 uv, float scrollX)
            {
                float2 tuv = cellUv * _TexRect.xy + _TexRect.zw;
                tuv.x += scrollX;
                float2 guv = uv * _TexRect.xy;
                return SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, tuv, ddx(guv), ddy(guv)).a;
            }

            void Frag(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float t = _Time.y * _Speed + _Seed * 17.0;
                float aspect = max(_Pixels.z, 0.05);

                // LED grid quantisation
                float2 grid = max(_Pixels.xy, float2(1.0, 1.0));
                float2 cellUv = (floor(uv * grid) + 0.5) / grid;
                float2 inCell = abs(frac(uv * grid) - 0.5);
                half led = 1.0 - smoothstep(0.3, 0.5, max(inCell.x, inCell.y));
                float fw = max(fwidth(uv.x * grid.x), fwidth(uv.y * grid.y));
                led = lerp(led, 0.72, saturate(fw * 1.6));

                half3 a = _ColorA.rgb;
                half3 b = _ColorB.rgb;
                half3 col = 0;

                if (_Style < 0.5)
                {
                    // 0: ticker - text scrolls sideways over faint moving lines
                    half text = TextMask(cellUv, uv, t * 0.07);
                    half lines = 0.05 + 0.05 * sin(cellUv.y * 46.0 + t * 2.0);
                    col = b * lines + a * text;
                }
                else if (_Style < 1.5)
                {
                    // 1: static logo over expanding pulse rings
                    half text = TextMask(cellUv, uv, 0.0);
                    float2 p = cellUv - 0.5;
                    p.x *= aspect;
                    float r = length(p);
                    half ring = pow(0.5 + 0.5 * cos((r * 5.0 - t * 0.9) * 6.2831853), 8.0) * saturate(1.3 - r * 1.6);
                    col = b * ring * 0.55 + a * text;
                }
                else if (_Style < 2.5)
                {
                    // 2: equalizer bars under the text
                    half text = TextMask(cellUv, uv, 0.0);
                    float bars = 22.0;
                    float bi = floor(cellUv.x * bars);
                    float h = 0.12 + 0.5 * (0.5 + 0.5 * sin(t * (1.5 + Hash11(bi + _Seed) * 3.0) + Hash11(bi * 1.7 + _Seed) * 6.28))
                              * (0.45 + 0.55 * Hash11(bi * 3.1 + floor(t * 2.0)));
                    half bar = step(cellUv.y, h) * step(0.18, frac(cellUv.x * bars));
                    half3 barCol = lerp(b, a, saturate(cellUv.y / 0.6));
                    col = barCol * bar * 0.85 + a * text;
                }
                else if (_Style < 3.5)
                {
                    // 3: striped sun over a perspective grid
                    half text = TextMask(cellUv, uv, 0.0);
                    float2 p = float2((cellUv.x - 0.5) * aspect, cellUv.y);
                    float horizon = 0.4;
                    if (p.y > horizon)
                    {
                        float2 sp = float2(p.x, p.y - horizon - 0.2);
                        float d = length(sp);
                        half sun = 1.0 - smoothstep(0.235, 0.25, d);
                        half stripe = step(0.45, frac((sp.y + 0.3) * 13.0 - t * 0.15));
                        sun *= lerp(1.0, stripe, saturate((0.06 - sp.y) * 9.0));
                        col = lerp(a, b, saturate(sp.y * 2.4 + 0.5)) * sun;
                        col += b * 0.14 * (1.0 - saturate((p.y - horizon) * 2.2));
                    }
                    else
                    {
                        float z = horizon / max(horizon - p.y, 0.001);
                        float gx = abs(frac(p.x * z * 1.6) - 0.5);
                        float gz = abs(frac(z * 0.8 - t * 0.5) - 0.5);
                        half gridLine = smoothstep(0.42, 0.5, max(gx, gz));
                        col = b * gridLine * saturate(1.5 - z * 0.14);
                    }
                    col += a * text;
                }
                else if (_Style < 4.5)
                {
                    // 4: hazard stripes top and bottom, blinking text
                    half text = TextMask(cellUv, uv, 0.0);
                    float d = frac((cellUv.x * aspect + cellUv.y) * 4.0 - t * 0.25);
                    half stripe = step(0.5, d);
                    half band = step(cellUv.y, 0.16) + step(0.84, cellUv.y);
                    col = b * stripe * band * 0.8 + a * text * (0.55 + 0.45 * step(0.5, frac(t * 0.8)));
                }
                else
                {
                    // 5: falling data columns behind the text
                    half text = TextMask(cellUv, uv, 0.0);
                    float cols = max(grid.x * 0.5, 1.0);
                    float ci = floor(cellUv.x * cols);
                    float speed = 0.25 + Hash11(ci + _Seed) * 0.8;
                    float y = frac(cellUv.y + t * speed + Hash11(ci * 2.3 + _Seed));
                    half trail = pow(y, 6.0);
                    half blockOn = step(0.35, Hash21(float2(ci, floor(cellUv.y * grid.y) + floor(t * speed * 4.0))));
                    col = b * trail * blockOn * 0.9 + a * text;
                }

                half flick = 1.0 - _Flicker * 0.6 * step(0.9, Hash11(floor(t * 12.0) + _Seed));
                col *= led * flick * _Intensity;
                col = col * input.fog + unity_FogColor.rgb * (1.0 - input.fog) * 0.6;

                outColor = half4(col + 0.002, 1.0);

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void DepthNormalsFrag(
                Varyings input
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
