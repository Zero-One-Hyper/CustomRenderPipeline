using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SetUpPass
{
    //手动添加内敛采样器 用于记录通道(不设置的话会自动生成但是会计算哈希)
    private static ProfilingSampler _setupSampler = new ProfilingSampler("SetUp");
    private CameraRender _render;

    private void Render(RenderGraphContext context)
    {
        _render.SetUp();
    }

    public static void Recode(RenderGraph renderGraph, CameraRender render)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Set Up", out SetUpPass setUpPass, _setupSampler);
        setUpPass._render = render;
        builder.SetRenderFunc<SetUpPass>((pass, context) => pass.Render(context));
    }
}