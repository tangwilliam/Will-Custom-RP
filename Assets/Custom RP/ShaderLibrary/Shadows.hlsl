#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
	#define OTHER_FILTER_SAMPLES 4
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
	#define OTHER_FILTER_SAMPLES 9
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
	#define OTHER_FILTER_SAMPLES 16
	#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif


#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	int _CascadeCount;
	float4 _ShadowAtlasSize;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4 _ShadowDistanceFade;

	float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
	float4 _OtherShadowAtlasSize;
	float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};

struct OtherLightShadowData{
	float strength;
	int tileIndex;
	bool isPoint;
	float normalBias;
	int shadowMaskChannel;
	float3 lightPositionWS;
	float3 lightDirectionWS;
	float3 spotDirectionWS;
};

struct ShadowMask{
	bool distance;
	bool always;
	float4 shadows;
};

// 当前是第几个级联，以采样到Shadowmap正确区域
struct ShadowData{
	int cascadeIndex;
	float cascadeBlend;
	float strength;
	ShadowMask shadowMask;
};

float FadedShadowStrength (float distance, float scale, float fade) {
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS){
	ShadowData data;
	data.cascadeIndex = 0;
	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y); // Cascaded Shadows 的 Strength
	int i;
	for(i = 0; i < _CascadeCount; i++){
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared( surfaceWS.position, sphere.xyz );
		if( distanceSqr < sphere.w ){
			float fade = FadedShadowStrength(
				distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
			);
			if (i == _CascadeCount - 1) {
				data.strength *= fade; // 最后一级的级联要进行 fade
			}
			else {
				data.cascadeBlend = fade;
			}
			
			break;
		}
	}
	
	if(i == _CascadeCount && _CascadeCount > 0 ){
		data.strength = 0.0; // i 被自增到最大级联数以上，说明已超过级联阴影最大阴影范围，则将阴影强度设置为0
	}

	#if defined( _CASCADE_BLEND_DITHER )
		if( data.cascadeBlend < surfaceWS.dither ){
			i = i + 1;
		}
	#endif

	#if !defined( _CASCADE_BLEND_SOFT )
		data.cascadeBlend = 1.0;		
	#endif

	data.cascadeIndex = i; // 根据splitSphere的球心位置、半径，及当前片元距离球心的距离来计算出是第几级级联

	return data;
}

float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	); // 该宏内部是调用 HLSL或OpenGL等的 SampleCmpLevelZero()方法，用形参的xy分量进行采样2d纹理，然后用z值与采样值进行对比，形参z值大则返回0
}
float SampleOtherShadowAtlas (float3 positionSTS, float3 bounds) {
	float2 tileMinPos = bounds.xy;
	float2 tileMaxPos = tileMinPos + bounds.z;
	positionSTS.xy = clamp( positionSTS.xy, tileMinPos, tileMaxPos ); // 根据每个tile的有效正方形区域，限制采样的区域，避免采样到周围的tile。虽然spot阴影边界处可能出现clamp形式的拉伸阴影，但好过采样到其他tile的错误阴影
	return SAMPLE_TEXTURE2D_SHADOW(
		_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS
	); // 该宏内部是调用 HLSL或OpenGL等的 SampleCmpLevelZero()方法，用形参的xy分量进行采样2d纹理，然后用z值与采样值进行对比，形参z值大则返回0
}

float FilterDirectionalShadow (float3 positionSTS) {
	#if defined(DIRECTIONAL_FILTER_SETUP)
		float weights[DIRECTIONAL_FILTER_SAMPLES];
		float2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleDirectionalShadowAtlas(
				float3(positions[i].xy, positionSTS.z)
			);
		}
		return shadow;
	#else
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif
}

float FilterOtherShadow (float3 positionSTS, float3 bounds) {
	#if defined(OTHER_FILTER_SETUP)
		float weights[OTHER_FILTER_SAMPLES];
		float2 positions[OTHER_FILTER_SAMPLES];
		float4 size = _OtherShadowAtlasSize.wwzz;
		OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < OTHER_FILTER_SAMPLES; i++) {
			shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);
		}
		return shadow;
	#else
		return SampleOtherShadowAtlas(positionSTS, bounds);
	#endif
}

