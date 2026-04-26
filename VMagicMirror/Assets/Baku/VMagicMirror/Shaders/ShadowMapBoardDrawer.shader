Shader "Custom/ShadowMapBoardDrawer"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowMap ("Shadow Map", 2D) = "white" {}
        _ShadowMapDepthBias ("Shadow Depth Bias", Float) = 0.0025
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ShadowMapBoard"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ShadowMap);
            SAMPLER(sampler_ShadowMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4x4 _ShadowMapViewProj;
                float _ShadowMapDepthBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 shadowPosition = mul(_ShadowMapViewProj, float4(input.positionWS, 1.0));
                if (shadowPosition.w <= 1e-5)
                {
                    return 0.0;
                }

                float3 shadowNdc = shadowPosition.xyz / shadowPosition.w;
                float2 shadowUv = shadowNdc.xy * 0.5 + 0.5;
                shadowUv.y = 1.0 - shadowUv.y;
                if (shadowUv.x < 0.0 || shadowUv.x > 1.0 ||
                    shadowUv.y < 0.0 || shadowUv.y > 1.0 ||
                    shadowNdc.z < 0.0 || shadowNdc.z > 1.0)
                {
                    return 0.0;
                }

                float occluderDepth = SAMPLE_TEXTURE2D(_ShadowMap, sampler_ShadowMap, shadowUv).r;
                float receiverDepth = shadowNdc.z;
                float shadow = receiverDepth - _ShadowMapDepthBias > occluderDepth ? 1.0 : 0.0;
                return float4(_Color.rgb, _Color.a * shadow);
            }
            ENDHLSL
        }
    }
}
