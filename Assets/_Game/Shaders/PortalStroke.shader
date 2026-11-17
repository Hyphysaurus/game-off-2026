// His portal drawing as 3D strokes (D-0073): each stroke's colour is baked into the model's vertices in Blender
// (written as sRGB; made linear here), so one material draws the whole ring, the dark centre and the small marks.
// - _BaseColor multiplies the picture: CarPortal brightens it for a flash.
// - _EdgeShade darkens a tube toward its edges so it reads round (1 = flat, like the drawing on paper). It follows
//   the eye, not the room's lights: a portal glows the same in a dark room.
// - _SpinSpeed turns the picture about its middle (the swirl's material; the marks' material leaves it at 0).
// - _Grow shrinks every small mark toward its own middle (0 = gone, 1 = as drawn). The middle is stored in the
//   model's first UV set by the Blender export; PortalRipple sets _Grow per ring. The ring and the centre leave it at 1.
Shader "ShiftJam/PortalStroke"
{
    Properties
    {
        [HDR] _BaseColor ("Brightness and tint (CarPortal flashes this)", Color) = (1, 1, 1, 1)
        _EdgeShade ("Edge shade (1 = flat)", Range(0, 1)) = 0.55
        _Grow ("Grow (small marks: 0 gone, 1 as drawn)", Range(0, 1)) = 1
        _SpinSpeed ("Spin speed (degrees a second about the middle; below 0 the other way)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _EdgeShade;
        float _Grow;
        float _SpinSpeed;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 markMiddle : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 colour : TEXCOORD2;
        };

        // A mark shrinks toward its own middle, which lies in the drawing's plane (z = 0 in the model).
        float3 Grown(Attributes input)
        {
            float3 middle = float3(input.markMiddle, 0.0);
            return lerp(middle, input.positionOS.xyz, _Grow);
        }

        // The swirl turns here, about the model's own z through its middle, and not by its transform: what the game
        // measures (Build Mode's box, the camera's culling box) stays still.
        float3 Spun(float3 v)
        {
            float s, c;
            sincos(radians(_SpinSpeed) * _Time.y, s, c);
            return float3(v.x * c - v.y * s, v.x * s + v.y * c, v.z);
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float3 positionOS = Spun(Grown(input));
            output.positionWS = TransformObjectToWorld(positionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(Spun(input.normalOS));
            output.colour = input.color.rgb;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Stroke"
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing = abs(dot(n, v));                     // both faces: the centre disc is seen from behind too
                float3 colour = SRGBToLinear(input.colour) * _BaseColor.rgb;
                return half4(colour * lerp(_EdgeShade, 1.0, facing), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth

            half4 FragDepth(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragNormals

            half4 FragNormals(Varyings input) : SV_Target { return half4(normalize(input.normalWS), 0.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
