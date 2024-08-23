using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class Shadows
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

    //private const string BufferName = "Shadows";

    private const int MaxShadowsDirectionLightCount = 4;
    private const int MaxShadowsOtherLightCount = 16;

    //最大级联阴影数
    private const int MaxCascades = 4;

    //private CommandBuffer _shadowBuffer = new CommandBuffer() { name = BufferName, };
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
    private static int _otherShadowMatricesID = UnityEngine.Shader.PropertyToID("_OtherLightShadowMatrices");
    private static int _otherShadowTilesID = UnityEngine.Shader.PropertyToID("_OtherShadowTiles");

    //用于级联阴影球形剔除
    private static int _cascadeCountID = UnityEngine.Shader.PropertyToID("_CascadeCount");

    private static int _cascadeCullingSpheresId = UnityEngine.Shader.PropertyToID("_CascadeCullingSpheres");

    //用于消除不正确的条纹状阴影暗斑
    private static int _cascadeDataID = UnityEngine.Shader.PropertyToID("_CascadeData");

    //private static int _maxShadowDistanceID = UnityEngine.Shader.PropertyToID("_MaxShadowDistance");
    //使用计算好的淡化距离替换最大距离
    private static int _shadowDistanceFadeID = UnityEngine.Shader.PropertyToID("_ShadowDistanceFade");

    private static int _shadowPancakingID = UnityEngine.Shader.PropertyToID("_ShadowPancaking");

    //过滤器越大需要贴图的采样次数越多 因此需要知道图集尺寸和纹素尺寸
    private static int _shadowAtlasSizeID = UnityEngine.Shader.PropertyToID("_ShadowAtlasSize");


    private static Vector4[] _cascadeCullingSpheres = new Vector4[MaxCascades];
    private static Vector4[] _cascadeData = new Vector4[MaxCascades];

    private static Vector4[] _otherShadowTiles = new Vector4[MaxShadowsOtherLightCount];

    //使用到了级联阴影，所以每个变换矩阵都要乘4倍大小
    private static Matrix4x4[] _dirShadowMatrices = new Matrix4x4[MaxShadowsDirectionLightCount * MaxCascades];

    private static Matrix4x4[] _otherShadowMatrices = new Matrix4x4[MaxShadowsOtherLightCount];

    //软阴影过滤模式着色器变体
    //平行光
    private static string[] _directionalLightFilterKeyWords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    //其他光源
    private static string[] _otherLightFilterKeyWords =
    {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7"
    };

    //切换软阴影和抖动混合关键字
    private static string[] _cascadeBlendKeyWords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    //切换阴影遮罩关键子
    private static string[] _shadowMaskKeyWords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE",
    };

    private bool _useShadowMask;
    private Vector4 _shadowAtlasSize;

    public void SetUp(RenderGraphContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this._shadowBuffer = context.cmd;
        this._context = context.renderContext;
        this._cullingResults = cullingResults;
        this._shadowSettings = shadowSettings;
        _shadowDirectionalLightCount = 0;
        _shadowOtherLightCount = 0;
        _useShadowMask = false;
    }

    public void Render()
    {
        if (_shadowDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            //必须在领取一个纹理后才可以释放它，
            //但是如WebGL2.0，不领取纹理会出现问题，因为它将领取纹理和采样器绑在了一起
            //若纹理丢失会返回一个默认的纹理，这个纹理与阴影采样器是不兼容的
            //可以用一个shader变体解决，或者像下面这样获取一个1x1的纹理
            _shadowBuffer.GetTemporaryRT(_dirShadowAtlasID, 1, 1, 32, FilterMode.Bilinear,
                RenderTextureFormat.Shadowmap);
        }

        if (_shadowOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        else
        {
            //如果没有其他阴影们则需要提供一张虚拟纹理 这里直接给了直接光的纹理
            _shadowBuffer.SetGlobalTexture(_otherShadowAtlasID, _dirShadowAtlasID);
        }

        //_shadowBuffer.BeginSample(BufferName);
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
        //_shadowBuffer.EndSample(BufferName);
        ExecuteBuffer();
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
        _shadowBuffer.GetTemporaryRT(_dirShadowAtlasID, atlasSize, atlasSize,
            32, //深度缓冲的位数（unity的是16位的）这里搞大一点
            FilterMode.Bilinear, //过滤模式
            RenderTextureFormat.Shadowmap); //纹理渲染类型 渲染阴影必须是这个
        //指示GPU渲染这个纹理， 不关心初始状态，设置存储状态位store
        _shadowBuffer.SetRenderTarget(_dirShadowAtlasID, RenderBufferLoadAction.DontCare,
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

        _shadowBuffer.SetGlobalVectorArray(_cascadeCullingSpheresId, _cascadeCullingSpheres);
        //消除条状阴影暗斑
        _shadowBuffer.SetGlobalVectorArray(_cascadeDataID, _cascadeData);
        //渲染完所有有阴影的光源
        _shadowBuffer.SetGlobalMatrixArray(_dirShadowMatricesID, _dirShadowMatrices);
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
        _shadowBuffer.GetTemporaryRT(_otherShadowAtlasID, atlasSize, atlasSize,
            32, //深度缓冲的位数（unity的是16位的）这里搞大一点
            FilterMode.Bilinear, //过滤模式
            RenderTextureFormat.Shadowmap); //纹理渲染类型 渲染阴影必须是这个
        //指示GPU渲染这个纹理， 不关心初始状态，设置存储状态位store
        _shadowBuffer.SetRenderTarget(_otherShadowAtlasID, RenderBufferLoadAction.DontCare,
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
        _shadowBuffer.SetGlobalMatrixArray(_otherShadowMatricesID, _otherShadowMatrices);
        _shadowBuffer.SetGlobalVectorArray(_otherShadowTilesID, _otherShadowTiles);
        //过滤模式keyworlds只有3个
        SetShadowKeyWorlds(_otherLightFilterKeyWords, (int)_shadowSettings.otherLight.filterMode - 1);
        _shadowBuffer.EndSample("Other Shadows");
        ExecuteBuffer();
    }

    private void DoRenderDirectionalShadows(int lightIndex, int split, int tileSize)
    {
        ShadowedDirectionalLight shadowedDirectionalLight = _shadowedDirectionalLights[lightIndex];
        //渲染阴影的设置
        var shadowDrawingSettings = new ShadowDrawingSettings(_cullingResults,
            shadowedDirectionalLight.visibleLightIndex)
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
                Vector4 cullingSphere = shadowSplitData.cullingSphere;
                SetCascadeData(i, cullingSphere, tileSize);
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
        //渲染阴影的设置
        var shadowDrawingSettings = new ShadowDrawingSettings(_cullingResults,
            shadowedOtherLight.visibleLightIndex)
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
        SetOtherShadowTileData(lightIndex, offset, tileScale, bias);

        _otherShadowMatrices[lightIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale);
        _shadowBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
        _shadowBuffer.SetGlobalDepthBias(0f, shadowedOtherLight.slopeScaleBias);
        ExecuteBuffer();
        _context.DrawShadows(ref shadowDrawingSettings);
        _shadowBuffer.SetGlobalDepthBias(0f, 0f);
    }

    private void DoRenderPointShadows(int lightIndex, int split, int tileSize)
    {
        ShadowedOtherLight shadowedOtherLight = _shadowedOtherLights[lightIndex];
        //渲染阴影的设置
        var shadowDrawingSettings = new ShadowDrawingSettings(_cullingResults,
            shadowedOtherLight.visibleLightIndex)
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
            SetOtherShadowTileData(tileIndex, offset, tileScale, bias);

            _otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, tileScale);
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

    private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        //把求级联阴影倒数部分放到C#来做
        //_cascadeData[index].x = 1.0f / cullingSphere.w;
        //只考虑单个维度的话，沿表面法相偏移进行阴影采样 只需偏移世界空间下纹素大小
        float texelSize = 2.0f * cullingSphere.w / tileSize *
                          ((float)_shadowSettings.directionalLight.filter + 1.0f) * //使用PCF的过滤等级自动调整偏移
                          1.4142136f; //纹素时正方形，最坏情况下需要考虑斜边（需要最大）
        //先在C#中计算平方 （本来需要在在着色器中计算表面与球心距离的平方及半径的平方）
        cullingSphere.w -= texelSize;
        cullingSphere.w *= cullingSphere.w;
        _cascadeCullingSpheres[index] = cullingSphere;
        _cascadeData[index] = new Vector4(
            1.0f / cullingSphere.w, texelSize);
    }

    private void SetShadowKeyWorlds(string[] keyWords, int enableIndex)
    {
        for (int i = 0; i < keyWords.Length; i++)
        {
            if (i == enableIndex)
            {
                _shadowBuffer.EnableShaderKeyword(keyWords[i]);
            }
            else
            {
                _shadowBuffer.DisableShaderKeyword(keyWords[i]);
            }
        }
    }

    private void SetOtherShadowTileData(int index, Vector2 offset, float scale, float bias)
    {
        //保证钳位采样
        float border = _shadowAtlasSize.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        _otherShadowTiles[index] = data;
    }

    public void CleaUp()
    {
        //使用完临时纹理后需要释放它
        _shadowBuffer.ReleaseTemporaryRT(_dirShadowAtlasID); //直接光的阴影纹理一直都有
        if (_shadowOtherLightCount > 0)
        {
            _shadowBuffer.ReleaseTemporaryRT(_otherShadowAtlasID);
        }

        ExecuteBuffer();
    }
}