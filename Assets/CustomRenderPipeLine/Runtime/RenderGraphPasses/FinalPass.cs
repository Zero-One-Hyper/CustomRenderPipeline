using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class FinalPass
{
    private static ProfilingSampler _finalSampler = new ProfilingSampler("FinalSampler");

    private CameraRendererCopier _rendererCopier;
    private TextureHandle _colorAttachment;

    private void Render(RenderGraphContext context)
    {
        CommandBuffer buffer = context.cmd;
        _rendererCopier.CopyToCameraTarget(buffer, _colorAttachment);
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static void Record(RenderGraph renderGraph,
        CameraRendererCopier copier, in CameraRendererTextures rendererTextures)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Final", out FinalPass finalPass, _finalSampler);
        finalPass._rendererCopier = copier;
        finalPass._colorAttachment = builder.ReadTexture(rendererTextures.colorAttachment);
        builder.SetRenderFunc<FinalPass>(
            static (pass, context) => pass.Render(context));
    }
}