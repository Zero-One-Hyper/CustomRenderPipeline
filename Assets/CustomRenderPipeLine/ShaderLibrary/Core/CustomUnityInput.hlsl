#ifndef _CUSTOM_UNITY_INPUT_INCLUDE_
#define _CUSTOM_UNITY_INPUT_INCLUDE_

//矩阵名必须是unity自己定义的名子，不能改变（也不是一定不能改，也可以在外面自己传）
//buffer中的矩阵都是针对每个物体都有一个属于自己的值
CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    //LOD
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;
    float4 unity_RenderingLayer; //渲染层掩码

    real4 unity_LightData;
    real4 unity_LightIndices[2]; //两个向量的每个通道都包含一个光索引 因此每个对象最多支持8个

    //unity光照贴图UV
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;
    //Unity还将阴影遮罩数据烘焙到光探针中，我们将其称为遮挡探针（Occlusion Probes）
    float4 unity_ProbesOcclusion;
    //unity光照探针
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;
    //unity LPPVs light Prob Proxy Volume 探针代理(很长的物体受到光照探针的影响)
    float4 unity_ProbeVolumeParams;
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;
    //反射探针
    float4 unity_SpecCube0_HDR;
CBUFFER_END

//buffer外的矩阵则都是全局的统一的值
float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;

float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;

float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

float4 unity_OrthoParmas;
float4 _ProjectionParams;
float4 _ScreenParams;
float4 _ZBufferParams;


#endif
