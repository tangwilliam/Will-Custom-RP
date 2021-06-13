#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF {
	float3 diffuse;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnel;
};

#define MIN_REFLECTIVITY 0.04

float OneMinusReflectivity (float metallic) {
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

// 表面材质对BRDF的影响，即计算 Fr = Kd * Fdiff + Ks * Fspec 中的 Kd 和 Ks
BRDF GetBRDF (Surface surface, bool applyAlphaToDiffuse = false) {
	BRDF brdf;
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);

	brdf.diffuse = surface.color * oneMinusReflectivity;
	if (applyAlphaToDiffuse) {
		brdf.diffuse *= surface.alpha;
	}
	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
	brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
	brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
	brdf.fresnel = saturate( surface.smoothness + 1.0 - oneMinusReflectivity );
	return brdf;
}

// 参考 URP 中的做法, CookTorrance BRDF 计算高光项的简化版
float SpecularStrength (Surface surface, BRDF brdf, Light light) {
	float3 h = SafeNormalize(light.direction + surface.viewDirection);
	float nh2 = Square(saturate(dot(surface.normal, h)));
	float lh2 = Square(saturate(dot(light.direction, h)));
	float r2 = Square(brdf.roughness);
	float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
	float normalization = brdf.roughness * 4.0 + 2.0;
	return r2 / (d2 * max(0.1, lh2) * normalization);
}

// 考虑了光照项后对BRDF的计算结果。这里实际是: Fr = Kd * Fdiff + Ks * Fspec
// Kd 和 Ks 在 GetBRDF中已经算好，因为它与入射光无关，只与材质属性有关
// Fdiff 并没有除以PI，这是为了跟 URP 一致。而 URP即内置管线都没有除以PI，Unity说是为了保持跟旧代码亮度一致。
float3 DirectBRDF (Surface surface, BRDF brdf, Light light) {
	return brdf.diffuse + SpecularStrength(surface, brdf, light) * brdf.specular;
}

float3 IndirectBRDF( Surface surface, BRDF brdf, float3 diffuse, float3 specular ){
	float3 diff = diffuse * brdf.diffuse; // 间接光照（diffuse项） * brdf.diffuse( 这是表面颜色、粗糙度金属度 进行BRDF 计算之后的 diffuse项 )
	
	float fresnelStrength = surface.fresnelStrength * Pow4( 1.0 - saturate( dot( surface.normal, surface.viewDirection )) );
	float3 reflection = specular * lerp( brdf.specular, brdf.fresnel, fresnelStrength);
	reflection = reflection / ( brdf.roughness * brdf.roughness + 1.0 ); // 很人为地让反射强度在(0.5,1)之间，其实就是Magic Numbers
	
	float3 indirectBRDF = ( diff + reflection ) * surface.occlusion;
	return indirectBRDF;
}

#endif