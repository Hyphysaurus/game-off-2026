// The shift orb: a crystal ball with something alive inside (Mekhala's orb, a palantir). Drawn on a plain sphere
// of radius 1, opaque and unlit, so it glows the same in a lit room and a dark wardrobe. Nothing is see-through:
// the "inside" is worked out per pixel by following the view ray into the ball.
//   Glass   a dark centre, a bright thin edge, one drawn highlight (always upper left, like an inked shine).
//   Mist    a slow swirl sampled at four depths along the ray, so it slides against itself as the camera moves.
//   Stars   his yellow dots, on two shells inside the ball at different depths.
//   Lightning  jagged arcs from the core to the glass, redrawn several times a second: a few faint ones while it
//              charges at rest, all of them bright when ShiftOrb flashes it. (His drawn lines stood for this; they
//              are kept as an option, off by default.)
// ShiftOrb.cs drives the last block of properties per orb (a property block, the material asset is never written).
Shader "ShiftJam/ShiftOrb"
{
    Properties
    {
        [Header(Colours)]
        _DeepColor ("Deep colour (the dark of the glass)", Color) = (0.235, 0.051, 0.361, 1)
        [HDR] _GlowColor ("Glow colour (edge, mist, lightning)", Color) = (0.25, 1.5, 1.9, 1)
        [HDR] _SparkColor ("Spark colour (stars, ember)", Color) = (2.0, 1.7, 0.25, 1)

        [Header(Glass)]
        _RimPower ("Edge sharpness", Range(1, 8)) = 4.4
        _RimStrength ("Edge brightness", Range(0, 3)) = 0.9
        _Shine ("Highlight", Range(0, 2)) = 1.0

        [Header(Inside)]
        _MistDensity ("Mist amount", Range(0, 3)) = 1.0
        _MistScale ("Mist size (higher = finer)", Range(0.5, 6)) = 2.0
        _SwirlSpeed ("Swirl speed at rest", Range(0, 2)) = 0.22
        _Dots ("Stars: his dots, white on black", 2D) = "black" {}
        _StarStrength ("Star brightness", Range(0, 4)) = 1.4
        _LinesRest ("Charge at rest (how much lightning without a flash)", Range(0, 1)) = 0.10
        _LinesFlash ("Lightning flash brightness", Range(0, 6)) = 2.0
        _Crackle ("Crackle speed (redraws per second)", Range(2, 30)) = 12
        _Lines ("His drawn lines, white on black", 2D) = "black" {}
        _LinesDrawn ("Show his drawn lines (0 = off)", Range(0, 1)) = 0

        [Header(Driven by ShiftOrb at run time)]
        _Awake ("Awake (0 dormant, 1 awake)", Range(0, 1)) = 1
        _Flare ("Flare (the strike)", Range(0, 2)) = 0
        _Lightning ("Lightning flash now", Range(0, 1)) = 0
        _LightningSeed ("Lightning variant", Range(0, 1)) = 0
        _SwirlPhase ("Extra swirl (added by ShiftOrb)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_Dots);
        SAMPLER(sampler_Dots);
        TEXTURE2D(_Lines);
        SAMPLER(sampler_Lines);

        CBUFFER_START(UnityPerMaterial)
        float4 _DeepColor;
        float4 _GlowColor;
        float4 _SparkColor;
        float4 _Dots_ST;
        float4 _Lines_ST;
        float _RimPower;
        float _RimStrength;
        float _Shine;
        float _MistDensity;
        float _MistScale;
        float _SwirlSpeed;
        float _StarStrength;
        float _LinesRest;
        float _LinesFlash;
        float _Crackle;
        float _LinesDrawn;
        float _Awake;
        float _Flare;
        float _Lightning;
        float _LightningSeed;
        float _SwirlPhase;
        CBUFFER_END

        // A clock set from outside (x = seconds, y > 0.5 = use it), so a capture can step the orb frame by frame.
        // Nobody sets it in the game: the orb then runs on the game's own time.
        float4 _ShiftOrbClock;
        float OrbTime() { return _ShiftOrbClock.y > 0.5 ? _ShiftOrbClock.x : _Time.y; }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionOS : TEXCOORD2;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.positionOS = input.positionOS.xyz;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Orb"
            Cull Back ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Smooth 3D value noise, 0..1.
            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x);
                float b = lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x);
                float c = lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x);
                float d = lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x);
                return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
            }

            float3 SpinY(float3 p, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
            }

            float2 Spin2(float2 p, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            float Hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            float Noise1(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), f);
            }

            float AngleGap(float a, float b) { return abs(atan2(sin(a - b), cos(a - b))); }

            // The angle arc `id` sits at when it is `r` from the core: it leaves straight and wanders more the further out it gets.
            float ArcAngle(float id, float r)
            {
                float theta = Hash11(id + 2.9) * 6.2831853;
                float wander = (Noise1(r * 6.0 + id * 5.3) - 0.5) * 1.5 + (Noise1(r * 21.0 + id * 9.1) - 0.5) * 0.45;
                return theta + wander * smoothstep(0.02, 0.4, r);
            }

            // Lightning on the ball's face: five jagged arcs from the core toward the glass, each with a fork and a bright
            // spot where it touches. Every arc is redrawn on its own beat, so the whole thing crackles. amount 0..1 = the
            // share of arcs that are lit. Returns (soft glow, hard white line).
            float2 Lightning(float2 face, float amount)
            {
                float r = length(face);
                float a = atan2(face.y, face.x);
                float width = max(0.012, fwidth(r) * 1.2);
                float2 sum = float2(0.0, 0.0);
                for (int k = 0; k < 5; k++)
                {
                    float tick = floor(OrbTime() * _Crackle + Hash11(k * 3.7));
                    float id = k * 17.0 + fmod(tick, 509.0) * 1.37;
                    float lit = step(1.0 - amount, Hash11(id + 0.7));
                    float reach = lerp(0.5, 0.93, Hash11(id + 1.3));   // short arcs read as pointing at the viewer

                    float d = AngleGap(a, ArcAngle(id, r)) * r;
                    float along = 1.0 - smoothstep(reach - 0.02, reach, r);
                    float halo = width * 1.6 / (d + width * 1.6);
                    float line0 = (1.0 - smoothstep(width * 0.5, width, d)) * along;

                    // A fork that splits off part of the way out and dies sooner.
                    float split = reach * 0.45;
                    float side = Hash11(id + 4.1) > 0.5 ? 1.0 : -1.0;
                    float df = AngleGap(a, ArcAngle(id, r) + side * max(r - split, 0.0) * 1.7) * r;
                    float fork = (1.0 - smoothstep(width * 0.4, width * 0.8, df)) * step(split, r)
                               * (1.0 - smoothstep(reach * 0.8 - 0.02, reach * 0.8, r));

                    // Where it touches the glass.
                    float tipAngle = ArcAngle(id, reach);
                    float2 tip = reach * float2(cos(tipAngle), sin(tipAngle));
                    float touch = exp(-dot(face - tip, face - tip) * 260.0);

                    sum.x += lit * (halo * halo * along * 0.6 + touch * 0.9);
                    sum.y += lit * (line0 + fork * 0.8);
                }
                return sum;
            }

            // His dots on a shell of the given radius inside the ball: where the ray first meets it, wrapped like a label.
            float Stars(float3 origin, float3 dir, float radius, float spin, float2 tiling)
            {
                float b = dot(origin, dir);
                float c = dot(origin, origin) - radius * radius;
                float disc = b * b - c;
                if (disc <= 0.0) return 0.0;
                float3 hit = SpinY((origin + dir * (-b - sqrt(disc))) / radius, spin);
                float2 uv = float2(atan2(hit.x, hit.z) * 0.15915494 + 0.5, hit.y * 0.5 + 0.5) * tiling;
                return SAMPLE_TEXTURE2D_LOD(_Dots, sampler_Dots, uv, 0).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float ndv = saturate(dot(N, V));

                // Where this pixel sits on the ball's face as the viewer sees it: x right, y up, each -1..1.
                float3 forward = -V;
                float3 right = normalize(cross(UNITY_MATRIX_V[1].xyz, forward));
                float3 up = cross(forward, right);
                float2 face = float2(dot(N, right), dot(N, up));
                float r2 = saturate(dot(face, face));

                float phase = OrbTime() * _SwirlSpeed + _SwirlPhase;
                float awake = saturate(_Awake);
                float inside = lerp(0.12, 1.0, awake);

                // The view ray inside the ball, in the ball's own space (unit sphere), bent a little like glass does.
                float3 P = normalize(input.positionOS);
                float3 D = normalize(TransformWorldToObjectDir(forward));
                float3 bent = refract(D, P, 0.78);
                D = dot(bent, bent) > 0.0 ? bent : D;
                float chord = max(-2.0 * dot(P, D), 0.0);

                // Mist: four samples along the ray, turning faster near the middle, thickest in the core.
                float mist = 0.0;
                for (int k = 0; k < 4; k++)
                {
                    float3 X = P + D * (chord * (k + 0.5) * 0.25);
                    float core = saturate(1.0 - dot(X, X));
                    float3 Q = SpinY(X, phase * (0.6 + 1.4 * core) + X.y * 1.3);
                    float n = Noise3(Q * _MistScale + float3(0.0, phase * 0.35, 0.0)) * 0.65
                            + Noise3(Q * _MistScale * 2.07 + 11.3) * 0.35;
                    mist += smoothstep(0.38, 0.78, n) * (0.25 + 0.75 * core);
                }
                mist = saturate(mist * 0.25 * chord * 0.5 * _MistDensity);

                float3 deep = _DeepColor.rgb;
                float3 glow = _GlowColor.rgb;
                float3 col = deep * (0.22 + 0.5 * ndv * ndv);
                // Mostly the deep colour, lifted; only the thickest wisps take the glow colour.
                col += lerp(deep * 3.0, glow * 0.85, smoothstep(0.5, 1.0, mist)) * mist * inside;

                // Stars at two depths.
                float twinkle = 0.75 + 0.25 * sin(OrbTime() * 2.3 + face.x * 9.0 + face.y * 7.0);
                float stars = Stars(P, D, 0.80, phase * 0.5, float2(2.0, 1.0))
                            + Stars(P, D, 0.48, -phase * 0.8 + 2.1, float2(2.0, 1.0)) * 0.7;
                col += _SparkColor.rgb * stars * twinkle * _StarStrength * inside;

                // Lightning: a few faint arcs while it charges at rest; all of them, bright, when ShiftOrb flashes it.
                float flash = saturate(_Lightning);
                float2 arcs = Lightning(face, saturate(_LinesRest * 2.0 + flash));
                float power = lerp(0.55, _LinesFlash, flash) * awake;
                col += (glow * arcs.x + (glow * 0.5 + 1.0) * arcs.y) * power;
                // The charge at the heart of it, pulsing.
                float heart = exp(-r2 * 38.0) * (0.35 + 0.2 * sin(OrbTime() * 5.0) + flash * 1.5);
                col += (glow + 0.4) * heart * awake;
                col += glow * flash * 0.15 * (1.0 - r2) * awake;

                // His drawn lines as a card facing the viewer (an option, off by default): one of eight turns/flips per flash.
                float turn = floor(_LightningSeed * 4.0) * 1.5707963;
                float2 luv = Spin2(face, turn);
                luv.x *= frac(_LightningSeed * 2.0 + 0.25) > 0.5 ? -1.0 : 1.0;
                float lines = SAMPLE_TEXTURE2D(_Lines, sampler_Lines, luv * 0.52 + 0.5).r;
                col += (glow + lines * 0.6) * lines * (0.10 + flash * _LinesFlash) * awake * _LinesDrawn;

                // Dormant: a small ember in the middle so it can be found in the dark.
                float ember = exp(-r2 * 16.0) * (1.0 - awake) * (0.75 + 0.25 * sin(OrbTime() * 2.0));
                col += _SparkColor.rgb * ember * 0.3;

                // Glass: bright thin edge, then the drawn shine on top.
                float fres = pow(1.0 - ndv, _RimPower);
                col += glow * fres * _RimStrength * lerp(0.3, 1.0, awake);
                col += pow(1.0 - ndv, _RimPower * 2.6) * 0.7 * lerp(0.35, 1.0, awake);

                float spot = 1.0 - smoothstep(0.105, 0.150, length((face - float2(-0.40, 0.46)) * float2(1.0, 1.3)));
                float dot2 = 1.0 - smoothstep(0.030, 0.050, length(face - float2(-0.16, 0.68)));
                float2 dirOut = face / max(sqrt(r2), 1e-4);
                float bounce = smoothstep(0.66, 0.86, sqrt(r2)) * (1.0 - smoothstep(0.90, 0.985, sqrt(r2)))
                             * saturate(dot(dirOut, float2(0.7071, -0.7071)) - 0.25);
                col += lerp(float3(1, 1, 1), glow, 0.15) * (spot * 0.9 + dot2 * 0.7) * _Shine * lerp(0.45, 1.0, awake);
                col += glow * bounce * 0.35 * _Shine * awake;

                // The strike: the whole crystal flares, bright but not blank: what is inside still shows through it.
                col += (glow * 0.9 + 0.55) * _Flare * (0.35 + 0.65 * ndv);

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepth
            half FragDepth(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragNormals
            half4 FragNormals(Varyings input) : SV_Target { return half4(normalize(input.normalWS), 0.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
