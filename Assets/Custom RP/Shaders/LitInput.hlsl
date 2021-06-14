#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
	UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

// 使用这样的输入是为了让 Input.hlsl 跟别的文件解耦
struct InputConfig{

	Fragment fragment;

	float2 baseUV;

	float2 detailUV;
	float details;
	float detailSmooth;

	float metallicMask;
	float occlusionMask;
	float smoothnessMask;
	float detailMask;

	bool useMask;
	bool useDetail;

}; // 注意分号不能丢

InputConfig GetInputConfig( float4 positionSS, float2 baseUV, float2 detailUV = 0.0, float details = 0.0, float detailSmooth = 0.0, float metallicMask = 0.0, float occlusionMask = 0.0, float smoothnessMask = 0.0, float detailMask = 0.0 ){
	InputConfig c;
	c.fragment = GetFragment( positionSS );
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	c.details = details;
	c.detailSmooth = detailSmooth;
	c.metallicMask = metallicMask;
	c.occlusionMask = occlusionMask;
	c.smoothnessMask = smoothnessMask;
	c.detailMask = detailMask;
	c.useMask = false;
	c.useDetail = false;
	return c;
}


float2 TransformBaseUV (float2 baseUV) {
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV (float2 uv) {
	float4 st = INPUT_PROP(_DetailMap_ST);
	return uv * st.xy + st.zw;
}

float4 GetDetail( InputConfig c ){

	if(c.useDetail){
		float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, c.detailUV);
		map = map * 2.0 - 1.0;
		map *= INPUT_PROP(_DetailAlbedo);
		return map;
	}else{
		return 0.0;
	}
}

float4 GetBase (InputConfig c ) {
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);

	if(c.useDetail){
		map.rgb = lerp( sqrt(map.rgb), c.details < 0.0 ? 0.0 : 1.0, abs(c.details) * c.detailMask ); // Gamma 的图要在Gamma空间混合才正确。 另注意：c.details范围是(-1,1)
		map.rgb *= map.rgb;
	}
	float4 color = INPUT_PROP(_BaseColor);
	return map * color;
}


float GetCutoff (InputConfig c) {
	return INPUT_PROP(_Cutoff);
}

float4 GetMask(InputConfig c){
	if(c.useMask){
		return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, c.baseUV);
	}else{
		return 1.0;
	}
}

float GetMetallic ( InputConfig c) {
	return INPUT_PROP(_Metallic) * c.metallicMask;
}

float GetSmoothness (InputConfig c) {
	float smoothness = INPUT_PROP(_Smoothness) * c.smoothnessMask ;
	if(c.useDetail){
		float detailSmooth = c.detailSmooth * INPUT_PROP(_DetailSmoothness); // 注意c.detailSmooth 与c.details 同样，是(-1,1)的范围。-1表示降低光滑度的程度
		smoothness = saturate( lerp( smoothness, detailSmooth < 0.0 ? 0.0 : 1.0, abs(detailSmooth) * c.detailMask ));
	}
	return smoothness;
}

float GetOcclusion(InputConfig c){
	float occlusion = c.occlusionMask;
	float strength = INPUT_PROP(_Occlusion);
	return lerp( 1.0, occlusion, strength );
}

float GetFresnel () {
	return INPUT_PROP(_Fresnel);
}

float3 GetEmission(InputConfig c){
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, c.baseUV);
    float4 color = INPUT_PROP(_EmissionColor);
    return map.rgb * color.rgb;
}

float3 GetNormalTS (InputConfig c) {
	float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, c.baseUV);
	float scale = INPUT_PROP(_NormalScale);
	float3 normal = DecodeNormal(map, scale);

	if(c.useDetail){
		float4 detailNormalMap = SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailMap, c.detailUV);
		float detailScale = INPUT_PROP(_DetailNormalScale) * c.detailMask;
		float3 detailNormal = DecodeNormal(detailNormalMap, 1.0 );
		float3 detailedNormal = BlendNormalRNM( normal, detailNormal );
		normal = lerp( normal, detailedNormal, detailScale ); // 这里与 CatLikeCoding中的代码不同，对方的代码无法正确地让非detail区域的detail法线效果消失
	}
		
	return normal;
}

// 该函数作用：让alphaTest的alpha输出到RenderTarget时也正确
// 为了让半透RT叠加到已有FrameBuffer上正确，alpha 混合使用了 One OneMinusSrcAlpha
// alphaTest不同于opaque材质于alpha不等于1，因为alpha要用来判断clip()。对于FrameBuffer本身alpha为0的情况，如果直接将alpha混合上去很多时候会得到小于1的alpha，这块区域最终会变成半透，但这不是alphaTest要的结果。
float GetFinalAlpha(float alpha){
	return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}


#endif