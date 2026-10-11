// Writes, for each point across a decal, how far into the decal box the first surface is
// (0 = front of the box, 1 = back). The decal shader then skips surfaces well behind that one,
// so a decal on a cab side doesn't also land on the inside of the far wall.
// Drawn by DecalRenderer with a CommandBuffer into a small RHalf texture; no camera involved.
Shader "Hidden/DadsDecals/Depth"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        // Keep the nearest surface: the texture is cleared to 1 (back of the box).
        BlendOp Min
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Mesh space -> decal space, set per draw. (unity_ObjectToWorld isn't reliable for
            // command-buffer draws made outside a camera render.)
            float4x4 _ObjectToDecal;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float z : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                float3 d = mul(_ObjectToDecal, float4(vertex.xyz, 1)).xyz;
                float y = d.y * 2;
            #if UNITY_UV_STARTS_AT_TOP
                // Straight to clip space with no projection matrix, so flip here to match how the
                // decal shader samples the texture (uv = decal xy + 0.5).
                y = -y;
            #endif
                v2f o;
                o.pos = float4(d.x * 2, y, 0.5, 1);
                o.z = d.z + 0.5;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // In front of the box (outside the car beyond the placement margin): not a surface we paint.
                clip(i.z);
                return i.z;
            }
            ENDCG
        }
    }
}
