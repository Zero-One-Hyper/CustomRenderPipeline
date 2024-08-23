using UnityEngine.Experimental.Rendering.RenderGraphModule;

public class SetUpPass
{
    private CameraRender _render;

    private void Render(RenderGraphContext context)
    {
        _render.SetUp();
    }

    public static void Recode(RenderGraph renderGraph, CameraRender render)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Set Up", out SetUpPass setUpPass);
        setUpPass._render = render;
        builder.SetRenderFunc<SetUpPass>((pass, context) => pass.Render(context));
    }
}