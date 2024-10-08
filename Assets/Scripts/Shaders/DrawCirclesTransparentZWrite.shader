Shader "Draw Circles Transparent ZWrite"
{
    Subshader
    {
        CGINCLUDE
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

        half Mod(half x, half y)
        {
            return x - y * floor(x/y);
        }

        shaderdata CommonVSMain(uint id:SV_VertexID, uint instance:SV_INSTANCEID)
        {
            shaderdata vs;
            float3 center = posbuffer[instance];
            float internalMod = Mod(float(id), 6.0) + 2.0;
            float u = sign(Mod(20.0, internalMod));
            float v = sign(Mod(18.0, internalMod));
            vs.uv = float2(u, v);
            dist = distance(camerapos, center);
            // This assumes we are only setting either the normal buffer or the color buffer 
            //vs.color = float4(normalbuffer[instance],1) + colorbuffer[instance];
            vs.color = colorbuffer[instance];
            vs.color = lerp(vs.color, farcolor, clamp(dist/fardist, 0, 1));

            // Billboard logic
            float4 pos2 = mul(UNITY_MATRIX_P,
            float4(UnityObjectToViewPos(float3(0.0, 0.0, 0.0)), 1.0)
            + float4(sign(u) - 0.5, sign(v) - 0.5, 0.0, 0.0) * 2
            * float4(_Scale, _Scale, 1.0, 1.0));

            vs.vertex = UnityObjectToClipPos(float4(center * 2.0, 1.0)) + pos2;
            vs.instance = instance;

            return vs;
        }
        ENDCG

        // First pass - opaque Z write
        Pass
        {
            Cull Off
            ZWrite On
            Blend Off // No blending, fully opaque
            CGPROGRAM
            #pragma vertex VSMain
            #pragma fragment PSMainOpaque
            #pragma target 5.0
            #include "UnityCG.cginc"
            
            shaderdata VSMain (uint id:SV_VertexID, uint instance:SV_INSTANCEID)
            {
                return CommonVSMain(id, instance);
            }
 
            float4 PSMainOpaque(shaderdata ps) : SV_Target
            {
                float2 S = ps.uv * 2.0 - 1.0;
                float distanceFromCenter = dot(S.xy, S.xy);
                int clipValue = 1.0;
                
                float value = max((timebuffer[ps.instance]+fadeTime-_Time.y) / (fadeTime),0);
                
                if (fadeTime != 0) clipValue = value;

                clip(clipValue - distanceFromCenter);

                float alpha = _SmoothEdges ? saturate(1.0 / (distanceFromCenter * 10.0) - 0.1f) : 1.0;

                // Only write fully opaque fragments (how does this work?)
                if (alpha < 0.99) clip(-1);

                return ps.color;
            }
            ENDCG
        }

        // Second pass: transparent blending
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

            // Fragment shader for the transparent pass
            float4 PSMainTransparent(shaderdata ps) : SV_Target
            {
                float2 S = ps.uv * 2.0 - 1.0;
                float distanceFromCenter = dot(S.xy, S.xy);

                clip(1.0 - distanceFromCenter);

                if (_SmoothEdges)
                {
                    ps.color.a = saturate(1.0 / (distanceFromCenter * 10.0) - 0.1f);
                }
                else
                {
                    ps.color.a = distanceFromCenter > 1.0 ? 0.0 : 1.0;
                }
                
                if (fadeTime != 0)
                {
                    ps.color.a *= max((timebuffer[ps.instance] + fadeTime - _Time.y) / fadeTime, 0);
                }

                return ps.color;
            }
            ENDCG
        }
    }
}