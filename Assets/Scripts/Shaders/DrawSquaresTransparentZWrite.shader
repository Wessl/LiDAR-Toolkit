Shader "Draw Squares Transparent ZWrite"
{
    Subshader
    {
        CGINCLUDE
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
        #include "UnityCG.cginc"

        struct shaderdata
        {
            float4 vertex : SV_POSITION;
            float4 color : TEXCOORD1;
            float2 uv : TEXCOORD0;
            uint instance : SV_INSTANCEID;
        };
         
        float Mod(half x, half y)
        {
            return x - y * floor(x/y);
        }
        
        shaderdata CommonVSMain (uint id:SV_VertexID, inout uint instance:SV_INSTANCEID)
        {
            shaderdata vs;
            float3 center = posbuffer[instance];
            float internalMod = Mod(id, 6.0) + 2.0;
            float u = sign(Mod(20.0, internalMod));
            float v = sign(Mod(18.0, internalMod));
            vs.uv = float2(u,v);
            dist = distance(camerapos, center);
            // This assumes we are only setting either the normal buffer or the color buffer 
            vs.color = float4(normalbuffer[instance],1) + colorbuffer[instance];
            vs.color = lerp(vs.color, farcolor, clamp(dist/fardist,0,1));
            // Billboard
            float4 pos2 = mul(UNITY_MATRIX_P, 
            float4(UnityObjectToViewPos(float3(0.0,0.0,0.0)),1.0)
            + float4(u-0.5, v-0.5, 0.0, 0.0)*2
            * float4(_Scale, _Scale, 1.0, 1.0));
            
            vs.vertex = UnityObjectToClipPos(float4(center*2.0, 1.0)) + pos2;
            vs.instance = instance;
            
            return vs;
        }
        ENDCG
        // Opaque pass
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMainOpaque
            #pragma target 5.0

            shaderdata VSMain (uint id:SV_VertexID, inout uint instance:SV_INSTANCEID)
            {
                return CommonVSMain(id, instance);
            }
            
            float4 PSMainOpaque (shaderdata ps, uint instance : SV_INSTANCEID) : SV_Target
            {
                int clipValue = 1.0;
                // hermite gradient 
                float gradL = 1.0 - smoothstep(0.0, 0.25, ps.uv.x);
                float gradR = smoothstep(0.75, 1.0, ps.uv.x);
                float gradT = 1.0 - smoothstep(0.0, 0.25, ps.uv.y);
                float gradB = smoothstep(0.75, 1.0, ps.uv.y);

                // as long as you use the grads, you get smooth edges, dont use grads, get regular square
                float value = max((timebuffer[instance]+fadeTime-_Time.y) / (fadeTime),0);

                if (fadeTime != 0) clipValue = value;
                float alpha = _SmoothEdges ? 1 - (gradB + gradL + gradT + gradR) : 1;
                clip(clipValue - alpha);
                

                if (alpha < 0.99) clip(-1);
                
                return ps.color;
            }
            ENDCG
        }
        
        // Transparent pass
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMainTransparent
            #pragma target 5.0
            #include "UnityCG.cginc"

            shaderdata VSMain (uint id:SV_VertexID, uint instance:SV_INSTANCEID)
            {
                return CommonVSMain(id, instance);
            }
            float4 PSMainTransparent (shaderdata ps) : SV_Target
            {
                // hermite gradient 
                float gradL = 1.0 - smoothstep(0.0, 0.25, ps.uv.x);
                float gradR = smoothstep(0.75, 1.0, ps.uv.x);
                float gradT = 1.0 - smoothstep(0.0, 0.25, ps.uv.y);
                float gradB = smoothstep(0.75, 1.0, ps.uv.y);

                float alpha = _SmoothEdges ? 1 - (gradB + gradL + gradT + gradR) : 1;

                clip(1.0 - alpha);

                ps.color.a = alpha;
                
                if (fadeTime != 0)
                    ps.color.a *= max((timebuffer[ps.instance] + fadeTime - _Time.y) / fadeTime, 0);

                return ps.color;
            }
            
            ENDCG
        }
    }
}