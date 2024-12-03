using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SkyBoxPass
{
    private static ProfilingSampler _skyBoxSampler = new ProfilingSampler("SkyBox Sampler");

    private Camera _camera;

    private RendererListHandle _skyBoxListHandle;

    private void Render(RenderGraphContext context)
    {
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
        context.renderContext.DrawSkybox(_camera);
    }

    public static void Recode(RenderGraph renderGraph, Camera renderCamera,
        in CameraRendererTextures rendererTextures)
    {
        if (renderCamera.clearFlags == CameraClearFlags.Skybox)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                "Draw SkyBox", out SkyBoxPass skyBoxPass, _skyBoxSampler);
            skyBoxPass._camera = renderCamera;
            //天空盒需要读写颜色，读深度 不需要写深度
            builder.ReadWriteTexture(rendererTextures.colorAttachment);
            builder.ReadTexture(rendererTextures.depthAttachment);
            builder.SetRenderFunc<SkyBoxPass>(
                static (pass, context) => pass.Render(context));
        }
    }
}