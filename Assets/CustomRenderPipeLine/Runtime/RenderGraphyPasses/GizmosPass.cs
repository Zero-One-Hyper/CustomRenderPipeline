using System.Diagnostics;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GizmosPass
{
#if UNITY_EDITOR
    private static ProfilingSampler _gizmosSampler = new ProfilingSampler("GizmosSampler");

    //private CameraRender _render;
    private bool _requiresDepthCopy;
    private CameraRendererCopier _copier;
    private TextureHandle _depthTextureHandle;

    private void Render(RenderGraphContext context)
    {
        //if (_render.useIntermediateBuffer)
        //{
        //    _render.Draw(CameraRender._depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
        //    _render.ExecuteCommandBuffer();
        //}
        //context.renderContext.DrawGizmos(_render.camera, GizmoSubset.PreImageEffects);
        //context.renderContext.DrawGizmos(_render.camera, GizmoSubset.PostImageEffects);

        CommandBuffer buffer = context.cmd;
        ScriptableRenderContext renderContext = context.renderContext;
        if (_requiresDepthCopy)
        {
            _copier.CopyByDrawing(buffer, _depthTextureHandle, BuiltinRenderTextureType.CameraTarget, true);
            renderContext.ExecuteCommandBuffer(buffer);
        }

        renderContext.DrawGizmos(_copier.Camera, GizmoSubset.PreImageEffects);
        renderContext.DrawGizmos(_copier.Camera, GizmoSubset.PostImageEffects);
    }
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, CameraRendererCopier copier,
        in CameraRendererTextures rendererTextures, bool useIntermediateBuffer)
    {
#if UNITY_EDITOR
        if (Handles.ShouldRenderGizmos())
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                "Gizmos", out GizmosPass gizmosPass, _gizmosSampler);

            gizmosPass._copier = copier;
            gizmosPass._requiresDepthCopy = useIntermediateBuffer;
            if (useIntermediateBuffer)
            {
                gizmosPass._depthTextureHandle = builder.ReadTexture(rendererTextures.depthAttachment);
            }

            builder.SetRenderFunc<GizmosPass>((pass, context) => pass.Render(context));
        }
#endif
    }
}