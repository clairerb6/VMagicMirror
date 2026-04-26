Shader "Hidden/Vmm/ShadowMapDepthCaster"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Main Tex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "ShadowMapDepthCaster"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MainTex_ST;
                float4 _BaseColor;
                float4 _Color;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uvBase : TEXCOORD0;
                float2 uvMain : TEXCOORD1;
                float deviceDepth : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uvBase = TRANSFORM_TEX(input.uv, _BaseMap);
                output.uvMain = TRANSFORM_TEX(input.uv, _MainTex);
                output.deviceDepth = positionInputs.positionCS.z / max(positionInputs.positionCS.w, 1e-5);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float baseAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uvBase).a * _BaseColor.a;
                float mainAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMain).a * _Color.a;
                clip(max(baseAlpha, mainAlpha) - _Cutoff);
                return float4(input.deviceDepth, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}
