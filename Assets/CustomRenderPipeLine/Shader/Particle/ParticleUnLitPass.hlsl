#ifndef _CUSTOM_RENDER_PARTICLE_UNLIT_INCLUDE_
#define _CUSTOM_RENDER_PARTICLE_UNLIT_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
#include "Assets/CustomRenderPipeLine/Shader/CustomRenderUnLitInput.hlsl"

struct Attribute
{
    float4 positionOS : POSITION;
    float4 color : COLOR;

    #ifdef _FLIPBOOKBLENDING
    float4 uv : TEXCOORD0; //flipbook粒子效果，通过texcoord0提供两个uv对
    float flipBookBlend : TEXCOORD1;
    #else
    float2 uv : TEXCOORD0;
    #endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varying
{
    float2 uv : TEXCOORD0;
    float4 positionCS_SS : SV_POSITION;
    #ifdef _VERTEXCOLORS
    float4 color : COLOR;
    #endif

    #ifdef _FLIPBOOKBLENDING
    float3 flipBookUVB : VAR_FLIPBOOK;
    #endif

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varying UnLitVert(Attribute input)
{
    Varying output = (Varying)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    //float3 positionWS = TransformObjectToWorld(input.positionOS);
    //顶点着色器中是顶点的clip-Space 片源着色器中是片源的screen-space(window-space)
    output.positionCS_SS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv.xy = GetUV(input.uv.xy);

    #ifdef _FLIPBOOKBLENDING
    output.flipBookUVB.xy = GetUV(input.uv.zw);
    output.flipBookUVB.z = input.flipBookBlend;
    #endif

    //TRANSFORM_TEX无法处理gpu实例化时的_MainTex_ST
    //output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    #ifdef _VERTEXCOLORS
    output.color = input.color;
    #endif
    
    return output;
}

half4 UnLitFrag(Varying input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

    InputConfig config = GetInputConfig(input.uv, input.positionCS_SS);

    #ifdef _VERTEXCOLORS
    config.color = input.color;
    #endif

    #ifdef _FLIPBOOKBLENDING
    config.flipBookUVB = input.flipBookUVB;
    config.flipBookBlending = true;
    #endif

    #ifdef _NEARFADE
    config.nearFade = true;
    #endif

    #ifdef _SOFTPARTIClES
    config.softParticle = true;
    #endif

    float4 base = GetBase(config);
    #if defined(_ALPHATEST)
    clip(base.a - GetCutoff(config));
    #endif

    #ifdef _DISTORTION
    float2 distortion = GetDistortion(config) * base.a; //扭曲效果应和粒子视觉强度(即透明度)相关，所以乘alpha
    base.rgb = lerp(GetBufferColor(config.fragment, distortion).rgb, //只保留RGB来隐藏硬边
                    base.rgb, GetDistortionBlend());
    #endif

    return float4(base.rgb, GetFinalAlpha(base.a));
}
#endif
