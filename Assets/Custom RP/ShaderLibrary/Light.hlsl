#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];

    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

struct Light {
    float3 color;
    float3 direction;
    float attenuation;
    uint renderingLayerMask;
};

int GetDirectionalLightCount(){
    return _DirectionalLightCount;
}

int GetOtherLightCount(){
    return _OtherLightCount;
}

DirectionalShadowData GetDirectionalShadowData (int lightIndex, ShadowData shadowData) {
	DirectionalShadowData data;
	data.strength = _DirectionalLightShadowData[lightIndex].x; // Light 设置面板上设置的 Shadow Strength
	data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
	return data;
}

OtherLightShadowData GetOtherLightShadowData(int lightIndex, ShadowData shadowData){
    OtherLightShadowData data;
    data.strength = _OtherLightShadowData[lightIndex].x;
    data.tileIndex = _OtherLightShadowData[lightIndex].y;
    data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
    data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
	data.lightPositionWS = 0.0;
    data.lightDirectionWS = 0.0;
    data.spotDirectionWS = 0.0;
    return data;
}

// 逐光源进行计算
Light GetDirectionalLight(int index , Surface surfaceWS, ShadowData shadowData){
    
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowData);
    light.attenuation = GetDirectionalShadowAttenuation( dirShadowData, shadowData , surfaceWS);
    return light;
}

Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData){

    Light light;
    light.color = _OtherLightColors[index].rgb;
    float3 position = _OtherLightPositions[index].xyz;
    float3 ray =  position - surfaceWS.position.xyz;
    light.direction = normalize(ray);

    float3 spotDirection = _OtherLightDirections[index].xyz;
    float distanceSquare = dot(ray, ray) ;
    float strengthAtten = saturate( 1.0 / max(distanceSquare ,0.0001 ) );
    float rangeAtten = Square( max( 0, 1.0 - Square(distanceSquare * _OtherLightPositions[index].w) ));
    float spotAtten = Square(saturate( dot( spotDirection, light.direction ) * _OtherLightSpotAngles[index].x + _OtherLightSpotAngles[index].y ));

    OtherLightShadowData otherLightShadowData = GetOtherLightShadowData(index, shadowData);
    otherLightShadowData.lightPositionWS = position;
    otherLightShadowData.lightDirectionWS = light.direction;
    otherLightShadowData.spotDirectionWS = spotDirection;
    float shadowAtten = GetOtherLightShadowAttenuation( otherLightShadowData, shadowData, surfaceWS );
    
    light.attenuation = spotAtten * strengthAtten * rangeAtten * shadowAtten;
    return light;
}




#endif