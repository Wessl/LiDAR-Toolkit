Shader "Draw Squares"
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

            struct shaderdata
            {
                float4 vertex : SV_POSITION;
                float4 color : TEXCOORD1;
                float2 uv : TEXCOORD0;
                uint instance : SV_INSTANCEID;
            };

#ifdef GL_ARB_gpu_shader_fp64
            double Mod(double x, double y)
            {
                return x - y * floor(x/y);
            }
#else
            float Mod(float x, float y)
            {
                return x - y * floor(x/y);
            }
#endif

            // not used anymore but it looks cool...!
            float3 hash(float p)
            {
                float3 p3 = frac(p.xxx * float3(.1239, .1237, .2367));
                p3 += dot(p3, p3.yzx+63.33);
                return frac((p3.xxy+p3.yzz)*p3.zyx);
            }
            
            shaderdata VSMain (uint id:SV_VertexID, uint instance:SV_INSTANCEID)
            {
                shaderdata vs;
                float3 center = posbuffer[instance];
                float internalMod = Mod(float(id), 6.0) + 2.0;
                float u = sign(Mod(20.0, internalMod));
                float v = sign(Mod(18.0, internalMod));
                vs.uv = float2(u,v);
                float4 position = float4(float3(sign(u) - 0.5, 0.0, sign(v) - 0.5) * _Scale + center, 1.0);
                dist = distance(camerapos, center);
                // This assumes we are only setting either the normal buffer or the color buffer 
                vs.color = float4(normalbuffer[instance],1) + colorbuffer[instance];
                vs.color = lerp(vs.color, farcolor, (dist/fardist));
                // Billboard
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
                // hermite gradient 
                float gradL = 1.0 - smoothstep(0.0, 0.25, ps.uv.x);
                float gradR = smoothstep(0.75, 1.0, ps.uv.x);
                float gradT = 1.0 - smoothstep(0.0, 0.25, ps.uv.y);
                float gradB = smoothstep(0.75, 1.0, ps.uv.y);

                
                ps.color.a = 1-(gradB + gradL + gradT + gradR);
            
                
                if (fadeTime != 0) ps.color.a *= max((timebuffer[ps.instance]+fadeTime-_Time.y) / (fadeTime),0);
                
                return ps.color;
            }
            ENDCG
        }
    }
}