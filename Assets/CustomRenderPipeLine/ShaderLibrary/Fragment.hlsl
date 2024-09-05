#ifndef _FRAGMENT_INCLUDE_
#define _FRAGMENT_INCLUDE_

TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

//用_CameraBufferSize替换_ScreenParams 而且在C#已经做了除法
float4 _CameraBufferSize;

struct Fragment
{
    float2 positionSS;
    float depth;
    float2 screenUV;
    float bufferDepth;
};

Fragment GetFragment(float4 positionSS)
{
    Fragment f = (Fragment)0;
    f.positionSS = positionSS.xy;
    //f.screenUV = positionSS.xy / _ScreenParams.xy;
    f.screenUV = positionSS * _CameraBufferSize.xy; //替换_ScreenParamas
    bool isOrthographicCamera = IsOrthographicCamera();
    f.depth = isOrthographicCamera ? OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
    f.bufferDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, f.screenUV);
    f.bufferDepth = isOrthographicCamera
                        ? OrthographicDepthBufferToLinear(f.bufferDepth)
                        : LinearEyeDepth(f.bufferDepth, _ZBufferParams);
    return f;
}

float4 GetBufferColor(Fragment fragment, float2 uvOffset = 0)
{
    float2 uv = fragment.screenUV + uvOffset;
    return SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_linear_clamp, uv);
}


#endif
