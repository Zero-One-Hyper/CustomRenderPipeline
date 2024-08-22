using UnityEngine.Experimental.Rendering.RenderGraphModule;

public class PostFXPass
{
    private PostFXStack _postFXStack;

    private void Render(RenderGraphContext context)
    {
        _postFXStack.Render(CameraRender._colorAttachmentID);
    }

    public static void Record(RenderGraph renderGraph, PostFXStack postFXStack)
    {
        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass("Post FX", out PostFXPass postFXPass);
        postFXPass._postFXStack = postFXStack;
        builder.SetRenderFunc<PostFXPass>((pass, context) => pass.Render(context));
    }
}
