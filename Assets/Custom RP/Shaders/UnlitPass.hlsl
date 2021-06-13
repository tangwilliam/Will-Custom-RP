#ifndef CUSTOM_RP_UNLIT_PASS_INCLUDED
#define CUSTOM_RP_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

// SRP Batcher Supporting, including with different textures, floats as uniforms

CBUFFER_START(UnityPerMaterial)

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

float4 _BaseMap_ST;
float4 _BaseColor;

CBUFFER_END


struct Attributes {
    float3 positionOS: POSITION;
    float2 baseUV:TEXCOORD0;
};

struct Varyings{
    float4 positionCS_SS:SV_POSITION;
    float2 baseUV:VAR_BASE_UV;
};

Varyings UnlitPassVertex( Attributes input ){

    Varyings output;
    output.positionCS_SS = TransformWorldToHClip( TransformObjectToWorld(input.positionOS) ); 
    output.baseUV = input.baseUV * _BaseMap_ST.xy + _BaseMap_ST.zw; 
    return output;
}

float4 UnlitPassFragment( Varyings input ):SV_TARGET{

    float4 tex = SAMPLE_TEXTURE2D( _BaseMap, sampler_BaseMap, input.baseUV );
    return _BaseColor * tex;
}


#endif