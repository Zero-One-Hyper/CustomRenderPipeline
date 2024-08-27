using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static PostFXStack;
using static PostFXSettings;

public class ColorLUTPass
{
    private static readonly ProfilingSampler Sampler = new ProfilingSampler("Color LUT");

    //LUT
    private static readonly int ColorGradingLUTID = UnityEngine.Shader.PropertyToID("_ColorGradingLUT");

    private static readonly int ColorGradingLUTParameters =
        UnityEngine.Shader.PropertyToID("_ColorGradingLUTParameters");

    private static readonly int ColorGradingLUTLogCID = UnityEngine.Shader.PropertyToID("_ColorGradingLUTLogC");

    //色彩调整
    private static readonly int ColorAdjustmentsID = UnityEngine.Shader.PropertyToID("_ColorAdjustments");

    private static readonly int ColorFilterID = UnityEngine.Shader.PropertyToID("_ColorFilter");

    //白平衡
    private static readonly int WhiteBalanceID = UnityEngine.Shader.PropertyToID("_WhiteBalance");

    //分离色调
    private static readonly int SplitToningShadowsID = UnityEngine.Shader.PropertyToID("_SplitToningShadows");
    private static readonly int SplitToningHighlightID = UnityEngine.Shader.PropertyToID("_SplitToningHighlight");

    //通道混合
    private static readonly int ChannelMixerRedID = UnityEngine.Shader.PropertyToID("_ChannelMixerRed");
    private static readonly int ChannelMixerGreenID = UnityEngine.Shader.PropertyToID("_ChannelMixerGreen");
    private static readonly int ChannelMixerBlueID = UnityEngine.Shader.PropertyToID("_ChannelMixerBlue");

    //ShadowMidHigh
    private static readonly int SmhShadowsID = UnityEngine.Shader.PropertyToID("_SMHShadows");
    private static readonly int SmhMidtonesID = UnityEngine.Shader.PropertyToID("_SMHMidtones");
    private static readonly int SmhHighlightsID = UnityEngine.Shader.PropertyToID("_SMHHighlights");
    private static readonly int SmhRangeID = UnityEngine.Shader.PropertyToID("_SMHRange");

