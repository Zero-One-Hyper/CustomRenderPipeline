using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class Shadows
{
    private struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    private struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }


    private const int MaxShadowsDirectionLightCount = 4;
    private const int MaxShadowsOtherLightCount = 16;

    //最大级联阴影数
    private const int MaxCascades = 4;

    private CommandBuffer _shadowBuffer;

    private ScriptableRenderContext _context;
    private CullingResults _cullingResults;
    private ShadowSettings _shadowSettings;

    private ShadowedDirectionalLight[] _shadowedDirectionalLights =
        new ShadowedDirectionalLight[MaxShadowsDirectionLightCount];

    private ShadowedOtherLight[] _shadowedOtherLights = new ShadowedOtherLight[MaxShadowsOtherLightCount];

    //用于技术已经预留的光源数量
    private int _shadowDirectionalLightCount = 0;
    private int _shadowOtherLightCount = 0;

    private static int _dirShadowAtlasID = UnityEngine.Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int _dirShadowMatricesID = UnityEngine.Shader.PropertyToID("_DirectionalShadowMatrices");
    private static int _otherShadowAtlasID = UnityEngine.Shader.PropertyToID("_OtherLightShadowAtlas");
    private static int _otherShadowDataID = UnityEngine.Shader.PropertyToID("_OtherShadowData");

    //用于级联阴影球形剔除 其中directionShadowCascade中的CascadeData用于消除不正确的条纹状阴影暗斑
    private static int _directionShadowCascadeID =
        UnityEngine.Shader.PropertyToID("_DirectionShadowCascade");

    private static int _cascadeCountID = UnityEngine.Shader.PropertyToID("_CascadeCount");

    //使用计算好的淡化距离替换最大距离
    private static int _shadowDistanceFadeID = UnityEngine.Shader.PropertyToID("_ShadowDistanceFade");

    private static int _shadowPancakingID = UnityEngine.Shader.PropertyToID("_ShadowPancaking");

    //过滤器越大需要贴图的采样次数越多 因此需要知道图集尺寸和纹素尺寸
    private static int _shadowAtlasSizeID = UnityEngine.Shader.PropertyToID("_ShadowAtlasSize");

    private static DirectionShadowCascade[] _directionShadowCascades =
        new DirectionShadowCascade[MaxCascades];

    //使用到了级联阴影，所以每个变换矩阵都要乘4倍大小
    private static Matrix4x4[] _dirShadowMatrices = new Matrix4x4[MaxShadowsDirectionLightCount * MaxCascades];

    private static readonly OtherShadowData[] OtherShadowDatas = new OtherShadowData[MaxShadowsOtherLightCount];


    private ComputeBufferHandle _otherShadowDataBuffer;
    private ComputeBufferHandle _directionShadowCascadesBuffer;
    private ComputeBufferHandle _directionShadowMatricesBuffer;

    //软阴影过滤模式着色器变体
    //平行光
    private static GlobalKeyword[] _directionalLightFilterKeyWords =
    {
        GlobalKeyword.Create("_DIRECTIONAL_PCF3"),
        GlobalKeyword.Create("_DIRECTIONAL_PCF5"),
        GlobalKeyword.Create("_DIRECTIONAL_PCF7")
    };

    //其他光源
    private static GlobalKeyword[] _otherLightFilterKeyWords =
    {
        GlobalKeyword.Create("_OTHER_PCF3"),
        GlobalKeyword.Create("_OTHER_PCF5"),
        GlobalKeyword.Create("_OTHER_PCF7")
    };

    //切换软阴影和抖动混合关键字
    private static GlobalKeyword[] _cascadeBlendKeyWords =
    {
        GlobalKeyword.Create("_CASCADE_BLEND_SOFT"),
        GlobalKeyword.Create("_CASCADE_BLEND_DITHER")
    };

    //切换阴影遮罩关键子
    private static GlobalKeyword[] _shadowMaskKeyWords =
    {
        GlobalKeyword.Create("_SHADOW_MASK_ALWAYS"),
        GlobalKeyword.Create("_SHADOW_MASK_DISTANCE"),
    };

    private TextureHandle _directionalShadowAtlas;
    private TextureHandle _otherShadowAtlas;

    private bool _useShadowMask;
    private Vector4 _shadowAtlasSize;

    public void SetUp(CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        //需要在记录Graph时设置光照和阴影 而不是在执行Graph时设置 
        this._cullingResults = cullingResults;
        this._shadowSettings = shadowSettings;
        this._shadowDirectionalLightCount = 0;
        this._shadowOtherLightCount = 0;
        this._useShadowMask = false;
    }

    public void Render(RenderGraphContext context)
    {
        this._shadowBuffer = context.cmd;
        this._context = context.renderContext;

        if (_shadowDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }

        if (_shadowOtherLightCount > 0)
        {
            RenderOtherShadows();
        }


        //转移了 在render时才设置buffer
        _shadowBuffer.SetGlobalBuffer(_directionShadowCascadeID, _directionShadowCascadesBuffer);
        _shadowBuffer.SetGlobalBuffer(_dirShadowMatricesID, _directionShadowMatricesBuffer);
        _shadowBuffer.SetGlobalBuffer(_otherShadowDataID, _otherShadowDataBuffer);


        _shadowBuffer.SetGlobalTexture(_dirShadowAtlasID, _directionalShadowAtlas);
        _shadowBuffer.SetGlobalTexture(_otherShadowAtlasID, _otherShadowAtlas);

        SetShadowKeyWorlds(_shadowMaskKeyWords,
            _useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);

        //避免没有DirectionalLight时还在使用级联阴影
        _shadowBuffer.SetGlobalInt(_cascadeCountID,
            _shadowDirectionalLightCount > 0 ? _shadowSettings.directionalLight.cascadeCount : 0);
        //使用最后一级级联阴影淡化(因为在没有平行光时也需要fade 所以从RenderDirectionalShadows挪到外面)
        float f = 1.0f - _shadowSettings.directionalLight.cascadeFade;
        _shadowBuffer.SetGlobalVector(_shadowDistanceFadeID,
            new Vector4(1.0f / _shadowSettings.maxDistance,
                1.0f / _shadowSettings.shadowDistanceFade,
                1.0f / (1.0f - f * f))); //由于级联阴影使用了距离的平方来剔除，所以这里也要以1-f^2来使得f^2与fade结果呈现线性
        //这里直接fade和级联阴影fade一起使用了

        //图集尺寸 平行光存储在X分量中，纹素尺寸存储在Y分量中 其他光源的存储在z和w中
        _shadowBuffer.SetGlobalVector(_shadowAtlasSizeID, _shadowAtlasSize);
        ExecuteBuffer();
    }

    public ShadowResource GetResources(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        int atlasSize = (int)_shadowSettings.directionalLight.atlasSize;
        var desc = new TextureDesc(atlasSize, atlasSize)
        {
            depthBufferBits = DepthBits.Depth32,
            isShadowMap = true, //配置isShadowMap 可以避免分配 模板缓冲 阴影贴图不需要模板测试
            name = "Directional Shadow Atlas",
        };
        _directionalShadowAtlas = _shadowDirectionalLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(desc))
            : renderGraph.defaultResources.defaultShadowTexture;
        _directionShadowCascadesBuffer = builder.WriteComputeBuffer(
            renderGraph.CreateComputeBuffer(new ComputeBufferDesc
            {
                name = "Direction Light Shadow Cascades",
                stride = DirectionShadowCascade.Stride,
                count = MaxCascades,
            }));
        _directionShadowMatricesBuffer = builder.WriteComputeBuffer(
            renderGraph.CreateComputeBuffer(new ComputeBufferDesc
            {
                name = "Direction Light Shadow Matrix",
                stride = 4 * 16,
                count = MaxShadowsDirectionLightCount * MaxCascades,
            }));

        atlasSize = (int)_shadowSettings.otherLight.atlasSize;
        desc.width = atlasSize;
        desc.height = atlasSize;
        desc.name = "Other Shadow Atlas";
        _otherShadowAtlas = _shadowOtherLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(desc))
            : renderGraph.defaultResources.defaultShadowTexture;

        _otherShadowDataBuffer = builder.WriteComputeBuffer(
            renderGraph.CreateComputeBuffer(new ComputeBufferDesc
            {
                name = "Other Shadow Data",
                stride = OtherShadowData.Stride,
                count = MaxShadowsOtherLightCount,
            }));

        return new ShadowResource(_directionalShadowAtlas, _otherShadowAtlas,
            _directionShadowCascadesBuffer, _directionShadowMatricesBuffer, _otherShadowDataBuffer);
    }


    private void ExecuteBuffer()
    {
        _context.ExecuteCommandBuffer(_shadowBuffer);
        _shadowBuffer.Clear();
    }

    //创建阴影图集
    //平行光
    private void RenderDirectionalShadows()
    {
        //往一个纹理上绘制投射阴影
        int atlasSize = (int)_shadowSettings.directionalLight.atlasSize;
        //图集尺寸存储在X分量中，纹素尺寸存储在Y分量中
        _shadowAtlasSize.x = atlasSize;
        _shadowAtlasSize.y = 1.0f / atlasSize;
        //指示GPU渲染这个纹理， 不关心初始状态，设置存储状态位store
        _shadowBuffer.SetRenderTarget(_directionalShadowAtlas, RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);
        _shadowBuffer.ClearRenderTarget(true, false, Color.clear);

        //开始渲染
        _shadowBuffer.SetGlobalFloat(_shadowPancakingID, 1.0f); //设置pancaking
        _shadowBuffer.BeginSample("Directional Shadows");
        //最多支持4个光源，分割2就是横轴纵轴二分即4块
        //再加上级联阴影的数量
        int tiles = _shadowDirectionalLightCount * _shadowSettings.directionalLight.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for (int i = 0; i < _shadowDirectionalLightCount; i++)
        {
            DoRenderDirectionalShadows(i, split, tileSize);
        }

        //设置级联参数 参数中的cascade data 用于消除条状阴影暗斑
        _shadowBuffer.SetBufferData(_directionShadowCascadesBuffer, _directionShadowCascades,
            0, 0, _shadowSettings.directionalLight.cascadeCount);
        //_shadowBuffer.SetGlobalBuffer(_directionShadowCascadeID, _directionShadowCascadesBuffer);

        //渲染完所有有阴影的光源
        _shadowBuffer.SetBufferData(_directionShadowMatricesBuffer, _dirShadowMatrices,
            0, 0,
            _shadowDirectionalLightCount * _shadowSettings.directionalLight.cascadeCount);
        //_shadowBuffer.SetGlobalBuffer(_dirShadowMatricesID, _directionShadowMatricesBuffer);

        //过滤模式keywords只有3个
        SetShadowKeyWorlds(_directionalLightFilterKeyWords, (int)_shadowSettings.directionalLight.filter - 1);
        //软阴影 和 抖动混合 的keyWorld 
        SetShadowKeyWorlds(_cascadeBlendKeyWords, (int)_shadowSettings.directionalLight.cascadeBlend - 1);
        _shadowBuffer.EndSample("Directional Shadows");
        ExecuteBuffer();
    }

    //其余光源
    private void RenderOtherShadows()
    {
        //往一个纹理上绘制投射阴影
        int atlasSize = (int)_shadowSettings.otherLight.atlasSize;
        //图集尺寸存储在X分量中，纹素尺寸存储在Y分量中
        _shadowAtlasSize.z = atlasSize;
        _shadowAtlasSize.w = 1.0f / atlasSize;
        _shadowBuffer.SetRenderTarget(_otherShadowAtlas, RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);
        _shadowBuffer.ClearRenderTarget(true, false, Color.clear);

        //开始渲染
        _shadowBuffer.SetGlobalFloat(_shadowPancakingID, 0.0f);
        _shadowBuffer.BeginSample("Other Shadows");
        //最多支持4个光源，分割2就是横轴纵轴二分即4块
        //再加上级联阴影的数量
        int tiles = _shadowOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        for (int i = 0; i < _shadowOtherLightCount;)
        {
            if (_shadowedOtherLights[i].isPoint)
            {
                DoRenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                DoRenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }

        //渲染完所有有阴影的光源
        _shadowBuffer.SetBufferData(_otherShadowDataBuffer, OtherShadowDatas,
            0, 0, _shadowOtherLightCount);
        //_shadowBuffer.SetGlobalBuffer(_otherShadowDataID, _otherShadowDataBuffer);

        //过滤模式keyworlds只有3个
        SetShadowKeyWorlds(_otherLightFilterKeyWords, (int)_shadowSettings.otherLight.filterMode - 1);
        _shadowBuffer.EndSample("Other Shadows");
        ExecuteBuffer();
    }

    private void DoRenderDirectionalShadows(int lightIndex, int split, int tileSize)
    {
        ShadowedDirectionalLight shadowedDirectionalLight = _shadowedDirectionalLights[lightIndex];
        //渲染阴影的设置   阴影贴图的投影模式 平行光一般为BatchCullingProjectionType.Orthographic
        var shadowDrawingSettings = new ShadowDrawingSettings(_cullingResults,
            shadowedDirectionalLight.visibleLightIndex, BatchCullingProjectionType.Orthographic)
        {
            useRenderingLayerMaskTest = true,
        };

        int cascadeCount = _shadowSettings.directionalLight.cascadeCount;
        int tileOffset = lightIndex * cascadeCount;
        Vector3 ratios = _shadowSettings.directionalLight.CascadeRatios;
        float cullFactor = Mathf.Max(0f, 0.8f - _shadowSettings.directionalLight.cascadeFade);
        //从光源角度渲染场景，只存储深度信息
        //平行光被认为无限远，所以需要计算与光源方向匹配的观察和投影矩阵，并的出一个裁剪立方体
        //使用cullResult中的方法来做
        for (int i = 0; i < cascadeCount; i++)
        {
            _cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                shadowedDirectionalLight.visibleLightIndex, i,
                cascadeCount, ratios, tileSize, shadowedDirectionalLight.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix,
                out ShadowSplitData shadowSplitData);
            //取反视图矩阵第二行 以保证shadowmap为正常的上下顺序 解决漏光的问题
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
            //开启剔除偏移
            shadowSplitData.shadowCascadeBlendCullingFactor = cullFactor;
            //ShadowSplitData包括了关于投影物体如何被剔除的信息(阴影分割数据)
            shadowDrawingSettings.splitData = shadowSplitData;
            //级联阴影的信息都由ComputeDirectionalShadowMatricesAndCullingPrimitives计算出来
            //感觉搞了半天都在用unity的东西，没有自己实现的部分
            if (lightIndex == 0)
            {
                //这个cullingSphere是从摄像机出发计算的
                //每个光源的级联都是等价的  只用做一次
                _directionShadowCascades[i] = new DirectionShadowCascade(
                    shadowSplitData.cullingSphere, tileSize,
                    _shadowSettings.directionalLight.filter);
            }

            int tileIndex = tileOffset + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            float tileScale = 1.0f / split;
            //从世界空间变换到光源空间的矩阵
            _dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projMatrix * viewMatrix,
                offset,
                tileScale);

            _shadowBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);

            //SetGlobalDepthBias使用深度偏移解决条纹阴影问题 
            _shadowBuffer.SetGlobalDepthBias(0.0f, shadowedDirectionalLight.slopeScaleBias);
            //执行和清除commandbuffer的操作总是一起做的，所以每次做都要调用ExecuteBuffer来执行并清理
            ExecuteBuffer();
            _context.DrawShadows(ref shadowDrawingSettings);
            _shadowBuffer.SetGlobalDepthBias(0.0f, 0.0f);
        }
    }

    private void DoRenderSpotShadows(int lightIndex, int split, int tileSize)
    {
        ShadowedOtherLight shadowedOtherLight = _shadowedOtherLights[lightIndex];
        //渲染阴影的设置   阴影贴图的投影模式 点光源和聚光灯一般为BatchCullingProjectionType.Perspective
        var shadowDrawingSettings = new ShadowDrawingSettings(_cullingResults,
            shadowedOtherLight.visibleLightIndex, BatchCullingProjectionType.Perspective)
        {
            useRenderingLayerMaskTest = true,
        };

        _cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            shadowedOtherLight.visibleLightIndex,
            out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix,
            out ShadowSplitData shadowSplitData);
        shadowDrawingSettings.splitData = shadowSplitData;

        //解决物体距离灯光太远可能导致的条状阴影问题
        //对于聚光灯 在距离为1的平面上采样的纹素大小是2*tan(spot角一半)
        //这与透视阴影匹配  ......等看你懂了投影矩阵这块也许就懂了......
        float texelSize = 2.0f / (tileSize * projMatrix.m00);
        float filterSize = texelSize * ((float)_shadowSettings.otherLight.filterMode + 1);
        float bias = shadowedOtherLight.normalBias * filterSize * 1.414f;
        Vector2 offset = SetTileViewport(lightIndex, split, tileSize);
        float tileScale = 1.0f / split;

        OtherShadowDatas[lightIndex] = new OtherShadowData(offset, tileScale, bias,
            _shadowAtlasSize.w * 0.5f, ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale));

        _shadowBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
        _shadowBuffer.SetGlobalDepthBias(0f, shadowedOtherLight.slopeScaleBias);
        ExecuteBuffer();
        _context.DrawShadows(ref shadowDrawingSettings);
        _shadowBuffer.SetGlobalDepthBias(0f, 0f);
    }

    private void DoRenderPointShadows(int lightIndex, int split, int tileSize)
    {
        ShadowedOtherLight shadowedOtherLight = _shadowedOtherLights[lightIndex];
        //渲染阴影的设置  阴影贴图的投影模式 点光源和聚光灯一般为BatchCullingProjectionType.Perspective
        var shadowDrawingSettings = new ShadowDrawingSettings(_cullingResults,
            shadowedOtherLight.visibleLightIndex, BatchCullingProjectionType.Perspective)
        {
            useRenderingLayerMaskTest = true,
        };

        float texelSize = 2.0f / tileSize; //cubmap单位tile大小固定
        float filterSize = texelSize * ((float)_shadowSettings.otherLight.filterMode + 1);
        float bias = shadowedOtherLight.normalBias * filterSize * 1.414f;
        float tileScale = 1.0f / split;
        //点光源需要渲染6次 是一张CubMap
        for (int i = 0; i < 6; i++)
        {
            //使用fov偏移来消减立方体采样时出现的不连续问题
            float fovBias = Mathf.Atan(1.0f + bias + filterSize) * Mathf.Rad2Deg * 2.0f - 90.0f;
            _cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                shadowedOtherLight.visibleLightIndex, (CubemapFace)i, fovBias,
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix,
                out ShadowSplitData shadowSplitData);
            shadowDrawingSettings.splitData = shadowSplitData;

            int tileIndex = lightIndex + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);

            OtherShadowDatas[tileIndex] = new OtherShadowData(offset, tileScale, bias,
                _shadowAtlasSize.w * 0.5f, ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale));

            _shadowBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            _shadowBuffer.SetGlobalDepthBias(0f, shadowedOtherLight.slopeScaleBias);
            ExecuteBuffer();
            _context.DrawShadows(ref shadowDrawingSettings);
            _shadowBuffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    //为了采样阴影模型需要知道在图集中的位置 返回一个v2向量
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (_shadowDirectionalLightCount < MaxShadowsDirectionLightCount &&
            light.shadows != LightShadows.None &&
            light.shadowStrength > 0.0f)
        {
            //多光源时（四个以下）将每个光源Mask数据扔进一个通道中去
            float maskChannel = -1f;
            //知道是否使用了shadowMask烘培
            LightBakingOutput lightBaking = light.bakingOutput;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                //正在使用阴影遮罩
                _useShadowMask = true;
                //取得该光源的mask所在通道
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            //先确定有没有使用ShadowMask，再检查有没有阴影投射器(超出阴影距离，需要使用shadowMask)
            if (!_cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds shadowBounds))
            {
                //这里强度取反是因为shader中阴影强度大于0时会采样阴影贴图，当没有阴影投射器时不需要采样阴影贴图
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }

            _shadowedDirectionalLights[_shadowDirectionalLightCount] =
                new ShadowedDirectionalLight()
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane,
                };
            //级联阴影多乘个级数
            Vector4 res = new Vector4(light.shadowStrength,
                _shadowDirectionalLightCount * _shadowSettings.directionalLight.cascadeCount,
                light.shadowBias,
                maskChannel);
            _shadowDirectionalLightCount++;
            return res;
        }

        return new Vector4(0f, 0f, 0f, -1f);
    }

    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        float maskChannel = -1.0f;
        LightBakingOutput lightBaking = light.bakingOutput;
        if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            _useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        bool isPointLight = light.type == LightType.Point;
        int newLightCount = _shadowOtherLightCount + (isPointLight ? 6 : 1);
        if (newLightCount >= MaxShadowsOtherLightCount ||
            !_cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds))
        {
            //检查是否超过最大计数、是否为不需要渲染的阴影
            return new Vector4(-light.shadowStrength, 1f, 0f, maskChannel);
        }

        //复制数据
        _shadowedOtherLights[_shadowOtherLightCount] = new ShadowedOtherLight()
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPointLight,
        };
        Vector4 data = new Vector4(light.shadowStrength, _shadowOtherLightCount,
            isPointLight ? 1f : 0f, maskChannel);
        _shadowOtherLightCount = newLightCount;
        return data;
    }

    private Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        _shadowBuffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize,
            tileSize, tileSize));
        return offset;
    }

    //因为使用了阴影图集
    private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 matrix, Vector2 offset, float scale)
    {
        //判断是否使用了反向z缓冲
        //OpenGL与其他图形学API对深度从0-1还是-1-0有不同的设置
        if (SystemInfo.usesReversedZBuffer)
        {
            matrix.m20 *= -1f;
            matrix.m21 *= -1f;
            matrix.m22 *= -1f;
            matrix.m23 *= -1f;
        }

        //（裁剪空间在-1，1之间）需要将矩阵的维度转换到0，1之间 （*0.5+0.5）
        matrix.m00 = (0.5f * (matrix.m00 + matrix.m30) + offset.x * matrix.m30) * scale;
        matrix.m01 = (0.5f * (matrix.m01 + matrix.m31) + offset.x * matrix.m31) * scale;
        matrix.m02 = (0.5f * (matrix.m02 + matrix.m32) + offset.x * matrix.m32) * scale;
        matrix.m03 = (0.5f * (matrix.m03 + matrix.m33) + offset.x * matrix.m33) * scale;
        matrix.m10 = (0.5f * (matrix.m10 + matrix.m30) + offset.y * matrix.m30) * scale;
        matrix.m11 = (0.5f * (matrix.m11 + matrix.m31) + offset.y * matrix.m31) * scale;
        matrix.m12 = (0.5f * (matrix.m12 + matrix.m32) + offset.y * matrix.m32) * scale;
        matrix.m13 = (0.5f * (matrix.m13 + matrix.m33) + offset.y * matrix.m33) * scale;
        matrix.m20 = 0.5f * (matrix.m20 + matrix.m30);
        matrix.m21 = 0.5f * (matrix.m21 + matrix.m31);
        matrix.m22 = 0.5f * (matrix.m22 + matrix.m32);
        matrix.m23 = 0.5f * (matrix.m23 + matrix.m33);

        return matrix;
    }

    private void SetShadowKeyWorlds(GlobalKeyword[] keyWords, int enableIndex)
    {
        for (int i = 0; i < keyWords.Length; i++)
        {
            _shadowBuffer.SetKeyword(keyWords[i], i == enableIndex);
        }
    }
}