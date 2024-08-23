using System.Diagnostics;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GizmosPass
{
#if UNITY_EDITOR
    private static ProfilingSampler _gizmosSampler = new ProfilingSampler("GizmosSampler");
    private CameraRender _render;

    private void Render(RenderGraphContext context)
    {
        if (_render.useIntermediateBuffer)
        {
            _render.Draw(CameraRender._depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
            _render.ExecuteCommandBuffer();
        }

        context.renderContext.DrawGizmos(_render.camera, GizmoSubset.PreImageEffects);
        context.renderContext.DrawGizmos(_render.camera, GizmoSubset.PostImageEffects);
    }
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, CameraRender cameraRender)
    {
#if UNITY_EDITOR
        if (Handles.ShouldRenderGizmos())
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                "Gizmos", out GizmosPass gizmosPass, _gizmosSampler);
            gizmosPass._render = cameraRender;
            builder.SetRenderFunc<GizmosPass>((pass, context) => pass.Render(context));
        }
#endif
    }
}