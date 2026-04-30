Shader "Custom/Outline"
{
    Properties
    {
        _Color ("Outline Color", Color) = (0.4, 0.85, 1.0, 1.0)
    }

    SubShader
    {
        // Render after solid geometry so the outline sits cleanly on top.
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }

        Pass
        {
            Name "OUTLINE"

            // Inverted hull: cull front faces, render back faces only.
            // OutlineRenderer.cs scales the duplicate mesh up so back faces
            // peek out from behind the original, forming the visible outline.
            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
