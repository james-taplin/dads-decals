// Dad's Decals projector shader (original work, MIT).
// Draw a target mesh with this material and the decal appears wherever that mesh's surface
// lies inside the decal box. The box is given as a world -> decal-space matrix, where the box
// is the unit cube centred on the origin, X/Y map to the image and +Z is the projection direction.
Shader "DadsDecals/Projected"
{
    Properties
    {
        _MainTex ("Decal", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _WrapCos ("Cosine of max wrap angle", Range(-1,1)) = 0.25
        _Glossiness ("Smoothness", Range(0,1)) = 0.35
        _Metallic ("Metallic", Range(0,1)) = 0
    }

    SubShader
    {
        // Forward-rendered after the deferred opaque pass, before regular transparents (glass).
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" "DisableBatching"="True" }
        Offset -1, -1

        CGPROGRAM
        #pragma surface surf Standard decal:blend vertex:vert nolightmap nodynlightmap nodirlightmap nometa noinstancing
        #pragma target 3.0

        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _Color;
        half _Opacity;
        half _WrapCos;
        half _Glossiness;
        half _Metallic;

        float4x4 _WorldToDecal;
        float3 _DecalForward;   // world-space projection direction (pointing into the surface)

        struct Input
        {
            float3 decalPos;
            float facing;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.decalPos = mul(_WorldToDecal, float4(worldPos, 1)).xyz;
            o.facing = dot(UnityObjectToWorldNormal(v.normal), -_DecalForward);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Outside the box, or surface turned away further than the wrap angle: nothing.
            clip(0.5 - abs(IN.decalPos));
            clip(IN.facing - _WrapCos);

            // Not TRANSFORM_TEX: it pastes its argument unbracketed, which broke this expression.
            float2 uv = (IN.decalPos.xy + 0.5) * _MainTex_ST.xy + _MainTex_ST.zw;
            fixed4 c = tex2D(_MainTex, uv) * _Color;

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a * _Opacity;
        }
        ENDCG
    }

    FallBack Off
}
