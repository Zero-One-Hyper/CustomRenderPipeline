using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

public class UnSupportedShadersPass
{
#if UNITY_EDITOR

    //为了支持绘制过时的渲染器
    private static ShaderTagId[] _legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrePassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    private static Material _errorMaterial;

    private static ProfilingSampler _unSupportedSampler = new ProfilingSampler("UnSupportedSampler");

    private RendererListHandle _unSupprotedRenderListHandle;

    private void Render(RenderGraphContext context)
    {
        //转为使用RenderList绘制几何图形
        context.cmd.DrawRendererList(_unSupprotedRenderListHandle);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Recode(RenderGraph renderGraph, Camera renderCamera, CullingResults cullingResults)
    {
#if UNITY_EDITOR
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "UnSupported Shaders", out UnSupportedShadersPass unSupportedShadersPass, _unSupportedSampler);

        if (_errorMaterial == null)
        {
            //给一个错误材质
            _errorMaterial = new Material(UnityEngine.Shader.Find("Hidden/Core/FallbackError"));
        }

        //使用渲染器列表（RendererList）替代之前对绘制的过滤、排序设置
        RendererListHandle handle = renderGraph.CreateRendererList( //在renderGraph上创建渲染列表
            new RendererListDesc(_legacyShaderTagIds, cullingResults, renderCamera)
            {
                overrideMaterial = _errorMaterial,
                renderQueueRange = RenderQueueRange.all,
            });
        //注册渲染列表Handle
        unSupportedShadersPass._unSupprotedRenderListHandle = builder.UseRendererList(handle);

        builder.SetRenderFunc<UnSupportedShadersPass>((pass, context) => pass.Render(context));
#endif
    }
}