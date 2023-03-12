Shader "Unlit/DrawSpheres" {

	Properties {
		_Smoothness ("Smoothness", Range(0,1)) = 0.5
	}
	
	SubShader 
	{	
		Pass {
			CGPROGRAM
			#pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
			#pragma editor_sync_compilation
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#include "UnityCG.cginc"
			
			
			StructuredBuffer<float4> colorbuffer;
			StructuredBuffer<float3> posbuffer;
			StructuredBuffer<float> timebuffer;
			
			float fadeTime;
			//#include "Assets/PointGPU.hlsl"
			
			
			struct Input {
				float3 worldPos;
			};

			struct shaderdata {
	                float4 loc  : POSITION;
					float4 color : TEXCOORD1;
					uint instance : SV_INSTANCEID;
	                UNITY_VERTEX_INPUT_INSTANCE_ID
	        };

			float _Smoothness;

			float _Scale;
			float4 color;
			float4 farcolor;
			float4 camerapos;
			float dist;
			float fardist;
			float currTime;


			void ConfigureProcedural () {
				#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
					float3 position = posbuffer[unity_InstanceID];
					color = colorbuffer[unity_InstanceID];
					currTime = timebuffer[unity_InstanceID];
					unity_ObjectToWorld = 0.0;
					unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
					unity_ObjectToWorld._m00_m11_m22 = _Scale;

					dist = distance(camerapos, position);
				#endif
			}
			
			shaderdata vert(shaderdata v, uint instance:SV_INSTANCEID) {
                shaderdata vs;
                UNITY_SETUP_INSTANCE_ID(v);
                vs.loc = UnityObjectToClipPos(v.loc * _Scale - camerapos + float4(posbuffer[instance],1.0));
				vs.color = lerp(colorbuffer[v.instance], farcolor, clamp((dist/fardist),0.0,1.0));
				vs.instance = instance;
                //f.uv = v.uv;
                return vs;
	        }

			float4 frag(shaderdata f) : SV_Target{
					return f.color;
	                if (fadeTime != 0) f.color.a *= clamp((timebuffer[f.instance]+fadeTime-_Time.y) * 1 / (fadeTime),0,1);
	                return f.color;
	        }
			ENDCG
		}
		
	}
					
		
	FallBack "Unlit/Color"
}