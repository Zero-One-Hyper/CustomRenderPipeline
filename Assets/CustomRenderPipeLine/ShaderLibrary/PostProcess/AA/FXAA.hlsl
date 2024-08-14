#ifndef _FXAA_PASS_INCLUDE_
#define _FXAA_PASS_INCLUDE_

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

#if defined(FXAA_QUALITY_LOW)

#define EXTRA_EDGE_STEPS 3
#define EDGE_STEP_SIZES 1.5, 2.0, 2.0
#define LAST_EDGE_STEP_GUESS 8.0

#elif defined(FXAA_QUALITY_MEDIUM)//以后都建议这样写define

#define EXTRA_EDGE_STEPS 8
#define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
#define LAST_EDGE_STEP_GUESS 8.0
#else

#define EXTRA_EDGE_STEPS 10
#define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
#define LAST_EDGE_STEP_GUESS 8.0

#endif


static const float edgeStepSizes[EXTRA_EDGE_STEPS] = {EDGE_STEP_SIZES};

float4 _FXAAConfig;

//相邻像素亮度
struct LumaNeighborhood
{
    float m;
    float n;
    float e;
    float w;
    float s;
    float ne;
    float nw;
    float se;
    float sw;
    float highest;
    float lowest;
    float range;
};

// 存储检测到的边缘的信息
struct FXAAEdge
{
    bool isHorizonal;
    float pixelStep;
    //用于确定处理的是那种边缘
    float lumaGradient;
    float otherLuma;
};


//选择性地降低图像对比度，是通过比较像素地感知强度来确定的，
//即只关注感知亮度，也就是gama调整后的亮度
float GetLuma(float2 uv, float uOffset = 0.0, float vOffset = 0.0)
{
    //偏移
    uv += float2(uOffset, vOffset) * GetSourceTexelSize().xy;
    #ifdef _FXAA_ALPHA_CONTANTS_LUMA_
    return GetSource(uv).a;
    #else
    //人眼对深色的变化比浅色的变化更敏感，所以需要对亮度图应用伽马调整，这里使用线性平方根
    //return sqrt(Luminance(GetSource(uv)));
    //或者 人眼对绿色最敏感，也可以直接用绿色通道替代亮度(效果会差一点)
    return GetSource(uv).g;
    #endif
}

LumaNeighborhood GetLumaNeighborhood(float2 uv)
{
    LumaNeighborhood lumas;
    lumas.m = GetLuma(uv);
    lumas.n = GetLuma(uv, 0.0, 1.0);
    lumas.e = GetLuma(uv, 1.0, 0.0);
    lumas.s = GetLuma(uv, 0.0, -1.0);
    lumas.w = GetLuma(uv, -1.0, 0.0);
    lumas.ne = GetLuma(uv, 1.0, 1.0);
    lumas.nw = GetLuma(uv, -1.0, 1.0);
    lumas.se = GetLuma(uv, 1.0, -1.0);
    lumas.sw = GetLuma(uv, -1.0, -1.0);
    //确定该领域亮度范围
    lumas.highest = max(max(max(max(lumas.m, lumas.n), lumas.e), lumas.s), lumas.w);
    lumas.lowest = min(min(min(min(lumas.m, lumas.n), lumas.e), lumas.s), lumas.w);
    //亮度范围在视觉上表现为边缘线条
    lumas.range = lumas.highest - lumas.lowest;
    return lumas;
}

bool CanSkipFXAA(LumaNeighborhood lumas)
{
    return lumas.range < max(_FXAAConfig.x, _FXAAConfig.y * lumas.highest);
}

//混合亮度 得到混合因子
float GetSubpixelBlendFactor(LumaNeighborhood luma)
{
    float filter = (luma.n + luma.e + luma.w + luma.s) * 2.0; //（对角线的像素贡献较低）加倍直接邻居
    filter += luma.ne + luma.nw + luma.se + luma.sw;
    filter *= 0.0833; //12个量
    filter = abs(filter - luma.m); //取插值 转变为高通滤波器
    filter = saturate(filter / luma.range); //归一化
    filter = smoothstep(0, 1, filter); //平滑
    return filter * filter * _FXAAConfig.z; //最后应用平方函数
}

//确定混合方向（取决于对比度的梯度方向）
bool IsHorizontalEdge(LumaNeighborhood luma)
{
    float horizon = 2.0 * abs(luma.n + luma.s - 2.0 * luma.m) +
        (luma.ne + luma.se - 2.0 * luma.m) +
        (luma.nw + luma.sw - 2.0 * luma.m);
    float vertical = 2.0 * abs(luma.w + luma.e - 2.0 * luma.m) +
        abs(luma.nw + luma.ne - 2.0 * luma.m) +
        abs(luma.sw + luma.sw - 2.0 * luma.m);
    return horizon >= vertical;
}

