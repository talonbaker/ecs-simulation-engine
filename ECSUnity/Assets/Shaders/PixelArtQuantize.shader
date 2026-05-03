// Pixel-art post-process shader (full resolution — no downscale).
// Pass 0: Bayer 4×4 ordered dither + nearest-palette-entry snap.
// Pass 1: plain blit (used internally by PixelArtRenderPass for the copy step).
// Used by PixelArtRenderPass via Blitter.BlitTexture.
Shader "Custom/PixelArtQuantize"
{
    Properties
    {
        // _BlitTexture / _BlitScaleBias are set by URP's Blitter — do not rename.
        [HideInInspector] _BlitTexture  ("Source",      2D)    = "white" {}
        [HideInInspector] _BlitScaleBias("Scale Bias", Vector) = (1,1,0,0)
        _PaletteTex     ("Palette Texture", 2D)    = "white" {}
        _PaletteCount   ("Palette Width",  Float)  = 16
        _DitherStrength ("Dither Strength", Range(0,1)) = 0.25
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

        // ── Pass 0: Bayer dither + palette snap ─────────────────────────────
        Pass
        {
            Name "PIXEL_ART_DITHER"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragDither

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_PaletteTex);
            SAMPLER(sampler_PaletteTex);
            float _PaletteCount;
            float _DitherStrength;

            // Bayer 4×4 threshold, values in [0, 1).
            float BayerThreshold(uint2 pos)
            {
                const float bayer[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                return bayer[(pos.y & 3u) * 4u + (pos.x & 3u)];
            }

            // Nearest-neighbour search in _PaletteTex using squared RGB distance.
            half3 NearestPaletteColor(half3 col)
            {
                half3 best     = 0;
                half  bestDist = 1e9h;
                int   count    = (int)max(_PaletteCount, 1.0);

                UNITY_LOOP
                for (int i = 0; i < count; i++)
                {
                    float u     = (i + 0.5) / count;
                    half3 entry = SAMPLE_TEXTURE2D(_PaletteTex, sampler_PaletteTex,
                                                   float2(u, 0.5)).rgb;
                    half3 delta = col - entry;
                    half  dist  = dot(delta, delta);
                    if (dist < bestDist) { bestDist = dist; best = entry; }
                }
                return best;
            }

            half4 FragDither(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                                               input.texcoord);

                // Screen-space pixel coordinate drives the Bayer pattern.
                uint2 pixelPos = (uint2)(input.texcoord * _ScreenParams.xy);
                float threshold = BayerThreshold(pixelPos);

                // Offset colour by dithered threshold, then snap to palette.
                col.rgb = saturate(col.rgb + (threshold - 0.5) * _DitherStrength);
                col.rgb = NearestPaletteColor(col.rgb);
                return col;
            }
            ENDHLSL
        }

        // ── Pass 1: plain blit (internal copy / fallback) ───────────────────
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
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
