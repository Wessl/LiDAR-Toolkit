Shader "Unlit/DrawMeshBRP" {

	Properties {
		_Smoothness ("Smoothness", Range(0,1)) = 0.5
	}
	
	SubShader 
	{	
		Tags {"Queue" = "Transparent" "RenderType"="Transparent" }

		CGPROGRAM
		#pragma surface ConfigureSurface Standard alpha:blend noshadow noambient novertexlights nolightmap nodynlightmap nodirlightmap nometa noforwardadd nolppv noshadowmask
		#pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
		#pragma editor_sync_compilation
		#pragma target 5.0

		// wot. why are these defined here, and in the pointgpu hlsl. and why only whne d3d11?
		#ifdef SHADER_API_D3D11
		//StructuredBuffer<float4> colorbuffer;
		//StructuredBuffer<float> timebuffer;
		//StructuredBuffer<float3> normalbuffer;
		#endif
		float fadeTime;
		#include "Assets/Scripts/Shaders/PointGPU.hlsl"
		
		
		struct Input {
			float3 worldPos;
		};

		float _Smoothness;
		
		void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
			float4 newcol = lerp(color, farcolor, clamp((dist/fardist),0.0,1.0));
			surface.Albedo = newcol.rgb;
			surface.Smoothness = 0;
			surface.Alpha = 1;
			if (fadeTime != 0)
			{
				surface.Alpha = clamp((currTime+fadeTime-_Time.y) * 1 / (fadeTime),0,1);
			}
			
		}
		ENDCG
			
	}
					
		
	FallBack "Diffuse"
}