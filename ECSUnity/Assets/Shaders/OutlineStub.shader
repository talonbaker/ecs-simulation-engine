// STUB ONLY — sandbox chain-validation. Not for production use.
// Applies a visible red vignette at screen edges to confirm OutlineStubRendererFeature
// executes after PixelArtRendererFeature in the SandboxURP-Renderer chain.
Shader "Custom/OutlineStub"
{
    Properties
    {
        [HideInInspector] _BlitTexture  ("Source",      2D)    = "white" {}
        [HideInInspector] _BlitScaleBias("Scale Bias", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  Off

        // Pass 0: red vignette
        Pass
        {
            Name "OUTLINE_STUB_VIGNETTE"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragStub

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragStub(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture,
                    sampler_LinearClamp, input.texcoord);

                // Red vignette: strength rises toward screen edges.
                float2 uv       = input.texcoord - 0.5;
                float  edgeDist = max(abs(uv.x), abs(uv.y));
                float  vignette = smoothstep(0.3, 0.5, edgeDist);

                col.rgb = lerp(col.rgb, half3(0.8, 0.1, 0.1), vignette * 0.6);
                return col;
            }
            ENDHLSL
        }

        // Pass 1: plain copy (used internally by the C# pass to blit active color to temp)
        Pass
        {
            Name "OUTLINE_STUB_COPY"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragCopy

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragCopy(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
