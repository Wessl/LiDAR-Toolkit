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
 
            StructuredBuffer<float3> posbuffer;
            StructuredBuffer<float3> colorbuffer;    // not used atm
 
            double Mod(double x, double y)
            {
                return x - y * floor(x/y);
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
                float3 center = posbuffer[instance];
                float u = sign(Mod(20.0, Mod(float(id), 6.0) + 2.0));
                float v = sign(Mod(18.0, Mod(float(id), 6.0) + 2.0));
                uv = float2(u,v);
                float4 position = float4(float3(sign(u) - 0.5, 0.0, sign(v) - 0.5) + center, 1.0);
                return UnityObjectToClipPos(position);
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