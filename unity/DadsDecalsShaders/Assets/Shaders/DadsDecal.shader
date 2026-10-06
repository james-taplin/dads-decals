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
        _Grime ("Grime", Range(0,1)) = 0
        _GrimeSeed ("Grime seed", Float) = 0
        _GrimeColor ("Grime colour", Color) = (0.18,0.15,0.12,1)
        _Chipping ("Chipping", Range(0,1)) = 0
        _ChipSeed ("Chip seed", Float) = 0
        _Glow ("Glow", Range(0,4)) = 0
        _Highlight ("Selection highlight", Range(0,1)) = 0

    }

    SubShader
    {
        // Queue 2499 = the last opaque slot. Unity only gives shadows to queues <= 2500, and opaque
        // queues draw before screen-space fog and before transparents (smoke, steam, glass), so the
        // decal is shadowed and fogged like the paint under it. (It was Transparent-100: no shadows,
        // and it drew on top of the fog.)
        Tags { "Queue"="AlphaTest+49" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" "DisableBatching"="True" }
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
        half _Grime;
        float _GrimeSeed;
        fixed4 _GrimeColor;
        half _Chipping;
        float _ChipSeed;
        half _Glow;
        half _Highlight;

        float4x4 _WorldToDecal;
        float3 _DecalForward;   // world-space projection direction (pointing into the surface)
        float2 _DecalSize;      // decal width/height in metres, so chipping has a real-world scale

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

        float hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float valueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            float a = hash21(i), b = hash21(i + float2(1, 0)), c = hash21(i + float2(0, 1)), d = hash21(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float fbm(float2 p)
        {
            return valueNoise(p) * 0.55 + valueNoise(p * 2.7 + 17.1) * 0.3 + valueNoise(p * 7.3 + 41.7) * 0.15;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Outside the box, or surface turned away further than the wrap angle: nothing.
            clip(0.5 - abs(IN.decalPos));
            clip(IN.facing - _WrapCos);

            // Not TRANSFORM_TEX: it pastes its argument unbracketed, which broke this expression.
            float2 uv = (IN.decalPos.xy + 0.5) * _MainTex_ST.xy + _MainTex_ST.zw;
            fixed4 c = tex2D(_MainTex, uv) * _Color;

            // Grime: a procedural mask of run-down streaks (noise stretched vertically) plus
            // blotches, in real-world units so it looks the same on big and small decals.
            // Strength grows the covered area; the seed picks the pattern; colour is editable.
            float2 gp = IN.decalPos.xy * max(_DecalSize, 0.01) + _GrimeSeed;
            float streaks = fbm(float2(gp.x * 9, gp.y * 1.2));
            float blotches = fbm(gp * 3.5 + 31.7);
            float mask = streaks * 0.6 + blotches * 0.4;
            float grime = saturate((mask - (1 - _Grime) * 0.75) / 0.25) * _Grime;
            c.rgb = lerp(c.rgb, _GrimeColor.rgb, grime * 0.85 * _GrimeColor.a);

            // Chipping: procedural paint loss, a few cm across, fixed per decal by its seed.
            float2 chipUV = IN.decalPos.xy * max(_DecalSize, 0.01) * 18 + _ChipSeed;
            float n = fbm(chipUV);
            float threshold = _Chipping * 0.75 - 0.05;
            c.a *= smoothstep(threshold, threshold + 0.06, n);

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness * (1 - grime * 0.7);   // dirt is dull
            o.Emission = c.rgb * _Glow
                + _Highlight * float3(0.25, 0.6, 1.0) * (0.55 + 0.45 * sin(_Time.y * 6));
            o.Alpha = c.a * _Opacity;
        }
        ENDCG
    }

    FallBack Off
}
