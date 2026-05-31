Shader "Hidden/Vmm/ColoredSsao"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_ScreenSpaceOcclusionTexture);
        float4 _SsaoColor;

        float4 Frag(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            float2 uv = i.texcoord;
            float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            float ao = SAMPLE_TEXTURE2D_X(_ScreenSpaceOcclusionTexture, sampler_LinearClamp, uv).r;
            float occlusion = saturate(1.0 - ao);
            float3 multiplier = lerp(float3(1.0, 1.0, 1.0), _SsaoColor.rgb, occlusion);
            return float4(original.rgb * multiplier, original.a);
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
