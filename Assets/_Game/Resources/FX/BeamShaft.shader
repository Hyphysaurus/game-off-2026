// The flashlight's beam seen in the air (D-0140): a soft glowing cone from the lens along the light, drawn additively
// on BeamShaft's cone mesh. Brightest along the cone's middle as seen from the camera (where the light passes through
// the most air), fading to nothing at its rim, at its lens end over a short way and toward its far end.
// uv.y runs 0 at the lens to 1 at the far end. BeamShaft sets _BaseColor from the light's colour.
Shader "ShiftJam/BeamShaft"
{
    Properties
    {
        _BaseColor ("Colour (BeamShaft sets it from the light)", Color) = (1, 0.9, 0.75, 1)
        _Strength ("Brightness", Range(0, 2)) = 0.22
        _Rim ("Rim softness (higher = thinner, softer edge)", Range(0.5, 6)) = 2.5
        _FarFade ("Far end fade (higher = fades sooner)", Range(0.5, 4)) = 1.6
        _LensFade ("Lens end fade-in (share of the length)", Range(0.001, 0.3)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Shaft"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Strength;
            float _Rim;
            float _FarFade;
            float _LensFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float along : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                o.along = v.uv.y;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));   // 1 face-on (the middle), 0 at the rim
                float rim = pow(saturate(facing), _Rim);
                float far = pow(saturate(1.0 - i.along), _FarFade);
                float lens = saturate(i.along / _LensFade);
                float a = _Strength * rim * far * lens;
                return half4(_BaseColor.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
