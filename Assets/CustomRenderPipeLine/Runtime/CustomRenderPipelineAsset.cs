using UnityEngine;
using UnityEngine.Rendering;

//https://zhuanlan.zhihu.com/p/693885113
//https://catlikecoding.com/unity/tutorials/custom-srp/custom-render-pipeline/
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline Asset")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    private bool useSRPBacher = true;

    [SerializeField]
    private bool useGPUInstancing = true;

    [SerializeField]
    private bool useDynamicBaching = false;

    [SerializeField]
    private bool useLightsPerObject;

    [SerializeField]
    private CameraBufferSettings cameraBufferSettings = new CameraBufferSettings()
    {
        allowHDR = true,
        renderScale = 1.0f,
        fxaa = new CameraBufferSettings.FXAA()
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f,
        },
    };

    [SerializeField]
    private ShadowSettings shadowSettings = default;

    [SerializeField]
    private PostFXSettings postFXSettings = default;

    public enum ColorLUTResolution
    {
        _16 = 16,
        _32 = 32,
        _64 = 64,
    }

    [SerializeField]
    private ColorLUTResolution colorLutResolution = ColorLUTResolution._32;

    [SerializeField]
    private UnityEngine.Shader cameraRendererShader = default;

    //当Unity编辑器检测到这个asset改变时会创建一个新的渲染管线实例。
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(useDynamicBaching, useGPUInstancing, useSRPBacher,
            useLightsPerObject, cameraBufferSettings, shadowSettings, postFXSettings,
            (int)colorLutResolution, cameraRendererShader);
    }
}