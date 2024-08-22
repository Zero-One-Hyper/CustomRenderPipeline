using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

//使用一个CameraRender类来专门对每个摄像机进行渲染
//目的为了将摄像机能看到的东西画出来
public partial class CameraRender
{
    public CameraRender(UnityEngine.Shader shader)
    {
        _cameraRendererMaterial = CoreUtils.CreateEngineMaterial(shader);
        _missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing",
        };
        _missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        _missingTexture.Apply(true, true);
    }

    //LightMode可以随便自己定一个
    private static ShaderTagId[] _shaderTagId =
    {
        new ShaderTagId("CustomRenderPipelineLightMode"),
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("SRPDefaultUnlit")
    };

    //像绘制天空盒这样的命令可以通过特殊的方法来添加到命令队列中，但是其他的特殊命令不一定有对应的方法
    //使用独立的CommandBuffer来存储部分特殊命令并给出
    private const string BUFFER_NAME = "Custom Render Camera";

    public const float RenderScaleMin = 0.01f;
    public const float RenderScaleMax = 2.0f;

    //unity的_ScreenParams中的值与Camera的width和height绑定，若要使用RenderScale需要调整
    private static int _bufferSizeID = UnityEngine.Shader.PropertyToID("_CameraBufferSize");

    //为相机使用单个缓冲区（包括颜色和深度）
    //private static int FrameBufferID = UnityEngine.Shader.PropertyToID("CameraFrameBuffer");
    //分别定义颜色缓冲和深度缓冲 分开二者
    private static int _colorAttachmentID = UnityEngine.Shader.PropertyToID("_CameraColorAttachment");
    public static int _depthAttachmentID = UnityEngine.Shader.PropertyToID("_CameraDepthAttachment");

    private static int _colorTextureID = UnityEngine.Shader.PropertyToID("_CameraColorTexture");
    private static int _depthTextureID = UnityEngine.Shader.PropertyToID("_CameraDepthTexture");

    private static int _sourceTextureID = UnityEngine.Shader.PropertyToID("_SourceTexture");

    //最终混合模式
    private int _finalSrcBlendID = UnityEngine.Shader.PropertyToID("_FinalSrcBlend");
    private int _finalDstBlendID = UnityEngine.Shader.PropertyToID("_FinalDstBlend");

    private CommandBuffer _commandBuffer = new CommandBuffer()
    {
        name = BUFFER_NAME,
    };

    private static CameraSettings _defaultCameraSettings = new CameraSettings();
    private static Rect _fullViewRect = new Rect(0f, 0f, 1f, 1f);

    private CullingResults _cullingResults;
    private ScriptableRenderContext _context;
    public Camera camera;
    private Lighting _lighting = new Lighting();
    private PostFXStack _postFXStack = new PostFXStack();
    private Material _cameraRendererMaterial;
    private Texture2D _missingTexture; //确保采样深度纹理时至少有正确的纹理存在

    private bool _useHDR;
    public bool useColorTexture;
    public bool useDepthTexture;
    public bool useIntermediateBuffer; //在不使用postFX时依旧可以使用深度纹理
    private bool _useRenderScaledRendering;

    //诸如WebGL上不能使用CopyTexture的平台使用着色器复制
    private bool _copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    private Vector2Int _bufferSize;

    public void Render(RenderGraph renderGraph, ScriptableRenderContext context, Camera camera,
        bool useDynamicBaching, bool useGPUInstancing, bool useLightPerObject,
        CameraBufferSettings cameraBufferSettings, ShadowSettings shadowSettings, PostFXSettings postFXSettings,
        int colorLUTResolution)
    {
        this._context = context;
        this.camera = camera;

        var customRenderPipelineCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = customRenderPipelineCamera
            ? customRenderPipelineCamera.CameraSetting
            : _defaultCameraSettings;

        if (this.camera.cameraType == CameraType.Reflection)
        {
            //用于渲染反射探针的摄像机
            useColorTexture = cameraBufferSettings.copyColorReflections;
            useDepthTexture = cameraBufferSettings.copyDepthReflections;
        }
        else
        {
            useColorTexture = cameraBufferSettings.copyColor && cameraSettings.copyColor;
            useDepthTexture = cameraBufferSettings.copyDepth && cameraSettings.copyDepth;
        }

        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings ?? postFXSettings;
        }

        //拿到实际的渲染缩放
        float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
        _useRenderScaledRendering = renderScale <= 0.99f || renderScale >= 1.01f;

        //将 UI 几何体发出到 Scene 视图中进行渲染。
        //有可能给场景添加几何体 所以必须在cull之前绘制
        PrepareForSceneWindow();
        //剔除不在相机中的物体
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        this._useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;
        if (_useRenderScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, RenderScaleMin, RenderScaleMax);
            _bufferSize.x = (int)(this.camera.pixelWidth * renderScale);
            _bufferSize.y = (int)(this.camera.pixelHeight * renderScale);
        }
        else
        {
            _bufferSize.x = this.camera.pixelWidth;
            _bufferSize.y = this.camera.pixelHeight;
        }

        //多个相机时，使相机的样本明分开
        PrepareBuffer();

        _commandBuffer.BeginSample(SampleName);

        _commandBuffer.SetGlobalVector(_bufferSizeID, new Vector4(
            1f / _bufferSize.x, 1f / _bufferSize.y,
            _bufferSize.x, _bufferSize.y));

        ExecuteCommandBuffer();
        _lighting.SetUp(this._context, _cullingResults, shadowSettings,
            useLightPerObject, cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
        //
        cameraBufferSettings.fxaa.enable &= cameraSettings.allowFXAA;
        _postFXStack.SetUp(this._context, this.camera, postFXSettings, this._useHDR, cameraSettings.keepAlpha,
            colorLUTResolution, _bufferSize,
            cameraSettings.finalBlendMode, cameraBufferSettings.bicubicRescaling,
            cameraBufferSettings.fxaa);
        _commandBuffer.EndSample(SampleName);

        //应在渲染常规几何体之前渲染阴影
        //设置摄像机参数
        SetUp();
        //将绘制命令存入命令缓存区中
        DrawVisibleGeometry(useDynamicBaching, useGPUInstancing, useLightPerObject,
            cameraSettings.renderingLayerMask);
        //针对不受支持的Shader的渲染代码放入了CamraRender.Edior中定义
        //绘制不受支持的Shader 
        DrawUnsupportedShaders(); //使用了partial定义

        //绘制gizmos后绘制后处理
        if (_postFXStack.IsActive)
        {
            _postFXStack.Render(_colorAttachmentID);
        }

        /*
        else if (useIntermediateBuffer)
        {
            //之前的方式知识简单的Copy了贴图，并没有考虑到不使用后处理时透明度的处理以及第二个相机不铺满屏幕
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteCommandBuffer();
        }
        */
        var renderGraphParameters = new RenderGraphParameters()
        {
            commandBuffer = CommandBufferPool.Get(),
            currentFrameIndex = Time.frameCount,
            executionName = "Render Camera",
            scriptableRenderContext = this._context,
        };

        //放在using中可以简单的不使用.Dispose() 相当于一个try块，finally中会调用dispose
        using (renderGraph.RecordAndExecute(renderGraphParameters))
        {
            //rendergraph的过程
            if (_postFXStack.IsActive)
            {
            }
            else if (useIntermediateBuffer)
            {
                FinalPass.Record(renderGraph, this, cameraSettings.finalBlendMode);
            }

            //后处理后绘制Gizmos(绘制gizmos的地方换到了RenderGraphy)
            GizmosPass.Record(renderGraph, this);
        }


        //在命令提交之前请求清理
        this.CleanUp();
        //提交context(只有我们提交context，才会真正开始渲染)
        Submit();
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);
    }

    private void SetUp()
    {
        //这一步用来将摄像机的属性应用到context上，渲染天空盒的时候主要是设置VP矩阵（unity_MatrixVP）
        _context.SetupCameraProperties(this.camera); //有了这个指令在scene中选中摄像机时才不会黑屏
        //清除标志位 写了这个在FrameDebug中才会显示 Clear(Color + z + stencil)
        CameraClearFlags flags = camera.clearFlags;

        useIntermediateBuffer = _useRenderScaledRendering || useColorTexture ||
                                useDepthTexture || _postFXStack.IsActive;
        if (useIntermediateBuffer)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }

            //颜色缓冲
            _commandBuffer.GetTemporaryRT(_colorAttachmentID, _bufferSize.x, _bufferSize.y,
                0, FilterMode.Bilinear,
                _useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            //深度缓冲
            _commandBuffer.GetTemporaryRT(_depthAttachmentID, _bufferSize.x, _bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth);

            _commandBuffer.SetRenderTarget(
                //颜色
                _colorAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                //深度
                _depthAttachmentID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        //清除可能对接下来要画的东西有干扰的旧的内容
        _commandBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        //注射分析样本 以便在FrameDebug中显示
        _commandBuffer.BeginSample(SampleName);
        _commandBuffer.SetGlobalTexture(_colorTextureID, _missingTexture);
        _commandBuffer.SetGlobalTexture(_depthTextureID, _missingTexture);

        ExecuteCommandBuffer();
    }

    public void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        _commandBuffer.SetGlobalTexture(_sourceTextureID, from);
        _commandBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        _commandBuffer.DrawProcedural(Matrix4x4.identity, _cameraRendererMaterial,
            isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    public void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        _commandBuffer.SetGlobalFloat(_finalSrcBlendID, (float)finalBlendMode.source);
        _commandBuffer.SetGlobalFloat(_finalDstBlendID, (float)finalBlendMode.destination);
        _commandBuffer.SetGlobalTexture(_sourceTextureID, _colorAttachmentID);
        _commandBuffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero && camera.rect == _fullViewRect
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store
        );
        _commandBuffer.SetViewport(camera.pixelRect);
        _commandBuffer.DrawProcedural(
            Matrix4x4.identity, _cameraRendererMaterial, 0, MeshTopology.Triangles, 3
        );
        _commandBuffer.SetGlobalFloat(_finalSrcBlendID, 1f);
        _commandBuffer.SetGlobalFloat(_finalDstBlendID, 0f);
    }

    //顾名思义使用这个方法实现绘制摄像机看到的东西
    private void DrawVisibleGeometry(bool useDynamicBaching, bool useGPUInstancing, bool useLightsPerObject,
        int renderingLayerMask)
    {
        PerObjectData lightsPerObjectFlag =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;
        //知道什么东西会被剔除后，就可以继续渲染了
        var sortingSettings = new SortingSettings(this.camera) //排序设置
        {
            //设置排序条件
            criteria = SortingCriteria.CommonOpaque,
        };

        var drawingSettings = new DrawingSettings(_shaderTagId[0], sortingSettings)
        {
            //若要使用动态合批，需要关闭gpuInstancing，并关闭SRP合批（因为他会优先生效）
            enableDynamicBatching = useDynamicBaching,
            enableInstancing = useGPUInstancing,
            //告诉管线将光照贴图的UV发送到着色器
            perObjectData = PerObjectData.ReflectionProbes |
                            PerObjectData.Lightmaps |
                            PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume |
                            PerObjectData.OcclusionProbe | PerObjectData.OcclusionProbeProxyVolume | //光照探针的阴影遮罩数据
                            PerObjectData.ShadowMask |
                            lightsPerObjectFlag,
        }; //绘制设置
        //调用SetShaderPassName来绘制多个通道
        for (int i = 1; i < _shaderTagId.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, _shaderTagId[i]);
        }

        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque,
            renderingLayerMask: (uint)renderingLayerMask); //过滤设置
        this._context.DrawRenderers(this._cullingResults, ref drawingSettings, ref filteringSettings);

        //这个方法只会将绘制命令缓存到命令队列中，需要使用Submint方法提交工作队列来执行
        this._context.DrawSkybox(this.camera);
        if (useColorTexture || useDepthTexture)
        {
            CopyAttachments(); //绘制完天空盒后复制深度贴图
        }

        //在天空盒渲染后再渲染透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        this._context.DrawRenderers(this._cullingResults, ref drawingSettings, ref filteringSettings);
    }

    //必须通过调用context的Submit方法提交这个工作队列来执行
    private void Submit()
    {
        //结束注射分析样本 以便在FrameDebug中显示
        _commandBuffer.EndSample(SampleName);

        ExecuteCommandBuffer();
        this._context.Submit();
    }

    //执行commandBuffer 并清除（执行和清除总是在一起做）
    public void ExecuteCommandBuffer()
    {
        this._context.ExecuteCommandBuffer(this._commandBuffer);
        this._commandBuffer.Clear();
    }

    private bool Cull(float maxShadowDistance)
    {
        ScriptableCullingParameters parameters;
        if (this.camera.TryGetCullingParameters(false, out parameters))
        {
            parameters.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            //使用context的Cull方法来进行剔除 (这里使用ref来避免对parmeters的拷贝，因为parameters可能很大）
            _cullingResults = _context.Cull(ref parameters);
            return true;
        }

        return false;
    }

    private void CopyAttachments()
    {
        if (useColorTexture)
        {
            _commandBuffer.GetTemporaryRT(_colorTextureID, _bufferSize.x, _bufferSize.y,
                0, FilterMode.Bilinear,
                _useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            if (_copyTextureSupported)
            {
                _commandBuffer.CopyTexture(_colorAttachmentID, _colorTextureID);
            }
            else
            {
                Draw(_colorAttachmentID, _colorTextureID);
            }
        }

        if (useDepthTexture)
        {
            _commandBuffer.GetTemporaryRT(_depthTextureID, _bufferSize.x, _bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth);
            if (_copyTextureSupported)
            {
                _commandBuffer.CopyTexture(_depthAttachmentID, _depthTextureID);
            }
            else
            {
                Draw(_depthAttachmentID, _depthTextureID, true);
            }
        }

        if (!_copyTextureSupported)
        {
            //Draw改变了渲染目标，因此需要重新设置回相机缓冲
            _commandBuffer.SetRenderTarget(
                _colorAttachmentID, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                _depthAttachmentID, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        }

        ExecuteCommandBuffer();
    }

    private void CleanUp()
    {
        _lighting.CleanUp();
        if (useIntermediateBuffer)
        {
            _commandBuffer.ReleaseTemporaryRT(_colorAttachmentID);
            _commandBuffer.ReleaseTemporaryRT(_depthAttachmentID);
            if (useColorTexture)
            {
                _commandBuffer.ReleaseTemporaryRT(_colorTextureID);
            }

            if (useDepthTexture)
            {
                _commandBuffer.ReleaseTemporaryRT(_depthTextureID);
            }
        }
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_cameraRendererMaterial);
        CoreUtils.Destroy(_missingTexture);
    }

    //使用partial只声明方法
    private partial void DrawUnsupportedShaders();

    /*旧的未使用renderGraphy的绘制Gizmos
    private partial void DrawGizmosBeforeFX();
    private partial void DrawGizmosAfterFX();
    */
    //UI会被单独渲染，而不是通过我们的renderPipeline
    //但是在Scene窗口中需要我们明确地将ui添加到世界几何体中去
    private partial void PrepareForSceneWindow();

//使缓冲的名子和摄像机相同
    private partial void PrepareBuffer();
}