#ifndef _CUSTOM_RENDER_META_PASS_INCLUDE_
#define _CUSTOM_RENDER_META_PASS_INCLUDE_

#include "Assets/CustomRenderPipeLine/ShaderLibrary/CustomSurface.hlsl"
#include "Assets/CustomRenderPipeLine/ShaderLibrary/CustomBRDF.hlsl"

CBUFFER_START(UnityMetaPass)
    bool4 unity_MetaFragmentControl;

CBUFFER_END

float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct MetaAttribute
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float2 lightMapUV : TEXCOORD1;
};

struct MetaVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 detailUV : TEXCOORD1;
};

MetaVaryings MetaVert(MetaAttribute input)
{
    MetaVaryings output = (MetaVaryings)0;

    input.positionOS.xy = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0;

    output.positionCS = TransformWorldToHClip(input.positionOS);

    output.uv = GetUV(input.uv);
    output.detailUV = GetDetailUV(output.uv);

    return output;
}

half4 MetaFrag(MetaVaryings input) : SV_Target
{
    half4 detailTex = GetDetail(input.detailUV);
    float4 maskTex = GetMask(input.uv);
    half4 col = GetColor(input.uv, detailTex, maskTex);
    #ifdef _ALPHATEST_ON
    clip(col.a - GetAlphaClip(input.uv));
    #endif

    float4 mask = GetMask(input.uv);
    Surface surface = (Surface)0;
    surface.color = col.rgb;
    surface.alpha = col.a;
    surface.metallic = GetMetallic(input.uv, mask);
    surface.smoothness = GetSmoothness(input.uv, mask, detailTex);
    //它会在给定屏幕空间XY坐标处生成一个旋转的平铺抖动图
    surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    //asuint将使用原始数据而非进行数字类型转换（这样做会改变位模式）
    surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

    BRDF brdf = GetBRDF(surface);
    float4 meta = 0;
    if (unity_MetaFragmentControl.x)
    {
        meta = half4(surface.color, 1);
        //高镜面但是粗糙的材质也可以传递一点间接光
        meta.rgb += brdf.specular * brdf.roughness * 0.5;
        //提高结果到unity_OneOverOutputBoost但是限制在unity_MaxOutputValue之下
        meta.rgb = min(PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue);
    }
    else if (unity_MetaFragmentControl.y)
    {
        //设置了y标志表示烘培自发光（必须在shaderGUI上手动调用LightmapEmissionProperty来手动配置）
        meta = float4(GetEmission(input.uv), 1);
    }

    return meta;
}
#endif
