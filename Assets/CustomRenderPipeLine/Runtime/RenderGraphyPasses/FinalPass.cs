using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class FinalPass
{
    private static ProfilingSampler _finalSampler = new ProfilingSampler("FinalSampler");
    private CameraRender _render;

    private CameraSettings.FinalBlendMode _finalBlendMode;

    private void Render(RenderGraphContext context)
    {
        _render.DrawFinal(_finalBlendMode);
        _render.ExecuteCommandBuffer();
    }

    public static void Record(
        RenderGraph renderGraph, CameraRender cameraRender, CameraSettings.FinalBlendMode finalBlendMode)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Final", out FinalPass finalPass, _finalSampler);
        finalPass._render = cameraRender;
        finalPass._finalBlendMode = finalBlendMode;
        builder.SetRenderFunc<FinalPass>((pass, context) => pass.Render(context));
    }
}