#ifndef _CUSTOM_RENDER_UNLIT_INCLUDE_
#define _CUSTOM_RENDER_UNLIT_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
#include "Assets/CustomRenderPipeLine/Shader/CustomRenderUnLitInput.hlsl"

struct Attribute
{
    float4 positionOS : POSITION;
    float4 color : COLOR;

    float2 uv : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varying
{
    float2 uv : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    #ifdef _VERTEXCOLORS
    float4 color : COLOR;
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
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv.xy = GetUV(input.uv.xy);


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

    InputConfig config = GetInputConfig(input.uv);

    #ifdef _VERTEXCOLORS
    config.color = input.color;
    #endif

    float4 base = GetBase(config);
    #if defined(_CLIPPING)
    clip(base.a - GetCutoff(config));
    #endif
    return float4(base.rgb, GetFinalAlpha(base.a));
}
#endif
