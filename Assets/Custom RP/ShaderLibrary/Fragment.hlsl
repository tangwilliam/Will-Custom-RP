#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

struct Fragment {
	float2 positionSS; // Screen Space Position
	float2 screenUV;
	float depth; // depth in View Space
	float bufferDepth; // depth in depth texture
};

Fragment GetFragment (float4 positionSS) {
	Fragment f;
	f.positionSS = positionSS.xy;
	f.screenUV = f.positionSS / _ScreenParams.xy;
	f.depth = IsOrthographicCamera() ?
		OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w; // 做完齐次除法之后w是否在任何平台下都仍然不变，这个有待实测
	
	f.bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD( _CameraDepthTexture, sampler_point_clamp, f.screenUV, 0 ); // 如果没有开启深度图，那么这将是一张1x1的图，理论上开销非常小
	f.bufferDepth = IsOrthographicCamera() ?
		OrthographicDepthBufferToLinear(f.bufferDepth) : LinearEyeDepth(f.bufferDepth, _ZBufferParams); // 深度图中的深度是非线性的

	return f;
}

float4 GetBufferColor( Fragment fragment, float2 uvOffset = float2(0.0,0.0) ){
	float2 uv = fragment.screenUV.xy + uvOffset;
	return SAMPLE_TEXTURE2D_LOD( _CameraColorTexture, sampler_linear_clamp, uv, 0 );
}

#endif