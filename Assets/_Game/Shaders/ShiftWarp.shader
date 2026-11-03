// Fullscreen warp for a shift, drawn by URP's Full Screen Pass Renderer Feature after post-processing.
// A ring grows from the screen centre (or collapses into it); inside the ring the picture is pushed
// radially with the red and blue channels fringed apart, and the ring itself flashes. An optional
// drawn mask (white = tear) replaces the plain ring with your own shape, expanding with it.
Shader "ShiftJam/ShiftWarp"
{
    Properties
    {
        _Progress ("Progress", Range(0, 1)) = 0
        _Direction ("Direction (+1 grow outward, -1 shrink inward)", Float) = 1
        _Strength ("Push strength", Range(0, 0.5)) = 0.12
        _RingWidth ("Ring width", Range(0.02, 1)) = 0.22
        _Fringe ("Colour fringe", Range(0, 3)) = 1
        _Flash ("Flash", Range(0, 3)) = 0.8
        _FlashColor ("Flash colour", Color) = (0.6, 0.95, 1, 1)
        _Centre ("Centre (screen 0..1)", Vector) = (0.5, 0.5, 0, 0)
        _Mask ("Rift mask (white = tear), optional", 2D) = "white" {}
        _MaskStrength ("Mask strength", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "ShiftWarp"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            CBUFFER_START(UnityPerMaterial)
            float _Progress;
            float _Direction;
            float _Strength;
            float _RingWidth;
            float _Fringe;
            float _Flash;
            float _MaskStrength;
            float4 _FlashColor;
            float4 _Centre;
            float4 _Mask_ST;
            CBUFFER_END

            half3 Tap(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).rgb;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // Measure from the centre in a square space so the ring is round on a wide screen.
                float2 d = uv - _Centre.xy;
                d.x *= aspect;
                float r = length(d);
                float2 dir = d / max(r, 1e-4);
                dir.x /= aspect;

                // The whole effect fades in and out over the shift; the ring's radius runs 0 -> past the corners.
                float env = sin(PI * saturate(_Progress));
                float p = _Direction >= 0.0 ? _Progress : 1.0 - _Progress;
                float R = p * 1.7;
                float band = exp(-pow((r - R) / _RingWidth, 2.0));

                // The drawn rift, if any: a square the size of the ring, so the drawing expands with it.
                float2 muv = d / max(R + _RingWidth, 0.08) * 0.5 + 0.5;
                float inside = step(0.0, muv.x) * step(muv.x, 1.0) * step(0.0, muv.y) * step(muv.y, 1.0);
                float drawn = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, muv).r * inside;
                float shape = lerp(band, drawn * saturate(band + 0.35), _MaskStrength);

                float push = shape * env * _Strength * _Direction;
                float2 off = dir * push;
                float fr = 1.0 + _Fringe * 0.5;

                half3 col;
                col.r = Tap(uv - off * fr).r;
                col.g = Tap(uv - off).g;
                col.b = Tap(uv - off / fr).b;
                col += _FlashColor.rgb * (shape * env * _Flash);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
