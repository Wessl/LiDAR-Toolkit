Shader "Draw Circles Experimental"
{
    Subshader
    {
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMain
            #pragma target 5.0
            #include "UnityCG.cginc"
 
            StructuredBuffer<float3> posbuffer;
            StructuredBuffer<float4> colorbuffer;    // not used atm
            StructuredBuffer<float> timebuffer;
            StructuredBuffer<float3> normalbuffer;
            float fadeTime;
            float _Scale;
            float4 farcolor;
            float4 camerapos;
            float dist;
            float fardist;
            bool _SmoothEdges;

            struct shaderdata
            {
                float4 vertex : SV_POSITION;
                float4 color : TEXCOORD1;
                float2 uv : TEXCOORD0;
                uint instance : SV_INSTANCEID;
            };

//#ifdef GL_ARB_gpu_shader_fp64
            double Mod(double x, double y)
            {
                return x - y * floor(x/y);
            }
//#else
//            float Mod(float x, float y)
//            {
//                return x - y * floor(x/y);
//            }
//#endif
            
            shaderdata VSMain (uint id:SV_VertexID, uint instance:SV_INSTANCEID)
            {
                shaderdata vs;
                float3 center = posbuffer[instance];
                float internalMod = Mod(float(id), 6.0) + 2.0;
                float u = sign(Mod(20.0, internalMod));
                float v = sign(Mod(18.0, internalMod));
                vs.uv = float2(u,v);
                dist = distance(camerapos, center);
                // This assumes we are only setting either the normal buffer or the color buffer 
                vs.color = float4(normalbuffer[instance],1) + colorbuffer[instance];
                vs.color = lerp(vs.color, farcolor, clamp(dist/fardist,0,1));
                // billboard.
                float4 pos2 = mul(UNITY_MATRIX_P, 
                float4(UnityObjectToViewPos(float3(0.0,0.0,0.0)),1.0)
                + float4(sign(u)-0.5, sign(v)-0.5, 0.0, 0.0)*2
                * float4(_Scale, _Scale, 1.0, 1.0));
                
                vs.vertex = UnityObjectToClipPos(float4(center*2.0, 1.0)) + pos2;
                vs.instance = instance;
                
                return vs;
            }
 
            float4 PSMain (shaderdata ps) : SV_Target
            {
                float2 S = ps.uv*2.0-1.0;
                ps.color.a = 1;
                if (_SmoothEdges) ps.color.a = 1/(dot(S.xy, S.xy)*10)-0.1f;
                else if (dot(S.xy, S.xy) > 1.0) discard;
                if (fadeTime != 0) ps.color.a *= max((timebuffer[ps.instance]+fadeTime-_Time.y) / (fadeTime),0);
                
                return ps.color;
            }
            ENDCG
        }
    }
}