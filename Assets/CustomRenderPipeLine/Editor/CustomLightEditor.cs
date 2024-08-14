using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    private static GUIContent _renderLayerMaskLabel = new GUIContent("Custom Rendering Layer Mask",
        "Functional version of above property");

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, _renderLayerMaskLabel);
        if (!settings.lightType.hasMultipleDifferentValues &&
            (LightType)settings.lightType.enumValueFlag == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle();
        }

        settings.ApplyModifiedProperties();

        var light = target as Light;
        if (light.cullingMask != -1)
        {
            EditorGUILayout.HelpBox(
                light.type == LightType.Directional
                    ? "Culling Mask only affects shadows."
                    : "Culling Mask only affects shadows unless Use lights PerObject is on",
                MessageType.Warning);
        }
    }
}