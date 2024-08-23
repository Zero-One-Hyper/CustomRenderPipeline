using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class GeometryPass
{
    //LightMode可以随便自己定一个
    private static ShaderTagId[] _shaderTagId =
    {
        new ShaderTagId("CustomRenderPipelineLightMode"),
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("SRPDefaultUnlit")
    };

    private RendererListHandle _geometryListHandle;

    private static ProfilingSampler _geometryOpaqueSampler = new ProfilingSampler("Geometry Opaque Sampler");
    private static ProfilingSampler _geometryTransparentSampler = new ProfilingSampler("Geometry Transparent Sampler");

    private CameraRender _render;

    private bool _useDynamicBatching;
    private bool _useGPUInstancing;
    private bool _useLightsPerObject;

    private int _renderingLayerMask;

    private void Render(RenderGraphContext context)
    {
        /*
        _render.DrawVisibleGeometry(_useDynamicBatching, _useGPUInstancing, _useLightsPerObject,
            _renderingLayerMask);
        */
        context.cmd.DrawRendererList(_geometryListHandle);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }

    //这里弃用了useDynamicBatching 和 useGPUInstancing
    //使用RenderGraph时动态批处理会被始终禁用 GPU实例化会始终开启
    public static void Recode(RenderGraph renderGraph, Camera renderCamera, CullingResults cullingResults,
        bool useLightsPerObject, int renderingLayerMask, bool isOpaque)
    {
        ProfilingSampler sampler = isOpaque ? _geometryOpaqueSampler : _geometryTransparentSampler;
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Draw Visible Geometry", out GeometryPass geometryPass, sampler);

        //使用渲染器列表（RendererList）替代之前对绘制的过滤、排序设置
        RendererListHandle handle = renderGraph.CreateRendererList( //在renderGraph上创建渲染列表
            new RendererListDesc(_shaderTagId, cullingResults, renderCamera)
            {
                sortingCriteria = isOpaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
                //告诉管线将光照贴图的UV发送到着色器
                rendererConfiguration = PerObjectData.ReflectionProbes |
                                        PerObjectData.Lightmaps |
                                        PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume |
                                        PerObjectData.OcclusionProbe |
                                        PerObjectData.OcclusionProbeProxyVolume | //光照探针的阴影遮罩数据
                                        PerObjectData.ShadowMask |
                                        (useLightsPerObject
                                            ? PerObjectData.LightData | PerObjectData.LightIndices
                                            : PerObjectData.None),
                renderQueueRange = isOpaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,
                renderingLayerMask = (uint)renderingLayerMask,
            });
        //注册渲染列表Handle
        geometryPass._geometryListHandle = builder.UseRendererList(handle);

        builder.SetRenderFunc<GeometryPass>((pass, context) => pass.Render(context));
    }
    /*
    public static void Recode(RenderGraph renderGraph, CameraRender render,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Draw Visible Geometry", out GeometryPass visibleGeometryPass, _geometrySampler);
        visibleGeometryPass._render = render;
        visibleGeometryPass._useDynamicBatching = useDynamicBatching;
        visibleGeometryPass._useGPUInstancing = useGPUInstancing;
        visibleGeometryPass._useLightsPerObject = useLightsPerObject;
        visibleGeometryPass._renderingLayerMask = renderingLayerMask;
        builder.SetRenderFunc<GeometryPass>((pass, context) => pass.Render(context));
    }
    */
}