#ifndef _CUSTOM_GI_INCLUDE_
#define _CUSTOM_GI_INCLUDE_

#if defined(LIGHTMAP_ON)
#define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;//光照贴图一般情况下都是1
#define GI_VARYINGS_DATA(index) float2 lightMapUV : TEXCOORD##index;
#define TRANSFER_GI_DATA(input, output) output.lightMapUV = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
#define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
#define GI_ATTRIBUTE_DATA
#define GI_VARYINGS_DATA(index) 
#define TRANSFER_GI_DATA(input, output)
#define GI_FRAGMENT_DATA(input) 0.0
#endif

#include "Assets/CustomRenderPipeLine/ShaderLibrary/CustomSurface.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

//GI负责采样光照贴图
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
//ShaodwMask
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

//光照探针代理LPPVs unity用texture3D存储
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);
//环境采样
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

struct GI
{
    float3 diffuse;
    float3 specular;
    ShadowMask shadowMask;
};

float3 SampleLightMap(float2 lightMapUV)
{
    #ifdef LIGHTMAP_ON
    //贴图SAMPER uv， 缩放， 是否压缩 包含解码指令的float4
    return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV, half4(1, 1, 0, 0),
    #ifdef UNITY_LIGHTMAP_FULL_HDR
                                false,
    #else
                                true,
    #endif
                                float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0, 0));
    #else
    return 0;
    #endif
}

float3 SampleLightProb(Surface surfaceWS)
{
    #ifdef LIGHTMAP_ON
    return 0;
    #else
    if (unity_ProbeVolumeParams.x)
    {
        //此处选择怎样的级数取决于光照烘培时光照探针采样次数的设置 
        return SampleProbeVolumeSH4(
            TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
            surfaceWS.position, surfaceWS.normal,
            unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz);
    }
    else
    {
        float4 coefficients[7];
        coefficients[0] = unity_SHAr;
        coefficients[1] = unity_SHAg;
        coefficients[2] = unity_SHAb;
        coefficients[3] = unity_SHBr;
        coefficients[4] = unity_SHBg;
        coefficients[5] = unity_SHBb;
        coefficients[6] = unity_SHC;
        return max(0, SampleSH9(coefficients, surfaceWS.normal));
    }
    #endif
}

float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS)
{
    #ifdef LIGHTMAP_ON
    return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
    #else
    if (unity_ProbeVolumeParams.x)
    {
        //阴影遮罩的LPPVs和检索光照的LPPVs基本相同，但是参数斌不需要法线
        return SampleProbeOcclusion(
            TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
            surfaceWS.position,
            unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz);
    }
    else
    {
        return unity_ProbesOcclusion;
    }
    #endif
}

float3 SampleEnvironment(Surface surfaceWS, BRDF brdf)
{
    //由反射方向采样cube
    float3 uvw = reflect(-surfaceWS.viewDirection, surfaceWS.normal);
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, uvw, mip);
    environment.rgb = DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
    return environment.rgb;
}

GI GetGI(float2 lightMapUV, Surface surfaceWS, BRDF brdf)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProb(surfaceWS);
    gi.specular = SampleEnvironment(surfaceWS, brdf);
    gi.shadowMask.distance = false;
    gi.shadowMask.always = false;
    gi.shadowMask.shadows = 1;

    #if defined(_SHADOW_MASK_ALWAYS)
    gi.shadowMask.always = true;
    gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #elif defined(_SHADOW_MASK_DISTANCE)
    gi.shadowMask.distance = true;
    gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #endif

    return gi;
}
#endif
