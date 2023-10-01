#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	StructuredBuffer<float3> posbuffer;
	StructuredBuffer<float4> colorbuffer;
	StructuredBuffer<float> timebuffer;
#endif

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
		//color = color * float4(normalbuffer[unity_InstanceID],1.0); // don't use the normal buffer here for now...
		unity_ObjectToWorld = 0.0;
		unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
		unity_ObjectToWorld._m00_m11_m22 = _Scale;

		dist = distance(camerapos, position);
	#endif
}