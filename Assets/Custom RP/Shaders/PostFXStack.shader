Shader "Hidden/Custom RP/Post FX Stack" {
	
	SubShader {
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "PostFXStackPasses.hlsl"
		ENDHLSL
		
		Pass { // 0
			Name "Bloom Combine"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomCombinePassFragment
			ENDHLSL
		}
        
		Pass { // 1
			Name "Bloom Scatter"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomScatterPassFragment
			ENDHLSL
		}
        Pass {// 2
			Name "Bloom Scatter Final"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomScatterFinalPassFragment
			ENDHLSL
		}
		Pass {// 3
			Name "Bloom Horizontal"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomHorizontalPassFragment
			ENDHLSL
		}

		Pass {// 4
			Name "Bloom Prefilter"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomPrefilterPassFragment
			ENDHLSL
		}

        Pass {// 5
			Name "Bloom Prefilter FadeFireflies"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomPrefilterFadeFirefliesPassFragment
			ENDHLSL
		}
		
		Pass {// 6
			Name "Bloom Vertical"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment BloomVerticalPassFragment
			ENDHLSL
		}
        
        //------------------------------
        // 都可以作为Final Pass 的 :  Tone Mapping / No Tone Mapping / Final(Enable Color Grade LUT)
		// 用于叠加别人的相机的clearColor必须设置为黑色，因为颜色也是 One OneMinusSrcAlpha了。属于Additive类型。否则背景部分本该没有颜色，却叠加了clearColor的颜色。
		Pass {// 7
			Name "Color Grading No ToneMapping"

            Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment ColorGradingAndToneMappingNonePassFragment
			ENDHLSL
		}
        Pass {// 8
			Name "Color Grading ACES ToneMapping"

            Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment ColorGradingAndToneMappingACESPassFragment
			ENDHLSL
		}
        Pass {// 9
			Name "Color Grading Neutral"

            Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment ColorGradingNeutralPassFragment
			ENDHLSL
		}
		
		Pass {// 10
			Name "Color Grading Reinhard"

            Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment ColorGradingReinhardPassFragment
			ENDHLSL
		}
        Pass {// 11
			Name "Apply Color Grading"

            Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment ApplyColorGradingFragment
			ENDHLSL
		}
		Pass {// 12
			Name "Final Rescale"

			Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment FinalPassFragmentRescale
			ENDHLSL
		}
		Pass {// 13
			Name "Copy"
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyPassFragment
			ENDHLSL
		}
		Pass {// 14
			Name "FXAA"

			Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment FXAAPassFragment
				#pragma multi_compile _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
				#include "FXAAPass.hlsl"
			ENDHLSL
		}
		
		Pass {
			Name "FXAA With Luma"

			Blend [_FinalSrcBlend] [_FinalDstBlend]
			
			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment FXAAPassFragment
				#pragma multi_compile _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
				#define FXAA_ALPHA_CONTAINS_LUMA
				#include "FXAAPass.hlsl"
			ENDHLSL
		}
	}
}