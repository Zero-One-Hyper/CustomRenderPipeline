using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class FinalPass
{
    private static ProfilingSampler _finalSampler = new ProfilingSampler("FinalSampler");

    //private CameraRender _render;
    //private CameraSettings.FinalBlendMode _finalBlendMode;

    private CameraRendererCopier _rendererCopier;
    private TextureHandle _colorAttachment;

    private void Render(RenderGraphContext context)
    {
        //_render.DrawFinal(_finalBlendMode);
        //_render.ExecuteCommandBuffer();
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
        // finalPass._render = cameraRender;
        // finalPass._finalBlendMode = finalBlendMode;
        finalPass._rendererCopier = copier;
        finalPass._colorAttachment = builder.ReadTexture(rendererTextures.colorAttachment);
        builder.SetRenderFunc<FinalPass>((pass, context) => pass.Render(context));
    }
}