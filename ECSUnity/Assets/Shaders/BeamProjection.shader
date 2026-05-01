// ECSUnity/BeamProjection
// ========================
// Translucent shader for sun-beam and night-spill-beam quads.
//
// DESIGN
// ───────
// The beam is a flat quad lying on the XZ floor plane, rotated and scaled by BeamRenderer.
// This shader renders the quad with:
//   - Base tint color from _Color (warm yellow for day, cool white for night)
//   - Alpha driven by _Color.a (set each frame by BeamRenderer based on sun elevation)
//   - Radial alpha falloff along the quad's V axis (0 = aperture edge, 1 = room interior)
//     so the beam fades away from the window naturally
//   - Additive-ish blending (SrcAlpha One) gives a light-bloom effect on the floor
//
// UV CONVENTION
// ──────────────
// The quad's UV ranges from (0,0) at one corner to (1,1) at the opposite corner.
// The beam quad is oriented so V=0 is at the aperture (window edge) and V=1 is the
// far end of the beam (room interior). Alpha falloff is applied along V.
//
// PERFORMANCE
// ────────────
// All day beams share one material (BeamRenderer creates _dayMaterial once).
// Night beams share a second material. With GPU instancing enabled, this is 1–2 draw calls
// for all 40 beams even without batching.

Shader "ECSUnity/BeamProjection"
{
    Properties
    {
        _Color       ("Beam Color + Alpha", Color) = (1, 0.92, 0.60, 0.30)
        _FalloffPower("Falloff Power",  Range(0.5, 4)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+1"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            // Additive-ish blending: SrcAlpha * src + 1 * dst.
            // This brightens the floor where the beam lands, simulating scattered sunlight.
            Blend SrcAlpha One

            ZWrite Off
            ZTest  LEqual
            Cull   Off

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float  _FalloffPower;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Distance from centre in U direction (cross-beam width falloff).
                // u=0..1; distance from centre = |u - 0.5| * 2 → 0 at centre, 1 at edges.
                float edgeDist = abs(i.uv.x - 0.5) * 2.0;
                float widthFade = 1.0 - pow(edgeDist, _FalloffPower);

                // V falloff: beam fades as it travels into the room (V=1 = far end).
                float lengthFade = 1.0 - pow(i.uv.y, _FalloffPower);

                float alpha = _Color.a * widthFade * lengthFade;

                return fixed4(_Color.rgb, saturate(alpha));
            }
            ENDCG
        }
    }

    Fallback "Particles/Additive"
}
