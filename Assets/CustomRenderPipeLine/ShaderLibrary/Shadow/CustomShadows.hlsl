#ifndef _CUSTOM_SHAODWS_INCLUDE_
#define _CUSTOM_SHAODWS_INCLUDE_
//引用PCF计算
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

//每次采样使用一个双线性2x2过滤采样器
//3x3需要4个采样器铺满
//5x5需要9个采样器铺满
//7x7需要16个采样器铺满
/*
#if defined(_DIRECTIONAL_PCF3)
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
#define DIRECTIONAL_FILTER_SAMPLES 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
#define DIRECTIONAL_FILTER_SAMPLES 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#if defined(_OTHER_PCF3)
#define OTHER_FILTER_SAMPLES 4
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
#define OTHER_FILTER_SAMPLES 9
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
#define OTHER_FILTER_SAMPLES 16
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif
*/

#if defined(_SHADOW_FILTER_HIGH)
#define DIRECTIONAL_FILTER_SAMPLES 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#define OTHER_FILTER_SAMPLES 16
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#elif defined (_SHADOW_FILTER_MEDIU)
#define DIRECTIONAL_FILTER_SAMPLES 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#define OTHER_FILTER_SAMPLES 9
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#else
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#define OTHER_FILTER_SAMPLES 4
#define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#endif


//级联阴影
#define MAX_SHADOW_CASCADE_COUNT 4

//这玩意儿叫DirectionalShadowSetting还差不多
struct DirectionalShadowDataSetting
{
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
};

struct OtherShadowDataSetting
{
    float strength;
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 lightDirectionWS;
    float3 spotDirectionWS;
};

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherLightShadowAtlas);
// 实际上，采样阴影贴图只有一种合适的方法，所以我们可以定义一个明确的采样器状态，而不是依赖Unity为我们的渲染纹理推断的那个。
// 通过在名称中使用特定的单词可以内联创建采样器状态。
// 我们可以使用sampler_linear_clamp_compare。
// 我们还需要为它定义一个简写的SHADOW_SAMPLER宏。
#define SHADOW_SAMPLER sampler_linear_clamp_compare//sampler_后面时内联着色器状态 此处使用了线性clamp compare用于采样深度图
//SAMPLER_CMP(sampler_DirectionalShadowAtlas);
SAMPLER_CMP(SHADOW_SAMPLER);


CBUFFER_START(_CustomShadows)
    int _CascadeCount;
    float4 _ShadowDistanceFade;
    float4 _ShadowAtlasSize; //图集尺寸

CBUFFER_END

struct DirectionShadowCascade
{
    float4 cullingSphere;
    float4 cascadeData;
};

struct OtherShadowBufferData
{
    float4 tileData;
    float4x4 shadowMatrix;
};

StructuredBuffer<DirectionShadowCascade> _DirectionShadowCascade;
StructuredBuffer<float4x4> _DirectionalShadowMatrices;
StructuredBuffer<OtherShadowBufferData> _OtherShadowData;

//ShadowMask
struct ShadowMask
{
    bool always;
    bool distance;
    float4 shadows;
};

//级联阴影的索引是逐片源决定的（不是逐光源
struct ShadowData
{
    int cascadeIndex;
    float cascadeBlend; //级联间的混合
    float strength;
    ShadowMask shadowMask;
};


static const float3 _PointShadowPlane[6] =
{
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0)
};

