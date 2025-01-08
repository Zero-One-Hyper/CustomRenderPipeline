using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class RenderingLayerMaskFieldAttribute : PropertyAttribute
{
}

/* 换成了unity6的新RenderingLayerMask  不再需要在Editor夏通过RenderingLayerMaskDrawer绘制层级关系
[CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
public class RenderingLayerMaskDrawer : PropertyDrawer
{
    public static void Draw(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int mask = property.intValue;
        bool isuint = property.type == "uint";
        if (isuint && mask == int.MaxValue)
        {
            mask = -1;
        }

        mask = EditorGUI.MaskField(rect, label, mask,
            GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);
        if (EditorGUI.EndChangeCheck())
        {
            property.intValue = isuint && mask == -1 ? int.MaxValue : mask;
        }

        EditorGUI.showMixedValue = false;
    }

    public static void Draw(SerializedProperty property, GUIContent label)
    {
        Draw(EditorGUILayout.GetControlRect(), property, label);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Draw(position, property, label);
    }
}
*/