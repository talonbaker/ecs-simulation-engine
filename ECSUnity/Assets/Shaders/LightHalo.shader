// ECSUnity/LightHalo
// ====================
// Soft radial halo shader for light-source quad overlays.
//
// DESIGN
// ───────
// Each light source in the scene gets a flat XZ quad centred on the fixture's tile.
// This shader renders the quad with a smooth radial falloff from bright centre to
// transparent edge, simulating a soft pool of light on the floor.
//
// The halo is purely additive (SrcAlpha One) to brighten the floor beneath the fixture
// rather than compositing on top and potentially darkening it.
//
// _Color: the Kelvin-temperature tint + alpha of the halo. Set per material instance by
// LightSourceHaloRenderer based on the source's state and intensity.
//
// FALLOFF
// ────────
// UV (0.5, 0.5) = centre of the quad. Distance from centre = length(uv - 0.5) * 2.
// Alpha = 1 - pow(dist, _FalloffPower). At FalloffPower = 2 this is a smooth cosine-like
// disc. Higher values give a sharper bright spot with a softer edge.
//
// PERFORMANCE
// ────────────
// All halos share one material template. LightSourceHaloRenderer clones it per source
// so each halo can have its own color/alpha. At ~40 sources this is 40 draw calls but
// each is a single 2-triangle quad — total GPU time is negligible vs. the engine tick cost.
// GPU instancing is enabled for scenes with batching support.

Shader "ECSUnity/LightHalo"
{
    Properties
    {
        _Color        ("Halo Color + Alpha", Color) = (1, 0.92, 0.70, 0.5)
        _FalloffPower ("Falloff Power",  Range(1, 4)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+2"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Pass
        {
            // Additive blend: halos only ever add brightness to the scene.
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
                // Radial distance from quad centre: 0 at centre, 1 at corners (before clamping).
                // length(uv - 0.5) * 2 maps the quad half-diagonal to 1.0.
                float2 delta = i.uv - float2(0.5, 0.5);
                float  dist  = length(delta) * 2.0;

                // Smooth radial falloff.  Clamp dist at 1 so corners are fully transparent.
                float alpha = saturate(1.0 - pow(saturate(dist), _FalloffPower));

                return fixed4(_Color.rgb, _Color.a * alpha);
            }
            ENDCG
        }
    }

    Fallback "Particles/Additive"
}
