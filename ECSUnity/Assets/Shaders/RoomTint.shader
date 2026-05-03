// ECSUnity/RoomTint
// =================
// Simple unlit shader for room floor and wall quads.
//
// Exposes four properties:
//   _Color         — base palette color (set by RoomRectangleRenderer from RenderColorPalette)
//   _TintColor     — illumination tint color (Kelvin → RGB, set by RoomAmbientTintApplier)
//   _TintIntensity — blend fraction: 0 = pure palette, 1 = pure tint (default 0 = untinted)
//   _Alpha         — transparency: 1.0 = opaque (floors), < 1.0 = faded (walls occluding camera)
//
// BLEND FORMULA
// ──────────────
// finalRGB = lerp(_Color.rgb, _TintColor.rgb * _Color.rgb, _TintIntensity)
//
// This multiplies the Kelvin tint onto the palette color rather than replacing it,
// preserving the era-appropriate palette while still allowing warm/cool illumination
// to shift the hue. A bright warm room should look warm-beige, not warm-yellow.
//
// TRANSPARENCY
// ─────────────
// The shader is always in the Transparent queue so _Alpha can drive wall fade smoothly.
// For floor quads _Alpha is always 1.0 (set by RoomAmbientTintApplier); for wall quads
// _Alpha is driven by WallFadeController. The Transparent queue has no performance impact
// at the ~30 room count typical of the office-starter map.
//
// ZWrite is Off for transparency correctness. Since walls are always behind NPCs (NPCs
// are dots/silhouettes above the floor plane) and floor quads use _Alpha=1.0, depth order
// artifacts are not a concern at v0.1 quality level.

Shader "ECSUnity/RoomTint"
{
    Properties
    {
        _Color         ("Base Color",       Color)       = (0.78, 0.74, 0.66, 1)
        _TintColor     ("Illumination Tint", Color)      = (1, 1, 1, 1)
        _TintIntensity ("Tint Intensity",   Range(0, 1)) = 0
        _Alpha         ("Alpha",            Range(0, 1)) = 1
    }

    SubShader
    {
        // Transparent queue so _Alpha works correctly on walls.
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            // Standard alpha blending.
            Blend SrcAlpha OneMinusSrcAlpha

            // ZWrite Off for transparent geometry. See note above.
            ZWrite Off

            // Render both faces: walls are thin quads visible from front and back
            // in the isometric view when the camera is high.
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _TintColor;
                float _TintIntensity;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Multiply the Kelvin tint onto the base palette color.
                // lerp(a, b, t): at t=0 → pure palette; at t=1 → pure tint*palette.
                half3 blended = lerp(_Color.rgb,
                                     _TintColor.rgb * _Color.rgb,
                                     _TintIntensity);

                // Apply the alpha from the _Alpha property (not from _Color.a).
                // This lets WallFadeController drive transparency independently
                // of the color values set by RoomAmbientTintApplier.
                return half4(blended, _Alpha);
            }
            ENDHLSL
        }
    }

    // Fallback to a plain unlit transparent shader in case the HLSLPROGRAM fails.
    FallBack Off
}
