Shader "Hidden/Vmm/Crop"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Range(0, 1)
        float _Margin;
        float _BorderWidth;
        float _SquareRate;

        float4 _BorderColor;

        float4 Frag(Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            float2 uv = i.texcoord;

            float2 screenPx = _ScreenParams.xy;
            float  screenSize = min(screenPx.x, screenPx.y);

            float shapeSize = screenSize * saturate(1.0 - _Margin);
            float halfShapeSize = 0.5 * shapeSize;

            // x0.5 of straight segment
            float halfStraightSegLength = halfShapeSize * _SquareRate;
            // Corner radius
            float radius = halfShapeSize * (1.0 - _SquareRate);

            // Border width
            float borderPx = max(0.0, screenSize * _BorderWidth);
            borderPx = min(borderPx, halfShapeSize);

            // Pixel space around screen center
            float2 pos = (uv - 0.5) * screenPx;

            float2 d = abs(pos) - float2(halfStraightSegLength, halfStraightSegLength);
            float sd = length(max(d, 0.0)) + min(max(d.x, d.y), 0) - radius;

            if (sd > 0.0)
                return float4(0.0, 0.0, 0.0, 0.0);

            if (borderPx > 0.0 && sd >= -borderPx)
                return _BorderColor;

            float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            return float4(col.r, col.g, col.b, 1.0);
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
