Shader "Unlit/DrawCircles"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PointSize ("Vector1", float) = 5
        _CircleColor ("Color of circle", Vector) = (1.0,0.0,1.0,1.0)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag


            #include "UnityCG.cginc"

            float _PointSize;
            float4 _CircleColor;

            struct appdata
            {
                float4 vertex : POSITION0;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 center : POSITION1;
                float size : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert(appdata v, out float4 vertex : SV_POSITION)
            {
                // I got a boatload of work to do here
                v2f o;
                vertex = UnityObjectToClipPos(v.vertex);
                o.vertex = vertex;
                o.center = ComputeScreenPos(vertex);
                o.size = _PointSize;
                return o;
            }

            // vpos contains the integer coordinates of the current pixel, which is used
            // to caculate the distance between current pixel and center of the point.
            fixed4 frag(v2f i, UNITY_VPOS_TYPE vpos : VPOS) : SV_Target
            {
                float4 center = i.center;
                // Converts center.xy into [0,1] range then mutiplies them with screen size.
                center.xy /= center.w;
                center.x *= _ScreenParams.x;
                center.y *= _ScreenParams.y;
                float dis = distance(vpos.xy, center.xy);
                if (dis > _PointSize / 2)
                {
                    discard;
                }
                return _CircleColor;
            }
            ENDCG
        }
    }
}