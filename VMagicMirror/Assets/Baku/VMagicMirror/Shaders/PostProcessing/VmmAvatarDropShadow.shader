Shader "Hidden/Vmm/AvatarDropShadow"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _ShadowColor;
        float2 _ShadowOffset;
        float2 _ShadowScale;
        float _AlphaThreshold;
        float _UseBackgroundPlane;
        float _UseOpaqueBackground;
        float _BackgroundEyeDepth;
        float _BackgroundDepthTolerance;

        TEXTURE2D_X(_AvatarMaskTex);
        SAMPLER(sampler_AvatarMaskTex);

        float SampleAvatarMask(float2 uv)
        {
            const float2 pivot = float2(0.5, 0.5);
            float2 safeScale = max(_ShadowScale, float2(0.0001, 0.0001));
            float2 sourceUv = ((uv - pivot) - _ShadowOffset) / safeScale + pivot;
            float sourceInRange =
                step(0.0, sourceUv.x) *
                step(0.0, sourceUv.y) *
                step(sourceUv.x, 1.0) *
                step(sourceUv.y, 1.0);

            float mask = SAMPLE_TEXTURE2D_X(_AvatarMaskTex, sampler_AvatarMaskTex, sourceUv).r;
            float alphaRange = max(1e-5, 1.0 - _AlphaThreshold);
            float shadowMask = saturate((mask - _AlphaThreshold) / alphaRange);
            return shadowMask * sourceInRange;
        }

        float SampleCurrentAvatarMask(float2 uv)
        {
            float mask = SAMPLE_TEXTURE2D_X(_AvatarMaskTex, sampler_AvatarMaskTex, uv).r;
            float alphaRange = max(1e-5, 1.0 - _AlphaThreshold);
            return saturate((mask - _AlphaThreshold) / alphaRange);
        }

        float IsBackgroundImagePixel(float2 uv)
        {
            if (_UseBackgroundPlane < 0.5)
            {
                return 0.0;
            }

            float rawDepth = SampleSceneDepth(uv);
            float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
            return step(abs(eyeDepth - _BackgroundEyeDepth), _BackgroundDepthTolerance);
        }

        float4 Frag(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            float4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord);
            float shadowAlpha = SampleAvatarMask(i.texcoord) * _ShadowColor.a;
            float currentAvatarMask = SampleCurrentAvatarMask(i.texcoord);

            if (IsBackgroundImagePixel(i.texcoord) > 0.5 || _UseOpaqueBackground > 0.5)
            {
                float backgroundShadowAlpha = shadowAlpha * (1.0 - currentAvatarMask);
                float3 outRgb = lerp(original.rgb, _ShadowColor.rgb, backgroundShadowAlpha);
                return float4(outRgb, original.a);
            }

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
