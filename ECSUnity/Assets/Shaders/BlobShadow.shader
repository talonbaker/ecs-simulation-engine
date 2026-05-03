// Soft multiply-blend blob shadow disc.
// Attach to a flat quad positioned on the ground by BlobShadowCaster.
// Darkens the ground smoothly so the Bayer dither post-process converts
// the gradient into a dithered shadow matching the ball shading.
Shader "Custom/BlobShadow"
{
    Properties
    {
        _Opacity  ("Shadow Opacity",  Range(0, 1))    = 0.65
        _Softness ("Edge Softness",   Range(0.05, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
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
                float _Softness;
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

                // Radial distance from disc centre: 0 at centre, 1 at rim.
                float2 center = input.uv - 0.5;
                float  dist   = length(center) * 2.0;

                // Smooth falloff: full shadow at centre, zero at rim.
                float inner  = max(0.0, 1.0 - _Softness);
                float shadow = saturate(1.0 - smoothstep(inner, 1.0, dist)) * _Opacity;

                // Multiply blend output: 1 = no change, approaching 0 = black.
                float mult = 1.0 - shadow;
                return half4(mult, mult, mult, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
