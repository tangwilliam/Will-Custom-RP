#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,name)

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


struct InputConfig {
	float4 vertexColor;
	float2 baseUV;
	float3 flipbookUVB;
	bool flipbookBlending;
	bool nearFade;
	Fragment fragment;
};

InputConfig GetInputConfig ( float4 positionSS ,float2 baseUV) {
	InputConfig c;
	c.vertexColor = 1.0;
	c.baseUV = baseUV;
	c.flipbookUVB = 0;
	c.flipbookBlending = false;
	c.nearFade = false;
	c.fragment = GetFragment(positionSS);
	return c;
}

float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = INPUT_PROP( _BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (InputConfig c) {
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 color = INPUT_PROP( _BaseColor);

	if(c.flipbookBlending){
		float4 baseMap2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy);
		color = lerp( baseMap, baseMap2, c.flipbookUVB.z );
	}
	if (c.nearFade) {
		float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) /
			INPUT_PROP(_NearFadeRange);
		baseMap.a *= saturate(nearAttenuation);
	}
#if defined(_VERTEX_COLOR)
    color *= c.vertexColor;
#endif
	return baseMap * color;
}

float GetCutoff (float2 baseUV) {
	return INPUT_PROP( _Cutoff);
}

float4 GetMask( float2 baseUV ){
	return SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, baseUV);
}

float GetMetallic ( float4 maskMap) {
	return 0.0;
}

float GetSmoothness (float4 maskMap) {
	return 0.0;
}

float GetFresnel (float2 baseUV) {
	return 0.0;
}

float3 GetEmission (InputConfig c) {
	return GetBase(c).rgb;
}


#endif