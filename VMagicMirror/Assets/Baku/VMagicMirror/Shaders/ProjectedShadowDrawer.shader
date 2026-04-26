Shader "Custom/ProjectedShadowDrawer"
{
    Properties
    {
        [MainColor] _Color ("Shadow Color", Color) = (0, 0, 0, 0.6)
        [NoScaleOffset] _ShadowTex ("Shadow Texture", 2D) = "black" {}
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.0001, 0.25)) = 0.02
        _ProjectionFade ("Projection Edge Fade", Range(0.0001, 0.25)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "Queue" = "AlphaTest+49"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ShadowTex);
            SAMPLER(sampler_ShadowTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _ShadowStrength;
                half _ShadowThreshold;
                half _ShadowSoftness;
                half _ProjectionFade;
            CBUFFER_END

            float4x4 _ShadowViewProjMatrix;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half ComputeEdgeFade(float2 uv, half fadeWidth)
            {
                half2 fade = smoothstep(0.0h, fadeWidth, uv) *
                    (1.0h - smoothstep(1.0h - fadeWidth, 1.0h, uv));
                return fade.x * fade.y;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 projected = mul(_ShadowViewProjMatrix, float4(input.positionWS, 1.0f));
                float invW = rcp(max(projected.w, 1e-5f));
                float3 ndc = projected.xyz * invW;
                float2 uv = ndc.xy * 0.5f + 0.5f;

                if (ndc.z < 0.0f || ndc.z > 1.0f || any(uv < 0.0f) || any(uv > 1.0f))
                {
                    return half4(_Color.rgb, 0.0h);
                }

                half shadowSample = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, uv).r;
                half shadowMask = smoothstep(
                    _ShadowThreshold - _ShadowSoftness,
                    _ShadowThreshold + _ShadowSoftness,
                    shadowSample
                );
                half edgeFade = ComputeEdgeFade(uv, _ProjectionFade);
                half alpha = _Color.a * _ShadowStrength * shadowMask * edgeFade;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
