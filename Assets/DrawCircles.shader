Shader "Draw Circles"
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
 
            StructuredBuffer<float3> posbuffer;
            StructuredBuffer<float4> colorbuffer;    // not used atm
            StructuredBuffer<float> timebuffer;
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
            
            shaderdata VSMain (uint id:SV_VertexID, uint instance:SV_INSTANCEID)
            {
                shaderdata vs;
                float3 center = posbuffer[instance];
                float u = sign(Mod(20.0, Mod(float(id), 6.0) + 2.0));
                float v = sign(Mod(18.0, Mod(float(id), 6.0) + 2.0));
                vs.uv = float2(u,v);
                float4 position = float4(float3(sign(u) - 0.5, 0.0, sign(v) - 0.5) * _Scale + center, 1.0);
                dist = distance(camerapos, center);
                vs.color = lerp(colorbuffer[instance], farcolor, clamp((dist/fardist),0.0,1.0));
                // billboard. why does this frankensteiny mess even work remotely
                float4 pos2 = mul(UNITY_MATRIX_P, 
                mul(UNITY_MATRIX_MV, float4(0.0, 0.0, 0.0, 1.0))
                + float4(sign(u)-0.5, sign(v)-0.5, 0.0, 0.0)*2
                * float4(_Scale, _Scale, 1.0, 1.0));
                
                vs.vertex = UnityObjectToClipPos(float4(center*2.0, 1.0)) + pos2;
                vs.instance = instance;
                
                return vs;
            }
 
            float4 PSMain (shaderdata ps) : SV_Target
            {
                float2 S = ps.uv*2.0-1.0;
                if (dot(S.xy, S.xy) > 1.0) discard;
                ps.color.a = 1;
                
                if (fadeTime != 0) ps.color.a *= clamp((timebuffer[ps.instance]+fadeTime-_Time.y) * 1 / (fadeTime),0,1);
                
                return ps.color;
            }
            ENDCG
        }
    }
}