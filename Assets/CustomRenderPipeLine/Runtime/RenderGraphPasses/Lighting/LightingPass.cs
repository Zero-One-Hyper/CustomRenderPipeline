using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class LightingPass
{
    private static ProfilingSampler _lightingSampler = new ProfilingSampler("LightingSampler");

    private const int MaxDirectionLightCount = 4;
    private const int MaxOtherLightCount = 64;

    private CommandBuffer _lightBuffer;

    private static int _directionLightCountID = UnityEngine.Shader.PropertyToID("_DirectionLightCount");
    private static int _directionLightDataID = UnityEngine.Shader.PropertyToID("_DirectionLightData");

    private static int _otherLightCountID = UnityEngine.Shader.PropertyToID("_OtherLightCount");
    private static int _otherLightDataID = UnityEngine.Shader.PropertyToID("_OtherLightData");

    //平行光设置
    private static readonly DirectionalLightData[] DirectionalLightDatas =
        new DirectionalLightData[MaxDirectionLightCount];

    //其他光设置
    private static readonly OtherLightData[] OtherLightDatas = new OtherLightData[MaxOtherLightCount];

    private static GlobalKeyword _lightsPerObjectKeyword = GlobalKeyword.Create("_LIGHTS_PER_OBJECT");

    //由于阴影渲染的延迟，我们需要跟踪这些变量
    private int _dirLightsCount;
    private int _otherLightsCount;
    private bool _useLightsPerObject;

    private CullingResults _cullingResults;

    private Shadows _shadows = new Shadows();

    //要让renderGraph管理计算缓冲 使用computerbufferHandle
    private ComputeBufferHandle _directionLightDataBuffer;
    private ComputeBufferHandle _otherLightDataBuffer;

    public static LightResource Recode(RenderGraph renderGraph,
        CullingResults cullingResults, ShadowSettings shadowSettings,
        bool useLightsPerObjects, int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Lighting Setup", out LightingPass lightingPass, _lightingSampler);
        lightingPass.SetUp(cullingResults, shadowSettings, useLightsPerObjects, renderingLayerMask);
        //用RenderGraph管理计算缓冲区
        lightingPass._directionLightDataBuffer = builder.WriteComputeBuffer(
            renderGraph.CreateComputeBuffer(new ComputeBufferDesc
            {
                name = "Direction Light Data",
                count = MaxDirectionLightCount,
                stride = DirectionalLightData.Stride,
            }));
        lightingPass._otherLightDataBuffer = builder.WriteComputeBuffer(
            renderGraph.CreateComputeBuffer(new ComputeBufferDesc
            {
                name = "Other Light Data",
                count = MaxOtherLightCount,
                stride = OtherLightData.Stride,
            }));

        builder.SetRenderFunc<LightingPass>(
            static (pass, context) => pass.Render(context));
        builder.AllowPassCulling(false); //没有使用阴影贴图时也不可以剔除 它还配置了所有光照数据

        return new LightResource(lightingPass._directionLightDataBuffer, lightingPass._otherLightDataBuffer,
            lightingPass.GetShadowTextures(renderGraph, builder));
    }

    private void Render(RenderGraphContext context)
    {
        _lightBuffer = context.cmd;
        //在setup中做相关配置，在render中设置关键字
        _lightBuffer.SetKeyword(_lightsPerObjectKeyword, _useLightsPerObject);
        //平行光
        _lightBuffer.SetGlobalInt(_directionLightCountID, _dirLightsCount);
        _lightBuffer.SetBufferData(_directionLightDataBuffer, DirectionalLightDatas,
            0, 0, _dirLightsCount);
        _lightBuffer.SetGlobalBuffer(_directionLightDataID, _directionLightDataBuffer);
        //其他光
        _lightBuffer.SetGlobalInt(_otherLightCountID, _otherLightsCount);
        _lightBuffer.SetBufferData(_otherLightDataBuffer, OtherLightDatas,
            0, 0, _otherLightsCount);
        _lightBuffer.SetGlobalBuffer(_otherLightDataID, _otherLightDataBuffer);

        //最后在light渲染结束使渲染shadow
        _shadows.Render(context);
        context.renderContext.ExecuteCommandBuffer(_lightBuffer);
        _lightBuffer.Clear();
    }

    private void SetUp(CullingResults cullingResults,
        ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask)
    {
        this._cullingResults = cullingResults;
        this._useLightsPerObject = useLightsPerObject;
        this._shadows.SetUp(cullingResults, shadowSettings);
        //在cull时unity也会计算那些光源会影响相机可视空间
        SetUpLights(renderingLayerMask);
        //现在只会在渲染光照后再去渲染阴影
    }

    private void SetUpLights(int renderingLayerMask)
    {
        //获取那些光源可以影响视锥体
        NativeArray<VisibleLight> visibleLights = this._cullingResults.visibleLights;
        //过滤灯光索引
        NativeArray<int> indexMap = _useLightsPerObject ? _cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        _dirLightsCount = 0;
        _otherLightsCount = 0;
        int i = 0;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            Light light = visibleLight.light;
            if ((renderingLayerMask & light.renderingLayerMask) != 0)
            {
                switch (visibleLight.lightType)
                {
                    case LightType.Directional:
                        //NativeArray<VisibleLight>一般很大，应用引用的模式传递
                        if (_dirLightsCount < MaxDirectionLightCount)
                        {
                            DirectionalLightDatas[_dirLightsCount] = new DirectionalLightData(
                                light, _shadows.ReserveDirectionalShadows(light, i), ref visibleLight);
                            _dirLightsCount++;
                        }

                        break;
                    case LightType.Point:
                        if (_otherLightsCount < MaxOtherLightCount)
                        {
                            newIndex = _otherLightsCount;
                            OtherLightDatas[_otherLightsCount] = CreatePointLight(light,
                                _shadows.ReserveOtherShadows(light, i), ref visibleLight);
                            _otherLightsCount++;
                        }

                        break;
                    case LightType.Spot:
                        if (_otherLightsCount < MaxOtherLightCount)
                        {
                            newIndex = _otherLightsCount;
                            OtherLightDatas[_otherLightsCount] = CreateSpotLight(light,
                                _shadows.ReserveOtherShadows(light, i), ref visibleLight);
                            _otherLightsCount++;
                        }

                        break;
                }
            }

            if (_useLightsPerObject)
            {
                indexMap[i] = newIndex;
            }
        }

        //著对象光照
        if (_useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                //消除其他所有看不到的光的索引
                indexMap[i] = -1;
            }

            //将调整后的索引map发送回unity
            _cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            //注意：启用著对象光照后 GPU实例化效率较低 因为只有灯光计数和索引列表匹配的对象才会分组
        }
    }

    private ShadowResource GetShadowTextures(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        return _shadows.GetResources(renderGraph, builder);
    }
}