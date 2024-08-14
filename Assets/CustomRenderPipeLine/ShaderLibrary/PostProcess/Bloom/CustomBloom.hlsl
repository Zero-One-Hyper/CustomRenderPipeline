#ifndef _CUSTOM_BLOOM_PASS_INCLUDE_
#define _CUSTOM_BLOOM_PASS_INCLUDE_
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

//Bloom
bool _BloomBicubicUpsampling;
float4 _BloomThreshold;
float _BloomIntensity;

float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_Target
{
    float3 color = 0;
    float offsets[] = {
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    //权重由Pascal三角形的来 属于固定做法
    float weights[] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622
    };

    for (int i = 0; i < 9; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(input.screenUV + float2(offset, 0)).rgb * weights[i];
    }
    return float4(color, 1);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_Target
{
    float3 color = 0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923 //使用一定的技巧减少竖直方向上的采样次数通过改变采样偏移的方法
    };
    //权重由Pascal三角形的来 属于固定做法
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0; i < 5; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().y;
        color += GetSource(input.screenUV + float2(0, offset)).rgb * weights[i];
    }
    return float4(color, 1);
}

//bloom的混合
//Add模式混合
float4 BloomAddPassFragment(Varyings input) : SV_Target
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }

    float4 highRes = GetSource2(input.screenUV);
    return float4(lowRes * _BloomIntensity + highRes.xyz, highRes.a);
}

//Scatter混合
float4 BloomScatterPassFragment(Varyings input) : SV_Target
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }

    float4 highRes = GetSource2(input.screenUV);
    return float4(lerp(highRes.xyz, lowRes.xyz, _BloomIntensity), highRes.a);
}

float4 BloomScatterFinalPassFragment(Varyings input) : SV_Target
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }

    float4 highRes = GetSource2(input.screenUV);
    lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
    return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}


float4 BloomPrefilterPassFragment(Varyings input) : SV_Target
{
    float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
    return float4(color, 1.0);
}

float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_Target
{
    //使萤火虫淡化最直接的方法是将预过滤通道的2×2降采样过滤器增长为大型6×6盒式过滤器
    float3 color = 0;
    float weightSum = 0;
    float2 offset[] =
    {
        float2(0, 0),
        float2(-1, -1), float2(-1, 1), float2(1, -1), float2(1, 1) //,
        //float2(-1, 0), float2(1, 0), float2(0, -1), float2(0, 1)
    };
    for (int i = 0; i < 5; i++)
    {
        float3 source = GetSource(input.screenUV + offset[i] * GetSourceTexelSize().xy * 2.0).rgb;
        source = ApplyBloomThreshold(source);
        float w = 1.0 / (Luminance(source) + 1.0); //将根据颜色的亮度使用加权平均值 color.hlsl
        color += source * w;
        weightSum += w;
    }
    color /= weightSum;
    return float4(color, 1);
}
#endif
