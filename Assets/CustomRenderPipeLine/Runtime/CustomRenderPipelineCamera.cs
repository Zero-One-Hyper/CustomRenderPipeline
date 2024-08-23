using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    [SerializeField]
    private CameraSettings cameraSettings = default;

    private ProfilingSampler _sampler;

    public ProfilingSampler Sampler => _sampler ??= new ProfilingSampler(GetComponent<Camera>().name);

    public CameraSettings CameraSetting => cameraSettings ?? (cameraSettings = new CameraSettings());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnEnable()
    {
        _sampler = null;
    }
#endif
}