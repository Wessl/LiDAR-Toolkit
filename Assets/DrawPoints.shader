Shader "Draw Points"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM        
            #pragma vertex VSMain
            #pragma fragment PSMain
            #pragma target 5.0
 
            StructuredBuffer<float3> posbuffer;
            StructuredBuffer<float4> colorbuffer;
            float4 farcolor;
            float4 camerapos;
            float dist;
            float fardist;
           
            struct shaderdata
            {
                float4 vertex : SV_POSITION;
                float4 color : TEXCOORD1;
            };
 
            shaderdata VSMain(uint id : SV_VertexID)
            {
                shaderdata vs;
                vs.vertex = UnityObjectToClipPos(float4(posbuffer[id], 1.0));
                dist = distance(camerapos, posbuffer[id]);
                float4 newcol = lerp(float4(colorbuffer[id]), farcolor, clamp((dist/fardist),0.0001,1));
                vs.color = newcol;
                return vs;
            }
 
            float4 PSMain(shaderdata ps) : SV_TARGET
            {
                return ps.color;
            }
           
            ENDCG
        }
    }
}