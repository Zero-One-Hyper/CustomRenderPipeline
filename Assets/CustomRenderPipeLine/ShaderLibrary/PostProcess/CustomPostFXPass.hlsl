#ifndef _CUSTOM_POST_FX_PASS_INCLUDE_
#define _CUSTOM_POST_FX_PASS_INCLUDE_

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : TEXCOORD0;
};

TEXTURE2D(_PostFXSource);
SAMPLER(sampler_PostFXSource);

TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_PostFXSource2);

float4 _PostFXSource_TexelSize;


Varyings DefaultFXVert(uint vertexID : SV_VertexID)
{
    Varyings output = (Varyings)0;
    //这里只绘制一个三角形 三个顶点刚好覆盖了NDC空间的[-1,1]
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0, 1);
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0);
    //在某些图形API下会导致某些图像如scene窗口图像颠倒
    if (_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}


float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}


float4 GetSource(float2 screenUV)
{
    return SAMPLE_TEXTURE2D(_PostFXSource, sampler_PostFXSource, screenUV);
}

float4 GetSource2(float2 screenUV)
{
    return SAMPLE_TEXTURE2D(_PostFXSource2, sampler_PostFXSource2, screenUV);
}

//双三次过滤消除Boom失真
float4 GetSourceBicubic(float2 screenUV)
{
    return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
                                  _PostFXSource_TexelSize.zwxy, 1.0, 0);
}

float4 CopyPassFrag(Varyings input) : SV_Target
{
    return GetSource(input.screenUV);
}

#endif
