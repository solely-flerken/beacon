Shader "Weapon/HitSpark"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HitSpark"

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Must match HitSparkRenderer.MaxStyles
            #define MAX_STYLES 8

            // Must match _Game.Core.HitSparkData
            struct spark_hit
            {
                float2 center;
                float2 direction;
                float radius;
                float start_time;
                uint seed;
                uint style;
            };

            // Ring buffer; the live hits are _FirstHit onward (wrapping at _Capacity)
            StructuredBuffer<spark_hit> _Hits;

            // Per style: rgb = color, w = spark count
            float4 _StyleColors[MAX_STYLES];
            // Per style: x = spray sign (-1 back toward the source, +1 away from it), y = spread (radians), z = speed scale
            float4 _StyleMotion[MAX_STYLES];

            float _FirstHit;
            float _Capacity;
            float _SparksPerHit;
            float _Lifetime;
            float _MinSpeed;
            float _MaxSpeed;
            float _Stretch;
            float _Width;
            float _GlowRadius;
            float _Z;

            // Global, set every frame by ShaderGlobalsSystem
            float _GameTime;

            // Quad corners as (0 = tail end / 1 = head end, side -1 / +1)
            static const float2 corners[6] =
            {
                float2(0, -1), float2(1, -1), float2(1, 1),
                float2(0, -1), float2(1, 1), float2(0, 1),
            };

            struct varyings
            {
                float4 position_cs : SV_POSITION;
                // x: distance along the streak from its tail, y: signed distance across it (world units)
                float2 local : TEXCOORD0;
                nointerpolation float streak_length : TEXCOORD1;
                nointerpolation float half_width : TEXCOORD2;
                nointerpolation float3 color : TEXCOORD3;
                nointerpolation float fade : TEXCOORD4;
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
                // Culled sparks return this: a degenerate triangle that rasterizes nothing
                varyings o = (varyings)0;

                uint per_hit = (uint)_SparksPerHit;
                uint slot = ((uint)_FirstHit + instance_id / per_hit) % (uint)_Capacity;
                uint index = instance_id % per_hit;
                spark_hit hit = _Hits[slot];

                uint style = min(hit.style, MAX_STYLES - 1u);
                float4 look = _StyleColors[style];

                // Spark slot past this style's count
                if (index >= (uint)look.w) return o;

                uint key = hit.seed * 16u + index;

                // Each spark lives 60-100% of the lifetime, so the burst thins out instead of vanishing at once
                float life = _Lifetime * lerp(0.6, 1.0, hash01(key, 3u));
                float t = (_GameTime - hit.start_time) / life;
                if (t < 0.0 || t >= 1.0) return o;

                float4 motion = _StyleMotion[style];
                float2 axis = hit.direction * motion.x;

                float angle = (hash01(key, 1u) * 2.0 - 1.0) * motion.y;
                float s, c;
                sincos(angle, s, c);
                float2 dir = float2(axis.x * c - axis.y * s, axis.x * s + axis.y * c);
                float2 side = float2(-dir.y, dir.x);

                // Starts on the target's edge on the spray side, decelerates to a stop at the end of its life: v = speed * (1 - t)
                float2 origin = hit.center + axis * hit.radius;
                float speed = lerp(_MinSpeed, _MaxSpeed, hash01(key, 2u)) * motion.z;
                float travel = speed * life * (t - 0.5 * t * t);
                float velocity = speed * (1.0 - t);

                // Streak stretches with speed, but never reaches back past the origin
                float half_width = _Width * 0.5 * lerp(1.0, 0.5, t);
                float len = min(max(velocity * _Stretch, half_width), travel + half_width);
                float2 tail = origin + dir * (travel - len);

                // Quad reaches 2 glow radii past the core, where the glow is down to ~1% (keeps the additive overdraw small)
                float pad = half_width + _GlowRadius * 2.0;
                float2 corner = corners[vertex_id];
                float along = lerp(-pad, len + pad, corner.x);
                float across = corner.y * pad;
                float2 position = tail + dir * along + side * across;

                o.position_cs = TransformWorldToHClip(float3(position, _Z));
                o.local = float2(along, across);
                o.streak_length = len;
                o.half_width = half_width;
                o.color = look.rgb;
                o.fade = 1.0 - t;
                return o;
            }

            half4 frag(varyings i) : SV_Target
            {
                // Distance to the streak segment (a capsule)
                float x = clamp(i.local.x, 0.0, i.streak_length);
                float d = length(float2(i.local.x - x, i.local.y));
                float aa = max(fwidth(d), 1e-4);

                float core = 1.0 - smoothstep(i.half_width - aa, i.half_width + aa, d);
                float edge = max(d - i.half_width, 0.0);
                float glow = exp(-(edge * edge) / (_GlowRadius * _GlowRadius)) * 0.6;

                // Bright head, dimmer tail
                float head = saturate(i.local.x / max(i.streak_length, 1e-4));
                float taper = lerp(0.35, 1.0, head);

                float3 color = (i.color * (core + glow) + core * 0.6) * (taper * i.fade);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