float FadeShadowStrength(float distance, float scale, float fade)
{
    //使用(1 - d/m) / f计算淡化阴影（部分除法已经在C#中预先计算了）
    return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data = (ShadowData)0;
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1;
    data.cascadeBlend = 1;
    data.strength = FadeShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    int i = 0;
    for (i = 0; i < _CascadeCount; i++)
    {
        DirectionShadowCascade shadowCascade = _DirectionShadowCascade[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, shadowCascade.cullingSphere.xyz);
        //寻找到正好包含当前像素的裁剪球（记住级联阴影的索引是逐片元的）
        if (distanceSqr < shadowCascade.cullingSphere.w)
        {
            float fade = FadeShadowStrength(distanceSqr, shadowCascade.cascadeData.x, _ShadowDistanceFade.z);
            if (i == _CascadeCount - 1)
            {
                //如果使用级联阴影最后一级替换fade的话↓
                data.strength *= FadeShadowStrength(distanceSqr, 1 / shadowCascade.cullingSphere.w,
                                                    _ShadowDistanceFade.z);
                //data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    //如果超出了最后的级联，那很可能就没有有效的阴影数据了，此时根本不应该再采样阴影
    if (i == _CascadeCount && _CascadeCount > 0)
    {
        data.strength = 0;
    }
    #if !defined(_SOFT_CASCADE_BLEND)
    else if (data.cascadeBlend < surfaceWS.dither)
    {
        //使用抖动混合时，如果我们不在最后一个级联，在混合值小于抖动值时，跳到下一个级联。
        i += 1;
    }
    //#endif
    //#ifndef _CASCADE_BLEND_SOFT
    //不使用软阴影九八混合设置为1 使得GetDirectionalShadowAttenuation的if判断不通过
    data.cascadeBlend = 1.0;
    #endif
    data.cascadeIndex = i;

    return data;
}


float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds)
{
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
    return SAMPLE_TEXTURE2D_SHADOW(_OtherLightShadowAtlas, SHADOW_SAMPLER, positionSTS);
}


float FilterDirectionalShadow(float3 positionSTS)
{
    #ifdef DIRECTIONAL_FILTER_SETUP
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
    }
    return shadow;
    #else
    return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
    #ifdef OTHER_FILTER_SETUP
    float weights[OTHER_FILTER_SAMPLES];
    float2 positions[OTHER_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.wwzz;
    OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0;
    for (int i = 0; i < OTHER_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z), bounds);
    }
    return shadow;
    #else
    return SampleOtherShadowAtlas(positionSTS, bounds);
    #endif
}

float GetCascadShadowAttenuation(DirectionalShadowDataSetting data, ShadowData shadowData, Surface surfaceWS)
{
    //阴影尖刺、Shadow Acne、阴影失真产生原因：
    //https://learnopengl-cn.github.io/05%20Advanced%20Lighting/03%20Shadows/01%20Shadow%20Mapping/
    //受限于阴影贴图的分辨率，在光源较远的情况下，多个片元有可能从同一个深度贴图中的同一个像素去采样，
    //当光源照射方向和平面存在夹角时 ，多个片元从同一个深度纹理采样，就会出现有的片源采样出来在上，有的在下 出现条状失真
    //以法线偏移的方式解决条状阴影暗斑           前面时是light的bias偏移， 后面是纹素的偏移
    //使用插值法线计算CascadeShadow
    float3 normalBias = surfaceWS.interpolatedNormal * (data.normalBias *
        _DirectionShadowCascade[shadowData.cascadeIndex].cascadeData.y);
    float3 positionSTS = mul(_DirectionalShadowMatrices[data.tileIndex],
                             float4(surfaceWS.position + normalBias, 1)).xyz;
    //将surface经过一个变换矩阵T变换到光源的坐标空间中，对比shadowmap中的值(最近的物体的深度)与变换后坐标的z值 对比 就能知道当前像素是否在阴影中
    float shadow = FilterDirectionalShadow(positionSTS); //换成PCF计算(阴影的边缘会比较软一点)
    //级联间的过度
    if (shadowData.cascadeBlend < 1.0)
    {
        //若处于过度区域，就必须从下一级级联中采样
        normalBias = surfaceWS.interpolatedNormal * (data.normalBias *
            _DirectionShadowCascade[shadowData.cascadeIndex + 1].cascadeData.y);
        positionSTS = mul(_DirectionalShadowMatrices[data.tileIndex + 1],
                          float4(surfaceWS.position + normalBias, 1)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, shadowData.cascadeBlend); //线性混合
    }
    return shadow;
}

float GetBakedShadow(ShadowMask shadowMask, int maskChannel)
{
    //使用任何一种模式时，两个版本的GetBakedShadow都应选择遮罩。
    if (shadowMask.always || shadowMask.distance)
    {
        if (maskChannel >= 0)
        {
            return shadowMask.shadows[maskChannel];
        }
    }
    return 1;
}

