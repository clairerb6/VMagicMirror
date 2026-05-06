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
        float _UseEmissiveBlend;

        float SampleAvatarMask(float2 screenUv)
        {
            const float2 pivot = float2(0.5, 0.5);
            float2 maskUv = (screenUv - pivot) * _MaskOverscanInv + pivot;
            float inRange =
                step(0.0, maskUv.x) *
                step(0.0, maskUv.y) *
                step(maskUv.x, 1.0) *
                step(maskUv.y, 1.0);

            return SAMPLE_TEXTURE2D_X(_AvatarMaskTex, sampler_AvatarMaskTex, maskUv).r * inRange;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            float currentMask = SampleAvatarMask(input.texcoord);

            // maskのinvが正しく適用できてるか見るすごいやつだよ
            // float3 c = lerp(original.rgb, _RimColor.rgb, currentMask);
            // return float4(c.r, c.g, c.b, 1.0);

            float minScreenSize = min(_BlitTexture_TexelSize.z, _BlitTexture_TexelSize.w);
            float2 uvOffsetPerUnit = float2(
                minScreenSize * _BlitTexture_TexelSize.x,
                minScreenSize * _BlitTexture_TexelSize.y);
            float2 maskOffset = _RimOffset * uvOffsetPerUnit;
            float2 offsetUv = input.texcoord + maskOffset;
            float offsetMask = SampleAvatarMask(offsetUv);
            // 「offsetすることでマスクから外れる」というのをリムの条件判定にしている
            float rim = saturate(currentMask - offsetMask);
            // RimColor.a は暗黙に1.0であるという前提で無視
            float rimAlpha = rim * saturate(_ApplyRate);

            // RGB は通常時は色上書き寄り、指定時は発光寄りの加算合成に切り替える。
            float3 outRgb = _UseEmissiveBlend > 0.5
                ? original.rgb + _RimColor.rgb * rimAlpha
                : lerp(original.rgb, _RimColor.rgb, rimAlpha);
            float outAlpha = original.a + rimAlpha * (1.0 - original.a);

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
