#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);

float4 _PostFXSource_TexelSize;


float4 GetSourceTexelSize () {
	return _PostFXSource_TexelSize;
}

float4 GetSource(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceBicubic (float2 screenUV) {
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}

float4 GetSource2(float2 screenUV) {
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}

struct Varyings {
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID) {
	Varyings output;
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);
	if (_ProjectionParams.x < 0.0) {
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}


float Luminance (float3 color, bool useACES) {
	return useACES ? AcesLuminance(color) : Luminance(color);
}

//------------------------------------------------------
// Bloom

bool _BloomBicubicUpsampling;
float _BloomIntensity;

float4 _BloomThreshold;

// 使用 Soft Knee Curve 来避免该情况：在阈值达到的时候，突然出现bloom效果，而导致有些亮度逐渐过渡的地方可能显得bloom出现得生硬。详见：https://catlikecoding.com/unity/tutorials/custom-srp/post-processing/
float3 ApplyBloomThreshold (float3 color) {
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}


float4 BloomCombinePassFragment (Varyings input) : SV_TARGET {
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
	return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}

float4 BloomScatterPassFragment (Varyings input) : SV_TARGET {
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

// Scatter Bloom: 主要思想是尽量维持能量守恒以及实现相机不够完美导致的拍到的照片存在光散射的缺陷效果。
// BloomAdd仅是让明亮的光溢出；而 BloomScatter仍然考虑画面里更明亮的部位溢出(或散射)更加明显。但它同时会将明亮区域因为计算被加亮的亮度减掉，以维持明亮区域的能量守恒。而不明亮区域则被笼罩上（有一定程度亮度变亮）了模糊的光散射效果。
// Bloom Scatter 的效果存在一些问题：如果非常明亮的光是白色，那么最终画面上原本最亮的区域光仍然很亮（但没有被add light），符合预期效果。但如果是蓝色等饱和度较高的光，那么这个计算被ApplyBloomThreshold()一减，则蓝色明亮区域反而没有未开后效之前那么明亮了，效果不太对劲。
float4 BloomScatterFinalPassFragment (Varyings input) : SV_TARGET {
	float3 lowRes;
	if (_BloomBicubicUpsampling) {
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else {
		lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
	lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb); // 将被threshold筛出来进行Scatter的区域减掉，以尽量维持能量守恒不'Add light'。那么被Scatter扩散出到附近像素的散射模糊效果仍然存在。
	return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}

float4 BloomHorizontalPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	for (int i = 0; i < 9; i++) {
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().x; // 为什么要 乘以2 ？是因为这里GPU每次采样是2x2的采然后bilinear插值，仅仅间隔1个像素取不到完全跟当前像素无关的像素
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

float4 BloomPrefilterPassFragment (Varyings input) : SV_TARGET {
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}

// 本来是要取上下左右九个像素的，但原作者说因为后续blur时左右上下会有高斯模糊，所以只取对角线加自己五个像素就能达到接近的效果。https://catlikecoding.com/unity/tutorials/custom-srp/hdr/
float4 BloomPrefilterFadeFirefliesPassFragment (Varyings input) : SV_TARGET {

	float2 offsets[] = { float2(-1,-1), float2(1,1), float2(-1,1), float2(1,-1), float2(0,0) };
	float totalWeight = 0;
	float3 totalColor = 0.0;
	for( int i = 0; i < 5; i++ ){
		float3 c = ApplyBloomThreshold(GetSource(input.screenUV + offsets[i] * 2.0 * GetSourceTexelSize()).rgb);
		float weight = 1.0 / ( Luminance(c) + 1.0 );
		c *=  weight;
		totalColor += c;
		totalWeight += weight;
	} 
	float3 color = totalColor / totalWeight;
	return float4(color, 1.0);
}

float4 BloomVerticalPassFragment (Varyings input) : SV_TARGET {
	float3 color = 0.0;
	float offsets[] = {
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	};
	float weights[] = {
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};
	for (int i = 0; i < 5; i++) {
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

// Bloom
//------------------------------------------------------------

//------------------------------------------------------------
// Color Grading and Tone Mapping

float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows, _SplitToningHighlights;
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;


float3 ColorGradingPostExposure(float3 color){
	return color *= _ColorAdjustments.x;
}

float3 ColorGradingContrast (float3 color, bool useACES) {
	color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color); // 在 log 空间进行对比度调整更适合调整滑块时的心理预期变化幅度。比如线性空间下，比较亮的颜色会因为略微增加对比度就对比度过于大幅地增加
	color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
	return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

float3 ColorGradingColorFilter(float3 color){
	return color *= _ColorFilter.rgb;
}

float3 ColorGradingHueShift (float3 color) {
	color = RgbToHsv(color);
	float hue = color.x + _ColorAdjustments.z;
	color.x = RotateHue(hue, 0.0, 1.0); // 让hue不论取何值，都输出结果是在[0,1]之间循环变化
	return HsvToRgb(color);
}
float3 ColorGradingSaturation (float3 color, bool useACES) {
	float luminance = Luminance(color, useACES);
	return luminance + _ColorAdjustments.w * (color - luminance);
}

float3 ColorGradingWhiteBalance (float3 color) {
	color = LinearToLMS(color);
	color *= _WhiteBalance.rgb;
	return LMSToLinear(color);
}

float3 ColorGradingSplitToning (float3 color, bool useACES) {
	color = PositivePow(color, 1.0 / 2.2);
	float t = saturate(Luminance(saturate(color),useACES) + _SplitToningShadows.w);
	float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
	float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
	color = SoftLight(color, shadows); // 类似于PhotoShop 中的“柔光”
	color = SoftLight(color, highlights);
	return PositivePow(color, 2.2);
}

float3 ColorGradingChannelMixer (float3 color) {
	return mul(
		float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),
		color
	);
}

float3 ColorGradingShadowsMidtonesHighlights (float3 color, bool useACES) {
	float luminance = Luminance(color, useACES);
	float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
	float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
	float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
	return
		color * _SMHShadows.rgb * shadowsWeight +
		color * _SMHMidtones.rgb * midtonesWeight +
		color * _SMHHighlights.rgb * highlightsWeight;
}


float3 ColorGrade (float3 color, bool useACES = false) {
	color = min(color, 60.0);
	color = ColorGradingPostExposure(color);
	color = ColorGradingWhiteBalance(color);
	color = ColorGradingContrast(color, useACES);
	color = ColorGradingColorFilter(color);
	color = max(color,0.0); // Contrast 可能带来负数， 但 ColorFilter在颜色为负数时也能正常工作，所以就放在ColorFilter之后
	color = ColorGradingSplitToning(color,useACES);
	color = ColorGradingChannelMixer(color);
	color = max(color,0.0);
	color = ColorGradingShadowsMidtonesHighlights(color, useACES);
	color = ColorGradingHueShift(color);
	color = ColorGradingSaturation(color,useACES);
	return max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
}

float4 _ColorGradingLUTParameters;
bool _ColorGradingLUTInLogC;
TEXTURE2D(_ColorGradingLUT);
bool _EnableColorGrading;

float3 GetColorGradedLUT( float2 uv, bool useACES = false ){
	// params = (lut_height, 0.5 / lut_width, 0.5 / lut_height, lut_height / lut_height - 1)
	float3 color = GetLutStripValue( uv, _ColorGradingLUTParameters);
	return ColorGrade( _ColorGradingLUTInLogC ? LogCToLinear( color ) : color , useACES); // 使用LogC空间时，假定由uv[0,1]转成的三维rgb本身就是logC空间的。所以做LogCToLinear()。届时在使用ApplyLut2D()进行采样LUT时，会将线性空间的rgb转换成LogC空间，以使之由最大值远大于1缩小到[0,1],然后采样LUT得到"在线性空间"中ColorGrade()的最终校色后的线性空间的结果。
}

// 既不做 ColorGrading 也不做 ToneMapping 的情况比较少见，所以就让这种情况变成一个单纯的拷贝好了。避免去动 C Sharp 代码的结构
float4 ColorGradingAndToneMappingNonePassFragment (Varyings input) : SV_TARGET {
	 float4 color = _EnableColorGrading ?  float4(GetColorGradedLUT(input.screenUV),1.0) : GetSource(input.screenUV);
	return color;
}

float4 ColorGradingAndToneMappingACESPassFragment(Varyings input) : SV_TARGET {
	float4 color = 0.0;
	if(_EnableColorGrading){
		color = float4(GetColorGradedLUT(input.screenUV, true),1.0);
	}else{
		color = GetSource(input.screenUV);
		color.rgb = unity_to_ACES(color.rgb);
	}
	color.rgb = AcesTonemap(color.rgb); // AcesTonemap()和 unity_to_ACES()都来自官方代码。注意 unity_to_ACES()是为了将RGB转换的到ACES所需的色彩空间ACES2065-1。它是一个3x3的矩阵，主要效果是给红通道(ACES空间中的第一个通道，也许也叫红通道)较多地混杂了绿色和蓝色，绿色和蓝色值也少量变化。
	return color;
}

float4 ColorGradingNeutralPassFragment (Varyings input) : SV_TARGET {
	float4 color = 0.0;
	if(_EnableColorGrading){
		color = float4(GetColorGradedLUT(input.screenUV, true),1.0);
	}else{
		color = GetSource(input.screenUV);
	}
	color.rgb = NeutralTonemap(color.rgb);
	return color;
}

float4 ColorGradingReinhardPassFragment (Varyings input) : SV_TARGET {
	float4 color = 0.0;
	if(_EnableColorGrading){
		color = float4(GetColorGradedLUT(input.screenUV, true),1.0);
	}else{
		color = GetSource(input.screenUV);
	}
	color.rgb /= color.rgb + 1.0;
	return color;
}

float3 ApplyColorGradingLUT( float3 color ){
	// scaleOffset = (1 / lut_width, 1 / lut_height, lut_height - 1)
	return ApplyLut2D(TEXTURE2D_ARGS( _ColorGradingLUT, sampler_linear_clamp), saturate( _ColorGradingLUTInLogC ? LinearToLogC(color.rgb) : color.rgb), _ColorGradingLUTParameters.xyz);
}

float4 ApplyColorGradingFragment(Varyings input) : SV_TARGET{
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT( color.rgb);
	return color; 
}

bool _CopyBicubic;

float4 FinalPassFragmentRescale (Varyings input) : SV_TARGET {
	if (_CopyBicubic) {
		return GetSourceBicubic(input.screenUV);
	}
	else {
		return GetSource(input.screenUV);
	}
}

// Color Grading and Tone Mapping
//------------------------------------------------------------

float4 CopyPassFragment (Varyings input) : SV_TARGET {
	return GetSource(input.screenUV);
}

#endif