using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    public CustomRenderPipeline(bool useDynamicBaching, bool useGPUInstancing, bool useSRPBacher,
        bool useLightPerObject,
        CameraBufferSettings cameraBufferSettings, ShadowSettings shadowSettings,
        PostFXSettings postFXSettings, int colorLUTResolution,
        UnityEngine.Shader cameraRendererShader)
    {
        this._colorLUTResolution = colorLUTResolution;
        //激活SRP合批，只用设置一次，再创建时自动设置
        //SRP合批不能处理逐对象的材质属性
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBacher;
        //GPU实例化可以处理逐对象的材质属性
        //GPU实例化不需要手动开启
        this._useDynamicBaching = useDynamicBaching;
        this._useGPUInstancing = useGPUInstancing;
        this._useLightPerObject = useLightPerObject;
        this._cameraBufferSettings = cameraBufferSettings;
        //设置光照使用线性空间强度
        GraphicsSettings.lightsUseLinearIntensity = true;
        this._shadowSettings = shadowSettings;
        this._postFXSettings = postFXSettings;
        InitializeForEditor();
        _renderer = new CameraRender(cameraRendererShader);
    }

    private CameraRender _renderer;
    private bool _useDynamicBaching;
    private bool _useGPUInstancing;
    private bool _useLightPerObject;

    private CameraBufferSettings _cameraBufferSettings;

    private ShadowSettings _shadowSettings;

    private PostFXSettings _postFXSettings;

    private int _colorLUTResolution;

    //unity每帧都会调用渲染管线的Render方法
    //context提供了到原生引擎的连接
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            _renderer.Render(context, cameras[i],
                _useDynamicBaching, _useGPUInstancing, _useLightPerObject,
                _cameraBufferSettings, _shadowSettings, _postFXSettings,
                _colorLUTResolution);
        }
    }

    //一个list版本的Render
    //protected override void Render(ScriptableRenderContext context, List<Camera> cameras)


    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        _renderer.Dispose();
    }

    private partial void InitializeForEditor();

    private partial void DisposeForEditor();
}