using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
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

    public static void Recode(RenderGraph renderGraph, Camera renderCamera)
    {
        if (renderCamera.clearFlags == CameraClearFlags.Skybox)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                "Draw SkyBox", out SkyBoxPass skyBoxPass, _skyBoxSampler);
            skyBoxPass._camera = renderCamera;
            builder.SetRenderFunc<SkyBoxPass>((pass, context) => pass.Render(context));
        }
    }
}