using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    [SerializeField]
    private CameraSettings cameraSettings = default;

    public CameraSettings CameraSetting => cameraSettings ?? (cameraSettings = new CameraSettings());
}