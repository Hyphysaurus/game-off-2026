// A thrown thing's flight seen ahead (D-0148): a dashed line drawn on a LineRenderer along the predicted path, and a
// solid ring where it lands. Unlit, see-through, drawn over nothing in particular (it is hidden behind walls like the
// world). The line's own colour (ArcPreview sets it per line) times _BaseColor. uv.x runs along the line in widths
// (Texture Mode: Tile), so _Dash dashes per width keep their size whatever the scale.
Shader "ShiftJam/AimArc"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 0.85)
        _Dash ("Dashes per line width (0 = solid)", Float) = 0.5
        _Fill ("Share of each dash drawn", Range(0.05, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Arc"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Dash;
            float _Fill;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                if (_Dash > 0.0 && frac(i.uv.x * _Dash) > _Fill) discard;
                return half4(i.color.rgb * _BaseColor.rgb, i.color.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
