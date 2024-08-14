#ifndef _CUSTOM_RENDER_UNLIT_INPUT_INCLUDE_
#define _CUSTOM_RENDER_UNLIT_INPUT_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
#ifdef _DISTORTION
TEXTURE2D(_DistortionNormal);
SAMPLER(sampler_DistortionNormal);
#endif

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _MainColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)

    //粒子 接近Fade
    #ifdef _NEARFADE
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance);
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange);
    #endif
    //粒子 软粒子
    #ifdef _SOFTPARTIClES
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance);
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange);
    #endif
    //粒子 热扭曲
    #ifdef _DISTORTION
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength);
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend);
    #endif

UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
    Fragment fragment;

    float2 baseUV;
    float4 color;
    #ifdef _FLIPBOOKBLENDING
    float3 flipBookUVB;
    bool flipBookBlending;
    #endif
    bool nearFade;
    bool softParticle;
    bool distortion;
};

InputConfig GetInputConfig(float2 baseUV, float4 positionCS)
{
    InputConfig config;
    config.fragment = GetFragment(positionCS);
    config.baseUV = baseUV;
    config.color = 1.0;

    #ifdef _FLIPBOOKBLENDING
    config.flipBookUVB = 0;;
    config.flipBookBlending = false;
    #endif
    config.nearFade = false;
    config.distortion = false;

    return config;
}

InputConfig GetInputConfig(float2 baseUV)
{
    InputConfig config = (InputConfig)0;
    config.baseUV = baseUV;
    config.color = 1.0;

    #ifdef _FLIPBOOKBLENDING
    config.flipBookUVB = 0;;
    config.flipBookBlending = false;
    #endif
    config.nearFade = false;
    config.softParticle = false;

    return config;
}

InputConfig GetInputConfig(float2 baseUV, float2 detailUV)
{
    return GetInputConfig(baseUV);
}

float2 GetUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_MainTex_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float2 GetDetailUV(float2 detailUV)
{
    return 0.0;
}

float4 GetMask(InputConfig c)
{
    return 1.0;
}

float4 GetMask(float2 uv)
{
    return 1.0;
}

float4 GetDetail(InputConfig c)
{
    return 0.0;
}

float4 GetDetail(float2 uv)
{
    return 0.0;
}

#ifdef _DISTORTION
float GetDistortionBlend()
{
    return INPUT_PROP(_DistortionBlend);
}

float2 GetDistortion(InputConfig config)
{
    float4 rawMap = SAMPLE_TEXTURE2D(_DistortionNormal, sampler_DistortionNormal, config.baseUV);
#ifdef _FLIPBOOKBLENDING
    //使用广告牌粒子时需要应用uv偏移
    if (config.flipBookBlending)
    {
        rawMap = lerp(rawMap, SAMPLE_TEXTURE2D(_DistortionNormal, sampler_DistortionNormal, config.flipBookUVB.xy),
                      config.flipBookUVB.z);
    }
#endif
    //广告牌只需要左右扭曲
    return DecodeNormal(rawMap, INPUT_PROP(_DistortionStrength)).xy;
}
#endif

float4 GetBase(InputConfig config)
{
    float4 mainMap = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, config.baseUV);
    #ifdef _FLIPBOOKBLENDING
    //if (config.flipBookBlending)
    {
        mainMap = lerp(mainMap, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, config.flipBookUVB.xy),
                       config.flipBookUVB.z);
    }
    #endif
    #ifdef _NEARFADE
    //if (config.nearFade)
    {
        float nearAttenuation = (config.fragment.depth - INPUT_PROP(_NearFadeDistance)) / INPUT_PROP(_NearFadeRange);
        mainMap.a *= saturate(nearAttenuation);
    }
    #endif

    #ifdef _SOFTPARTIClES
    //if (config.softParticle)
    {
        float depthDelta = config.fragment.bufferDepth - config.fragment.depth;
        float nearAttenuation = (depthDelta - INPUT_PROP(_SoftParticlesDistance)) / INPUT_PROP(_SoftParticlesRange);
        mainMap.a *= saturate(nearAttenuation);
    }

    #endif
    float4 mainColor = INPUT_PROP(_MainColor);
    return mainMap * mainColor * config.color;
}


float4 GetColor(float2 uv)
{
    InputConfig config = (InputConfig)0;
    config.baseUV = uv;
    return GetBase(config);
}

float4 GetColor(InputConfig config)
{
    return GetColor(config.baseUV);
}

float4 GetColor(float2 uv, float4 detailTex, float4 maskTex)
{
    return GetColor(uv);
}

float GetFinalAlpha(float alpha)
{
    return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

float3 GetNormalTS(InputConfig c)
{
    return float3(0.0, 0.0, 1.0);
}

float3 GetEmission(InputConfig c)
{
    return GetBase(c).rgb;
}

float3 GetEmission(float2 uv)
{
    return GetColor(uv).xyz;
}

float GetCutoff(InputConfig c)
{
    return INPUT_PROP(_Cutoff);
}

float GetCutoff(float2 uv)
{
    return INPUT_PROP(_Cutoff);
}

float GetMetallic(InputConfig c)
{
    return 0.0;
}

float GetMetallic(float2 uv)
{
    return 0.0;
}

float GetMetallic(float2 uv, float4 maskTex)
{
    return 0;
}

float GetSmoothness(InputConfig c)
{
    return 0.0;
}

float GetSmoothness(float2 uv)
{
    return 0.0;
}

float GetSmoothness(float2 uv, float4 maskTex, float4 detail)
{
    return GetSmoothness(uv);
}

float GetFresnel(InputConfig c)
{
    return 0.0;
}

float GetFresnel(float2 uv)
{
    return 0;
}

#endif
