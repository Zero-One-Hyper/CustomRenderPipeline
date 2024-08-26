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
        context.cmd.DrawRendererList(_geometryListHandle);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }

    //这里弃用了useDynamicBatching 和 useGPUInstancing
    //使用RenderGraph时动态批处理会被始终禁用 GPU实例化会始终开启
    public static void Recode(RenderGraph renderGraph, Camera renderCamera, CullingResults cullingResults,
        bool useLightsPerObject, int renderingLayerMask, bool isOpaque,
        in CameraRendererTextures rendererTextures, in ShadowTextures shadowTextures)
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

        //几何过程有可能使用复制纹理 所以始终读入写入纹理
        builder.ReadWriteTexture(rendererTextures.colorAttachment);
        builder.ReadWriteTexture(rendererTextures.depthAttachment);
        if (!isOpaque)
        {
            //如果副本存在，透明通道会从副本中读取。我们可以通过调用IsValid纹理句柄来检查这一点。
            //我们也不需要跟踪这些句柄，因为纹理已经全局设置了。
            if (rendererTextures.colorCopy.IsValid())
            {
                builder.ReadTexture(rendererTextures.colorCopy);
            }

            if (rendererTextures.depthCopy.IsValid())
            {
                builder.ReadTexture(rendererTextures.depthCopy);
            }
        }

        //获取阴影贴图 这里就是只要配置了才会使用(不错的资源管理方式)
        builder.ReadTexture(shadowTextures.directionalAtlas);
        builder.ReadTexture(shadowTextures.otherAtlas);

        builder.SetRenderFunc<GeometryPass>((pass, context) => pass.Render(context));
    }
}