Shader "Unit/Neon"
{
    Properties
    {
        [Enum(Square, 0, Triangle, 1, Hexagon, 2, Circle, 3)]
        _Shape ("Shape", Float) = 0
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.5
        _LineWidth ("Line Width", Range(0.01, 0.2)) = 0.05
        _CoreWidth ("White Core Width (fraction of line)", Range(0, 1)) = 0.35
        _GlowRadius ("Glow Radius", Range(0.01, 0.3)) = 0.07
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.6
        _Fill ("Fill", Range(0, 1)) = 0

        [Header(Hit Flash)]
        // Per instance from HitFlashTime; entities without it keep this default and never flash
        [HideInInspector] _HitTime ("Hit Time", Float) = -1000
        _HitFlashDuration ("Hit Flash Duration", Range(0.02, 0.5)) = 0.12
        _HitFlashWhiten ("Hit Flash Whiten", Range(0, 1)) = 0.75
        _HitFlashFill ("Hit Flash Fill", Range(0, 1)) = 0.35
        _HitFlashBoost ("Hit Flash Brightness Boost", Range(0, 2)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Neon"

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Shape;
                float _Intensity;
                float _LineWidth;
                float _CoreWidth;
                float _GlowRadius;
                float _GlowStrength;
                float _Fill;
                float _HitTime;
                float _HitFlashDuration;
                float _HitFlashWhiten;
                float _HitFlashFill;
                float _HitFlashBoost;
            CBUFFER_END

            // Global, set every frame
            float _GameTime;

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START (MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DOTS_INSTANCED_PROP(float, _HitTime)
            UNITY_DOTS_INSTANCING_END (MaterialPropertyMetadata)
            #define BASE_COLOR UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
            #define HIT_TIME UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _HitTime)
            #else
            #define BASE_COLOR _BaseColor
            #define HIT_TIME _HitTime
            #endif

            struct attributes
            {
                float4 position_os : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct varyings
            {
                float4 position_cs : SV_POSITION;
                // Object-space position on the (grown) quad; the shape spans [-0.5, 0.5]
                float2 local : TEXCOORD0;
                nointerpolation float3 hue : TEXCOORD1;
                // Tint alpha dims the whole shape (additive blending ignores the alpha channel)
                nointerpolation float fade : TEXCOORD2;
                // 1 right after a hit, eases to 0 over _HitFlashDuration
                nointerpolation float flash : TEXCOORD3;
            };

            // Shapes on the unit quad: full quad square, equilateral triangle (side 1, apex up,
            // vertically centered), flat-top hexagon with its points on the quad's side edges, circle touching all four edges.
            float sd_square(float2 p)
            {
                float2 d = abs(p) - 0.5;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            float sd_triangle(float2 p)
            {
                const float k = 1.7320508; // sqrt(3)
                const float r = 0.5; // half the side
                p.x = abs(p.x) - r;
                p.y += 0.4330127; // half the height, so base and apex are equidistant from the center
                if (p.x + k * p.y > 0.0) p = float2(p.x - k * p.y, -k * p.x - p.y) * 0.5;
                p.x -= clamp(p.x, -2.0 * r, 0.0);
                return -length(p) * sign(p.y);
            }

            float sd_hexagon(float2 p)
            {
                const float3 k = float3(-0.8660254, 0.5, 0.5773503);
                const float r = 0.4330127; // apothem of a hexagon with circumradius 0.5
                p = abs(p);
                p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
                p -= float2(clamp(p.x, -k.z * r, k.z * r), r);
                return length(p) * sign(p.y);
            }

            float sd_circle(float2 p)
            {
                return length(p) - 0.5;
            }

            float sd_shape(float2 p)
            {
                if (_Shape < 0.5) return sd_square(p);
                if (_Shape < 1.5) return sd_triangle(p);
                if (_Shape < 2.5) return sd_hexagon(p);
                return sd_circle(p);
            }

            // 1 inside half_width, 0 outside, antialiased across +-aa
            float band(float dist, float half_width, float aa)
            {
                return 1.0 - smoothstep(half_width - aa, half_width + aa, dist);
            }

            varyings vert(attributes v)
            {
                varyings o;
                UNITY_SETUP_INSTANCE_ID(v);

                // Grow the quad 2.5 glow radii past the edge, where the halo has faded to about 0.2%
                float2 local = v.position_os.xy * (1.0 + 5.0 * _GlowRadius);
                o.position_cs = TransformObjectToHClip(float3(local, v.position_os.z));
                o.local = local;

                // Normalize so dark tints (e.g. the tank's) still glow at full neon brightness
                float3 c = BASE_COLOR.rgb;
                float m = max(max(c.r, c.g), c.b);
                o.hue = m > 1e-3 ? c / m : float3(1, 1, 1);
                o.fade = BASE_COLOR.a;

                float since_hit = _GameTime - HIT_TIME;
                float flash = since_hit >= 0.0 ? saturate(1.0 - since_hit / _HitFlashDuration) : 0.0;
                o.flash = flash * flash;
                return o;
            }

            half4 frag(varyings i) : SV_Target
            {
                const float tube_boost = 1.5;

                float d = sd_shape(i.local);
                float aa = max(fwidth(d), 1e-4);

                // Tube centered just inside the edge so the line doesn't grow the silhouette;
                // never thinner than a pixel so small enemies don't shimmer
                float half_width = max(_LineWidth, aa) * 0.5;
                float to_center = abs(d + half_width);

                float tube = band(to_center, half_width, aa);
                // step: a zero-width core would otherwise still leave a one-pixel line
                float core = band(to_center, half_width * _CoreWidth, aa) * step(1e-4, _CoreWidth);

                float edge = max(to_center - half_width, 0.0);
                float glow = exp(-(edge * edge) / (_GlowRadius * _GlowRadius)) * _GlowStrength;

                // Hit flash: whiten the hue, light up the interior and push the brightness
                float inside = 1.0 - smoothstep(-aa, aa, d);
                float fill = inside * saturate(_Fill + i.flash * _HitFlashFill);
                float3 hue = lerp(i.hue, float3(1, 1, 1), i.flash * _HitFlashWhiten);

                float3 color = hue * (tube * tube_boost + glow + fill) + core;
                return half4(color * (_Intensity * i.fade * (1.0 + i.flash * _HitFlashBoost)), 0);
            }
            ENDHLSL
        }
    }
}
