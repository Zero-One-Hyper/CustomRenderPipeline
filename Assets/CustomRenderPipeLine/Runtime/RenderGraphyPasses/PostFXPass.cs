using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXPass
{
    private static ProfilingSampler _postFXSampler = new ProfilingSampler("PostFXSampler");
    private PostFXStack _postFXStack;

    private void Render(RenderGraphContext context)
    {
        _postFXStack.Render(context, CameraRender._colorAttachmentID);
    }

    public static void Record(RenderGraph renderGraph, PostFXStack postFXStack)
    {
        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass("Post FX", out PostFXPass postFXPass, _postFXSampler);
        postFXPass._postFXStack = postFXStack;
        builder.SetRenderFunc<PostFXPass>((pass, context) => pass.Render(context));
    }
}