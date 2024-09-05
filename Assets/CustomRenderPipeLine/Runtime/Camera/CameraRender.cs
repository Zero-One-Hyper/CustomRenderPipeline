using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

//使用一个CameraRender类来专门对每个摄像机进行渲染
//目的为了将摄像机能看到的东西画出来
public class CameraRender
{
    public CameraRender(UnityEngine.Shader shader, UnityEngine.Shader debugShader)
    {
        _cameraRendererMaterial = CoreUtils.CreateEngineMaterial(shader);
        CameraDebugger.Initialize(debugShader);
    }

    public const float RenderScaleMin = 0.01f;
    public const float RenderScaleMax = 2.0f;

    private CommandBuffer _commandBuffer;

    private static CameraSettings _defaultCameraSettings = new CameraSettings();

    private CullingResults _cullingResults;
    private ScriptableRenderContext _context;

    private Camera _camera;
    private PostFXStack _postFXStack = new PostFXStack();
    private Material _cameraRendererMaterial;

    public void Render(RenderGraph renderGraph, ScriptableRenderContext context, Camera renderCamera,
        CustomRenderPipelineSetting setting)
    {
        this._context = context;
        this._camera = renderCamera;
        CameraBufferSettings cameraBufferSettings = setting.cameraBufferSettings;
        PostFXSettings postFXSettings = setting.postFXSettings;
        ShadowSettings shadowSettings = setting.shadowSettings;
        bool useLightPerObject = setting.useLightPerObject;

        ProfilingSampler cameraSampler;
        CameraSettings cameraSettings;
        if (renderCamera.TryGetComponent(out CustomRenderPipelineCamera customRenderPipelineCamera))
        {
            cameraSampler = customRenderPipelineCamera.Sampler;
            cameraSettings = customRenderPipelineCamera.CameraSetting;
        }
        else
        {
            cameraSampler = ProfilingSampler.Get(_camera.cameraType);
            cameraSettings = _defaultCameraSettings;
        }

        bool useColorTexture;
        bool useDepthTexture;
        if (this._camera.cameraType == CameraType.Reflection)
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

        bool hasActivePostFX = postFXSettings != null && postFXSettings.AreApplicableTo(renderCamera);

        //拿到实际的渲染缩放
        float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
        bool useRenderScaledRendering = renderScale <= 0.99f || renderScale >= 1.01f;

#if UNITY_EDITOR
        //UI会被单独渲染，而不是通过我们的renderPipeline
        //但是在Scene窗口中需要我们明确地将ui添加到世界几何体中去
        if (_camera.cameraType == CameraType.SceneView)
        {
            //当摄像机类型为场景相机时，使用这个场景相机渲染
            //将 UI 几何体发出到 Scene 视图中进行渲染。
            ScriptableRenderContext.EmitWorldGeometryForSceneView(_camera);
            useRenderScaledRendering = false; //不希望渲染缩放影响scene相机
        }
#endif
        //剔除不在相机中的物体
        ScriptableCullingParameters parameters;
        if (!this._camera.TryGetCullingParameters(false, out parameters))
        {
            return;
        }

        parameters.shadowDistance = Mathf.Min(shadowSettings.maxDistance, _camera.farClipPlane);
        //使用context的Cull方法来进行剔除 (这里使用ref来避免对parmeters的拷贝，因为parameters可能很大）
        _cullingResults = _context.Cull(ref parameters);

        //bool useHDR = cameraBufferSettings.allowHDR && renderCamera.allowHDR;
        cameraBufferSettings.allowHDR &= renderCamera.allowHDR;

        Vector2Int bufferSize = default;
        if (useRenderScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, RenderScaleMin, RenderScaleMax);
            bufferSize.x = (int)(this._camera.pixelWidth * renderScale);
            bufferSize.y = (int)(this._camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = this._camera.pixelWidth;
            bufferSize.y = this._camera.pixelHeight;
        }

        
        //设置FX堆栈及验证FXAA
        cameraBufferSettings.fxaa.enable &= cameraSettings.allowFXAA;
        //将是否使用中间纹理挪到setup外面来
        bool useIntermediateBuffer = useRenderScaledRendering || useColorTexture ||
                                     useDepthTexture || hasActivePostFX ||
                                     !useLightPerObject;


        var renderGraphParameters = new RenderGraphParameters()
        {
            commandBuffer = CommandBufferPool.Get(),
            currentFrameIndex = Time.frameCount,
            executionName = cameraSampler.name,
            rendererListCulling = true, //开启渲染列表的剔除
            scriptableRenderContext = this._context,
        };
        _commandBuffer = renderGraphParameters.commandBuffer;

        //使用RenderGraph使所有命令缓冲的执行和渲染都在其中进行
        //放在using中可以简单的不使用.Dispose() 相当于一个try块，finally中会调用dispose
        using (renderGraph.RecordAndExecute(renderGraphParameters))
        {
            //做一个记录步骤 不需要手动在任何地方访问它
            using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);

            //rendergraph的过程
            //光照设置
            LightResource lightResource = LightingPass.Recode(renderGraph,
                _cullingResults, shadowSettings, setting.forwardPlusSettings,
                useLightPerObject, cameraSettings.renderingLayerMask, bufferSize);

            //应在渲染常规几何体之前渲染阴影
            //设置摄像机参数
            CameraRendererTextures cameraRendererTextures =
                SetUpPass.Recode(renderGraph, renderCamera, bufferSize, useIntermediateBuffer,
                    cameraBufferSettings.allowHDR,
                    useColorTexture, useDepthTexture);

            //将绘制命令存入命令缓存区中 绘制可见物体
            //绘制不透明物体
            GeometryPass.Recode(renderGraph, renderCamera, _cullingResults,
                useLightPerObject, cameraSettings.renderingLayerMask, true,
                cameraRendererTextures, lightResource);

            //绘制天空盒
            SkyBoxPass.Recode(renderGraph, renderCamera, cameraRendererTextures);

            //若使用中间纹理 则拷贝(在CopyAttachmentsPass中判断)
            CameraRendererCopier copier = new CameraRendererCopier(_cameraRendererMaterial, renderCamera,
                cameraSettings.finalBlendMode);
            CopyAttachmentsPass.Recode(renderGraph,
                copier, cameraRendererTextures,
                useColorTexture, useDepthTexture);

            //绘制透明物体
            GeometryPass.Recode(renderGraph, renderCamera, _cullingResults,
                useLightPerObject, cameraSettings.renderingLayerMask, false,
                cameraRendererTextures, lightResource);

            //绘制不受支持的Shader 
            UnSupportedShadersPass.Recode(renderGraph, renderCamera, _cullingResults);
            //后处理
            if (hasActivePostFX)
            {
                _postFXStack.CameraBufferSettings = cameraBufferSettings;
                _postFXStack.BufferSize = bufferSize;
                _postFXStack.Camera = renderCamera;
                _postFXStack.FinalBlendMode = cameraSettings.finalBlendMode;
                _postFXStack.PostFXSettings = postFXSettings;
                PostFXPass.Record(renderGraph, _postFXStack,
                    (int)setting.colorLutResolution, cameraSettings.keepAlpha,
                    cameraRendererTextures);
            }
            else if (useIntermediateBuffer)
            {
                FinalPass.Record(renderGraph, copier, cameraRendererTextures);
            }

            DebugPass.Recode(renderGraph, setting, renderCamera, lightResource);
            //绘制gizmos后绘制后处理
            //后处理后绘制Gizmos(绘制gizmos的地方换到了RenderGraphy)
            GizmosPass.Record(renderGraph, copier, cameraRendererTextures, useIntermediateBuffer);
        }

        //在命令提交之前请求清理
        context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
        context.Submit();
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(_cameraRendererMaterial);
        CameraDebugger.CleanUp();
    }
}