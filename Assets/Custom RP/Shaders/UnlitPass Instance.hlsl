#ifndef CUSTOM_RP_UNLIT_PASS_INSTANCE_INCLUDED
#define CUSTOM_RP_UNLIT_PASS_INSTANCE_INCLUDED


struct Attributes {
    float3 positionOS: POSITION;
    float4 vertexColor:COLOR;
#if defined(_FLIPBOOK_BLENDING)
    float4 baseUV:TEXCOORD0;
    float flipbookBlend:TEXCOORD1;
#else
    float2 baseUV:TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
    float4 positionCS_SS:SV_POSITION;
    float2 baseUV:VAR_BASE_UV;
#if defined(_FLIPBOOK_BLENDING)
    float3 flipbookUVB:VAR_FLIPBOOK_UVB;
#endif
#if defined(_VERTEX_COLOR)
    float4 vertexColor:VAR_VERTEX_COLOR;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex(Attributes input){

    UNITY_SETUP_INSTANCE_ID(input);

    Varyings output;
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip( positionWS );
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    output.baseUV = TransformBaseUV(input.baseUV.xy); 
#if defined(_FLIPBOOK_BLENDING)
    output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
    output.flipbookUVB.z = input.flipbookBlend;
#endif
#if defined(_VERTEX_COLOR)
    output.vertexColor = input.vertexColor;
#endif
    return output;

}

float4 UnlitPassFragment( Varyings input ):SV_TARGET{

    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig( input.positionCS_SS ,input.baseUV);
    ClipLOD( config.fragment, unity_LODFade.x );

    #if defined(_VERTEX_COLOR)
        config.color = input.vertexColor;
    #endif
    #if defined(_FLIPBOOK_BLENDING)
        config.flipbookUVB = input.flipbookUVB;
        config.flipbookBlending = true;
    #else
        config.flipbookBlending = false;
    #endif

    #if defined(_NEAR_FADE)
        config.nearFade = true;
    #endif
    #if defined(_SOFT_PARTICLES)
        config.softParticles = true;
    #endif

    float4 color = GetBase(config);
    #if defined(_CLIPPING)
        clip( color.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_Cutoff) );
    #endif
    #if defined(_DISTORTION)
        float2 distortion = GetDistortion(config) * color.a;
        color.rgb = lerp( GetBufferColor( config.fragment, distortion ).rgb, color.rgb, saturate( color.a - GetDistortionBlend(config)));
    #endif
    

    color.a = GetFinalAlpha(color.a);

    // return GetBufferColor(config.fragment, float2(0.05,0.05)); //test
    // color = float4( config.fragment.bufferDepth.xxx / 20.0, 1.0);  //test

    return color;
}


#endif