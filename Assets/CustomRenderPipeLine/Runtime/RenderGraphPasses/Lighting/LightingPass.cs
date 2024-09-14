using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using Unity.Jobs;
using Unity.Burst;
using static Unity.Mathematics.math;
using float4 = Unity.Mathematics.float4;

public partial class LightingPass
{
    private static ProfilingSampler _lightingSampler = new ProfilingSampler("LightingSampler");

    private const int MaxDirectionLightCount = 4;
    private const int MaxOtherLightCount = 128;

    private CommandBuffer _lightBuffer;

    private static int _directionLightCountID = UnityEngine.Shader.PropertyToID("_DirectionLightCount");
    private static int _directionLightDataID = UnityEngine.Shader.PropertyToID("_DirectionLightData");

    private static int _otherLightCountID = UnityEngine.Shader.PropertyToID("_OtherLightCount");
    private static int _otherLightDataID = UnityEngine.Shader.PropertyToID("_OtherLightData");

    //图块设置
    private static int _tilesID = UnityEngine.Shader.PropertyToID("_ForwardPlusTiles");
    private static int _tileSettingsID = UnityEngine.Shader.PropertyToID("_ForwardPlusTileSettings");

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
    private ComputeBufferHandle _tilesBuffer;

    //使用来收集所有可见光的屏幕空间UV边界，用于确定光线覆盖了屏幕的那个区域
    private NativeArray<float4> _lightBounds;
    private NativeArray<int> _tileData; //图块数据组

    private JobHandle _forwardPlusJobHandle;

    private Vector2 _screenUVToTileCoordinates; //屏幕UV坐标转换因子
    private Vector2Int _tileCount;
    private int _maxLightPerTile;
    private int _tileDataSize;
    private int _maxTileDataSize;
    private int TileCount => _tileCount.x * _tileCount.y;

    public static LightResource Recode(RenderGraph renderGraph,
        CullingResults cullingResults, ShadowSettings shadowSettings, ForwardPlusSettings forwardPlusSettings,
        bool useLightsPerObjects, int renderingLayerMask, Vector2Int attachmentSize)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Lighting Setup", out LightingPass lightingPass, _lightingSampler);
        lightingPass.SetUp(cullingResults, shadowSettings, forwardPlusSettings,
            attachmentSize, useLightsPerObjects, renderingLayerMask);
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

        if (!useLightsPerObjects)
        {
            lightingPass._tilesBuffer = builder.WriteComputeBuffer(
                renderGraph.CreateComputeBuffer(new ComputeBufferDesc
                {
                    name = "Forward+ Tiles",
                    count = lightingPass.TileCount * lightingPass._maxTileDataSize,
                    stride = 4,
                }));
        }

        builder.SetRenderFunc<LightingPass>(
            static (pass, context) => pass.Render(context));
        builder.AllowPassCulling(false); //没有使用阴影贴图时也不可以剔除 它还配置了所有光照数据

        return new LightResource(lightingPass._directionLightDataBuffer,
            lightingPass._otherLightDataBuffer,
            lightingPass._tilesBuffer,
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
        if (_useLightsPerObject)
        {
            context.renderContext.ExecuteCommandBuffer(_lightBuffer);
            _lightBuffer.Clear();
            return;
        }

        //使用job 让unity决定何时执行job 
        //只需要在Render结束时得到结果
        _forwardPlusJobHandle.Complete();

        //设置GPU上的图块数据
        _lightBuffer.SetBufferData(_tilesBuffer, _tileData,
            0, 0, _tileData.Length);
        _lightBuffer.SetGlobalBuffer(_tilesID, _tilesBuffer);

        _lightBuffer.SetGlobalVector(_tileSettingsID, new Vector4(
            _screenUVToTileCoordinates.x, _screenUVToTileCoordinates.y,
            _tileCount.x.ReinterpretAsFloat(),
            _tileDataSize.ReinterpretAsFloat())); //这个地方也可以不用转成位模式的float

        context.renderContext.ExecuteCommandBuffer(_lightBuffer);
        _lightBuffer.Clear();
        _lightBounds.Dispose();
        _tileData.Dispose();
    }

