// The flashlight's beam seen in the air (D-0140): a soft glowing cone from the lens along the light, drawn additively
// on BeamShaft's cone mesh. Brightest along the cone's middle as seen from the camera (where the light passes through
// the most air), fading to nothing at its rim, at its lens end over a short way and toward its far end.
// uv.y runs 0 at the lens to 1 at the far end. BeamShaft sets _BaseColor from the light's colour.
// It stops at what it meets (D-0244): BeamShaft finds the first solid thing on the beam's middle and sets that surface
// here (_CutPoint, _CutNormal with w 1). Nothing is drawn behind it; the cone fades out over _CutSoft in front of it.
Shader "ShiftJam/BeamShaft"
{
    Properties
    {
        _BaseColor ("Colour (BeamShaft sets it from the light)", Color) = (1, 0.9, 0.75, 1)
        _Strength ("Brightness", Range(0, 2)) = 0.22
        _Rim ("Rim softness (higher = thinner, softer edge)", Range(0.5, 6)) = 2.5
        _FarFade ("Far end fade (higher = fades sooner)", Range(0.5, 4)) = 1.6
        _LensFade ("Lens end fade-in (share of the length)", Range(0.001, 0.3)) = 0.06
        [HideInInspector] _CutPoint ("Where the beam stops: a point on that surface (BeamShaft sets it)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _CutNormal ("That surface's normal; w 1 = stop there, 0 = no stop (BeamShaft sets it)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _CutSoft ("How far before the surface the cone fades out, world units (BeamShaft sets it)", Float) = 1
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
            float4 _CutPoint;
            float4 _CutNormal;
            float _CutSoft;
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
                float3 positionWS : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                o.along = v.uv.y;
                o.positionWS = p.positionWS;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float facing = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));   // 1 face-on (the middle), 0 at the rim
                float rim = pow(saturate(facing), _Rim);
                float far = pow(saturate(1.0 - i.along), _FarFade);
                float lens = saturate(i.along / _LensFade);
                // How far in front of the surface the beam stops at (on the light's side); behind it this is below 0.
                float d = dot(i.positionWS - _CutPoint.xyz, _CutNormal.xyz);
                float stop = lerp(1.0, smoothstep(0.0, max(_CutSoft, 1e-4), d), saturate(_CutNormal.w));
                float a = _Strength * rim * far * lens * stop;
                return half4(_BaseColor.rgb * a, 0);
            }
            ENDHLSL
        }
    }
}
