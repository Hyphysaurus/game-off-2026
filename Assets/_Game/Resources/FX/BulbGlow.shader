// A lit flashlight's bulb (D-0142, his "since the light is ON, it should have a glow in the center"): drawn additively
// on a small ball round the bulb, brightest where the ball faces the camera, fading to nothing at its rim, so it reads
// as a hot core with a soft halo. BeamShaft sets _BaseColor from the light's colour and _Strength (with the focus).
Shader "ShiftJam/BulbGlow"
{
    Properties
    {
        _BaseColor ("Colour (BeamShaft sets it from the light)", Color) = (1, 0.9, 0.75, 1)
        _Strength ("Brightness (over 1 blooms)", Range(0, 8)) = 2.5
        _Rim ("Halo softness (higher = smaller hot core)", Range(0.5, 8)) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Bulb"
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Strength;
            float _Rim;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float facing = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));   // 1 at the middle, 0 at the rim
                float a = _Strength * pow(facing, _Rim);
                return half4(_BaseColor.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
