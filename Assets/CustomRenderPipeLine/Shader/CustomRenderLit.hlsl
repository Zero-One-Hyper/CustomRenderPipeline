#ifndef _CUSTOM_RENDER_LIT_INCLUDE_
#define _CUSTOM_RENDER_LIT_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
#include "Assets/CustomRenderPipeLine/Shader/CustomRenderLitInput.hlsl"
#include "Assets/CustomRenderPipeLine/ShaderLibrary/CustomSurface.hlsl"
#include "Assets/CustomRenderPipeLine/ShaderLibrary/Lighting/CustomLight.hlsl"
#include "Assets/CustomRenderPipeLine/ShaderLibrary/CustomBRDF.hlsl"
#include "Assets/CustomRenderPipeLine/ShaderLibrary/Lighting/CustomLighting.hlsl"


struct LitAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float3 normalOS : NORMAL;
    #ifdef _NORMAL_MAP_ON
    float4 tangentOS : TANGENT;
    #endif

    //自定义的GI顶点着色器数据
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct LitVaryings
{
    float2 uv : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    float3 normalWS : NORMAL;
    #ifdef _NORMAL_MAP_ON
    float4 tangentWS : TANGENT;
    #endif
    float3 positionWS : TEXCOORD1;
    float2 detailUV : TEXCOORD3;

    //自定义的GI片元着色器数据
    GI_VARYINGS_DATA(2)
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

LitVaryings LitVert(LitAttributes input)
{
    LitVaryings output = (LitVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input, output)

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

    output.uv = GetUV(input.uv);
    //TRANSFORM_TEX无法处理gpu实例化时的_MainTex_ST
    //output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    #ifdef _NORMAL_MAP_ON
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS), input.tangentOS.w);
    #endif
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.detailUV = GetDetailUV(output.uv);

    return output;
}

half4 LitFrag(LitVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    //在AlphaClip前进行LOD的混合
    InputConfig inputConfig = GetInputConfig(input.uv);
    ClipLod(inputConfig.fragment, unity_LODFade.x);

    half4 detailTex = GetDetail(input.detailUV);
    float4 maskTex = GetMask(input.uv);
    half4 col = GetColor(input.uv, detailTex, maskTex);
    #ifdef _ALPHATEST_ON
    clip(col.a - GetAlphaClip(input.uv));
    #endif


    Surface surface;
    surface.position = input.positionWS;
    #ifdef _NORMAL_MAP_ON
    surface.normal = NormalTangentToWorld(GetNormalTS(input.uv), input.normalWS, input.tangentWS);
    #else
    surface.normal = normalize(input.normalWS);
    #endif
    surface.interpolatedNormal = input.normalWS;
    //将观察方向视为表面数据的一部分
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z; //视线空间的深度
    surface.color = col.rgb;
    surface.alpha = col.a;
    surface.metallic = GetMetallic(input.uv, maskTex);
    surface.smoothness = GetSmoothness(input.uv, maskTex, detailTex);
    surface.occlusion = GetOcclusion(input.uv, maskTex);
    //它会在给定屏幕空间XY坐标处生成一个旋转的平铺抖动图
    surface.dither = InterleavedGradientNoise(inputConfig.fragment.positionSS, 0);
    //asuint将使用原始数据而非进行数字类型转换（这样做会改变位模式）
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

    BRDF brdf = GetBRDF(surface);

    half4 res = half4(surface.color, surface.alpha);

    /*
    //只使用单一光源
    ShadowData shadowData = GetShadowData(surface);
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface);
    Light mainLight = GetDirectionMainLight(surface, gi);
    res.rgb = GetLighting(surface, brdf, mainLight);
    */
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    //所有平行光
    res.rgb = GetAllLighting(inputConfig.fragment, surface, brdf, gi);
    res.rgb += GetEmission(input.uv);
    res.a = GetFinalAlpha(surface.alpha);
    return res;
}
#endif
