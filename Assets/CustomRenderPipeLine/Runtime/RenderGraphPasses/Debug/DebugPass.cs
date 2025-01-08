using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class DebugPass
{
    private static readonly ProfilingSampler Sampler = new ProfilingSampler("Debug Pass");

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Recode(RenderGraph renderGraph, CustomRenderPipelineSetting setting,
        Camera renderCamera, in LightResource lightResource)
    {
        if (CameraDebugger.IsActive && renderCamera.cameraType < CameraType.SceneView)// && !setting.useLightPerObject)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                Sampler.name, out DebugPass debugPass, Sampler);
            builder.ReadBuffer(lightResource.tilesBuffer);
            builder.SetRenderFunc<DebugPass>(
                static (pass, context) => CameraDebugger.Render(context));
        }
    }
}