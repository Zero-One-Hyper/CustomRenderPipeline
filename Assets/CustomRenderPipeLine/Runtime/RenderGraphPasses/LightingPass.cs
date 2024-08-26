using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class LightingPass
{
    private static ProfilingSampler _lightingSampler = new ProfilingSampler("LightingSampler");
    private Lighting _lighting;

    private CullingResults _cullingResults;

    private ShadowSettings _shadowSettings;

    private bool _useLightsPerObjet;

    private int _renderingLayerMask;

    private void Render(RenderGraphContext context)
    {
        _lighting.SetUp(context, _cullingResults, _shadowSettings,
            _useLightsPerObjet, _renderingLayerMask);
    }

    public static void Recode(RenderGraph renderGraph, Lighting lighting,
        CullingResults cullingResults, ShadowSettings shadowSettings,
        bool useLightsPerObjects, int renderingLayerMask)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Lighting Setup", out LightingPass lightingPass, _lightingSampler);
        lightingPass._lighting = lighting;
        lightingPass._cullingResults = cullingResults;
        lightingPass._shadowSettings = shadowSettings;
        lightingPass._useLightsPerObjet = useLightsPerObjects;
        lightingPass._renderingLayerMask = renderingLayerMask;
        builder.SetRenderFunc<LightingPass>((pass, context) => pass.Render(context));
    }
}