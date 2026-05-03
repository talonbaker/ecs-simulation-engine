// Pixel-art post-process shader.
// Pass 0: down-sample + nearest-palette-entry quantize.
// Pass 1: down-sample only (palette quantize disabled).
// Used by PixelArtRenderPass via Blitter.BlitCameraTexture.
Shader "Custom/PixelArtQuantize"
{
    Properties
    {
        // _BlitTexture is set by URP's Blitter — do not rename.
        [HideInInspector] _BlitTexture  ("Source",      2D)    = "white" {}
        [HideInInspector] _BlitScaleBias("Scale Bias", Vector) = (1,1,0,0)
        _PaletteTex   ("Palette Texture", 2D)    = "white" {}
        _PaletteCount ("Palette Width",  Float)  = 16
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

        // ── Pass 0: quantize ────────────────────────────────────────────────
        Pass
        {
            Name "PIXEL_ART_QUANTIZE"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragQuantize

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_PaletteTex);
            SAMPLER(sampler_PaletteTex);
            float _PaletteCount;

            // Find the nearest color in _PaletteTex using squared-distance search.
            half3 NearestPaletteColor(half3 col)
            {
                half3 best     = 0;
                half  bestDist = 1e9;
                int   count    = (int)max(_PaletteCount, 1.0);

                UNITY_LOOP
                for (int i = 0; i < count; i++)
                {
                    float u     = (i + 0.5) / count;
                    half3 entry = SAMPLE_TEXTURE2D_X(_PaletteTex, sampler_PaletteTex, float2(u, 0.5)).rgb;
                    half3 delta = col - entry;
                    half  dist  = dot(delta, delta);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best     = entry;
                    }
                }
                return best;
            }

            half4 FragQuantize(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture,
                    sampler_LinearClamp, input.texcoord);
                col.rgb = NearestPaletteColor(col.rgb);
                return col;
            }
            ENDHLSL
        }

        // ── Pass 1: blit only (no quantize, linear) ────────────────────────
        Pass
        {
            Name "PIXEL_ART_BLIT"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragBlit

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 FragBlit(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X(_BlitTexture,
                    sampler_LinearClamp, input.texcoord);
            }
            ENDHLSL
        }

        // ── Pass 2: point-filter upscale (pixel-art final blit) ─────────────
        Pass
        {
            Name "PIXEL_ART_UPSCALE"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragUpscale

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_PointClamp);

            half4 FragUpscale(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X(_BlitTexture,
                    sampler_PointClamp, input.texcoord);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
