#ifndef _CUSTOM_RENDER_PIPELINE_COMMON_INCLUDE_
#define _CUSTOM_RENDER_PIPELINE_COMMON_INCLUDE_

//可以先定义宏再Include
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM

//UnityInstancing仅在定义SHADOWS_SHADOWMASK时才会自动获得ShadowMask的遮挡数据
//必须定义SHADOWS_SHADOWMASK 否则会打断实例化
#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
#define SHADOWS_SHADOWMASK
#endif


//include的先后顺序有要求，不能先定义的引用后定义的
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomUnityInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

bool IsOrthographicCamera()
{
    return unity_OrthoParmas.w;
}

//正交相机深度转换为线性
float OrthographicDepthBufferToLinear(float rawDepth)
{
    #if UNITY_REVERSED_Z
    rawDepth = 1.0 - rawDepth;
    #endif
    return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

//定义常用的线性和点钳位采样器
SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Fragment.hlsl"

void ClipLod(Fragment fragment, float fade)
{
    #ifdef LOD_FADE_CROSSFADE
    float dither = InterleavedGradientNoise(fragment.positionSS, 0);
    clip(fade + (fade < 0.0? dither : -dither));
    #endif
}

//空间变化方法使用了URP中的方法

float Square(float v)
{
    return v * v;
}

float DistanceSquared(float3 pA, float3 pB)
{
    return dot(pA - pB, pA - pB);
}

float3 DecodeNormal(float4 sample, float scale)
{
    #ifdef UNITY_NO_DXT5nm
    return UnpackNormalRGB(sample, scale);
    #else
    return UnpackNormalmapRGorAG(sample, scale);
    #endif
}

float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
    float3x3 tbn = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
    return TransformTangentToWorld(normalTS, tbn);
}

#endif
