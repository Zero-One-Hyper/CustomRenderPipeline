using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CameraDebugger
{
    private const string PanelName = "Forward+";

    private static readonly int OpacityID = UnityEngine.Shader.PropertyToID("_DebugOpacity");

    private static Material _debugMaterial;
    private static bool _showTiles;
    private static float _opacity = 0.5f;
    public static bool IsActive => _showTiles && _opacity > 0f;

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Initialize(UnityEngine.Shader shader)
    {
        _debugMaterial = CoreUtils.CreateEngineMaterial(shader);
        DebugManager.instance.GetPanel(PanelName, true).children.Add(
            new DebugUI.BoolField()
            {
                displayName = "Show Tiles",
                tooltip = "Whether the debug overlay is shown.",
                getter = static () => _showTiles,
                setter = static value => _showTiles = value,
            },
            new DebugUI.FloatField()
            {
                displayName = "Opacity",
                tooltip = "Opacity of the debug overlay.",
                min = static () => 0f,
                max = static () => 1f,
                getter = static () => _opacity,
                setter = static value => _opacity = value
            });
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void CleanUp()
    {
        CoreUtils.Destroy(_debugMaterial);
        DebugManager.instance.RemovePanel(PanelName);
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Render(RenderGraphContext context)
    {
        CommandBuffer buffer = context.cmd;
        buffer.SetGlobalFloat(OpacityID, _opacity);
        buffer.DrawProcedural(Matrix4x4.identity, _debugMaterial, 0,
            MeshTopology.Triangles, 3);
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}