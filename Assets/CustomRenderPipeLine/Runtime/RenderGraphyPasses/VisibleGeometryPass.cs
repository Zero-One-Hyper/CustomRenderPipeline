using UnityEngine.Experimental.Rendering.RenderGraphModule;

public class VisibleGeometryPass
{
    private CameraRender _render;

    private bool _useDynamicBatching;
    private bool _useGPUInstancing;
    private bool _useLightsPerObject;

    private int _renderingLayerMask;

    private void Render(RenderGraphContext context)
    {
        _render.DrawVisibleGeometry(_useDynamicBatching, _useGPUInstancing, _useLightsPerObject,
            _renderingLayerMask);
    }

    public static void Recode(RenderGraph renderGraph, CameraRender render,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Draw Visible Geometry", out VisibleGeometryPass visibleGeometryPass);
        visibleGeometryPass._render = render;
        visibleGeometryPass._useDynamicBatching = useDynamicBatching;
        visibleGeometryPass._useGPUInstancing = useGPUInstancing;
        visibleGeometryPass._useLightsPerObject = useLightsPerObject;
        visibleGeometryPass._renderingLayerMask = renderingLayerMask;
        builder.SetRenderFunc<VisibleGeometryPass>((pass, context) => pass.Render(context));
    }
}