#ifndef _CUSTOM_LIGHT_INCLUDE_
#define _CUSTOM_LIGHT_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Shadow/CustomShadows.hlsl"

#define MAX_DIRECTION_LIGHT_COUNT 4
//#define MAX_OTHER_LIGHT_COUNT 64 使用了computerbuffer 不再需要再shader中定义

//创建uniform值 在buffer中存储光照信息
CBUFFER_START(_CustomLight)
    int _DirectionLightCount;
    int _OtherLightCount;

CBUFFER_END

struct DirectionLightData
{
    float4 color;
    float4 directionAndMask;
    float4 shadowData;
};

struct OtherLightData
{
    float4 color;
    float4 position;
    float4 directionAndMask;
    float4 spotAngle;
    float4 shadowData;
};

StructuredBuffer<DirectionLightData> _DirectionLightData;
StructuredBuffer<OtherLightData> _OtherLightData;

//存放光源数据
struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
    uint renderingLayerMask;
};

int GetDirectionLightCount()
{
    return _DirectionLightCount;
}

int GetOtherLightCount()
{
    return _OtherLightCount;
}

bool RenderingLayersOverlap(Surface surface, Light light)
{
    return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}

DirectionalShadowDataSetting GetDirectionShadowDataSetting(float4 lightShadowData, ShadowData shadowData)
{
    DirectionalShadowDataSetting data;
    data.strength = lightShadowData.x * shadowData.strength; //多乘一个用来避免采样不存在的级联采样 同意设置为0
    data.tileIndex = lightShadowData.y + shadowData.cascadeIndex;
    data.normalBias = lightShadowData.z;
    data.shadowMaskChannel = lightShadowData.w;
    return data;
}

OtherShadowDataSetting GetOtherShadowDataSetting(float4 lightShadowData)
{
    OtherShadowDataSetting otherShadowData;
    otherShadowData.strength = lightShadowData.x;
    otherShadowData.tileIndex = lightShadowData.y;
    otherShadowData.shadowMaskChannel = lightShadowData.w;
    otherShadowData.isPoint = lightShadowData.z == 1;
    otherShadowData.lightPositionWS = 0; //在外面填充
    otherShadowData.lightDirectionWS = 0;
    otherShadowData.spotDirectionWS = 0;
    return otherShadowData;
}

Light GetDirectionLight(int index, Surface surface, ShadowData shadowData)
{
    DirectionLightData directionLightData = _DirectionLightData[index];
    Light light;
    light.color = directionLightData.color.xyz;
    light.direction = directionLightData.directionAndMask.xyz;
    light.renderingLayerMask = asuint(directionLightData.directionAndMask.w);

    DirectionalShadowDataSetting dirShadowData = GetDirectionShadowDataSetting(
        directionLightData.shadowData, shadowData);
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surface);
    //light.attenuation = shadowData.cascadeIndex * 0.25;
    //light.attenuation = 1;
    return light;
}

Light GetOtherLight(int index, Surface surface, ShadowData shadowData)
{
    OtherLightData otherLightData = _OtherLightData[index];
    Light light;
    light.color = otherLightData.color.xyz;
    float3 lightPosition = otherLightData.position.xyz;
    float3 ray = lightPosition - surface.position;
    light.direction = normalize(ray);
    float distanceSqr = max(dot(ray, ray), 0.00001);
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * otherLightData.position.w)));

    //float4 spotAngles = otherLightData.spotAngle;
    float3 spotDirection = otherLightData.directionAndMask.xyz;
    light.renderingLayerMask = asuint(otherLightData.directionAndMask.w);
    float spotAttenuation = Square(saturate(dot(spotDirection, light.direction) *
        otherLightData.spotAngle.x + otherLightData.spotAngle.y));

    OtherShadowDataSetting otherShadowData = GetOtherShadowDataSetting(otherLightData.shadowData);
    otherShadowData.lightPositionWS = lightPosition;
    otherShadowData.lightDirectionWS = light.direction;
    otherShadowData.spotDirectionWS = spotDirection;
    light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surface) *
        spotAttenuation * rangeAttenuation / distanceSqr; //点光源平方反比计算衰减
    return light;
}

#endif
