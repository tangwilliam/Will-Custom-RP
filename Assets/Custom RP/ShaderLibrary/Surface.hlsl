#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED


struct Surface {
	float3 position;
	float3 normal;
	float3 interplatedNormal; // 顶点法线
	float3 viewDirection;
	float3 color;
	float alpha;
	float metallic;
	float smoothness;
	float depth;
	float dither;
	float fresnelStrength;
	float occlusion;
};



#endif