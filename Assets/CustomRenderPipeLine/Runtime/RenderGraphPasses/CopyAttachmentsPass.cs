using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CopyAttachmentsPass
{
    private static ProfilingSampler _copySampler = new ProfilingSampler("Copy Attanchments");

    private static int _colorTextureID = UnityEngine.Shader.PropertyToID("_CameraColorTexture");
    private static int _depthTextureID = UnityEngine.Shader.PropertyToID("_CameraDepthTexture");

    private bool _copyColor;
    private bool _copyDepth;

    private CameraRendererCopier _cameraRendererCopier;

    private TextureHandle _colorAttachments;
    private TextureHandle _depthAttachments;
    private TextureHandle _colorCopy;
    private TextureHandle _depthCopy;

    private void Render(RenderGraphContext context)
    {
        CommandBuffer cmd = context.cmd;
        if (_copyColor)
        {
            _cameraRendererCopier.Copy(cmd, _colorAttachments, _colorCopy, false);
            cmd.SetGlobalTexture(_colorTextureID, _colorCopy);
        }

        if (_copyDepth)
        {
            _cameraRendererCopier.Copy(cmd, _depthAttachments, _depthCopy, true);
            cmd.SetGlobalTexture(_depthTextureID, _depthCopy);
        }

        if (CameraRendererCopier.requiresRenderTargetResetAfterCopy)
        {
            //Draw改变了渲染目标，因此需要重新设置回相机缓冲
            cmd.SetRenderTarget(
                _colorAttachments, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                _depthAttachments, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        }

        context.renderContext.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    public static void Recode(RenderGraph renderGraph, CameraRendererCopier copier,
        //使用in关键字表明之从中读取
        in CameraRendererTextures textures, bool copyColor, bool copyDepth)
    {
        if (copyColor || copyDepth)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                "Copy Attachments", out CopyAttachmentsPass copyPass, _copySampler);
            copyPass._copyColor = copyColor;
            copyPass._copyDepth = copyDepth;
            copyPass._cameraRendererCopier = copier;

            copyPass._colorAttachments = builder.ReadTexture(textures.colorAttachment);
            copyPass._depthAttachments = builder.ReadTexture(textures.depthAttachment);
            if (copyColor)
            {
                copyPass._colorCopy = builder.WriteTexture(textures.colorCopy);
            }

            if (copyDepth)
            {
                copyPass._depthCopy = builder.WriteTexture(textures.depthCopy);
            }

            builder.SetRenderFunc<CopyAttachmentsPass>((pass, context) => pass.Render(context));
        }
    }
}