    private static readonly GraphicsFormat ColorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);

    private PostFXStack _postFXStack;

    private int _colorLUTResolution;

    private TextureHandle _colorLUT;

    private void ConfigureColorAdjustments(CommandBuffer buffer, PostFXSettings postFXSettings)
    {
        PostFXSettings.ColorAdjustmentsSettings colorAdjustmentsSettings =
            postFXSettings.ColorAdjustmentsSetting;
        buffer.SetGlobalVector(ColorAdjustmentsID, new Vector4(
            Mathf.Pow(2.0f, colorAdjustmentsSettings.postExposure),
            colorAdjustmentsSettings.contrast * 0.01f + 1f,
            colorAdjustmentsSettings.hueShift * (1f / 360f),
            colorAdjustmentsSettings.saturation * 0.01f + 1f)
        );
        buffer.SetGlobalColor(ColorFilterID, colorAdjustmentsSettings.colorFilter);
    }

    private void ConfigureWhiteBalance(CommandBuffer buffer, PostFXSettings postFXSettings)
    {
        PostFXSettings.WhiteBalanceSettings whiteBalanceSettings = postFXSettings.WhiteBalanceSetting;
        buffer.SetGlobalVector(WhiteBalanceID,
            ColorUtils.ColorBalanceToLMSCoeffs(whiteBalanceSettings.temperature, whiteBalanceSettings.tint));
    }

    private void ConfigureSplitToning(CommandBuffer buffer, PostFXSettings postFXSettings)
    {
        PostFXSettings.SplitToningSettings toningSettings = postFXSettings.SplitToningSetting;
        Color splitColor = toningSettings.shadows;
        splitColor.a = toningSettings.balance * 0.01f;
        buffer.SetGlobalColor(SplitToningShadowsID, splitColor);
        buffer.SetGlobalColor(SplitToningHighlightID, toningSettings.highlights);
    }

    private void ConfigureChannelMix(CommandBuffer buffer, PostFXSettings postFXSettings)
    {
        PostFXSettings.ChannelMixerSettings channelMixerSettings = postFXSettings.ChannelMixerSetting;
        buffer.SetGlobalVector(ChannelMixerRedID, channelMixerSettings.red);
        buffer.SetGlobalVector(ChannelMixerGreenID, channelMixerSettings.green);
        buffer.SetGlobalVector(ChannelMixerBlueID, channelMixerSettings.blue);
    }

    private void ConfigureShadowsMidtonesHighLights(CommandBuffer buffer, PostFXSettings postFXSettings)
    {
        PostFXSettings.ShadowsMidtonesHighLightsSettings setting = postFXSettings.ShadowsMidtonesHighLightsSetting;
        buffer.SetGlobalColor(SmhShadowsID, setting.shadow.linear);
        buffer.SetGlobalColor(SmhMidtonesID, setting.midTone.linear);
        buffer.SetGlobalColor(SmhHighlightsID, setting.highLights.linear);
        buffer.SetGlobalVector(SmhRangeID, new Vector4(
            setting.shadowsStart, setting.shadowsEnd, setting.highLightsStart, setting.highLightsEnd));
    }

    private void Render(RenderGraphContext context)
    {
        PostFXSettings postFXSettings = _postFXStack.PostFXSettings;
        CommandBuffer commandBuffer = context.cmd;

        ConfigureColorAdjustments(commandBuffer, postFXSettings);
        ConfigureWhiteBalance(commandBuffer, postFXSettings);
        ConfigureSplitToning(commandBuffer, postFXSettings);
        ConfigureChannelMix(commandBuffer, postFXSettings);
        ConfigureShadowsMidtonesHighLights(commandBuffer, postFXSettings);

        //LUT
        //可以只渲染一次LUT图然后缓存下来，但是判断是否刷新会比较复杂，而且只做颜色分级和色调渲染消耗比渲染整个屏幕的画面要小
        int lutHeight = _colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        //LUT图本应是3D的，但常规着色器无法渲染3D纹理，将3DLUT图切片连续放置
        commandBuffer.SetGlobalVector(ColorGradingLUTParameters, new Vector4(
            lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)));

        PostFXSettings.ToneMappingSettings.ToneMappingMode mappingMode =
            postFXSettings.ToneMappingSetting.toneMappingMode;
        FXPass pass = FXPass.ToneMappingNone + (int)mappingMode;

        commandBuffer.SetGlobalFloat(ColorGradingLUTLogCID,
            _postFXStack.CameraBufferSettings.allowHDR && pass !=
            FXPass.ToneMappingNone
                ? 1f
                : 0f);

        _postFXStack.Draw(commandBuffer, _colorLUT, pass); //把颜色分级和色调映射都渲染到LUT上

        commandBuffer.SetGlobalVector(ColorGradingLUTParameters, new Vector4(
            1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1.0f));
        commandBuffer.SetGlobalTexture(ColorGradingLUTID, _colorLUT);
    }

    public static TextureHandle Recode(RenderGraph renderGraph, PostFXStack postFXStack, int colorLutResolution)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            Sampler.name, out ColorLUTPass colorLutPass, Sampler);
        colorLutPass._postFXStack = postFXStack;
        colorLutPass._colorLUTResolution = colorLutResolution;
        int lutHeight = colorLutResolution;
        int lutWidth = lutHeight * lutHeight;
        var desc = new TextureDesc(lutWidth, lutHeight)
        {
            colorFormat = ColorFormat,
            name = "Color LUT",
        };
        colorLutPass._colorLUT = builder.WriteTexture(renderGraph.CreateTexture(desc));
        builder.SetRenderFunc<ColorLUTPass>(
            static (pass, context) => pass.Render(context));
        return colorLutPass._colorLUT;
    }
}