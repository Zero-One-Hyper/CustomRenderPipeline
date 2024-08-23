using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    //用于寻找pass
    private enum FXPass
    {
        Copy,
        BloomHorizontal,
        BloomVertical,
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        BloomPrefilter,
        BloomPrefilterFireflies,
        ToneMappingNone,
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard,
        ApplyColorGrading,
        ApplyColorGradingWithAlpha,
        FinalRescale,
        FXAA,
        FXAAWithLuma,
    }

    public PostFXStack()
    {
        _bloomPyramidID = UnityEngine.Shader.PropertyToID("_BloomPyramid0");
        for (int i = 0; i < MaxBloomPyramidLevels * 2.0f; i++)
        {
            UnityEngine.Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    //private const string BufferName = "Post FX";

    /*
    private CommandBuffer _fXBuffer = new CommandBuffer()
    {
        name = BufferName,
    };
    */
    private CommandBuffer _fXBuffer;

    private const int MaxBloomPyramidLevels = 16;

    private int _bloomBicubicUpsamplingID = UnityEngine.Shader.PropertyToID("_BloomBicubicUpsampling");
    private int _bloomPrefilterID = UnityEngine.Shader.PropertyToID("_BloomPrefilter");
    private int _bloomThresholdID = UnityEngine.Shader.PropertyToID("_BloomThreshold");
    private int _bloomIntensityID = UnityEngine.Shader.PropertyToID("_BloomIntensity");

    private int _bloomResultID = UnityEngine.Shader.PropertyToID("BloomResult");

    //色彩调整
    private int _colorAdjustmentsID = UnityEngine.Shader.PropertyToID("_ColorAdjustments");

    private int _colorFilterID = UnityEngine.Shader.PropertyToID("_ColorFilter");

    //白平衡
    private int _whiteBalanceID = UnityEngine.Shader.PropertyToID("_WhiteBalance");

    //分离色调
    private int _splitToningShadowsID = UnityEngine.Shader.PropertyToID("_SplitToningShadows");
    private int _splitToningHighlightID = UnityEngine.Shader.PropertyToID("_SplitToningHighlight");

    //通道混合
    private int _channelMixerRedID = UnityEngine.Shader.PropertyToID("_ChannelMixerRed");
    private int _channelMixerGreenID = UnityEngine.Shader.PropertyToID("_ChannelMixerGreen");
    private int _channelMixerBlueID = UnityEngine.Shader.PropertyToID("_ChannelMixerBlue");

    //ShadowMidHigh
    private int _smhShadowsID = UnityEngine.Shader.PropertyToID("_SMHShadows");
    private int _smhMidtonesID = UnityEngine.Shader.PropertyToID("_SMHMidtones");
    private int _smhHighlightsID = UnityEngine.Shader.PropertyToID("_SMHHighlights");
    private int _smhRangeID = UnityEngine.Shader.PropertyToID("_SMHRange");

    //LUT
    private int _colorGradingLUTID = UnityEngine.Shader.PropertyToID("_ColorGradingLUT");
    private int _colorGradingLUTParameters = UnityEngine.Shader.PropertyToID("_ColorGradingLUTParameters");
    private int _colorGradingLUTLogCID = UnityEngine.Shader.PropertyToID("_ColorGradingLUTLogC");

    private int _fxSourceID = UnityEngine.Shader.PropertyToID("_PostFXSource");
    private int _fxSource2ID = UnityEngine.Shader.PropertyToID("_PostFXSource2");

    private int _colorGradingResultID = UnityEngine.Shader.PropertyToID("_ColorGradingResult");
    private int _finalResultID = UnityEngine.Shader.PropertyToID("_FinalResult");

    //最终混合模式
    private int _finalSrcBlendID = UnityEngine.Shader.PropertyToID("_FinalSrcBlend");
    private int _finalDstBlendID = UnityEngine.Shader.PropertyToID("_FinalDstBlend");

    //双三次采样
    private int _copyBicubicID = UnityEngine.Shader.PropertyToID("_CopyBicubic");

    //FXAA
    private int _fxaaConfigID = UnityEngine.Shader.PropertyToID("_FXAAConfig");

    private static string _fxaaQualityLowKeyword = "FXAA_QUALITY_LOW";
    private static string _fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";
    private static string _fxaaAlphaContantsLumaKeyword = "_FXAA_ALPHA_CONTANTS_LUMA_";

    //private ScriptableRenderContext _context;
    private Camera _camera;
    private PostFXSettings _postFXSettings;
    private CameraSettings.FinalBlendMode _finalBlendMode;
    private CameraBufferSettings.BicubicRescalingMode _bicubicScaling;
    private CameraBufferSettings.FXAA _fxaa; //FXAA属于后处理

    private int _bloomPyramidID;
    private Vector4 _threshold;

    private bool _useHDR;
    private bool _keepAlpha;
    private int _colorLUTResolution;
    private Vector2Int _bufferSize;
    public bool IsActive => _postFXSettings != null;

    public void SetUp(Camera camera, PostFXSettings postFXSettings,
        bool useHDR, bool keepAlpha, int colorLUTResolution, Vector2Int bufferSize,
        CameraSettings.FinalBlendMode finalBlendMode, CameraBufferSettings.BicubicRescalingMode bicubicScaling,
        CameraBufferSettings.FXAA fxaa)
    {
        this._camera = camera;
        this._useHDR = useHDR;
        this._keepAlpha = keepAlpha;
        //将后处理用于适当的相机
        this._postFXSettings = _camera.cameraType <= CameraType.SceneView ? postFXSettings : null;
        this._colorLUTResolution = colorLUTResolution;
        this._bufferSize = bufferSize;
        this._finalBlendMode = finalBlendMode;
        this._bicubicScaling = bicubicScaling;
        this._fxaa = fxaa;

        if (_postFXSettings != null)
        {
            //填充bloom亮度阈值
            //b为亮度 t为阈值 k是拐点(knee)
            // w = (max(s, b-t)) / (max(b, 0.00001))
            //其中 s = (min(max(0, b - t + tk), 2tk)^2) / (4tk + 0.00001)
            _threshold.x = Mathf.GammaToLinearSpace(postFXSettings.BloomSetting.threshold);
            _threshold.y = _threshold.x * postFXSettings.BloomSetting.thresholdKnee;
            _threshold.z = 2.0f * _threshold.y;
            _threshold.w = 0.25f / (_threshold.y + 0.00001f);
        }

        ApplySceneViewState();
    }

    public void Render(RenderGraphContext context, int sourceID)
    {
        _fXBuffer = context.cmd;
        if (DoBloom(sourceID))
        {
            DoFinal(_bloomResultID);
            _fXBuffer.ReleaseTemporaryRT(_bloomResultID);
        }
        else
        {
            DoFinal(sourceID);
        }

        context.renderContext.ExecuteCommandBuffer(_fXBuffer);
        _fXBuffer.Clear();
    }

    private void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, FXPass pass)
    {
        _fXBuffer.SetGlobalTexture(_fxSourceID, from);
        //使用SetRenderTarget重置视口以覆盖整个目标
        _fXBuffer.SetRenderTarget(to,
            _finalBlendMode.destination == BlendMode.Zero
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);
        _fXBuffer.DrawProcedural(Matrix4x4.identity, _postFXSettings.Material, (int)pass,
            MeshTopology.Triangles, 3);
    }

    private void DrawFinal(RenderTargetIdentifier from, FXPass pass)
    {
        _fXBuffer.SetGlobalFloat(_finalSrcBlendID, (float)_finalBlendMode.source);
        _fXBuffer.SetGlobalFloat(_finalDstBlendID, (float)_finalBlendMode.destination);

        _fXBuffer.SetGlobalTexture(_fxSourceID, from);
        //使用SetRenderTarget重置视口以覆盖整个目标
        _fXBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
            _finalBlendMode.destination == BlendMode.Zero
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store); //为了使PostFX可以使用图层透明度 选择RenderBufferLoadAction.Load加载目标缓冲
        //再设置渲染目标后，最终绘制之前设置视口
        _fXBuffer.SetViewport(_camera.pixelRect);
        _fXBuffer.DrawProcedural(Matrix4x4.identity, _postFXSettings.Material,
            (int)pass, MeshTopology.Triangles, 3);
    }

    private bool DoBloom(int sourceID)
    {
        PostFXSettings.BloomSettings bloomSettings = _postFXSettings.BloomSetting;
        int width;
        int height;

        if (bloomSettings.ignoreRenderScale)
        {
            width = _camera.pixelWidth / 2;
            height = _camera.pixelHeight / 2;
        }
        else
        {
            width = _bufferSize.x / 2;
            height = _bufferSize.y / 2;
        }

        //完全跳过Bloom
        if (bloomSettings.maxIterations == 0 || bloomSettings.intensity <= 0f ||
            height < bloomSettings.downScaleLimit * 2 || width < bloomSettings.downScaleLimit * 2)
        {
            return false;
        }

        _fXBuffer.BeginSample("Bloom");

        //填充阈值
        _fXBuffer.SetGlobalVector(_bloomThresholdID, _threshold);

        RenderTextureFormat format = this._useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        //预先降低分辨率
        _fXBuffer.GetTemporaryRT(_bloomPrefilterID, width, height, 0,
            FilterMode.Bilinear, format);
        Draw(sourceID, _bloomPrefilterID,
            bloomSettings.fadeFireflies ? FXPass.BloomPrefilterFireflies : FXPass.BloomPrefilter);
        width /= 2;
        height /= 2;

        int fromID = _bloomPrefilterID;
        int toID = _bloomPyramidID + 1;
        int i;
        //bloom的像素减半金字塔
        for (i = 0; i < bloomSettings.maxIterations; i++)
        {
            if (height < bloomSettings.downScaleLimit || width < bloomSettings.downScaleLimit)
            {
                break;
            }

            int midID = toID - 1;
            _fXBuffer.GetTemporaryRT(midID, width, height, 0, FilterMode.Bilinear, format);
            _fXBuffer.GetTemporaryRT(toID, width, height, 0, FilterMode.Bilinear, format);

            Draw(fromID, midID, FXPass.BloomHorizontal); //横模糊
            Draw(midID, toID, FXPass.BloomVertical); //纵模糊
            fromID = toID;
            toID += 2; //每次模糊Tex都会有两个
            width /= 2;
            height /= 2;
        }

        _fXBuffer.ReleaseTemporaryRT(_bloomPrefilterID);
        _fXBuffer.SetGlobalFloat(_bloomBicubicUpsamplingID, bloomSettings.bloomBicubicUpsampling ? 1.0f : 0f);

        FXPass combinePass;
        FXPass finalPass;
        float finalIntensity;
        if (bloomSettings.bloomMode == PostFXSettings.BloomSettings.BloomMode.Additive)
        {
            combinePass = finalPass = FXPass.BloomAdd;
            _fXBuffer.SetGlobalFloat(_bloomIntensityID, 1.0f); //只在合并过程用强度加权分辨率
            finalIntensity = bloomSettings.intensity;
        }
        else
        {
            combinePass = FXPass.BloomScatter;
            finalPass = FXPass.BloomScatterFinal;
            _fXBuffer.SetGlobalFloat(_bloomIntensityID, bloomSettings.scatter);
            finalIntensity = Mathf.Min(0.95f, bloomSettings.intensity);
        }

        //保证至少有2次迭代 (向下采样)
        if (i > 1)
        {
            _fXBuffer.ReleaseTemporaryRT(fromID - 1);
            toID -= 5;
            //循环结束后，往相反的方向再次迭代
            for (i -= 1; i > 0; i--)
            {
                _fXBuffer.SetGlobalTexture(_fxSource2ID, toID + 1);
                Draw(fromID, toID, combinePass);
                //释放所有请求的RT
                _fXBuffer.ReleaseTemporaryRT(fromID);
                _fXBuffer.ReleaseTemporaryRT(toID + 1);
                fromID = toID;
                toID -= 2;
            }
        }
        else
        {
            _fXBuffer.ReleaseTemporaryRT(_bloomPyramidID);
        }

        _fXBuffer.SetGlobalFloat(_bloomIntensityID, finalIntensity);
        _fXBuffer.SetGlobalTexture(_fxSource2ID, sourceID);

        _fXBuffer.GetTemporaryRT(_bloomResultID, _bufferSize.x, _bufferSize.y, 0,
            FilterMode.Bilinear, format);

        Draw(fromID, _bloomResultID, finalPass);
        _fXBuffer.ReleaseTemporaryRT(fromID);

        _fXBuffer.EndSample("Bloom");
        return true;
    }

    private void ConfigureColorAdjustments()
    {
        PostFXSettings.ColorAdjustmentsSettings colorAdjustmentsSettings = _postFXSettings.ColorAdjustmentsSetting;
        _fXBuffer.SetGlobalVector(_colorAdjustmentsID, new Vector4(
            Mathf.Pow(2.0f, colorAdjustmentsSettings.postExposure),
            colorAdjustmentsSettings.contrast * 0.01f + 1f,
            colorAdjustmentsSettings.hueShift * (1f / 360f),
            colorAdjustmentsSettings.saturation * 0.01f + 1f)
        );
        _fXBuffer.SetGlobalColor(_colorFilterID, colorAdjustmentsSettings.colorFilter);
    }

    private void ConfigureWhiteBalance()
    {
        PostFXSettings.WhiteBalanceSettings whiteBalanceSettings = _postFXSettings.WhiteBalanceSetting;
        _fXBuffer.SetGlobalVector(_whiteBalanceID,
            ColorUtils.ColorBalanceToLMSCoeffs(whiteBalanceSettings.temperature, whiteBalanceSettings.tint));
    }

    private void ConfigureSplitToning()
    {
        PostFXSettings.SplitToningSettings toningSettings = _postFXSettings.SplitToningSetting;
        Color splitColor = toningSettings.shadows;
        splitColor.a = toningSettings.balance * 0.01f;
        _fXBuffer.SetGlobalColor(_splitToningShadowsID, splitColor);
        _fXBuffer.SetGlobalColor(_splitToningHighlightID, toningSettings.highlights);
    }

    private void ConfigureChannelMix()
    {
        PostFXSettings.ChannelMixerSettings channelMixerSettings = _postFXSettings.ChannelMixerSetting;
        _fXBuffer.SetGlobalVector(_channelMixerRedID, channelMixerSettings.red);
        _fXBuffer.SetGlobalVector(_channelMixerGreenID, channelMixerSettings.green);
        _fXBuffer.SetGlobalVector(_channelMixerBlueID, channelMixerSettings.blue);
    }

    private void ConfigureShadowsMidtonesHighLights()
    {
        PostFXSettings.ShadowsMidtonesHighLightsSettings setting = _postFXSettings.ShadowsMidtonesHighLightsSetting;
        _fXBuffer.SetGlobalColor(_smhShadowsID, setting.shadow.linear);
        _fXBuffer.SetGlobalColor(_smhMidtonesID, setting.midTone.linear);
        _fXBuffer.SetGlobalColor(_smhHighlightsID, setting.highLights.linear);
        _fXBuffer.SetGlobalVector(_smhRangeID, new Vector4(
            setting.shadowsStart, setting.shadowsEnd, setting.highLightsStart, setting.highLightsEnd));
    }

    private void ConfigureFXAA()
    {
        if (_keepAlpha)
        {
            _fXBuffer.EnableShaderKeyword(_fxaaAlphaContantsLumaKeyword);
        }
        else
        {
            _fXBuffer.DisableShaderKeyword(_fxaaAlphaContantsLumaKeyword);
        }

        if (_fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
        {
            _fXBuffer.EnableShaderKeyword(_fxaaQualityLowKeyword);
            _fXBuffer.DisableShaderKeyword(_fxaaQualityMediumKeyword);
        }
        else if (_fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
        {
            _fXBuffer.EnableShaderKeyword(_fxaaQualityMediumKeyword);
            _fXBuffer.DisableShaderKeyword(_fxaaQualityLowKeyword);
        }
        else
        {
            _fXBuffer.DisableShaderKeyword(_fxaaQualityLowKeyword);
            _fXBuffer.DisableShaderKeyword(_fxaaQualityMediumKeyword);
        }


        _fXBuffer.SetGlobalVector(_fxaaConfigID, new Vector4(
            _fxaa.fixedThreshold, _fxaa.relativeThreshold, _fxaa.subpixelBlending, 0));
    }

    private void DoFinal(int sourceID)
    {
    #region ColorGrading

        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMix();
        ConfigureShadowsMidtonesHighLights();

        //LUT
        //可以只渲染一次LUT图然后缓存下来，但是判断是否刷新会比较复杂，而且只做颜色分级和色调渲染消耗比渲染整个屏幕的画面要小
        int lutHeight = _colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        _fXBuffer.GetTemporaryRT(_colorGradingLUTID, lutWidth, lutHeight, 0,
            FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
        //LUT图本应是3D的，但常规着色器无法渲染3D纹理，将3DLUT图切片连续放置
        _fXBuffer.SetGlobalVector(_colorGradingLUTParameters, new Vector4(
            lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)));

        _fXBuffer.BeginSample("ToneMapping");
        PostFXSettings.ToneMappingSettings.ToneMappingMode mappingMode =
            _postFXSettings.ToneMappingSetting.toneMappingMode;
        FXPass pass = mappingMode < 0 ? FXPass.Copy : FXPass.ToneMappingNone + (int)mappingMode;

        _fXBuffer.SetGlobalFloat(_colorGradingLUTLogCID,
            _useHDR && pass != FXPass.ToneMappingNone ? 1f : 0f);

        Draw(sourceID, _colorGradingLUTID, pass); //把颜色分级和色调映射都渲染到LUT上

        _fXBuffer.SetGlobalVector(_colorGradingLUTParameters, new Vector4(
            1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1.0f));

        //保证透明混合是不透明的 one zero
        _fXBuffer.SetGlobalFloat(_finalSrcBlendID, 1f);
        _fXBuffer.SetGlobalFloat(_finalDstBlendID, 0f);

        if (_fxaa.enable)
        {
            //FXAA关键字设置
            ConfigureFXAA();

            _fXBuffer.GetTemporaryRT(_colorGradingResultID, _bufferSize.x, _bufferSize.y, 0,
                FilterMode.Bilinear, RenderTextureFormat.Default);
            Draw(sourceID, _colorGradingResultID,
                _keepAlpha ? FXPass.ApplyColorGradingWithAlpha : FXPass.ApplyColorGrading);
        }

        if (_bufferSize.x == _camera.pixelWidth)
        {
            if (_fxaa.enable)
            {
                DrawFinal(_colorGradingResultID, _keepAlpha ? FXPass.FXAAWithLuma : FXPass.FXAA);
                _fXBuffer.ReleaseTemporaryRT(_colorGradingResultID);
            }
            else
            {
                DrawFinal(sourceID, FXPass.ApplyColorGrading);
            }
        }
        else
        {
            _fXBuffer.GetTemporaryRT(_finalResultID, _bufferSize.x, _bufferSize.y, 0,
                FilterMode.Bilinear, RenderTextureFormat.Default);

            if (_fxaa.enable)
            {
                Draw(_colorGradingResultID, _finalResultID, _keepAlpha ? FXPass.FXAAWithLuma : FXPass.FXAA);
            }
            else
            {
                Draw(sourceID, _finalResultID, FXPass.ApplyColorGrading);
            }

            bool bicubicSampling =
                _bicubicScaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                _bicubicScaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                _bufferSize.x < _camera.pixelWidth;

            _fXBuffer.SetGlobalFloat(_copyBicubicID, bicubicSampling ? 1 : 0);
            DrawFinal(_finalResultID, FXPass.FinalRescale);
        }

    #endregion

        _fXBuffer.ReleaseTemporaryRT(_colorGradingLUTID);
        _fXBuffer.EndSample("ToneMapping");
    }
}