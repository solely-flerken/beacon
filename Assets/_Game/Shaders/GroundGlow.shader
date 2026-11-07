Shader "Unit/GroundGlow"
{
    Properties
    {
        _BaseColor ("Color (alpha = brightness)", Color) = (1, 1, 1, 0.35)
        _Softness ("Edge Softness (fraction of radius)", Range(0.01, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            // Below the neon queue, like a light pool on the ground under the neon shapes
            "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GroundGlow"

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
                float _Softness;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START (MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DOTS_INSTANCING_END (MaterialPropertyMetadata)
            #define BASE_COLOR UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor)
            #else
            #define BASE_COLOR _BaseColor
            #endif

            struct attributes
            {
                float4 position_os : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct varyings
            {
                float4 position_cs : SV_POSITION;
                // Object-space position on the quad; the circle touches its edges at radius 0.5
                float2 local : TEXCOORD0;
                nointerpolation float4 color : TEXCOORD1;
            };

            varyings vert(attributes v)
            {
                varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.position_cs = TransformObjectToHClip(v.position_os.xyz);
                o.local = v.position_os.xy;
                o.color = BASE_COLOR;
                return o;
            }

            half4 frag(varyings i) : SV_Target
            {
                // 0 at the center, 1 at the rim; full brightness core, smooth falloff over the outer _Softness
                float r = length(i.local) * 2.0;
                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, r);
                return half4(i.color.rgb * (i.color.a * a), 0);
            }
            ENDHLSL
        }
    }
}
