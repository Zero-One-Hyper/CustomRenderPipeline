using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class LightingPass
{
    private static ProfilingSampler _lightingSampler = new ProfilingSampler("LightingSampler");
    //private Lighting _lighting = new Lighting();//lighting也不需要了·

    /*lighting现在自己追踪这些东西
    private CullingResults _cullingResults;

    private ShadowSettings _shadowSettings;

    private bool _useLightsPerObjet;

    private int _renderingLayerMask;
    */

    private const int MaxDirectionLightCount = 4;
    private const int MaxOtherLightCount = 64;

    private CommandBuffer _lightBuffer;

    private static int _directionLightCountID = UnityEngine.Shader.PropertyToID("_DirectionLightCount");
    private static int _directionLightColorsID = UnityEngine.Shader.PropertyToID("_DirectionLightColors");
    private static int _directionLightDirsAndMasksID = UnityEngine.Shader.PropertyToID("_DirectionLightDirsAndMasks");

    private static int _otherLightCountID = UnityEngine.Shader.PropertyToID("_OtherLightCount");
    private static int _otherLightColorsID = UnityEngine.Shader.PropertyToID("_OtherLightColors");
    private static int _otherLightPositionID = UnityEngine.Shader.PropertyToID("_OtherLightPosition");

    private static int _otherLightDirectionsAndMasksID =
        UnityEngine.Shader.PropertyToID("_OtherLightDirectionsAndMasks");

    private static int _otherLightSpotAnglesID = UnityEngine.Shader.PropertyToID("_OtherLightSpotAngles");


    private static int _directionShadowDataID = UnityEngine.Shader.PropertyToID("_DirectionShadowData");
    private static int _otherShadowDataID = UnityEngine.Shader.PropertyToID("_OtherShadowData");

    private static Vector4[] _directionLightColors = new Vector4[MaxDirectionLightCount];
    private static Vector4[] _directionLightDirsAndMasks = new Vector4[MaxDirectionLightCount];
    private static Vector4[] _directionShadowData = new Vector4[MaxDirectionLightCount];

    private static Vector4[] _otherLightColors = new Vector4[MaxOtherLightCount];
    private static Vector4[] _otherLightPosition = new Vector4[MaxOtherLightCount];
    private static Vector4[] _otherLightDirectionsAndMasks = new Vector4[MaxOtherLightCount];
    private static Vector4[] _otherLightSpotAngles = new Vector4[MaxOtherLightCount];
    private static Vector4[] _otherShadowData = new Vector4[MaxOtherLightCount];

    private static GlobalKeyword _lightsPerObjectKeyword = GlobalKeyword.Create("_LIGHTS_PER_OBJECT");

    //由于阴影渲染的延迟，我们需要跟踪这些变量
    private int _dirLightsCount;
    private int _otherLightsCount;
    private bool _useLightsPerObject;

    private CullingResults _cullingResults;

    private Shadows _shadows = new Shadows();

    //将lighting转移到了lightPass中
    //private void Render(RenderGraphContext context)
    //{
    //    _lighting.Render(context
    //        /*, _cullingResults, _shadowSettings,
    //        _useLightsPerObjet, _renderingLayerMask
    //        */);
    //}

    public static ShadowTextures Recode(RenderGraph renderGraph,
        //Lighting lighting, //lighting在lightingpass中new了
        CullingResults cullingResults, ShadowSettings shadowSettings,
        bool useLightsPerObjects, int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Lighting Setup", out LightingPass lightingPass, _lightingSampler);
        /*
        lightingPass._lighting = lighting;
        lightingPass._cullingResults = cullingResults;
        lightingPass._shadowSettings = shadowSettings;
        lightingPass._useLightsPerObjet = useLightsPerObjects;
        lightingPass._renderingLayerMask = renderingLayerMask;
        */
        lightingPass.SetUp(cullingResults, shadowSettings, useLightsPerObjects, renderingLayerMask);
        builder.SetRenderFunc<LightingPass>((pass, context) => pass.Render(context));
        builder.AllowPassCulling(false); //没有使用阴影贴图时也不可以剔除 它还配置了所有光照数据
        return lightingPass.GetShadowTextures(renderGraph, builder);
    }

    private void SetUp( //RenderGraphContext context, 
        CullingResults cullingResults,
        ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask)
    {
        //this._lightBuffer = context.cmd;
        this._cullingResults = cullingResults;
        this._useLightsPerObject = useLightsPerObject;
        this._shadows.SetUp(cullingResults, shadowSettings);
        //在cull时unity也会计算那些光源会影响相机可视空间
        SetUpLights( /*useLightsPerObject,*/ renderingLayerMask);
        /*现在只会在渲染光照后再去渲染阴影
        this._shadows.Render(context);
        context.renderContext.ExecuteCommandBuffer(_lightBuffer);
        this._lightBuffer.Clear();
        */
    }

    private void SetUpLights( /*bool useLightsPerObject,*/ int renderingLayerMask)
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
                            SetDirectionLight(_dirLightsCount, i, light, ref visibleLight);
                            _dirLightsCount++;
                        }

                        break;
                    case LightType.Point:
                        if (_otherLightsCount < MaxOtherLightCount)
                        {
                            newIndex = _otherLightsCount;
                            SetPointLight(_otherLightsCount, i, light, ref visibleLight);
                            _otherLightsCount++;
                        }

                        break;
                    case LightType.Spot:
                        if (_otherLightsCount < MaxOtherLightCount)
                        {
                            newIndex = _otherLightsCount;
                            SetSpotLight(_otherLightsCount, i, light, ref visibleLight);
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
            //改为在render中使用commandbuffer设置
            //注意：启用著对象光照后 GPU实例化效率较低 因为只有灯光计数和索引列表匹配的对象才会分组
            //UnityEngine.Shader.EnableKeyword(_lightsPerObjectKeyword);
        } /*
        else
        {
            //改为在render中使用commandbuffer设置
            //UnityEngine.Shader.DisableKeyword(_lightsPerObjectKeyword);
        }*/
    }

    private void Render(RenderGraphContext context)
    {
        _lightBuffer = context.cmd;
        //在setup中做相关配置，在render中设置关键字
        _lightBuffer.SetKeyword(_lightsPerObjectKeyword, _useLightsPerObject);

        _lightBuffer.SetGlobalInt(_directionLightCountID, _dirLightsCount);
        if (_dirLightsCount > 0)
        {
            _lightBuffer.SetGlobalVectorArray(_directionLightDirsAndMasksID, _directionLightDirsAndMasks);
            _lightBuffer.SetGlobalVectorArray(_directionLightColorsID, _directionLightColors);
            _lightBuffer.SetGlobalVectorArray(_directionShadowDataID, _directionShadowData);
        }

        _lightBuffer.SetGlobalInt(_otherLightCountID, _otherLightsCount);
        if (_otherLightsCount > 0)
        {
            _lightBuffer.SetGlobalVectorArray(_otherLightColorsID, _otherLightColors);
            _lightBuffer.SetGlobalVectorArray(_otherLightPositionID, _otherLightPosition);
            _lightBuffer.SetGlobalVectorArray(_otherLightDirectionsAndMasksID, _otherLightDirectionsAndMasks);
            _lightBuffer.SetGlobalVectorArray(_otherLightSpotAnglesID, _otherLightSpotAngles);
            _lightBuffer.SetGlobalVectorArray(_otherShadowDataID, _otherShadowData);
        }

        //最后在light渲染结束使渲染shadow
        _shadows.Render(context);
        context.renderContext.ExecuteCommandBuffer(_lightBuffer);
        _lightBuffer.Clear();
    }

    public ShadowTextures GetShadowTextures(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        return _shadows.GetShadowTextures(renderGraph, builder);
    }

    private void SetDirectionLight(int index, int visibleIndex, Light light, ref VisibleLight visibleLight)
    {
        //这里暂时没有给到一个主光源，后面需要添加
        _directionLightColors[index] = visibleLight.finalColor;
        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        _directionLightDirsAndMasks[index] = dirAndMask;
        _directionShadowData[index] = _shadows.ReserveDirectionalShadows(light, visibleIndex);
    }

    private void SetPointLight(int index, int visibleIndex, Light light, ref VisibleLight visibleLight)
    {
        _otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        //光的范围 使用衰减距离来平滑淡入淡出光线 max(0, 1 - (d^2/r^2)^2)^2
        //把坟墓范围放到position的w分量中去
        position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.000001f);
        _otherLightPosition[index] = position;
        _otherLightSpotAngles[index] = new Vector4(0.0f, 1.0f);
        Vector4 dirAndMask = Vector4.zero; //点光源没有方向
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        _otherLightDirectionsAndMasks[index] = dirAndMask;
        _otherShadowData[index] = _shadows.ReserveOtherShadows(light, visibleIndex);
    }

    private void SetSpotLight(int index, int visibleIndex, Light light, ref VisibleLight visibleLight)
    {
        _otherLightColors[index] = visibleLight.finalColor;
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        _otherLightPosition[index] = position;
        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        _otherLightDirectionsAndMasks[index] = dirAndMask;

        //计算聚光灯的角度
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1.0f / Mathf.Max(innerCos - outerCos, 0.00001f);
        _otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        _otherShadowData[index] = _shadows.ReserveOtherShadows(light, visibleIndex);
    }
    /*shadow已经不再使用cleanup清理了
    public void CleanUp()
    {
        _shadows.CleaUp();
    }
    */
}