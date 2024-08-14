using System;
using UnityEngine;

[Serializable]
public class ShadowSettings
{
    //PCF 百分比渐进过滤 用于软阴影
    public enum FilterMode
    {
        PCF2x2,
        PCF3x3,
        PCF5x5,
        PCF7x7,
    }

    public enum TextureSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
        _8192 = 8192
    }

    [Serializable]
    public struct Directional
    {
        public enum CascadeBlendMode
        {
            Hard, //硬阴影
            Soft, //软阴影
            Dither, //抖动过度
        }

        public TextureSize atlasSize;
        public FilterMode filter;

        [Range(1, 4)]
        public int cascadeCount;

        [Range(0, 1)]
        public float cascadeRatio1;

        [Range(0, 1)]
        public float cascadeRatio2;

        [Range(0, 1)]
        public float cascadeRatio3;


        //也可以最最后一个级联阴影使用淡化
        [Range(0.0001f, 1)]
        public float cascadeFade; //淡化级联

        public CascadeBlendMode cascadeBlend;

        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
    }

    [Serializable]
    public struct OtherLight
    {
        public TextureSize atlasSize;
        public FilterMode filterMode;
    }

    [Min(0.0f)]
    public float maxDistance = 100.0f;

    [Range(0.00001f, 1)]
    public float shadowDistanceFade;

    public Directional directionalLight = new Directional()
    {
        atlasSize = TextureSize._1024,
        filter = FilterMode.PCF2x2,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        cascadeBlend = Directional.CascadeBlendMode.Hard,
    };

    public OtherLight otherLight = new OtherLight()
    {
        atlasSize = TextureSize._1024,
        filterMode = FilterMode.PCF5x5,
    };
}