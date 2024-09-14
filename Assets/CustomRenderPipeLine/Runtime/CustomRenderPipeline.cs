using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule; //使用实验性功能 renderGraph

public partial class CustomRenderPipeline : RenderPipeline
{
    public CustomRenderPipeline(CustomRenderPipelineSetting setting)
    {
        this._setting = setting;
        //this._colorLUTResolution = colorLUTResolution;
        //激活SRP合批，只用设置一次，再创建时自动设置
        //SRP合批不能处理逐对象的材质属性
        GraphicsSettings.useScriptableRenderPipelineBatching = _setting.useSRPBatcher;
        //GPU实例化可以处理逐对象的材质属性
        //GPU实例化不需要手动开启
        //使用RenderGraph时动态批处理将会始终关闭 GPU实例化将始终开启
        //this._useLightPerObject = useLightPerObject;
        //this._cameraBufferSettings = cameraBufferSettings;
        //设置光照使用线性空间强度
        GraphicsSettings.lightsUseLinearIntensity = true;
        //this._shadowSettings = shadowSettings;
        //this._postFXSettings = postFXSettings;
        InitializeForEditor();
        _renderer = new CameraRender(_setting.cameraRenderShader, _setting.cameraDebugShader);
    }

    private CameraRender _renderer;

    private readonly CustomRenderPipelineSetting _setting;

    //RenderGraph 一种管理各个feature引用的资源及使用的pass 对渲染管线中存在的耦合问题进行解耦合
    private readonly RenderGraph _renderGraph = new RenderGraph("Custom SRP Render Graph");


    //unity每帧都会调用渲染管线的Render方法
    //context提供了到原生引擎的连接
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            _renderer.Render(_renderGraph, context, cameras[i], _setting);
        }

        _renderGraph.EndFrame();
    }

    //一个list版本的Render
    //protected override void Render(ScriptableRenderContext context, List<Camera> cameras)


    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        _renderer.Dispose();
        _renderGraph.Cleanup();
    }

    private partial void InitializeForEditor();

    private partial void DisposeForEditor();
}