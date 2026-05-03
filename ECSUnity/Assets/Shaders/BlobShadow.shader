// Contact-aware blob shadow disc.
// UV.y = 0 → contact point (darkest), UV.y = 1 → far shadow end (lightest).
// Disc is positioned by BlobShadowCaster so UV.y=0 sits at the ball's base.
// Multiply-blend darkens the ground; the Bayer post-process dithers the gradient.
Shader "Custom/BlobShadow"
{
    Properties
    {
        _Opacity ("Shadow Opacity",   Range(0, 1))    = 0.75
        _Falloff ("Contact Falloff",  Range(0.3, 3))  = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BLOB_SHADOW"

            ZWrite  Off
            ZTest   LEqual
            Blend   DstColor Zero   // multiply: darkens whatever is below
            Offset  -1, -1          // prevents Z-fight with ground

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
                float _Falloff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;

                // Forward: UV.y = 0 at contact (full dark), 1 at far end (zero dark).
                float forwardT  = uv.y;
                float forward   = pow(max(0.0, 1.0 - forwardT), _Falloff);

                // Shadow narrows at the contact tip, widens toward the far end.
                float halfW     = lerp(0.3, 1.0, saturate(forwardT * 1.4));
                float lateralT  = abs(uv.x - 0.5) * 2.0;
                float lateral   = saturate(1.0 - smoothstep(0.5, 1.0, lateralT / halfW));

                float shadow    = forward * lateral * _Opacity;
                float mult      = 1.0 - shadow;
                return half4(mult, mult, mult, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
