using UnityEditor;
using UnityEngine;

partial class PostFXStack
{
    private partial void ApplySceneViewState();
    
#if UNITY_EDITOR

    private partial void ApplySceneViewState()
    {
        if (_camera.cameraType == CameraType.SceneView &&
            !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            _postFXSettings = null;
        }
    }
#endif
}