Shader "Weapon/ChainLightning"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ChainLightning"

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Must match _Game.Core.ChainArcVisualData
            struct arc_data
            {
                float2 from;
                float2 to;
                float2 from_velocity;
                float2 to_velocity;
                float start_time;
                float intensity;
                uint seed;
            };

            StructuredBuffer<arc_data> _Arcs;

            float _Now;
            float _Lifetime;
            float _CoreWidth;
            float _GlowRadius;
            float _Jaggedness;
            float _NoiseFrequency;
            float _FlickerRate;
            float4 _Color;
            float _BranchStrength;
            float _BranchOffset;
            float _FlashRadius;
            float _Z;

            // Quad corners as (0 = start end / 1 = target end, side -1 / +1)
            static const float2 corners[6] =
            {
                float2(0, -1), float2(1, -1), float2(1, 1),
                float2(0, -1), float2(1, 1), float2(0, 1),
            };

            struct varyings
            {
                float4 position_cs : SV_POSITION;
                // x: distance along the arc from its start, y: signed distance across it (world units)
                float2 local : TEXCOORD0;
                nointerpolation float arc_length : TEXCOORD1;
                nointerpolation float age : TEXCOORD2;
                nointerpolation float brightness : TEXCOORD3;
                nointerpolation uint seed : TEXCOORD4;
            };

            float hash01(uint a, uint b)
            {
                uint h = a * 747796405u + b * 2891336453u;
                h = ((h >> ((h >> 28u) + 4u)) ^ h) * 277803737u;
                h = (h >> 22u) ^ h;
                return h * (1.0 / 4294967295.0);
            }

            // Smooth 1D value noise in [-1, 1]
            float value_noise(float x, uint seed)
            {
                float cell = floor(x);
                float f = x - cell;
                uint i = (uint)(int)cell;
                float a = hash01(seed, i);
                float b = hash01(seed, i + 1u);
                return lerp(a, b, f * f * (3.0 - 2.0 * f)) * 2.0 - 1.0;
            }

            // Three octaves: big swings plus small jitter, which reads as electric rather than wavy
            float fbm(float x, uint seed)
            {
                return value_noise(x, seed) * 0.6
                    + value_noise(x * 2.3, seed + 17u) * 0.3
                    + value_noise(x * 5.1, seed + 31u) * 0.15;
            }

            // Sideways offset of the main bolt at distance u along an arc of length arc_length
            float centerline(float u, float arc_length, uint seed)
            {
                float envelope = sin(saturate(u / arc_length) * PI); // pinned to zero at both enemies
                return fbm(u * _NoiseFrequency, seed) * _Jaggedness * envelope;
            }

            // x: white core coverage (anti-aliased), y: colored glow
            float2 bolt(float distance, float core_width, float glow_radius, float aa)
            {
                float core = smoothstep(core_width + aa, core_width - aa, distance);
                float glow = exp(-(distance * distance) / (glow_radius * glow_radius));
                return float2(core, glow);
            }

            varyings vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                varyings o = (varyings)0;

                arc_data arc = _Arcs[instance_id];
                float age = _Now - arc.start_time;

                // Expired, or left over from an earlier run: collapse the quad outside the view
                if (age < 0.0 || age >= _Lifetime)
                {
                    o.position_cs = float4(2.0, 2.0, 2.0, 1.0);
                    return o;
                }

                // Endpoints follow the enemies while the arc is visible
                float2 from = arc.from + arc.from_velocity * age;
                float2 to = arc.to + arc.to_velocity * age;

                float2 delta = to - from;
                float arc_length = max(length(delta), 1e-3);
                float2 dir = delta / arc_length;
                float2 normal = float2(-dir.y, dir.x);

                // Tight quad: zigzag + branch drift + glow margin sideways, glow or flash margin past the ends
                float glow_margin = _GlowRadius * 2.5;
                float half_width = _Jaggedness + (_BranchStrength > 0.0 ? _BranchOffset : 0.0) + glow_margin;
                float end_pad = max(glow_margin, _FlashRadius * 2.5);

                float2 corner = corners[vertex_id];
                float along = lerp(-end_pad, arc_length + end_pad, corner.x);
                float across = corner.y * half_width;
                float2 position = from + dir * along + normal * across;

                o.position_cs = TransformWorldToHClip(float3(position, _Z));
                o.local = float2(along, across);
                o.arc_length = arc_length;
                o.age = age;
                o.brightness = arc.intensity * (1.0 - age / _Lifetime);
                o.seed = arc.seed;
                return o;
            }

            half4 frag(varyings i) : SV_Target
            {
                float u = i.local.x;
                float v = i.local.y;
                float arc_length = i.arc_length;
                float aa = fwidth(v); // world units per pixel, for anti-aliasing

                uint seed = i.seed + (uint)(i.age * _FlickerRate) * 7919u;

                // Main bolt: distance to the centerline, corrected for its slope so steep zigzags keep their thickness
                float y = centerline(u, arc_length, seed);
                float slope = (centerline(u + 0.05, arc_length, seed) - y) / 0.05;
                float distance = abs(v - y) * rsqrt(1.0 + slope * slope);

                // Past either end: round caps around the enemies
                if (u < 0.0) distance = length(float2(u, v));
                else if (u > arc_length) distance = length(float2(u - arc_length, v));

                float2 main_bolt = bolt(distance, _CoreWidth, _GlowRadius, aa);

                // Branch: splits off between 20% and 50% along, drifts to one side for 35% of the arc, fading out
                if (_BranchStrength > 0.0)
                {
                    float s = u / arc_length;
                    float s0 = lerp(0.2, 0.5, hash01(i.seed, 11u));
                    float k = (s - s0) / 0.35;
                    if (k > 0.0 && k < 1.0)
                    {
                        float side = hash01(i.seed, 12u) < 0.5 ? -1.0 : 1.0;
                        float branch_y = y + side * _BranchOffset * k
                            + fbm(u * _NoiseFrequency * 2.0, seed + 101u) * _Jaggedness * 0.5 * k;
                        float2 branch = bolt(abs(v - branch_y), _CoreWidth * 0.6, _GlowRadius * 0.6, aa);
                        main_bolt += branch * (_BranchStrength * (1.0 - k));
                    }
                }

                // Impact flash at the struck enemy, strongest when the arc appears
                float flash = 0.0;
                if (_FlashRadius > 0.0)
                {
                    float2 to_target = float2(u - arc_length, v);
                    float fresh = 1.0 - saturate(i.age / _Lifetime);
                    flash = exp(-dot(to_target, to_target) / (_FlashRadius * _FlashRadius)) * fresh;
                }

                float3 color = _Color.rgb * (main_bolt.y + flash) + main_bolt.x * 2.5;
                return half4(color * i.brightness, 0);
            }
            ENDHLSL
        }
    }
}
