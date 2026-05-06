Shader "Custom/Outline"
{
    Properties
    {
        _Color ("Outline Color", Color) = (0.4, 0.85, 1.0, 1.0)
    }

    SubShader
    {
        // Render after solid geometry so the outline sits cleanly on top.
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OUTLINE"

            // Inverted hull: cull front faces, render back faces only.
            // OutlineRenderer.cs scales the duplicate mesh up so back faces
            // peek out from behind the original, forming the visible outline.
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
