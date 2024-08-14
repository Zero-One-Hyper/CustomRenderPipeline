#ifndef _CUSTOM_LIGHT_INCLUDE_
#define _CUSTOM_LIGHT_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Shadow/CustomShadows.hlsl"

#define MAX_DIRECTION_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

//创建uniform值 在buffer中存储光照信息
CBUFFER_START(_CustomLight)
    int _DirectionLightCount;
    float4 _DirectionLightColors[MAX_DIRECTION_LIGHT_COUNT];
    float4 _DirectionLightDirsAndMasks[MAX_DIRECTION_LIGHT_COUNT];

    int _OtherLightCount;
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightPosition[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];

    float4 _DirectionShadowData[MAX_DIRECTION_LIGHT_COUNT];
    float4 _OtherShadowData[MAX_OTHER_LIGHT_COUNT];
CBUFFER_END

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

DirectionalShadowDataSetting GetDirectionShadowDataSetting(int lightIndex, ShadowData shadowData)
{
    DirectionalShadowDataSetting data;
    data.strength = _DirectionShadowData[lightIndex].x * shadowData.strength; //多乘一个用来避免采样不存在的级联采样 同意设置为0
    data.tileIndex = _DirectionShadowData[lightIndex].y + shadowData.cascadeIndex;
    data.normalBias = _DirectionShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionShadowData[lightIndex].w;
    return data;
}

OtherShadowDataSetting GetOtherShadowDataSetting(int lightIndex)
{
    OtherShadowDataSetting otherShadowData;
    otherShadowData.strength = _OtherShadowData[lightIndex].x;
    otherShadowData.tileIndex = _OtherShadowData[lightIndex].y;
    otherShadowData.shadowMaskChannel = _OtherShadowData[lightIndex].w;
    otherShadowData.isPoint = _OtherShadowData[lightIndex].z == 1;
    otherShadowData.lightPositionWS = 0; //在外面填充
    otherShadowData.lightDirectionWS = 0;
    otherShadowData.spotDirectionWS = 0;
    return otherShadowData;
}

Light GetDirectionLight(int index, Surface surface, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionLightColors[index].xyz;
    light.direction = _DirectionLightDirsAndMasks[index].xyz;
    light.renderingLayerMask = asuint(_DirectionLightDirsAndMasks[index].w);

    DirectionalShadowDataSetting dirShadowData = GetDirectionShadowDataSetting(index, shadowData);
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surface);
    //light.attenuation = shadowData.cascadeIndex * 0.25;
    //light.attenuation = 1;
    return light;
}

Light GetOtherLight(int index, Surface surface, ShadowData shadowData)
{
    Light light;
    light.color = _OtherLightColors[index].xyz;
    float3 lightPosition = _OtherLightPosition[index].xyz;
    float3 ray = lightPosition - surface.position;
    light.direction = normalize(ray);
    float distanceSqr = max(dot(ray, ray), 0.00001);
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPosition[index].w)));

    float4 spotAngles = _OtherLightSpotAngles[index];
    float3 spotDirection = _OtherLightDirectionsAndMasks[index].xyz;
    light.renderingLayerMask = asuint(_OtherLightDirectionsAndMasks[index].w);
    float spotAttenuation = Square(saturate(dot(spotDirection, light.direction) *
        spotAngles.x + spotAngles.y));

    OtherShadowDataSetting otherShadowData = GetOtherShadowDataSetting(index);
    otherShadowData.lightPositionWS = lightPosition;
    otherShadowData.lightDirectionWS = light.direction;
    otherShadowData.spotDirectionWS = spotDirection;
    light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surface) *
        spotAttenuation * rangeAttenuation / distanceSqr; //点光源平方反比计算衰减
    return light;
}

#endif
