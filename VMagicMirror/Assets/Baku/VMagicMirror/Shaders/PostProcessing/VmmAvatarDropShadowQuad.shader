Shader "Hidden/Vmm/AvatarDropShadowQuad"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_AvatarMaskTex);
        SAMPLER(sampler_AvatarMaskTex);

        float4 _ShadowColor;
        float2 _ShadowOffset;
        float2 _ShadowScale;
        float _AlphaThreshold;
        float _MaskOverscanInv;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = positionInputs.positionCS;
            output.uv = input.uv;
            return output;
        }

        float SampleShadowMask(float2 uv)
        {
            const float2 pivot = float2(0.5, 0.5);
            float2 safeScale = max(_ShadowScale, float2(0.0001, 0.0001));
            float2 sourceUv = (((uv - pivot) - _ShadowOffset) / safeScale) * _MaskOverscanInv + pivot;
            float sourceInRange =
                step(0.0, sourceUv.x) *
                step(0.0, sourceUv.y) *
                step(sourceUv.x, 1.0) *
                step(sourceUv.y, 1.0);

            float mask = SAMPLE_TEXTURE2D(_AvatarMaskTex, sampler_AvatarMaskTex, sourceUv).r;
            float alphaRange = max(1e-5, 1.0 - _AlphaThreshold);
            float shadowMask = saturate((mask - _AlphaThreshold) / alphaRange);
            return shadowMask * sourceInRange;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            float shadowAlpha = SampleShadowMask(input.uv) * _ShadowColor.a;
            return float4(_ShadowColor.rgb, shadowAlpha);
        }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
