using System.Diagnostics;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class UnSupportedShadersPass
{
#if UNITY_EDITOR
    private static ProfilingSampler _unSupportedSampler = new ProfilingSampler("UnSupportedSampler");
    private CameraRender _render;

    private void Render(RenderGraphContext context)
    {
        //针对不受支持的Shader的渲染代码放入了CamraRender.Edior中定义
        _render.DrawUnsupportedShaders(); //使用了partial定义
    }
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Recode(RenderGraph renderGraph, CameraRender render)
    {
#if UNITY_EDITOR
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "UnSupported Shaders", out UnSupportedShadersPass unSupportedShadersPass, _unSupportedSampler);
        unSupportedShadersPass._render = render;
        builder.SetRenderFunc<UnSupportedShadersPass>((pass, context) => pass.Render(context));
#endif
    }
}