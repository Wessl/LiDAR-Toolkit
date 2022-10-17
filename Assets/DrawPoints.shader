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
           
            struct shaderdata
            {
                float4 vertex : SV_POSITION;
                float4 color : TEXCOORD1;
            };
 
            shaderdata VSMain(uint id : SV_VertexID)
            {
                shaderdata vs;
                vs.vertex = UnityObjectToClipPos(float4(posbuffer[id], 1.0));
                vs.color = float4(colorbuffer[id]);
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