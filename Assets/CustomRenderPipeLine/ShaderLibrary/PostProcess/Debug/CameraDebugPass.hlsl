#ifndef _CAMERA_DEBUG_PASS_INCLUDE_
#define _CAMERA_DEBUG_PASS_INCLUDE_

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"

float _DebugOpacity;

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVert(uint vertexID : SV_VertexID)
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

float4 ForwardPlusTilesPassFragment(Varyings input) : SV_TARGET
{
    ForwardPlusTile tile = GetForwardPlusTile(input.screenUV);
    float3 color;
    if (tile.IsMinimumEdgePixel(input.screenUV))
    {
        color = 1;
    }
    else
    {
        //使用RP core 中和显示热图函数
        //像素坐标、图块大小、灯光数量、允许的最大值和不透明度
        color = OverlayHeatMap(input.screenUV * _CameraBufferSize.zw, tile.GetScreenSize(),
                               tile.GetLightCount(), tile.GetMaxLightPerTile(), 1.0).rgb;
    }
    return float4(color, _DebugOpacity);
}

#endif
