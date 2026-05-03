Shader "Hidden/Vmm/AvatarOffsetRim"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_AvatarMaskTex);
        SAMPLER(sampler_AvatarMaskTex);

        float2 _RimOffset;
        float4 _RimColor;
        float _ApplyRate;
        float _MaskOverscanInv;

        float SampleAvatarMask(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_AvatarMaskTex, sampler_AvatarMaskTex, uv).r;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            float currentMask = SampleAvatarMask(input.texcoord);

            float invMinScreenSize = max(_BlitTexture_TexelSize.x, _BlitTexture_TexelSize.y);
            float2 maskOffset = _RimOffset * invMinScreenSize * _MaskOverscanInv;
            float2 offsetUv = input.texcoord - maskOffset;
            float offsetInRange =
                step(0.0, offsetUv.x) *
                step(0.0, offsetUv.y) *
                step(offsetUv.x, 1.0) *
                step(offsetUv.y, 1.0);

            float offsetMask = SampleAvatarMask(offsetUv) * offsetInRange;
            float rim = saturate(offsetMask - currentMask);
            float rimAlpha = rim * saturate(_RimColor.a) * saturate(_ApplyRate);

            float outAlpha = original.a + rimAlpha * (1.0 - original.a);
            float3 outPremul =
                original.rgb * original.a +
                _RimColor.rgb * rimAlpha * (1.0 - original.a);
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
