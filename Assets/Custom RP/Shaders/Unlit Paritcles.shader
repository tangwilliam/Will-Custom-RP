Shader "Custom RP/Particles/Unlit"
{
    Properties
    {
        _BaseMap("MainTex",2D) = "white" {}
        [HDR]_BaseColor("Color",Color) = (1.0,1.0,1.0,1.0)
		[Toggle(_VERTEX_COLOR)]_VertexColor("Vertex Color", Float) = 1
		[Toggle(_FLIPBOOK_BLENDING)]_FlipbookBlending("Flipbook Blending",Float) = 0
		[Toggle(_NEAR_FADE)] _NearFade ("Near Fade", Float) = 0
		_NearFadeDistance ("Near Fade Distance", Range(0.0, 10.0)) = 1
		_NearFadeRange ("Near Fade Range", Range(0.01, 10.0)) = 1

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0
        [KeywordEnum(On,Clip,Dither,Off)] _Shadows("Shadows",Float) = 0

    }
    SubShader
    {
        HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "UnlitInput.hlsl"
		ENDHLSL

        Pass
        {
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]

            HLSLPROGRAM

            #pragma target 3.5

			#pragma shader_feature _VERTEX_COLOR
			#pragma shader_feature _FLIPBOOK_BLENDING
			#pragma shader_feature _NEAR_FADE
            #pragma shader_feature _CLIPPING
            #pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            #include "UnlitPass Instance.hlsl"
            ENDHLSL

        }

		// 暂时去掉，现阶段不想花时间完善相关代码
        // Pass {
		// 	Tags {
		// 		"LightMode" = "ShadowCaster"
		// 	}

		// 	ColorMask 0

		// 	HLSLPROGRAM
		// 	#pragma target 3.5
		// 	#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
		// 	#pragma multi_compile _ LOD_FADE_CROSSFADE
		// 	#pragma multi_compile_instancing
		// 	#pragma vertex ShadowCasterPassVertex
		// 	#pragma fragment ShadowCasterPassFragment
		// 	#include "ShadowCasterPass.hlsl"
		// 	ENDHLSL
		// }

        // Pass {
		// 	Tags {
		// 		"LightMode" = "Meta"
		// 	}

		// 	Cull Off

		// 	HLSLPROGRAM
		// 	#pragma target 3.5
		// 	#pragma vertex MetaPassVertex
		// 	#pragma fragment MetaPassFragment
		// 	#include "MetaPass.hlsl"
		// 	ENDHLSL
		// }
    }

    CustomEditor "CustomShaderGUI"

}