    private void SetUp(CullingResults cullingResults, ShadowSettings shadowSettings,
        ForwardPlusSettings forwardPlusSettings,
        Vector2Int attachmentSize, bool useLightsPerObject, int renderingLayerMask)
    {
        this._cullingResults = cullingResults;
        this._useLightsPerObject = useLightsPerObject;
        this._shadows.SetUp(cullingResults, shadowSettings);

        if (!_useLightsPerObject)
        {
            _maxLightPerTile = forwardPlusSettings.maxLightPerTile <= 0 ? 31 : forwardPlusSettings.maxLightPerTile;
            //不再于SetUp中设置，而在计算灯光时设置     
            //_tileDataSize = _maxLightPerTile + 1;
            _maxTileDataSize = _maxLightPerTile + 1;

            _lightBounds = new NativeArray<float4>(
                MaxOtherLightCount, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            //使用Forward+渲染，需要在设置灯光之前确定图块的数量和屏幕uv坐标转换因子
            float tileScreenPixelSize = forwardPlusSettings.tileSize <= 0 ? 64f : (float)forwardPlusSettings.tileSize;
            //就是屏幕大小 / 图块数量
            _screenUVToTileCoordinates.x = attachmentSize.x / tileScreenPixelSize;
            _screenUVToTileCoordinates.y = attachmentSize.y / tileScreenPixelSize;
            _tileCount.x = Mathf.CeilToInt(_screenUVToTileCoordinates.x);
            _tileCount.y = Mathf.CeilToInt(_screenUVToTileCoordinates.y);
        }

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

        //确定每个图块所需要的最大灯光数量
        int requiredMaxLightsPerTile = Mathf.Min(_maxLightPerTile, visibleLights.Length);
        //Debug.Log(visibleLights.Length);
        _tileDataSize = requiredMaxLightsPerTile + 1;

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
                            SetUpForwardPlus(_otherLightsCount, ref visibleLight);
                            OtherLightDatas[_otherLightsCount] = OtherLightData.CreatePointLight(light,
                                _shadows.ReserveOtherShadows(light, i), ref visibleLight);
                            _otherLightsCount++;
                        }

                        break;
                    case LightType.Spot:
                        if (_otherLightsCount < MaxOtherLightCount)
                        {
                            newIndex = _otherLightsCount;
                            SetUpForwardPlus(_otherLightsCount, ref visibleLight);
                            OtherLightDatas[_otherLightsCount] = OtherLightData.CreateSpotLight(light,
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
        else
        {
            //使用Forward+
            _tileData = new NativeArray<int>(
                TileCount * _tileDataSize, Allocator.TempJob);
            //当前 场景中总共3盏光  _tileDataSize为4 分辨率为1920*1080 每块像素为64*64
            //在屏幕上分块向上取整为 (30，17) 即_tileCount
            //共scene共分510块 每块中有3+1(0，1，2，3)个数据
            //数据总数510*4=2040个
            _forwardPlusJobHandle = new ForwardPlusTilesJob()
            {
                lightBounds = _lightBounds,
                tileData = _tileData,
                otherLightCount = _otherLightsCount,
                tileScreenUVSize = float2(1f / _screenUVToTileCoordinates.x,
                    1f / _screenUVToTileCoordinates.y),
                maxLightsPerTile = requiredMaxLightsPerTile,
                tilePerRow = _tileCount.x,
                tileDataSize = _tileDataSize,
            }.ScheduleParallel(TileCount, _tileCount.x, default);
            // ScheduleParallel 会调度作业到多个工作线程上同时运行。
            // 此调度选项可以提供最佳性能，但是需要用户了解，在从多个工作线程同时访问相同数据时可能发生的冲突。
        }
    }

    private ShadowResource GetShadowTextures(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        return _shadows.GetResources(renderGraph, builder);
    }

    private void SetUpForwardPlus(int lightIndex, ref VisibleLight visibleLight)
    {
        //给定一个光索引和对其数据的引用。将 XY 边界最小值和最大值存储在 中float4。
        if (!_useLightsPerObject)
        {
            Rect rect = visibleLight.screenRect;
            _lightBounds[lightIndex] = float4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
        }
    }
}