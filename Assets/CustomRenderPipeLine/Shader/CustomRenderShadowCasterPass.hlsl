#ifndef _CUSTOM_RENDER_SHADOWCASTER_PASS_INCLUDE_
#define _CUSTOM_RENDER_SHADOWCASTER_PASS_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"

struct ShadowCasterAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ShadowCasterVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

bool _ShadowPancaking;

ShadowCasterVaryings ShadowCasterVert(ShadowCasterAttributes input)
{
    ShadowCasterVaryings output = (ShadowCasterVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

    if (_ShadowPancaking) //避免在不必要的地方clamp
    {
        //解决阴影平坠问题
        #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #else
        output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #endif
    }

    output.uv = GetUV(input.uv);

    return output;
}

half4 ShadowCasterFrag(ShadowCasterVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

    InputConfig inputConfig = GetInputConfig(input.uv, input.positionCS);
    ClipLod(inputConfig.fragment, unity_LODFade.x);

    half4 col = GetColor(GetInputConfig(input.uv, input.positionCS));
    #ifdef _ALPHATEST_ON
    #ifdef _CASCADE_BLEND_DITHER
    float dither = InterleavedGradientNoise(inputConfig.fragment.positionSS, 0);
    clip(dither - col.a);
    #else
    clip(col.a - GetAlphaClip(input.uv));
    #endif
    #endif
    return 0;
}
#endif
