using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;

[CanEditMultipleObjects]
[CustomEditor(typeof(Camera))]
[SupportedOnRenderer(typeof(CustomRenderPipelineAsset))]
public class CustomCameraEditor : Editor
{

}
