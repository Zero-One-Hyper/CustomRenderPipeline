using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CustomRenderPipelineSetting
{
    public CameraBufferSettings cameraBufferSettings = new CameraBufferSettings()
    {
        allowHDR = true,
        renderScale = 1f,
        fxaa = new CameraBufferSettings.FXAA()
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f,
        },
    };

    public bool useSRPBatcher = true;
    public bool useLightPerObject = true;

    /*
    [SerializeField]//使用RenderGraph时GPU实例化会始终开启
    private bool useGPUInstancing = true;

    [SerializeField]//动态批处理在使用RenderGraph时始终禁用(过于古老，而且相比较SRP合批及GPU实例化实在过于逊色)
    private bool useDynamicBaching = false;
    */

    public ShadowSettings shadowSettings;
    public PostFXSettings postFXSettings;

    public enum ColorLUTResolution
    {
        _16 = 16,
        _32 = 32,
        _64 = 64,
    }

    public ColorLUTResolution colorLutResolution = ColorLUTResolution._32;
    public UnityEngine.Shader cameraRenderShader;
    public UnityEngine.Shader cameraDebugShader;
}