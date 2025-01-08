using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{
    //相机的复制颜色开关（用于热扭曲粒子效果）
    public bool copyColor = true;

    //相机的复制深度开关
    public bool copyDepth = true;

    //相机的掩码只会在Game窗口才看得见
    [HideInInspector]
    [Obsolete("使用新的RenderLayerMask替代")]
    public int renderingLayerMask = -1;

    public RenderingLayerMask newRenderLayerMask;

    //每个摄像机的灯光mask //开启后通过配置Layermask不同摄像机可以有不同的灯光效果
    public bool maskLights = false;

    public enum RenderScaleMode
    {
        Inherit,
        Multiple,
        Override
    }

    public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

    [Range(0.01f, 2.0f)]
    public float renderScale = 1.0f;

    //向摄像机添加可配置的最终混合模式
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source;
        public BlendMode destination;
    }

    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;

    public FinalBlendMode finalBlendMode = new FinalBlendMode()
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };

    public bool allowFXAA = false;
    public bool keepAlpha = true;

    public float GetRenderScale(float scale)
    {
        return this.renderScaleMode == RenderScaleMode.Inherit ? scale :
            this.renderScaleMode == RenderScaleMode.Override ? this.renderScale :
            scale * this.renderScale;
    }
}