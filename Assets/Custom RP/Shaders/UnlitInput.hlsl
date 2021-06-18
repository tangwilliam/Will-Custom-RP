#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_MaskMap);
SAMPLER(sampler_MaskMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
	UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
	UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


struct InputConfig {
	float4 color;
	float2 baseUV;
	float3 flipbookUVB;
	bool flipbookBlending;
	bool nearFade;
	bool softParticles;
	Fragment fragment;
};

InputConfig GetInputConfig ( float4 positionSS ,float2 baseUV) {
	InputConfig c;
	c.color = 1.0;
	c.baseUV = baseUV;
	c.flipbookUVB = 0;
	c.flipbookBlending = false;
	c.nearFade = false;
	c.softParticles = false;
	c.fragment = GetFragment(positionSS);
	return c;
}

float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = INPUT_PROP( _BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (InputConfig c) {
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 baseColor = INPUT_PROP( _BaseColor);

	if(c.flipbookBlending){
		float4 baseMap2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy);
		baseMap = lerp( baseMap, baseMap2, c.flipbookUVB.z );
	}
	if (c.nearFade) {
		float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) /
			INPUT_PROP(_NearFadeRange);
		baseMap.a *= saturate(nearAttenuation);
	}
	if(c.softParticles){
		float deltaDepth = c.fragment.bufferDepth - c.fragment.depth;
		float depthAttenuation = ( deltaDepth - INPUT_PROP(_SoftParticlesDistance) ) / INPUT_PROP( _SoftParticlesRange );
		baseMap.a *= saturate(depthAttenuation);
	}

	return baseMap * baseColor * c.color;
}

float2 GetDistortion(InputConfig c){
	float4 distortionMap = SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, c.baseUV);
	if(c.flipbookBlending){
		float4 distortionMap2 = SAMPLE_TEXTURE2D(_DistortionMap, sampler_BaseMap, c.flipbookUVB.xy);
		distortionMap = lerp( distortionMap, distortionMap2, c.flipbookUVB.z );
	}
	return DecodeNormal( distortionMap, INPUT_PROP(_DistortionStrength) ).xy;
}

float GetDistortionBlend(InputConfig c){
	return INPUT_PROP(_DistortionBlend);
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

// 该函数作用：让alphaTest的alpha输出到RenderTarget时也正确
// 为了让半透RT叠加到已有FrameBuffer上正确，alpha 混合使用了 One OneMinusSrcAlpha
// alphaTest不同于opaque材质于alpha不等于1，因为alpha要用来判断clip()。对于FrameBuffer本身alpha为0的情况，如果直接将alpha混合上去很多时候会得到小于1的alpha，这块区域最终会变成半透，但这不是alphaTest要的结果。
float GetFinalAlpha(float alpha){
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}


#endif