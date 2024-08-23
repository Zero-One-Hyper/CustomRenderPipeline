using UnityEngine.Experimental.Rendering.RenderGraphModule;

//用于在各个RenderPass之间通讯
public readonly ref struct CameraRendererTextures
{
    public readonly TextureHandle colorAttachment;
    public readonly TextureHandle depthAttachment;
    public readonly TextureHandle colorCopy;
    public readonly TextureHandle depthCopy;

    public CameraRendererTextures(
        TextureHandle colorAttachment, TextureHandle depthAttachment,
        TextureHandle colorCopy, TextureHandle depthCopy)
    {
        this.colorAttachment = colorAttachment;
        this.depthAttachment = depthAttachment;
        this.colorCopy = colorCopy;
        this.depthCopy = depthCopy;
    }
}