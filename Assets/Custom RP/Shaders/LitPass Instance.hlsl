#ifndef CUSTOM_RP_LIT_PASS_INSTANCE_INCLUDED
#define CUSTOM_RP_LIT_PASS_INSTANCE_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"


struct Attributes {
    float3 positionOS: POSITION;
    float3 normalOS: NORMAL;
    float2 baseUV:TEXCOORD0;
#if defined(_NORMAL_MAP)
    float4 tangentOS:TANGENT;
#endif
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings{
    float4 positionCS_SS:SV_POSITION;
    float3 positionWS:VAR_POSITION;
    float3 normalWS:VAR_NORMAL_WS;
    float2 baseUV:VAR_BASE_UV; // 冒号后面的部分名字任意取,参杂小写字母数字也行，只要不是纯数字纯符号即可
#if defined(_DETAIL_MAP)
    float2 detailUV:VAR_DETAIL_UV;
#endif
#if defined(_NORMAL_MAP)
    float4 tangentWS:VAR_TANGENT;
#endif
    GI_VARINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes input){

    UNITY_SETUP_INSTANCE_ID(input);

    Varyings output;
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip( output.positionWS );
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    output.baseUV = TransformBaseUV(input.baseUV);
#if defined(_DETAIL_MAP)
    output.detailUV = TransformDetailUV(input.baseUV);
#endif
#if defined(_NORMAL_MAP)
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
#endif

    GI_TRANSFER_GI_DATA(input,output)
    return output;

}

float4 LitPassFragment( Varyings input ):SV_TARGET{

    UNITY_SETUP_INSTANCE_ID(input);

    // Calc Inputs for Lighting
    InputConfig config = GetInputConfig( input.positionCS_SS ,input.baseUV);
    ClipLOD( config.fragment, unity_LODFade.x );

    // return float4(config.fragment.depth.xxx / 20.0,1.0);//test

    #if defined(_MASK_MAP)
        config.useMask = true;
    #endif

    #if defined(_DETAIL_MAP)
        config.useDetail = true;
        config.detailUV = input.detailUV;
    #endif

    // Some Properties for Lighting
    float4 maskMap = GetMask(config);
    float4 detailMap = GetDetail(config);

    // 补充后续计算需要的 config
	config.details = detailMap.r;
	config.detailSmooth = detailMap.b;
	config.metallicMask = maskMap.r;
	config.occlusionMask = maskMap.g;
	config.smoothnessMask = maskMap.a;
	config.detailMask = maskMap.b;

    float4 color = GetBase(config);

    
    
#if defined(_CLIPPING)
    clip( color.a - GetCutoff(config) );
#endif


    // Calc Lighting
    Surface surface = (Surface)0;
    surface.color = color.rgb;
    surface.alpha = color.a;
    surface.interplatedNormal = normalize(input.normalWS);

#if defined(_NORMAL_MAP)    
    surface.normal = NormalTangentToWorld( GetNormalTS(config), input.normalWS, input.tangentWS );
#else
    surface.normal = surface.interplatedNormal;
#endif
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);
    surface.occlusion = GetOcclusion(config);
    surface.fresnelStrength = GetFresnel();
    surface.dither = InterleavedGradientNoise( config.fragment.positionSS.xy, 0 ); // Function from Core RP Library
    surface.position = input.positionWS;

#if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface,true);
#else
    BRDF brdf = GetBRDF(surface);
#endif

    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);

    float3 lighting = GetLighting(surface, brdf, gi);
    
    // Final Color
    float4 finalColor = 0.0;
    finalColor.rgb = lighting;

    finalColor.rgb += GetEmission(config);
    finalColor.a = surface.alpha;

    // return float4( surface.smoothness + color.rgb * 0.00001,1 ); //test
    return finalColor;
}


#endif