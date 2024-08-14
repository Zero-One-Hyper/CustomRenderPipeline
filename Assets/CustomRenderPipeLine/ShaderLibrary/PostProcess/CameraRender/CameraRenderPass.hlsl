#ifndef _CAMERA_RENDER_PASS_INCLUDE_
#define _CAMERA_RENDER_PASS_INCLUDE_

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : TEXCOORD0;
};

TEXTURE2D(_SourceTexture);

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

float4 CopyPassFragment(Varyings input) : SV_Target
{
    return SAMPLE_TEXTURE2D(_SourceTexture, sampler_linear_clamp, input.screenUV);
}

float CopyDepthFragment(Varyings input) : SV_Depth
{
    return SAMPLE_DEPTH_TEXTURE(_SourceTexture, sampler_point_clamp, input.screenUV);
}

#endif
