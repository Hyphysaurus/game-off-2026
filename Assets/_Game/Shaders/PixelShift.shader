// The Flat Zones' pixel look (D-0209), drawn by URP's Full Screen Pass Renderer Feature after post-processing (after
// the shift warp's feature, so a warp is pixelated too). The picture is cut into big square pixels (Rows of them top to
// bottom, the columns following the screen's width); each big pixel takes the colour at its middle, is nudged by a 4 by
// 4 ordered dither and snapped to the nearest colour of a small palette (his title's colours and a few warm ones for the
// toy room, one texel each), and the result is laid over the picture by Weight. At Weight 0 nothing changes; as Weight
// rises the big pixels grow from the screen's own pixels to Rows, so the look comes in (and goes) with the camera's swing.
// The numbers that change while playing are GLOBALS that FlatZone sets (Shader.SetGlobalFloat), not material properties,
// so playing never writes the material asset. WebGL 2 / GLES 3: no compute; the palette loop has a fixed upper bound and
// samples with an explicit level.
Shader "ShiftJam/PixelShift"
{
    Properties
    {
        _Palette ("Palette (one colour a texel, in a row; point filtered)", 2D) = "white" {}
        _PaletteCount ("Colours in the palette", Range(1, 32)) = 21
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "PixelShift"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            #define PIXELSHIFT_MOST_COLOURS 32

            TEXTURE2D(_Palette);

            CBUFFER_START(UnityPerMaterial)
            float _PaletteCount;
            float4 _Palette_ST;
            CBUFFER_END

            // Globals, set by FlatZone every frame the look shows: how much of it shows (0..1), the rows of big pixels,
            // the dither's reach (a share of full brightness), how far a big pixel is pulled to the palette (0..1).
            float _PixelShiftWeight;
            float _PixelShiftRows;
            float _PixelShiftDither;
            float _PixelShiftPalette;

            // 2 by 2 ordered dither from whole cell numbers: 0, 0.5, 0.75, 0.25 (Bayer); it repeats every 2 cells.
            float Bayer2(float2 a)
            {
                a = floor(a);
                return frac(a.x * 0.5 + a.y * a.y * 0.75);
            }

            // 4 by 4 ordered dither: 0 to 15/16 over a block of 4 by 4 big pixels.
            float Bayer4(float2 cell)
            {
                float2 p = fmod(cell, 4.0);
                return Bayer2(p) + Bayer2(p * 0.5) * 0.25;
            }

            // Colours are compared as the screen shows them (sRGB), so the palette's steps are even to the eye.
            float3 Shown(float3 c)
            {
            #if defined(UNITY_COLORSPACE_GAMMA)
                return c;
            #else
                return LinearToSRGB(c);
            #endif
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                float3 src = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).rgb;
                float w = saturate(_PixelShiftWeight);
                if (w <= 0.0)
                    return half4(src, 1.0);

                // The big pixels: from the screen's own rows (Weight 0) to Rows (Weight 1), square.
                float screenRows = max(_ScreenParams.y, 1.0);
                float rows = max(1.0, floor(lerp(screenRows, max(_PixelShiftRows, 1.0), w)));
                float2 grid = float2(max(1.0, floor(rows * _ScreenParams.x / screenRows)), rows);
                float2 cell = floor(uv * grid);
                float3 big = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, (cell + 0.5) / grid, 0).rgb;

                // Nudged by the dither, then the nearest palette colour (weighted as the eye weighs red, green, blue).
                float3 seen = saturate(Shown(saturate(big))) + (Bayer4(cell) - 0.46875) * _PixelShiftDither;
                float count = clamp(floor(_PaletteCount), 1.0, (float)PIXELSHIFT_MOST_COLOURS);
                float3 best = big;
                float bestDistance = 1e9;
                [loop]
                for (int k = 0; k < PIXELSHIFT_MOST_COLOURS; k++)
                {
                    if ((float)k >= count)
                        break;
                    float3 colour = SAMPLE_TEXTURE2D_LOD(_Palette, sampler_PointClamp, float2(((float)k + 0.5) / count, 0.5), 0).rgb;
                    float3 d = Shown(colour) - seen;
                    float apart = dot(d * d, float3(0.30, 0.59, 0.11));
                    if (apart < bestDistance)
                    {
                        bestDistance = apart;
                        best = colour;
                    }
                }
                float3 snapped = lerp(big, best, saturate(_PixelShiftPalette));
                return half4(lerp(src, snapped, w), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
