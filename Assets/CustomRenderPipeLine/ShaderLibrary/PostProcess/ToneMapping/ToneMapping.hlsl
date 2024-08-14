#ifndef _TONE_MAPPING_PASS_INCLUDE_
#define _TONE_MAPPING_PASS_INCLUDE_

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

float4 _ColorAdjustments;
float4 _ColorFilter;

float4 _WhiteBalance;

//分离色调
float4 _SplitToningShadows;
float4 _SplitToningHighlight;

//通道混合
float4 _ChannelMixerRed;
float4 _ChannelMixerGreen;
float4 _ChannelMixerBlue;

//ShadowsMidHigh
float4 _SMHShadows;
float4 _SMHMidtones;
float4 _SMHHighlights;
float4 _SMHRange;

//LUT
TEXTURE2D(_ColorGradingLUT);
float4 _ColorGradingLUTParameters;
bool _ColorGradingLUTLogC;

float Luminance(float3 color, bool useACES)
{
    return useACES ? AcesLuminance(color) : Luminance(color);
}

//曝光(始终应用于线性空间)
float3 ColorGradePostExposure(float3 color)
{
    return color * _ColorAdjustments.x;
}

//对比度
float3 ColorGradeContrast(float3 color, bool useACES)
{
    //在ACES色彩空间变换会有更好的视觉效果
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color); //为了效果好看不在线性空间中做这个
    //ACEScc是ACES颜色空间的对数子集。中间灰度值为0.4135884。
    color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

//颜色滤镜
float3 ColorGradeColorFilter(float3 color)
{
    return color * _ColorFilter.rgb;
}

//色相偏移
float3 ColorGradeHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjustments.z;
    color.x = RotateHue(hue, 0.0, 1.0); //色相在0-1之间定义，使用rotateHue截断在0-1
    return HsvToRgb(color);
}

//饱和度
float3 ColorGradeSaturation(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES); //求亮度
    return (color - luminance) * _ColorAdjustments.w + luminance;
}

//白平衡(始终应用于线性空间)
float3 ColorGradeWhiteBalance(float3 color)
{
    //LMS由人眼的三种锥体细胞接收的长波长中波长短波长响应度峰值 命名
    color = LinearToLMS(color); //LMS颜色空间中的矢量来应用白平衡
    color *= _WhiteBalance.xyz;
    return LMSToLinear(color);
}

//分离色调
float3 ColorGradeSplitToning(float3 color, bool useACES)
{
    //在近似的Gamma空间中执行分色处理
    color = PositivePow(color, 1.0 / 2.0);
    float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
    //0-0.5和0.5-1 限制颜色在各自的区域
    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
    float3 highLight = lerp(0.5, _SplitToningHighlight.rgb, t);
    color = SoftLight(color, shadows); //柔光混合
    color = SoftLight(color, highLight);
    return PositivePow(color, 2.0);
}

//通道混合
float3 ColorGradeChannelMixer(float3 color)
{
    return mul(float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb), color);
}

//ShadowMidHigh
float3 ColorGradeShadowsMidtonesHighlight(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    float shadowWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float highlightWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtonesWeight = 1.0 - shadowWeight - highlightWeight;
    return
        color * _SMHShadows.xyz * shadowWeight +
        color * _SMHMidtones.xyz * midtonesWeight +
        color * _SMHHighlights.xyz * highlightWeight;
}

float3 ColorGrade(float3 color, bool useACES = false)
{
    //color = min(color, 60);//改为LUT后不再依赖图像，因此开始时不需要限制
    color = ColorGradePostExposure(color);
    color = ColorGradeWhiteBalance(color);
    color = ColorGradeContrast(color, useACES);
    color = ColorGradeColorFilter(color);
    color = max(color, 0); //对比度增加会导致颜色分量变暗 需要消除负值
    color = ColorGradeSplitToning(color, useACES);
    color = ColorGradeChannelMixer(color); //通道混合可能出现负值
    color = max(color, 0);
    color = ColorGradeShadowsMidtonesHighlight(color, useACES);
    color = ColorGradeHueShift(color);
    color = ColorGradeSaturation(color, useACES);
    return max(useACES ? ACEScg_to_ACES(color) : color, 0);
}

//LUT
float3 GetColorGradeLUT(float2 uv, bool useACES = false)
{
    float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
    return ColorGrade(_ColorGradingLUTLogC ? LogCToLinear(color) : color, useACES);
}

float3 ApplyColorGradingLUT(float3 color)
{
    //LUT图本应是3D的，但常规着色器无法渲染3D纹理，使用ApplyLu2D重新解释回3D
    return ApplyLut2D(TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
                      saturate(_ColorGradingLUTLogC ? LinearToLogC(color) : color),
                      _ColorGradingLUTParameters.xyz);
}

float4 ToneMappingNoneFragment(Varyings input) : SV_Target
{
    float3 color = GetColorGradeLUT(input.screenUV);
    return float4(color.rgb, 1);
}

float4 ToneMappingACESFragment(Varyings input) : SV_Target
{
    float3 color = GetColorGradeLUT(input.screenUV, true); //这里变为了在ACES空间下
    color = AcesTonemap(color.rgb);
    return float4(color, 1.0);
}

float4 ToneMappingNeutralFragment(Varyings input) : SV_Target
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color = NeutralTonemap(color.rgb);
    return float4(color, 1.0);
}

float4 ToneMappingReinhardFragment(Varyings input) : SV_Target
{
    float3 color = GetColorGradeLUT(input.screenUV);
    color /= 1.0 + color.rgb;
    return float4(color, 1.0);
}

float4 ApplyColorGradingPassFragment(Varyings input) : SV_Target
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    return color;
}

bool _CopyBicubic;

float4 FinalCopyScalePassFragment(Varyings input) : SV_Target
{
    if (_CopyBicubic)
    {
        return GetSourceBicubic(input.screenUV);
    }
    else
    {
        return GetSource(input.screenUV);
    }
}

float4 ApplyColorGradingWithLumaPassFragment(Varyings input) : SV_Target
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    color.a = sqrt(Luminance(color.rgb));
    return color;
}

#endif
