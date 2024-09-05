#ifndef _CUSTOM_RENDER_LIT_INPUT_INCLUDE_
#define _CUSTOM_RENDER_LIT_INPUT_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"

//UnityInstancing中对UNITY_INSTANCING_BUFFER_START等已经做了宏定义
//在不使用实例化时以下代码等同于CBUFFER
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float, _CutOff)
    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
    UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _MainColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
    UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailNormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)

UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

TEXTURE2D(_MaskTex);
SAMPLER(sampler_MaskTex);

TEXTURE2D(_NormalTex);
SAMPLER(sampler_NormalTex);

TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);

TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailNormalMap);

struct InputConfig
{
    Fragment fragment;
    float2 baseUV;
    float2 detailUV;
};

/*
InputConfig GetInputConfig(float2 baseUV)
{
    InputConfig config = (InputConfig)0;
    config.baseUV = baseUV;
    return config;
}
*/

InputConfig GetInputConfig(float2 baseUV, float4 positionCS, float2 detailUV = 0)
{
    InputConfig config;
    config.fragment = GetFragment(positionCS);
    config.baseUV = baseUV;
    config.detailUV = detailUV;
    return config;
}

float GetFinalAlpha(float alpha)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _ZWrite) ? 1.0 : alpha;
}

float2 GetUV(float2 uv)
{
    float4 map_ST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainTex_ST);
    float2 uvRes = uv * map_ST.xy + map_ST.zw;
    return uvRes;
}

float2 GetDetailUV(float2 uv)
{
    float4 mapST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailMap_ST);
    return uv * mapST.xy + mapST.zw;
}

//ANySNx格式，R中存储反照率调制，B中存储平滑度调制，AG中存储细节法向矢量的XY分量。
float4 GetDetail(float2 uv)
{
    float4 detailTex = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, uv);
    //detail是中性的，较高的应该增大，较小的应该减小
    return detailTex * 2 - 1; //从0-1 映射到 -1-1
}

float4 GetColor(InputConfig config)
{
    float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, config.baseUV);
    //detail只有r通道会影响反照率
    float detail = GetDetail(config.detailUV).r;
    //lerp中会进行gamma矫正
    mainTex.rgb = lerp(sqrt(mainTex.rgb), detail <= 0 ? 0 : 1, abs(detail));
    mainTex.rgb *= mainTex.rgb;
    float4 color = mainTex;

    float4 mainColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainColor);
    color *= mainColor;
    return color;
}

float4 GetColor(float2 uv, float4 detailTex, float4 maskTex)
{
    float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    //detail只有r通道会影响反照率
    float detail = detailTex.r * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailAlbedo);
    float mask = maskTex.b; //金属度

    //lerp中会进行gamma矫正
    mainTex.rgb = lerp(sqrt(mainTex.rgb), detail < 0 ? 0 : 1, abs(detail) * mask);
    mainTex.rgb *= mainTex.rgb;
    float4 color = mainTex;


    float4 mainColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainColor);
    color *= mainColor;
    return color;
}

float GetAlphaClip(float2 uv)
{
    float cutOff = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _CutOff);
    return cutOff;
}

float4 GetMask(float2 uv)
{
    float4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);
    return mask;
}

float GetMetallic(float2 uv, float4 maskTex)
{
    float metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
    return metallic * maskTex.r;
}

float GetSmoothness(float2 uv, float4 maskTex)
{
    float smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
    return smoothness * maskTex.a;
}

float GetSmoothness(float2 uv, float4 maskTex, float4 detail)
{
    float smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
    smoothness *= maskTex.a;

    float detailSmoothness = detail.b * UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _DetailSmoothness);
    smoothness = lerp(smoothness, detailSmoothness < 0 ? 0 : 1, abs(detailSmoothness) * maskTex.b);
    return smoothness;
}

float3 GetNormalTS(float2 uv)
{
    float4 normalTS = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv);
    float normalScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalScale);
    float3 normal = DecodeNormal(normalTS, normalScale);
    return normal;
}

float3 GetNormalTS(float2 uv, float2 detailUV, float4 mask)
{
    float4 normalTS = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv);
    float normalScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalScale);
    float3 normal = DecodeNormal(normalTS, normalScale);

    normalTS = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv);
    normalScale = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _NormalScale) * mask.b;
    float3 detailNormal = DecodeNormal(normalTS, normalScale);
    normal = BlendNormalRNM(normal, detailNormal); //来自CommonMaterial中的方法根据基础法线旋转细节法线
    return normal;
}

float GetOcclusion(float2 uv, float4 maskTex)
{
    float occlusion = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Occlusion);
    return lerp(maskTex.g, 1, occlusion);
}

float3 GetEmission(float2 uv)
{
    float3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
    float3 emissionColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor).rgb;
    return emissionTex * emissionColor;
}

#endif
