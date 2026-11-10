Shader "Unit/DeathBurst"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DeathBurst"

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Must match _Game.Core.DeathBurstData
            struct burst_data
            {
                float2 center;
                float radius;
                float start_time;
                float3 color;
                uint seed;
            };

            // Ring buffer; the live bursts are _FirstBurst onward (wrapping at _Capacity)
            StructuredBuffer<burst_data> _Bursts;

            float _FirstBurst;
            float _Capacity;
            // Instances per burst: one ring, then the fragments
            float _InstancesPerBurst;

            float _Lifetime;
            float _MinSpeed;
            float _MaxSpeed;
            float _FragmentLength; // In enemy radii
            float _MaxSpin;        // Radians per second at launch
            float _LineWidth;
            float _CoreWidth;      // Fraction of the line
            float _GlowRadius;
            float _Intensity;

            float _RingLifetime;
            float _RingGrowth;     // Final ring radius, in enemy radii
            float _RingIntensity;

            float _Z;

            // Global, set every frame by ShaderGlobalsSystem
            float _GameTime;

            // Quad corners in [-1, 1]
            static const float2 corners[6] =
            {
                float2(-1, -1), float2(1, -1), float2(1, 1),
                float2(-1, -1), float2(1, 1), float2(-1, 1),
            };

            struct varyings
            {
                float4 position_cs : SV_POSITION;
                // Fragment: x along the segment from its center, y across it. Ring: offset from the burst center (world units)
                float2 local : TEXCOORD0;
                // Fragment: half its length. Ring: its radius
                nointerpolation float size : TEXCOORD1;
                nointerpolation float half_width : TEXCOORD2;
                nointerpolation float3 hue : TEXCOORD3;
                nointerpolation float brightness : TEXCOORD4;
                // 1 = ring, 0 = fragment
                nointerpolation float is_ring : TEXCOORD5;
            };

            float hash01(uint a, uint b)
            {
                uint h = a * 747796405u + b * 2891336453u;
                h = ((h >> ((h >> 28u) + 4u)) ^ h) * 277803737u;
                h = (h >> 22u) ^ h;
                return h * (1.0 / 4294967295.0);
            }

            varyings vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                // Culled instances return this: a degenerate triangle that rasterizes nothing
                varyings o = (varyings)0;

                uint per_burst = (uint)_InstancesPerBurst;
                uint slot = ((uint)_FirstBurst + instance_id / per_burst) % (uint)_Capacity;
                uint index = instance_id % per_burst;
                burst_data burst = _Bursts[slot];

                // Normalization, so dark tints still glow at full brightness
                float m = max(max(burst.color.r, burst.color.g), burst.color.b);
                o.hue = m > 1e-3 ? burst.color / m : float3(1, 1, 1);

                float age = _GameTime - burst.start_time;
                float2 corner = corners[vertex_id];
                float2 position;

                if (index == 0u)
                {
                    // Ring: a quick shockwave that grows out of the outline and fades
                    // Lifetime <= 0: ring turned off
                    if (_RingLifetime <= 0.0) return o;
                    float t = age / _RingLifetime;
                    if (t < 0.0 || t >= 1.0) return o;

                    float ease = 1.0 - (1.0 - t) * (1.0 - t);
                    float radius = burst.radius * lerp(1.0, _RingGrowth, ease);
                    float half_width = _LineWidth * 0.5 * (1.0 - t);
                    float pad = radius + half_width + _GlowRadius * 2.0;

                    position = burst.center + corner * pad;
                    o.local = corner * pad;
                    o.size = radius;
                    o.half_width = half_width;
                    o.brightness = _RingIntensity * (1.0 - t) * (1.0 - t);
                    o.is_ring = 1.0;
                }
                else
                {
                    uint fragments = per_burst - 1u;
                    uint key = burst.seed * 16u + index;

                    // Each fragment lives 60-100% of the lifetime, so the burst thins out instead of vanishing at once
                    float life = _Lifetime * lerp(0.6, 1.0, hash01(key, 3u));
                    float t = age / life;
                    if (t < 0.0 || t >= 1.0) return o;

                    // Evenly spaced around the outline with some jitter, rotated per burst
                    float spin_offset = hash01(burst.seed, 7u) * 6.2831853;
                    float angle = ((index - 1u) + (hash01(key, 1u) - 0.5) * 0.6) / fragments * 6.2831853 + spin_offset;
                    float s, c;
                    sincos(angle, s, c);
                    float2 dir = float2(c, s);

                    // Bigger enemies throw their pieces a bit farther
                    float size_scale = sqrt(max(burst.radius, 0.05) / 0.4);
                    float speed = lerp(_MinSpeed, _MaxSpeed, hash01(key, 2u)) * size_scale;

                    // Decelerates to a stop at the end of its life: v = speed * (1 - t)
                    float eased = t - 0.5 * t * t;
                    float2 center = burst.center + dir * (burst.radius + speed * life * eased);

                    // Starts tangent to the outline (a piece of it), then tumbles
                    float spin = (hash01(key, 4u) * 2.0 - 1.0) * _MaxSpin * life * eased;
                    float segment_angle = angle + 1.5707963 + spin;
                    sincos(segment_angle, s, c);
                    float2 along = float2(c, s);
                    float2 side = float2(-along.y, along.x);

                    // Shrinks as it fades
                    float shrink = lerp(1.0, 0.35, t);
                    float half_length = burst.radius * _FragmentLength * lerp(0.7, 1.3, hash01(key, 5u)) * 0.5 * shrink;
                    float half_width = _LineWidth * 0.5 * lerp(1.0, 0.6, t);

                    float pad = half_width + _GlowRadius * 2.0;
                    float2 local = float2(corner.x * (half_length + pad), corner.y * pad);
                    position = center + along * local.x + side * local.y;

                    float fade = 1.0 - t;
                    o.local = local;
                    o.size = half_length;
                    o.half_width = half_width;
                    o.brightness = _Intensity * fade * fade;
                    o.is_ring = 0.0;
                }

                o.position_cs = TransformWorldToHClip(float3(position, _Z));
                return o;
            }

            half4 frag(varyings i) : SV_Target
            {
                float d;
                if (i.is_ring > 0.5)
                {
                    d = abs(length(i.local) - i.size);
                }
                else
                {
                    // Distance to the segment (a capsule)
                    float x = clamp(i.local.x, -i.size, i.size);
                    d = length(float2(i.local.x - x, i.local.y));
                }

                float aa = max(fwidth(d), 1e-4);
                float half_width = max(i.half_width, aa * 0.5);

                float tube = 1.0 - smoothstep(half_width - aa, half_width + aa, d);
                float core_width = half_width * _CoreWidth;
                float core = (1.0 - smoothstep(core_width - aa, core_width + aa, d)) * step(1e-4, _CoreWidth);

                float edge = max(d - half_width, 0.0);
                float glow = exp(-(edge * edge) / (_GlowRadius * _GlowRadius)) * 0.6;

                float3 color = i.hue * (tube * 1.5 + glow) + core;
                return half4(color * i.brightness, 0);
            }
            ENDHLSL
        }
    }
}
