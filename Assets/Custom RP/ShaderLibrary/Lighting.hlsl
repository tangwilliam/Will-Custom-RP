#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 IncomingLight (Surface surface, Light light) {
	return saturate(dot(surface.normal, light.direction) * light.attenuation ) * light.color;
}

// 将光照计算拆成 IncomingLight()的能量(并考虑入射光线与入射平面的夹角带来的能量衰减) 和 BRDF， 是符合渲染方程的
float3 GetLighting (Surface surface, BRDF brdf, Light light) {
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) {
	
	ShadowData shadowData = GetShadowData(surfaceWS); // 之所以计算cascadeIndex放到这里，是因为它仅跟世界空间位置有关，每盏光都是使用同一个值，没必要每盏光都计算一遍
	shadowData.shadowMask = gi.shadowMask;

	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular );  

	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		color += GetLighting(surfaceWS, brdf, GetDirectionalLight(i, surfaceWS, shadowData));
	}

	#if defined(_LIGHTS_PER_OBJECT)
		for(int j = 0; j < min( unity_LightData.y, 8 ); j++){ // How many valid other lights there are depends on lihgts in lightIndexMap calculated by Unity per Object.
			int newIndex = unity_LightIndices[ (uint)j/4 ][ (uint)j%4 ];
			color += GetLighting(surfaceWS, brdf, GetOtherLight(newIndex, surfaceWS, shadowData));
		}
	#else
		for(int j = 0; j < GetOtherLightCount(); j++){ // How many valid other lights there are depends on visibleLights from C#, which changes with cullingResult at runtime.
			color += GetLighting(surfaceWS, brdf, GetOtherLight(j, surfaceWS, shadowData));
		}
	#endif

	return color;
}

#endif