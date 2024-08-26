using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXPass
{
    private static ProfilingSampler _postFXSampler = new ProfilingSampler("PostFXSampler");
    private PostFXStack _postFXStack;
    private TextureHandle _colorHandle;

    private void Render(RenderGraphContext context)
    {
        _postFXStack.Render(context, _colorHandle);
    }

    public static void Record(RenderGraph renderGraph, PostFXStack postFXStack,
        in CameraRendererTextures rendererTextures)
    {
        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass("Post FX", out PostFXPass postFXPass, _postFXSampler);
        postFXPass._postFXStack = postFXStack;
        postFXPass._colorHandle = builder.ReadTexture(rendererTextures.colorAttachment);
        builder.SetRenderFunc<PostFXPass>((pass, context) => pass.Render(context));
    }
}