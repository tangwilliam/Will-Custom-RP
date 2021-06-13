#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

struct Fragment {
	float2 positionSS; // Screen Space Position
	float depth; // depth in View Space
};

Fragment GetFragment (float4 positionSS) {
	Fragment f;
	f.positionSS = positionSS.xy;
	f.depth = IsOrthographicCamera() ?
		OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w; // 做完齐次除法之后w是否在任何平台下都仍然不变，这个有待实测
	return f;
}

#endif