float GetCascadedShadows(DirectionalShadowData directionalData, ShadowData shadowData ,Surface surfaceWS) {
	
	float3 normalBias = surfaceWS.interplatedNormal * _CascadeData[shadowData.cascadeIndex].y * directionalData.normalBias;
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directionalData.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow(positionSTS); // positionSTS没有除以w是因为直射光投影的VP矩阵是在正交投影下进行变换的，w始终是1

	// 在相邻级联之间做混合，解决不同级联的交界处的生硬过渡。（这个做法性价比较低）
	if (shadowData.cascadeBlend < 1.0) {
		normalBias = surfaceWS.interplatedNormal *
			(directionalData.normalBias * _CascadeData[shadowData.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directionalData.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, shadowData.cascadeBlend
		);
	}
	return shadow;
}

float GetBakedShadow(ShadowMask shadowMask, int maskChannel){

	float shadow = 1.0;
	if( shadowMask.always || shadowMask.distance ){
		if(maskChannel >= 0){
			shadow = shadowMask.shadows[maskChannel]; // 这里使用数组的方式访问了float4，如果愿意多从CPU传入一个Vector4那么可以变成用dot()的方式计算。但catlikecoding说编译器会在这个步骤将这个计算变成dot()计算。
		}
	}
	return shadow;
}

// 多一个参数 shadowStrength, 直接计算出最终的阴影强度。注：它不需要跟实时阴影混合，因为往往这时已经超出了最大阴影距离。所以可以直接求出最终阴影强度。
float GetBakedShadow(ShadowMask shadowMask, int maskChannel, float strength){

	if( shadowMask.always || shadowMask.distance ){
		return lerp( 1.0, GetBakedShadow(shadowMask, maskChannel), strength );
	}
	return 1.0;
}

float MixBakedAndRealtimeShadows( ShadowData global, float shadow, int maskChannel, float strength  ){

	float bakedShadow = GetBakedShadow( global.shadowMask, maskChannel  );
	if(global.shadowMask.always){
		shadow = lerp( 1.0, shadow, global.strength );
		shadow = min( bakedShadow, shadow);
		return lerp( 1.0, shadow, strength );
	}
	else if(global.shadowMask.distance){
		shadow = lerp( bakedShadow, shadow, global.strength ); // lerp by strength of Cascaded Shadows, which takes in count of Max Shadow Distance
		return lerp(1.0, shadow, strength);
	}

	return lerp( 1.0, shadow, strength * global.strength );
}

// 注:这是逐光源计算的
float GetDirectionalShadowAttenuation (DirectionalShadowData directionalData, ShadowData global ,Surface surfaceWS) {

	#ifndef _RECEIVE_SHADOWS
		return 1.0;
	#endif

	float shadow;

	if( directionalData.strength * global.strength <= 0.0 ){ 
		// directionalData.strength 小于零的情况：最大的投射阴影的splitSphere内都没有该光能投射的物体时(CPU给directionalData.strength传了一个负数); 
		// global.strength 等于0的情况：1. 超出最后一级阴影级联的范围，那么要采样烘焙的ShadowMask。2. 光的ShadowStrength等于0，strength传入到GetBakedShadow()中了，计算结果出来不会有阴影
		return GetBakedShadow( global.shadowMask, directionalData.shadowMaskChannel, abs( directionalData.strength) );
	}else{

		shadow = GetCascadedShadows(directionalData, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows( global, shadow, directionalData.shadowMaskChannel,  directionalData.strength );
	}

	return shadow;
	
}

static const float3 pointShadowPlanes[6] = {
	float3(-1.0, 0.0, 0.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, -1.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, 0.0, -1.0),
	float3(0.0, 0.0, 1.0)
}; // order: +X, −X, +Y, −Y, +Z, −Z, Unity SRP 渲染ShadowMap的时候就是用的这个顺序

float GetOtherShadows(OtherLightShadowData otherData, ShadowData global, Surface surfaceWS){

	float tileIndex = otherData.tileIndex;
	float3 lightPlane = otherData.spotDirectionWS;
	if (otherData.isPoint) {
		float faceOffset = CubeMapFaceID(-otherData.lightDirectionWS); // 根据世界空间的受光方向，得到点光shadowMap的投射平面，以正确计算bias, from Core RP Library
		tileIndex += faceOffset;
		lightPlane = pointShadowPlanes[faceOffset];
	}
	float4 tileData = _OtherShadowTiles[tileIndex];
	float3 surfaceToLight = otherData.lightPositionWS - surfaceWS.position;
	float distanceLightToPlane = dot( surfaceToLight, lightPlane ); // 注意要的是光源到片元所处的与近平面平行的平面之间的距离，非光源与片元的距离
	float bias = distanceLightToPlane * tileData.w;
	float3 normalBias = surfaceWS.interplatedNormal * bias;
	float4 positionWS = float4(surfaceWS.position + normalBias, 1.0);
	float4 positionSTS = mul(_OtherShadowMatrices[tileIndex], positionWS); // 得到的光源空间坐标仍是齐次裁剪空间的(所有tiles总共起来是(0,w)，注意非(-w,w))
	float shadow = FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz); // 因为坐标仍然是齐次裁剪空间的，对于透视投影的光源(spot/point)，在Shader中乘以本矩阵后，还要除以w，才能将众小视锥体变成小立方体，用于ShadowMap采样
	return shadow;
}

float GetOtherLightShadowAttenuation(OtherLightShadowData otherData, ShadowData global, Surface surfaceWS){

	#ifndef _RECEIVE_SHADOWS
		return 1.0;
	#endif

	float shadow;
	if( otherData.strength * global.strength <= 0.0 ){ 
		return GetBakedShadow( global.shadowMask, otherData.shadowMaskChannel, abs( otherData.strength) );
	}else{
		shadow = GetOtherShadows(otherData, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows( global, shadow, otherData.shadowMaskChannel, otherData.strength );
	}
	return shadow;
}

#endif