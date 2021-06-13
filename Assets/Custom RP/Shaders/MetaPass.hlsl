#ifndef CUSTOM_META_PASS_INCLUDED
#define CUSTOM_META_PASS_INCLUDED

/* 注：
 Unity 在烘焙阴影时，对于投射阴影的alphaTest，只看正常Pass的frag的输出，来看投射出的阴影是否存在镂空。不看我们材质球里 _SHADOW_CLIP 之类的设置

*/

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

bool4 unity_MetaFragmentControl;

float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
    float2 lightmapUV :TEXCOORD1;
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
};

Varyings MetaPassVertex (Attributes input) {
	Varyings output;
    input.positionOS.xy = input.lightmapUV.xy * unity_LightmapST.xy + unity_LightmapST.zw;
    input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
	output.positionCS_SS = TransformWorldToHClip(input.positionOS); // Unity 用它转换到做烘焙时用到的空间，所以并非函数本身名字代表的意思了
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

float4 MetaPassFragment (Varyings input) : SV_TARGET {

	InputConfig c = GetInputConfig( input.positionCS_SS, input.baseUV );
	float4 base = GetBase(c);

	// Some Properties for Lighting
    float4 maskMap = GetMask(c);
	c.metallicMask = maskMap.r;
	c.smoothnessMask = maskMap.a;

	Surface surface;
	ZERO_INITIALIZE(Surface, surface);
	surface.color = base.rgb;
	surface.metallic = GetMetallic(c);
	surface.smoothness = GetSmoothness(c);
	BRDF brdf = GetBRDF(surface);
	float4 meta = 0.0;

    if(unity_MetaFragmentControl.x){
        meta = float4(brdf.diffuse, 1.0);
        meta.rgb += brdf.specular * brdf.roughness * 0.5; // Unity 认为能反射很强镜面光且很粗糙的物体，会传递出一些间接光
        meta.rgb = min(
			PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue
		);
    }

    if(unity_MetaFragmentControl.y){
        meta = float4( GetEmission(c), 1.0);
    }

	return meta;
}

#endif