//用于由阴影遮罩但是没有实时阴影时
float GetBakedShadow(ShadowMask shadowMask, int maskChannel, float strength)
{
    if (shadowMask.always || shadowMask.distance)
    {
        return lerp(1, GetBakedShadow(shadowMask, maskChannel), strength);
    }
    return 1;
}

float GetOtherShadow(OtherShadowDataSetting otherShadowData, ShadowData globalShadowData, Surface surfaceWS)
{
    float tileIndex = otherShadowData.tileIndex;
    float3 lightPlane = otherShadowData.spotDirectionWS;
    if (otherShadowData.isPoint)
    {
        //点光源shadowMap是一个cubmap 共有六张Tex
        float faceOffset = CubeMapFaceID(-otherShadowData.lightDirectionWS);
        tileIndex += faceOffset;
        lightPlane = _PointShadowPlane[faceOffset];
    }
    //float4 tileData = shadowBufferData.tileData;    
    OtherShadowBufferData shadowBufferData = _OtherShadowData[tileIndex];
    float3 surfaceToLight = otherShadowData.lightPositionWS - surfaceWS.position;
    float distanceToLightPlane = dot(surfaceToLight, lightPlane);
    float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane * shadowBufferData.tileData.w);
    float4 positionSTS = mul(shadowBufferData.shadowMatrix,
                             float4(surfaceWS.position + normalBias, 1));
    //聚光灯是透视阴影，所以需将变换的xyz坐标除以w分量
    return FilterOtherShadow(positionSTS.xyz / positionSTS.w, shadowBufferData.tileData.xyz);
}

float MixBakedAndRealtimeShadows(ShadowData shadowData, float realTimeShadow, int maskChannel,
                                 float shadowSettingStrength)
{
    float bakedShadow = GetBakedShadow(shadowData.shadowMask, maskChannel);
    float shadow = realTimeShadow;
    if (shadowData.shadowMask.always)
    {
        shadow = lerp(1, shadow, shadowData.strength);
        shadow = min(bakedShadow, shadow);
        return lerp(1, shadow, shadowSettingStrength);
    }
    if (shadowData.shadowMask.distance)
    {
        shadow = lerp(bakedShadow, shadow, shadowData.strength);
        return lerp(1, shadow, shadowSettingStrength);
    }
    return lerp(1, shadow, shadowSettingStrength * shadowData.strength);
}

float GetDirectionalShadowAttenuation(DirectionalShadowDataSetting dataSetting, ShadowData shadowData,
                                      Surface surfaceWS)
{
    /*
    #ifndef _RECEIVE_SHADOWS
    return 1;
    #endif
    */
    //判断实时阴影是否存在（如果没有实时阴影shadow.cs的ReserveDirectionalShadows会给strength取反)
    if (dataSetting.strength * shadowData.strength <= 0.0)
    {
        return GetBakedShadow(shadowData.shadowMask, dataSetting.shadowMaskChannel, abs(dataSetting.strength));
    }
    float realTimeShadow = GetCascadShadowAttenuation(dataSetting, shadowData, surfaceWS);

    //最终值在最大1和采样到的值之间用强度插值
    return MixBakedAndRealtimeShadows(shadowData, realTimeShadow, dataSetting.shadowMaskChannel, dataSetting.strength);
}

float GetOtherShadowAttenuation(OtherShadowDataSetting otherShadowData, ShadowData shadowData,
                                Surface surfaceWS)
{
    /*
    #ifndef _RECEIVE_SHADOWS
    return 1;
    #endif
    */
    float shadow;
    if (otherShadowData.strength * shadowData.strength <= 0)
    {
        shadow = GetBakedShadow(shadowData.shadowMask, otherShadowData.shadowMaskChannel,
                                abs(otherShadowData.strength));
    }
    else
    {
        shadow = GetOtherShadow(otherShadowData, shadowData, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(shadowData, shadow, otherShadowData.shadowMaskChannel,
                                            otherShadowData.strength);
    }
    return shadow;
}

#endif
