#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

// SpaceTransforms.hlsl 中都是使用的 UNITY_MATRIX_M 等宏定义,因为它要支持根据不同的渲染方案来灵活定义 UNITY_MATRIX_M 等代表的变量。只需要换个宏定义，这个宏就能代表不同的变量。这里我们使用 unity_ObjectToWorld， 它是Unity直接给的Uniform，我们这里简单的渲染方案直接使用它作为 UNITY_MATRIX_M 即可。
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp); // filterMode = linear, clampMode = clamp 的sampler， 这个sampler之所以取名叫这个，是提醒用户可以在别的需要同样设置的sampler时使用它
SAMPLER(sampler_point_clamp); // 有些图进行滤波没有意义，比如深度图

bool IsOrthographicCamera () {
	return unity_OrthoParams.w;
}

// 正交相机在进行投影矩阵变换时，对于所有分量(x,y,z)的变化都是线性变化( 类似于 ax + b的计算 )。可以说它们的值始终是线性的。w分量始终是1，齐次裁剪之后z分量仍然维持在Clip空间（裁剪空间）中的值(OpenGL范围[-1,1])。故可以同样通过线性计算（如 ax + b）变回到View空间的值。（view空间中处在near到far之间的值往往是负数，所以要使用z值最后还要对它求相反数）
float OrthographicDepthBufferToLinear (float rawDepth) {
	#if UNITY_REVERSED_Z
		rawDepth = 1.0 - rawDepth;
	#endif
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

float Square (float x) {
	return x * x;
}

float DistanceSquared( float3 from, float3 to ){

	float3 delta = float3(from.x - to.x, from.y- to.y, from.z - to.z);
	return dot( delta, delta );
}

// 从LOD0拉远到LOD1的过程：进入Fade阶段时，先渲染LOD1的模型，此时 unity_LODFade.x 瞬变成0，然后随着距离拉远由0变大(不一定能达到1)。然后渲染LOD0的模型，对它unity_LODFade.x是负数。
void ClipLOD( Fragment f, float fade  ){
	#if defined(LOD_FADE_CROSSFADE)
		// float dither = (uv.y % 16) / 16; // y方向上不停地从0变成1
		float dither = InterleavedGradientNoise( f.positionSS.xy,0 );
		clip( fade + (fade < 0.0 ? dither : - dither) );
		
	#endif
}

float3 DecodeNormal (float4 sample, float scale) {
	#if defined(UNITY_NO_DXT5nm)
	    return UnpackNormalRGB(sample, scale);
	#else
	    return UnpackNormalmapRGorAG(sample, scale);
	#endif
}

float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) {
	float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

#endif