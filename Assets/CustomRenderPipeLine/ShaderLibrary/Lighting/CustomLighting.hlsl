#ifndef _CUSTOM_LIGHTING_INCLUDE_
#define _CUSTOM_LIGHTING_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Lighting/CustomLight.hlsl"
#include "Assets/CustomRenderPipeLine/ShaderLibrary/Lighting/CustomGI.hlsl"

//计算给定表面和光源有多少入射光
float3 IncommingLight(Surface surface, Light light)
{
    //光源的lambert
    return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, Light light)
{
    return IncommingLight(surfaceWS, light) * DirectBRDF(surfaceWS, brdf, light);
}

Light GetDirectionMainLight(Surface surface, GI gi)
{
    ShadowData shadowData = GetShadowData(surface);
    shadowData.shadowMask = gi.shadowMask;
    return GetDirectionLight(0, surface, shadowData);
}

float3 GetAllLighting(Fragment fragment, Surface surface, BRDF brdf, GI gi)
{
    ShadowData shadowData = GetShadowData(surface);
    shadowData.shadowMask = gi.shadowMask;

    float3 color = IndirectBRDF(surface, brdf, gi.diffuse, gi.specular);
    //gi已经在IndirectBRDF中加过

    for (int i = 0; i < GetDirectionLightCount(); i++)
    {
        Light light = GetDirectionLight(i, surface, shadowData);
        if (RenderingLayersOverlap(surface, light))
        {
            color += GetLighting(surface, brdf, light);
        }
    }
    #ifdef _LIGHTS_PER_OBJECT
    for (int j = 0; j < min(unity_LightData.y, 8); j++)//这里做一个数量限制 提供的的灯光计数可能超过8
    {
        int lightIndex = unity_LightIndices[j / 4][j % 4];
        Light light = GetOtherLight(lightIndex, surface, shadowData);
        if (RenderingLayersOverlap(surface, light))
        {            
            color += GetLighting(surface, brdf, light);
        }
    }
    #else
    ForwardPlueTile tile = GetForwardPlusTile(fragment.screenUV);
    int lastLightIndex = tile.GetLastLightIndexInTile();
    //for (int j = 0; j < GetOtherLightCount(); j++)
    for (int j = tile.GetFirstLightIndexInTile(); j <= lastLightIndex; j++)
    {
        Light light = GetOtherLight(tile.GetLightIndex(j), surface, shadowData);
        if (RenderingLayersOverlap(surface, light))
        {
            color += GetLighting(surface, brdf, light);
        }
    }
    #endif
    return color;
}

#endif
