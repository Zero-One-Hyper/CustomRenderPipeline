using System;
using UnityEngine;

[System.Serializable]
public struct CameraBufferSettings
{
    public bool allowHDR;
    public bool copyColor;
    public bool copyColorReflections;
    public bool copyDepth;
    public bool copyDepthReflections;

    [Range(0.1f, 2.0f)]
    public float renderScale; //使用FXAA时渲染比例最好是 4/3 这将使得像素增加1.78 而不是2倍渲染缩放带来的4

    public enum BicubicRescalingMode
    {
        Off,
        UpOnly,
        UpAndDown
    }

    public BicubicRescalingMode bicubicRescaling; //双三次采样

    [Serializable]
    public struct FXAA
    {
        public enum Quality
        {
            Low,
            Medium,
            High,
        }

        public bool enable;

        public Quality quality;

        [Range(0.0312f, 0.0833f)]
        public float fixedThreshold; //对比度范围混合阈值

        [Range(0.063f, 0.333f)]
        public float relativeThreshold; //最高亮度阈值(邻阈越亮，对比度必须越高)

        [Range(0f, 1.0f)]
        public float subpixelBlending; //混合强度
    }

    public FXAA fxaa;
}