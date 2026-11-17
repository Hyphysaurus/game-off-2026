// The air around the shift orb: a soft outer glow and small crackles of lightning that escape the glass.
// Drawn additively on the orb's halo quad (the camera-facing square behind the crystal, a second material beside his
// mote). The crystal hides the middle of the quad, so only what lies outside the orb's edge shows.
// ShiftOrb.cs already tints this quad per orb (_BaseColor = glow colour x brightness): brighter than 1 means the orb
// is flashing or striking, and the glow and the crackles answer to that. Dormant orbs send a dim colour: it fades.
Shader "ShiftJam/ShiftOrbAura"
{
    Properties
    {
        [HDR] _BaseColor ("Colour (ShiftOrb sets this per orb)", Color) = (0.25, 1.0, 1.3, 1)
        _OrbEdge ("Orb edge (share of the square's half width)", Range(0.1, 0.9)) = 0.4

        [Header(Outer glow)]
        _GlowStrength ("Glow brightness", Range(0, 3)) = 0.75
        _GlowReach ("Glow falloff (higher = tighter)", Range(0.5, 8)) = 5
        _FarColor ("Glow colour away from the glass", Color) = (0.45, 0.22, 1.0, 1)
        _FarAt ("Where that colour takes over (orb radii out)", Range(0.1, 2)) = 0.45
        _FlashGain ("How much brighter on a flash or strike", Range(0, 2)) = 0.8

        [Header(Escaping crackles)]
        _Escapes ("How many escape at rest", Range(0, 1)) = 0.3
        _EscapeReach ("How far they get (orb radii)", Range(1.1, 2.4)) = 1.75
        _EscapeBright ("Crackle brightness", Range(0, 6)) = 2.2
        _Crackle ("Crackle speed (redraws per second)", Range(2, 30)) = 12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Aura"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _OrbEdge;
            float _GlowStrength;
            float _GlowReach;
            float _Escapes;
            float _EscapeReach;
            float _EscapeBright;
            float _Crackle;
            float4 _FarColor;
            float _FarAt;
            float _FlashGain;
            CBUFFER_END

            // Same outside clock as the crystal's shader (see ShiftOrb.shader): unset in the game.
            float4 _ShiftOrbClock;
            float OrbTime() { return _ShiftOrbClock.y > 0.5 ? _ShiftOrbClock.x : _Time.y; }

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

            float Hash11(float n) { return frac(sin(n * 127.1 + 311.7) * 43758.5453); }

            float Noise1(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), f);
            }

            float AngleGap(float a, float b) { return abs(atan2(sin(a - b), cos(a - b))); }

            // The angle escaping arc `id` sits at when it is `beyond` orb radii past the glass: straight at the glass,
            // wandering more the further it gets.
            float ArcAngle(float id, float beyond)
            {
                float theta = Hash11(id + 2.9) * 6.2831853;
                float wander = (Noise1(beyond * 9.0 + id * 5.3) - 0.5) * 1.1 + (Noise1(beyond * 31.0 + id * 9.1) - 0.5) * 0.35;
                return theta + wander * smoothstep(0.0, 0.3, beyond);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 q = (input.uv - 0.5) * 2.0;          // the square, -1..1
                float edge = length(q);
                float ro = edge / max(_OrbEdge, 0.01);      // distance from the centre in orb radii: 1 = the glass
                float beyond = max(ro - 1.0, 0.0);
                float a = atan2(q.y, q.x);

                float3 tint = _BaseColor.rgb;
                float level = max(tint.r, max(tint.g, tint.b));
                float excite = saturate((level - 1.0) * 2.0);   // above 1 = the orb is flashing or striking
                float fade = 1.0 - smoothstep(0.8, 1.0, edge);  // nothing reaches the square's border

                // Outer glow: strongest at the glass, falling away into the air.
                float glow = exp(-beyond * _GlowReach) * _GlowStrength * (1.0 + excite * _FlashGain);
                // Next to the glass it is the orb's own colour; further out it turns to the far colour (his purple).
                float3 glowTint = lerp(tint, _FarColor.rgb * level, smoothstep(0.0, _FarAt, beyond));

                // Escaping crackles: jagged arcs that leave the glass and die a little way out, each on its own beat.
                float width = max(0.02, fwidth(ro) * 1.2);
                float reachMax = max(_EscapeReach, 1.11);
                float soft = 0.0, hard = 0.0;
                for (int k = 0; k < 6; k++)
                {
                    float tick = floor(OrbTime() * _Crackle + Hash11(k * 3.7 + 1.0));
                    float id = k * 13.0 + fmod(tick, 509.0) * 1.61;
                    float lit = step(1.0 - saturate(_Escapes * 0.5 + excite), Hash11(id + 0.7));
                    float reach = lerp(1.12, reachMax, Hash11(id + 1.3));

                    float t = saturate(beyond / (reach - 1.0));       // 0 at the glass, 1 at the arc's tip
                    float d = AngleGap(a, ArcAngle(id, beyond)) * ro;
                    float along = step(0.9, ro) * (1.0 - smoothstep(0.9, 1.0, t));   // starts just inside the edge: the crystal hides that part
                    float w = width * lerp(1.0, 0.45, t);              // thinner toward the tip
                    hard += lit * (1.0 - smoothstep(w * 0.5, w, d)) * along;
                    float halo = w * 1.5 / (d + w * 1.5);
                    soft += lit * halo * halo * along * 0.5;
                }

                float3 col = glowTint * glow;
                col += (tint * soft + (tint * 0.5 + 1.0) * hard * min(level, 1.0)) * _EscapeBright;
                return half4(col * fade, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
