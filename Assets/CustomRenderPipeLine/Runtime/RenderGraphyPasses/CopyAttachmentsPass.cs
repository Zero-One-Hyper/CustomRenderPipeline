using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CopyAttachmentsPass
{
    private static ProfilingSampler _copySampler = new ProfilingSampler("Copy Attanchments");

    private CameraRender _cameraRender;

    private void Render(RenderGraphContext context)
    {
        _cameraRender.CopyAttachments();
    }

    public static void Recode(RenderGraph renderGraph, CameraRender render)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Copy Attanchments", out CopyAttachmentsPass copyPass, _copySampler);
        copyPass._cameraRender = render;
        builder.SetRenderFunc<CopyAttachmentsPass>((pass, context) => pass.Render(context));
    }
}