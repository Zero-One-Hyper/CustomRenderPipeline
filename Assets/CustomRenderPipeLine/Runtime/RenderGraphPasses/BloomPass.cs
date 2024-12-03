using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static PostFXStack;
using static PostFXSettings;

public class BloomPass
{
    private static readonly ProfilingSampler Sampler = new ProfilingSampler("Bloom Sampler");

    private const int MaxBloomPyramidLevels = 16;


    private static readonly int FxSource2ID = UnityEngine.Shader.PropertyToID("_PostFXSource2");

    private static readonly int BloomBicubicUpsamplingID = UnityEngine.Shader.PropertyToID("_BloomBicubicUpsampling");
    private static readonly int BloomIntensityID = UnityEngine.Shader.PropertyToID("_BloomIntensity");
    private static readonly int BloomThresholdID = UnityEngine.Shader.PropertyToID("_BloomThreshold");

    private readonly TextureHandle[] _pyramid = new TextureHandle[2 * MaxBloomPyramidLevels + 1];

    private TextureHandle _colorSource;
    private TextureHandle _bloomResult;

    private PostFXStack _postFXStack;

    private int _stepCount;

    private void Render(RenderGraphContext context)
    {
        CommandBuffer commandBuffer = context.cmd;
        PostFXSettings.BloomSettings bloomSettings = _postFXStack.PostFXSettings.BloomSetting;

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(_postFXStack.PostFXSettings.BloomSetting.threshold);
        threshold.y = threshold.x * _postFXStack.PostFXSettings.BloomSetting.thresholdKnee;
        threshold.z = 2.0f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;

        //填充阈值
        commandBuffer.SetGlobalVector(BloomThresholdID, threshold);

        _postFXStack.Draw(commandBuffer, _colorSource, _pyramid[0],
            bloomSettings.fadeFireflies ? FXPass.BloomPrefilterFireflies : FXPass.BloomPrefilter);

        int fromID = 0;
        int toID = 2;
        int i;
        //bloom的像素减半金字塔
        for (i = 0; i < _stepCount; i++)
        {
            int midID = toID - 1;
            _postFXStack.Draw(commandBuffer, _pyramid[fromID], _pyramid[midID], FXPass.BloomHorizontal); //横模糊
            _postFXStack.Draw(commandBuffer, _pyramid[midID], _pyramid[toID], FXPass.BloomVertical); //纵模糊
            fromID = toID;
            toID += 2; //每次模糊Tex都会有两个
        }

        commandBuffer.SetGlobalFloat(BloomBicubicUpsamplingID, bloomSettings.bloomBicubicUpsampling ? 1.0f : 0f);

        FXPass combinePass;
        FXPass finalPass;
        float finalIntensity;
        if (bloomSettings.bloomMode == PostFXSettings.BloomSettings.BloomMode.Additive)
        {
            combinePass = finalPass = FXPass.BloomAdd;
            commandBuffer.SetGlobalFloat(BloomIntensityID, 1.0f); //只在合并过程用强度加权分辨率
            finalIntensity = bloomSettings.intensity;
        }
        else
        {
            combinePass = FXPass.BloomScatter;
            finalPass = FXPass.BloomScatterFinal;
            commandBuffer.SetGlobalFloat(BloomIntensityID, bloomSettings.scatter);
            finalIntensity = Mathf.Min(1f, bloomSettings.intensity);
        }

        //保证至少有2次迭代 (向下采样)
        if (i > 1)
        {
            toID -= 5;
            //循环结束后，往相反的方向再次迭代
            for (i -= 1; i > 0; i--)
            {
                commandBuffer.SetGlobalTexture(FxSource2ID, _pyramid[toID + 1]);
                _postFXStack.Draw(commandBuffer, _pyramid[fromID], _pyramid[toID], combinePass);
                //使用了RenderGraph 不再需要手动释放所有请求的RT
                fromID = toID;
                toID -= 2;
            }
        }

        commandBuffer.SetGlobalFloat(BloomIntensityID, finalIntensity);
        commandBuffer.SetGlobalTexture(FxSource2ID, _colorSource);
        _postFXStack.Draw(commandBuffer, _pyramid[fromID], _bloomResult, finalPass);
    }

    public static TextureHandle Recode(RenderGraph renderGraph, PostFXStack postFXStack,
        in CameraRendererTextures cameraRendererTextures)
    {
        PostFXSettings.BloomSettings bloomSettings = postFXStack.PostFXSettings.BloomSetting;
        //预先降采样
        Vector2Int size = (bloomSettings.ignoreRenderScale
            ? new Vector2Int(postFXStack.Camera.pixelWidth, postFXStack.Camera.pixelHeight)
            : postFXStack.BufferSize) / 2;

        //完全跳过Bloom
        if (bloomSettings.maxIterations == 0 || bloomSettings.intensity <= 0f ||
            size.y < bloomSettings.downScaleLimit * 2 || size.x < bloomSettings.downScaleLimit * 2)
        {
            return cameraRendererTextures.colorAttachment;
        }

        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            Sampler.name, out BloomPass bloomPass, Sampler);
        bloomPass._postFXStack = postFXStack;
        bloomPass._colorSource = builder.ReadTexture(cameraRendererTextures.colorAttachment);

        var desc = new TextureDesc(size.x, size.y)
        {
            colorFormat =
                SystemInfo.GetGraphicsFormat(postFXStack.CameraBufferSettings.allowHDR
                    ? DefaultFormat.HDR
                    : DefaultFormat.LDR),
            name = "Bloom Prefilter",
            wrapMode = TextureWrapMode.Clamp, //放置bloom时边缘泄露
        };
        //填充TextureHandle
        TextureHandle[] pyramid = bloomPass._pyramid;
        pyramid[0] = builder.CreateTransientTexture(desc);
        size /= 2;
        int pyramidIndex = 1;
        int i = 0;
        for (i = 0; i < bloomSettings.maxIterations; i++, pyramidIndex += 2)
        {
            if (size.y < bloomSettings.downScaleLimit || size.x < bloomSettings.downScaleLimit)
            {
                break;
            }

            desc.width = size.x;
            desc.height = size.y;
            desc.name = string.Format("Bloom Pyramid {0} H", i);
            pyramid[pyramidIndex] = builder.CreateTransientTexture(desc);
            desc.name = string.Format("Bloom Pyramid {0} V", i);
            pyramid[pyramidIndex + 1] = builder.CreateTransientTexture(desc);
            size /= 2;
        }

        bloomPass._stepCount = i;
        desc.width = postFXStack.BufferSize.x;
        desc.height = postFXStack.BufferSize.y;
        desc.name = "Bloom Result";
        bloomPass._bloomResult = builder.WriteTexture(renderGraph.CreateTexture(desc));
        builder.SetRenderFunc<BloomPass>(
            static (pass, context) => pass.Render(context));
        return bloomPass._bloomResult;
    }
}