using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static PostFXStack;

public class PostFXPass
{
    private static ProfilingSampler _groupFXSampler = new ProfilingSampler("PostFXSampler");
    private static ProfilingSampler _finalFXSampler = new ProfilingSampler("Final PostFX Sampler");

    //双三次采样
    private int _copyBicubicID = UnityEngine.Shader.PropertyToID("_CopyBicubic");

    //FXAA
    private int _fxaaConfigID = UnityEngine.Shader.PropertyToID("_FXAAConfig");

    private static GlobalKeyword _fxaaQualityLowKeyword = GlobalKeyword.Create("FXAA_QUALITY_LOW");
    private static GlobalKeyword _fxaaQualityMediumKeyword = GlobalKeyword.Create("FXAA_QUALITY_MEDIUM");
    private static GlobalKeyword _fxaaAlphaContantsLumaKeyword = GlobalKeyword.Create("_FXAA_ALPHA_CONTANTS_LUMA_");

    private static readonly GraphicsFormat ColorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);

    private PostFXStack _postFXStack;
    private TextureHandle _colorHandle;

    private bool _keepAlpha;

    private enum ScaleMode
    {
        None,
        Linear,
        Bicubic,
    }

    private ScaleMode _scaleMode;
    private TextureHandle _colorSource;
    private TextureHandle _colorGradingResult;
    private TextureHandle _scaledResult;

    private void Render(RenderGraphContext context)
    {
        CommandBuffer fxBuffer = context.cmd;
        fxBuffer.SetGlobalFloat(FinalSrcBlendID, 1f);
        fxBuffer.SetGlobalFloat(FinalDstBlendID, 0f);

        RenderTargetIdentifier finalSource;

        FXPass finalPass;
        if (_postFXStack.CameraBufferSettings.fxaa.enable)
        {
            finalSource = _colorGradingResult;
            finalPass = _keepAlpha ? FXPass.FXAA : FXPass.FXAAWithLuma;
            ConfigureFXAA(fxBuffer);
            _postFXStack.Draw(fxBuffer, _colorSource, finalSource,
                _keepAlpha ? FXPass.ApplyColorGrading : FXPass.ApplyColorGradingWithAlpha);
        }
        else
        {
            finalSource = _colorSource;
            finalPass = FXPass.ApplyColorGrading;
        }

        if (_scaleMode == ScaleMode.None)
        {
            _postFXStack.DrawFinal(fxBuffer, finalSource, finalPass);
        }
        else
        {
            _postFXStack.Draw(fxBuffer, finalSource, _scaledResult, finalPass);
            fxBuffer.SetGlobalFloat(_copyBicubicID,
                _scaleMode == ScaleMode.Bicubic ? 1f : 0f);
            _postFXStack.DrawFinal(fxBuffer, _scaledResult, FXPass.FinalRescale);
        }

        context.renderContext.ExecuteCommandBuffer(fxBuffer);
        fxBuffer.Clear();
    }

    public static void Record(RenderGraph renderGraph, PostFXStack postFXStack,
        int colorLutResolution, bool keepAlpha,
        in CameraRendererTextures rendererTextures)
    {
        using var _ = new RenderGraphProfilingScope(renderGraph, _groupFXSampler);
        TextureHandle colorSource = BloomPass.Recode(renderGraph, postFXStack, rendererTextures);

        TextureHandle colorLUT = ColorLUTPass.Recode(renderGraph, postFXStack, colorLutResolution);

        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass(_finalFXSampler.name, out PostFXPass postFXPass, _finalFXSampler);

        postFXPass._postFXStack = postFXStack;
        postFXPass._keepAlpha = keepAlpha;
        postFXPass._colorSource = builder.ReadTexture(colorSource);

        builder.ReadTexture(colorLUT);

        if (postFXStack.BufferSize.x == postFXStack.Camera.pixelWidth)
        {
            postFXPass._scaleMode = ScaleMode.None;
        }
        else
        {
            postFXPass._scaleMode =
                postFXStack.CameraBufferSettings.bicubicRescaling ==
                CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                postFXStack.CameraBufferSettings.bicubicRescaling ==
                CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                postFXStack.BufferSize.x < postFXStack.Camera.pixelWidth
                    ? ScaleMode.Bicubic
                    : ScaleMode.Linear;
        }

        bool applyFXAA = postFXStack.CameraBufferSettings.fxaa.enable;
        if (applyFXAA || postFXPass._scaleMode != ScaleMode.None)
        {
            var desc = new TextureDesc(postFXStack.BufferSize.x, postFXStack.BufferSize.y)
            {
                colorFormat = ColorFormat,
            };
            if (applyFXAA)
            {
                desc.name = "Color Grading Result";
                postFXPass._colorGradingResult = builder.CreateTransientTexture(desc);
            }

            if (postFXPass._scaleMode != ScaleMode.None)
            {
                desc.name = "Scaled Result";
                postFXPass._scaledResult = builder.CreateTransientTexture(desc);
            }
        }

        builder.SetRenderFunc<PostFXPass>(
            static (pass, context) => pass.Render(context));
    }

    void ConfigureFXAA(CommandBuffer buffer)
    {
        CameraBufferSettings.FXAA fxaa = _postFXStack.CameraBufferSettings.fxaa;

        buffer.SetKeyword(_fxaaAlphaContantsLumaKeyword, _keepAlpha);

        buffer.SetKeyword(_fxaaQualityLowKeyword, fxaa.quality ==
                                                  CameraBufferSettings.FXAA.Quality.Low);
        buffer.SetKeyword(_fxaaQualityMediumKeyword, fxaa.quality ==
                                                     CameraBufferSettings.FXAA.Quality.Medium);
        buffer.SetGlobalVector(_fxaaConfigID, new Vector4(
            fxaa.fixedThreshold,
            fxaa.relativeThreshold,
            fxaa.subpixelBlending));
    }
}