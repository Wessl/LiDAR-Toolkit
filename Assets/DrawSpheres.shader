Shader "Unlit/DrawSpheres" {

	Properties {
		_Smoothness ("Smoothness", Range(0,1)) = 0.5
	}
	
	SubShader {
		CGPROGRAM
		#pragma surface ConfigureSurface Standard fullforwardshadows addshadow
		#pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
		#pragma editor_sync_compilation
		#pragma target 5.0

		#ifdef SHADER_API_D3D11
		StructuredBuffer<float4> colorbuffer;
		#endif
		
		#include "Assets/PointGPU.hlsl"
		
		
		struct Input {
			float3 worldPos;
		};

		float _Smoothness;
		
		void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
			surface.Albedo = color;
			surface.Smoothness = 0;
		}
		ENDCG
	}
					
		
	FallBack "Diffuse"
}