Shader "Hidden/Vmm/AvatarDropShadow"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _ShadowColor;
        float2 _ShadowOffset;
        float2 _ShadowScale;
        float _AlphaThreshold;

        float SampleShadowMask(float2 uv)
        {
            const float2 pivot = float2(0.5, 0.5);
            float2 safeScale = max(_ShadowScale, float2(0.0001, 0.0001));
            float2 sourceUv = ((uv - pivot) - _ShadowOffset) / safeScale + pivot;
            float sourceInRange =
                step(0.0, sourceUv.x) *
                step(0.0, sourceUv.y) *
                step(sourceUv.x, 1.0) *
                step(sourceUv.y, 1.0);

            float sourceAlpha = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUv).a;
            float alphaRange = max(1e-5, 1.0 - _AlphaThreshold);
            float shadowMask = saturate((sourceAlpha - _AlphaThreshold) / alphaRange);
            return shadowMask * sourceInRange;
        }

        float4 Frag(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord);
            float shadowAlpha = SampleShadowMask(i.texcoord) * _ShadowColor.a;

            float outAlpha = original.a + shadowAlpha * (1.0 - original.a);
            float3 outPremul =
                original.rgb * original.a +
                _ShadowColor.rgb * shadowAlpha * (1.0 - original.a);

            float3 outRgb = outAlpha > 1e-5 ? outPremul / outAlpha : 0.0.xxx;
            return float4(outRgb, outAlpha);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment Frag
            ENDHLSL
        }
    }
}
