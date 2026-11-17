// His drawn glow (the orb mote) on the orb's halo quad, added to the picture: the first material of the halo, under
// the aura. It does what the stock additive shader did, with one difference: the drawing's ground is one shade above
// black, and the orb multiplies this layer when it flashes or strikes, which lit the whole square as a faint box in
// the dark. The black point takes that ground away (the drawing file is not touched) and the layer fades out before
// the square's border.
// ShiftOrb.cs tints it per orb (_BaseColor = glow colour x brightness), as it does the aura.
Shader "ShiftJam/ShiftOrbMote"
{
    Properties
    {
        _BaseMap ("Drawn glow (on black)", 2D) = "black" {}
        [HDR] _BaseColor ("Colour (ShiftOrb sets this per orb)", Color) = (1, 1, 1, 1)
        _BlackPoint ("Black point (the drawing's ground, taken away)", Range(0, 0.01)) = 0.0006
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Mote"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _BlackPoint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 drawn = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb;
                drawn = max(drawn - _BlackPoint, 0.0) / (1.0 - _BlackPoint);
                float edge = length((input.uv - 0.5) * 2.0);
                float fade = 1.0 - smoothstep(0.85, 1.0, edge);     // nothing reaches the square's border
                return half4(drawn * _BaseColor.rgb * fade, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
