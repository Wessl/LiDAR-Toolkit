Shader "Draw Circles"
{
    Subshader
    {
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain
            #pragma target 5.0
 
            float BufferX[2048];
            float BufferY[2048];
 
            double mod(double x, double y)
            {
                return x-(floor(x/y)*y);
            }
 
            float3 hash(float p)
            {
                float3 p3 = frac(p.xxx * float3(.1239, .1237, .2367));
                p3 += dot(p3, p3.yzx+63.33);
                return frac((p3.xxy+p3.yzz)*p3.zyx);
            }
 
            float4 VSMain (uint id:SV_VertexID, out float2 uv:TEXCOORD0, inout uint instance:SV_INSTANCEID) : SV_POSITION
            {
                // holy shit przemyslav is a fucking genius. the modulos for v turn into a 0,0,1,0,1,1... Got damn. which is the UV coordinates for two triangles forming a quad. ok
                float3 center = float3(BufferX[instance], 0.0, BufferY[instance]);
                float u = mod(id, 2.0);
                float v = sign(mod(126.0L,mod(double(id),6.0L)+6.0L));
                uv = float2(u,v);
                return UnityObjectToClipPos(float4(float3(sign(u)-0.5, 0.0, v-0.5) + center,1.0));
            }
 
            float4 PSMain (float4 vertex:SV_POSITION, float2 uv:TEXCOORD0, uint instance:SV_INSTANCEID) : SV_Target
            {
                float2 S = uv*2.0-1.0;
                if (dot(S.xy, S.xy) > 1.0) discard;
                return float4(hash(float(instance)), 1.0);
            }
            ENDCG
        }
    }
}