FXAAEdge GetFXAAEdge(LumaNeighborhood luma)
{
    FXAAEdge fxaaEdge;
    fxaaEdge.isHorizonal = IsHorizontalEdge(luma);
    float lumaP;
    float lumaN;
    if (fxaaEdge.isHorizonal)
    {
        fxaaEdge.pixelStep = GetSourceTexelSize().y;
        lumaP = luma.n;
        lumaN = luma.s;
    }
    else
    {
        fxaaEdge.pixelStep = GetSourceTexelSize().x;
        lumaP = luma.e;
        lumaN = luma.w;
    }
    //通过比较相应两边的对比度确定往正方向还是负方向混合
    float gradientP = abs(lumaP - luma.m);
    float gradientN = abs(lumaN - luma.m);
    if (gradientP < gradientN)
    {
        fxaaEdge.pixelStep = -fxaaEdge.pixelStep;
        fxaaEdge.lumaGradient = lumaN;
        fxaaEdge.otherLuma = lumaP;
    }
    else
    {
        fxaaEdge.lumaGradient = lumaP;
        fxaaEdge.otherLuma = lumaN;
    }
    return fxaaEdge;
}

float GetEdgeBlendFactor(LumaNeighborhood luma, FXAAEdge edge, float2 uv)
{
    //追踪边缘 沿着边缘向两边走，直到找到端点
    float2 edgeUV = uv;
    float2 uvStep = 0.0;
    //在中间像素与边缘像素中间采样 得到二者的平均值 就不需要每次都采样两个像素了
    if (edge.isHorizonal)
    {
        edgeUV.y += 0.5 * edge.pixelStep;
        uvStep.x = GetSourceTexelSize().x;
    }
    else
    {
        edgeUV.x += 0.5 * edge.pixelStep;
        uvStep.y = GetSourceTexelSize().y;
    }

    float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
    float gradientThreshold = 0.25 * edge.lumaGradient;

    //朝正方向移动一步，检查是否是末端
    float2 uvP = edgeUV + uvStep;
    float lumaDeltaP = GetLuma(uvP) - edgeLuma; //追踪亮度增量 而不是亮度的梯度变化
    bool atEndP = abs(lumaDeltaP) >= gradientThreshold; //超过阈值表示到达末端

    //遍历整个边缘，并且需要有最大采样限制 直到到达末端
    int i;
    //降低采样次数提升性能，但会降低质量
    //for (i = 0; i < 99 && !atEndP; i++)
    UNITY_UNROLL
    for (i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++)
    {
        uvP += uvStep * edgeStepSizes[i];
        lumaDeltaP = GetLuma(uvP) - edgeLuma;
        atEndP = abs(lumaDeltaP) >= gradientThreshold;
    }
    //如果执行到最后一次采样依旧没有找到边缘，就认为边缘在最大采样数+1的地方
    if (!atEndP)
    {
        uvP += uvStep * LAST_EDGE_STEP_GUESS;
    }

    //计算饭反方向
    float2 uvN = edgeUV - uvStep;
    float lumaDeltaN = GetLuma(uvN) - edgeLuma;
    bool atEndN = abs(lumaDeltaN) >= gradientThreshold; //超过阈值表示到达末端

    //for (i = 0; i < 99 && !atEndN; i++)
    //降低采样次数提升性能，但会降低质量
    UNITY_UNROLL
    for (i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++)
    {
        uvN -= uvStep * edgeStepSizes[i];
        lumaDeltaN = GetLuma(uvN) - edgeLuma;
        atEndN = abs(lumaDeltaN) >= gradientThreshold;
    }
    //如果执行到最后一次采样依旧没有找到边缘，就认为边缘在最大采样数+1的地方
    if (!atEndN)
    {
        uvN -= uvStep * LAST_EDGE_STEP_GUESS;
    }

    //uv上像素到末端的距离（正方向
    float distanceToEndP;
    //uv上像素到末端的距离（反方向
    float distanceToEndN;
    if (edge.isHorizonal)
    {
        distanceToEndP = uvP.x - uv.x;
        distanceToEndN = uv.x - uvN.x;
    }
    else
    {
        distanceToEndP = uvP.y - uv.y;
        distanceToEndN = uv.y - uvN.y;
    }
    //距离像素最近的边缘
    float distanceToNearestEnd;
    bool deltaSign; //确定亮度增量的方向
    if (distanceToEndP <= distanceToEndN)
    {
        distanceToNearestEnd = distanceToEndP;
        deltaSign = lumaDeltaP >= 0;
    }
    else
    {
        distanceToNearestEnd = distanceToEndN;
        deltaSign = lumaDeltaN >= 0;
    }
    //如果亮度增量方向
    if (deltaSign == (luma.m - edgeLuma >= 0))
    {
        return 0;
    }
    else
    {
        return 0.5 - distanceToNearestEnd / (distanceToEndN + distanceToEndP);
    }
}

//FXAA的原理是选择性地降低图像对比度，平滑视觉上的锯齿与孤立像素
float4 FXAAPassFragment(Varyings input) : SV_Target
{
    LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
    if (CanSkipFXAA(luma))
    {
        return GetSource(input.screenUV);
    }
    FXAAEdge fxaaEdge = GetFXAAEdge(luma);
    //两种融合方式取最大
    float blendFactor = max(GetSubpixelBlendFactor(luma), //全力子像素融合
                            GetEdgeBlendFactor(luma, fxaaEdge, input.screenUV)); //仅进行边缘混合 
    //最终混合(说是混合，本质是uv偏移采样）
    float2 blendUV = input.screenUV;
    if (fxaaEdge.isHorizonal)
    {
        blendUV.y += blendFactor * fxaaEdge.pixelStep;
    }
    else
    {
        blendUV.x += blendFactor * fxaaEdge.pixelStep;
    }

    return GetSource(blendUV);
}

#endif
