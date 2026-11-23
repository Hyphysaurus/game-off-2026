// A gun sight's lens and its reticle (D-0166, his "it's hard to see targets through those reticles or target sights").
// One flat colour that the room's light never touches, so it shows the same in a bright room and in a Dark Zone.
// With "Use the picture" on, the picture's ALPHA is the shape (the pack's reticles are black drawings on a clear
// background, so their own colour is no use) and Size grows or shrinks it about the middle of the lens. With it off
// it is an even tint: a lens (a little alpha) or nothing at all (alpha 0). SightLook puts these on the pack's sights.
Shader "ShiftJam/SightOverlay"
{
    Properties
    {
        [MainTexture] _BaseMap ("Picture (its alpha is the shape)", 2D) = "white" {}
        [MainColor] [HDR] _BaseColor ("Colour (alpha = how solid; brighter than 1 blooms)", Color) = (1, 1, 1, 1)
        [ToggleUI] _Shape ("Use the picture (off = an even tint)", Float) = 1
        _Scale ("Size of the picture (1 = as drawn)", Range(0.2, 4)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces drawn (Back = seen from the shooter's side only)", Float) = 0
        // Not used by the drawing. BodyFade fades a gun's materials only when each has _Surface and _BaseColor: with
        // this the sight fades with the rest of the gun when the camera is pushed inside the body.
        [HideInInspector] _Surface ("Surface (read by the body fade)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Sight"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Shape;
            float _Scale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                // The picture grows or shrinks about the middle of the lens (uv 0.5, 0.5).
                o.uv = (TRANSFORM_TEX(v.uv, _BaseMap) - 0.5) / max(_Scale, 0.01) + 0.5;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Outside its own square the picture is nothing (the textures repeat: a shrunk one would tile).
                float2 inside = step(0.0, i.uv) * step(i.uv, 1.0);
                float drawn = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * inside.x * inside.y;
                float shape = lerp(1.0, drawn, saturate(_Shape));
                return half4(_BaseColor.rgb, _BaseColor.a * shape);
            }
            ENDHLSL
        }
    }
}
