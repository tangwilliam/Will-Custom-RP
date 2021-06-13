#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

struct Attributes {
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

bool _ShadowPancaking;

Varyings ShadowCasterPassVertex (Attributes input) {
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(positionWS);

	if(_ShadowPancaking > 0){
		// 解决有些点比近裁减平面还近的问题。该方法不能应对较大的物体，因为挪顶点会让很大的三角面发生变形，出现这种情况还是需要将投影的近裁减面向后推一点
		#if UNITY_REVERSED_Z
			output.positionCS_SS.z = min( output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE );
		#else
			output.positionCS_SS.z = max( output.positionCS_SS.z, output.positionCS_SS.w * UNITY_NEAR_CLIP_VALUE ); // OpenGL 中 UNITY_NEAR_CLIP_VALUE 等于 -1 
		#endif
	}

	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

void ShadowCasterPassFragment (Varyings input) {
	UNITY_SETUP_INSTANCE_ID(input);

	InputConfig c = GetInputConfig( input.positionCS_SS, input.baseUV );
	ClipLOD( c.fragment, unity_LODFade.x );

	float4 base = GetBase(c);
	#if defined(_SHADOWS_CLIP)
		clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
	#elif defined( _SHADOWS_DITHER )
		float dither = InterleavedGradientNoise( c.fragment.positionSS.xy, 0 ); // Function from Core RP Library	
		clip( base.a - dither );
	#endif


}

#endif