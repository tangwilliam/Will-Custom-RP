#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

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
	
	f.bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD( _CameraDepthTexture, sampler_point_clamp, f.screenUV, 0 );
	f.bufferDepth = IsOrthographicCamera() ?
		OrthographicDepthBufferToLinear(f.bufferDepth) : LinearEyeDepth(f.bufferDepth, _ZBufferParams);

	return f;
}

#endif