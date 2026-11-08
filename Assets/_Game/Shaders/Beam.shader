Shader "Weapon/Beam"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Beam"

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Must match _Game.Shared.BeamVisualData
            struct beam_data
            {
                float2 origin;
                float2 end;
                float width;
                float since_pulse;
                uint seed;
            };

            StructuredBuffer<beam_data> _Beams;

            // Global, set every frame by ShaderGlobalsSystem
            float _GameTime;

            float _GlowRadius;
            float4 _Color;
            float _FlowSpeed;
            float _FlowFrequency;
            float _FlowAmount;
            float _FilamentStrength;
            float _FilamentWidth;
            float _Jaggedness;
            float _NoiseFrequency;
            float _FlickerRate;
            float _PulseDecay;
            float _PulseBrightness;
            float _PulseWidth;
            float _MuzzleRadius;
            float _TipFade;
            float _Z;

            // Quad corners as (0 = weapon end / 1 = far end, side -1 / +1)
            static const float2 corners[6] =
            {
                float2(0, -1), float2(1, -1), float2(1, 1),
                float2(0, -1), float2(1, 1), float2(0, 1),
            };

            struct varyings
            {
                float4 position_cs : SV_POSITION;
                // x: distance along the beam from the weapon, y: signed distance across it (world units)
                float2 local : TEXCOORD0;
                nointerpolation float beam_length : TEXCOORD1;
                nointerpolation float half_width : TEXCOORD2;
                nointerpolation float pulse : TEXCOORD3;
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

            // Three octaves, same as the chain lightning's bolt
            float fbm(float x, uint seed)
            {
                return value_noise(x, seed) * 0.6
                    + value_noise(x * 2.3, seed + 17u) * 0.3
                    + value_noise(x * 5.1, seed + 31u) * 0.15;
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
                varyings o;

                beam_data beam = _Beams[instance_id];

                float2 delta = beam.end - beam.origin;
                float beam_length = max(length(delta), 1e-3);
                float2 dir = delta / beam_length;
                float2 normal = float2(-dir.y, dir.x);

                // Damage pulse: 1 the frame it fires, decaying over _PulseDecay seconds
                float pulse = exp(-beam.since_pulse / max(_PulseDecay, 1e-4));
                float half_width = beam.width * 0.5 * (1.0 + _PulseWidth * pulse);

                // Tight quad: core + filaments + glow margin sideways, glow or muzzle margin past the ends
                float glow_margin = _GlowRadius * 2.5;
                float quad_half_width = half_width + _Jaggedness + glow_margin;
                float start_pad = max(glow_margin, _MuzzleRadius * 2.5);

                float2 corner = corners[vertex_id];
                float along = lerp(-start_pad, beam_length + glow_margin, corner.x);
                float across = corner.y * quad_half_width;
                float2 position = beam.origin + dir * along + normal * across;

                o.position_cs = TransformWorldToHClip(float3(position, _Z));
                o.local = float2(along, across);
                o.beam_length = beam_length;
                o.half_width = half_width;
                o.pulse = pulse;
                o.seed = beam.seed;
                return o;
            }

            half4 frag(varyings i) : SV_Target
            {
                float u = i.local.x;
                float v = i.local.y;
                float beam_length = i.beam_length;
                float aa = fwidth(v); // world units per pixel, for anti-aliasing

                // Distance to the beam's centerline segment, with round caps past either end
                float along = clamp(u, 0.0, beam_length);
                float distance = length(float2(u - along, v));

                // Core and glow, measured from the core's edge so a wider beam keeps the same glow falloff
                float core = smoothstep(i.half_width + aa, i.half_width - aa, distance);
                float edge = max(distance - i.half_width, 0.0);
                float glow = exp(-(edge * edge) / (_GlowRadius * _GlowRadius));

                // Brighter bands flowing out from the weapon
                float flow = 1.0 + _FlowAmount * value_noise(u * _FlowFrequency - _GameTime * _FlowSpeed, i.seed + 53u);

                // Filaments: two strands crackling around the core, emerging from the muzzle
                float2 filaments = 0.0;
                if (_FilamentStrength > 0.0)
                {
                    uint flicker = (uint)(_GameTime * _FlickerRate) * 7919u;
                    float envelope = saturate(u / max(_MuzzleRadius * 2.0, 1e-3));
                    float amplitude = (i.half_width + _Jaggedness) * envelope;

                    [unroll]
                    for (uint k = 0u; k < 2u; k++)
                    {
                        uint seed = i.seed + flicker + k * 1013u;
                        float y = fbm(u * _NoiseFrequency, seed) * amplitude;
                        float slope = (fbm((u + 0.05) * _NoiseFrequency, seed) * amplitude - y) / 0.05;
                        float d = abs(v - y) * rsqrt(1.0 + slope * slope);
                        filaments += bolt(d, _FilamentWidth, _GlowRadius * 0.5, aa);
                    }

                    // Clip to the beam itself; past its ends the strands would run on as straight lines
                    filaments *= _FilamentStrength * step(0.0, u) * step(u, beam_length);
                }

                // Muzzle glow at the weapon, flaring on each pulse
                float muzzle = 0.0;
                if (_MuzzleRadius > 0.0)
                {
                    muzzle = exp(-dot(i.local, i.local) / (_MuzzleRadius * _MuzzleRadius)) * (0.6 + i.pulse);
                }

                // Fade out over the last _TipFade of the range
                float tip = 1.0 - smoothstep(beam_length * (1.0 - _TipFade), beam_length, u);

                float brightness = 1.0 + _PulseBrightness * i.pulse;
                float3 color = _Color.rgb * (glow * flow + filaments.y + muzzle) + (core + filaments.x) * 2.5;
                return half4(color * brightness * tip, 0);
            }
            ENDHLSL
        }
    }
}
