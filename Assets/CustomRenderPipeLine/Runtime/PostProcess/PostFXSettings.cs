using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom/Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    private UnityEngine.Shader shader = default;

    [System.NonSerialized]
    private Material _material;

    public Material Material
    {
        get
        {
            if (_material == null && shader != null)
            {
                _material = new Material(shader);
                _material.hideFlags = HideFlags.HideAndDontSave;
            }

            return _material;
        }
    }

    [System.Serializable]
    public struct BloomSettings
    {
        public bool ignoreRenderScale;

        [Range(0, 16)]
        public int maxIterations;

        [Min(1f)]
        public int downScaleLimit;

        public bool bloomBicubicUpsampling;

        [Min(0.0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0.0f)]
        public float intensity;

        public bool fadeFireflies; //解决HDR+Bloom带来的萤火虫闪烁问题

        public enum BloomMode
        {
            Additive,
            Scattering
        }

        public BloomMode bloomMode;

        [Range(0.05f, 0.95f)]
        public float scatter;
    }

    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum ToneMappingMode
        {
            None,
            ACES,
            Neutral,
            Reinhard,
        }

        public ToneMappingMode toneMappingMode;
    }

    [Serializable]
    public struct ColorAdjustmentsSettings //颜色分级
    {
        public float postExposure; //曝光

        [Range(-100f, 100f)]
        public float contrast; //对比度

        [ColorUsage(false, true)]
        public Color colorFilter; //颜色滤镜

        [Range(-180f, 180f)]
        public float hueShift; //色相偏移

        [Range(-100, 100)]
        public float saturation; //饱和度
    }

    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)]
        public float temperature;

        [Range(-100f, 100f)]
        public float tint;
    }

    [Serializable]
    public struct SplitToningSettings //色调分离
    {
        [ColorUsage(false)]
        public Color shadows;

        [ColorUsage(false)]
        public Color highlights;

        [Range(-100, 100)]
        public float balance;
    }

    [Serializable]
    public struct ChannelMixerSettings //通道混合
    {
        public Vector3 red;
        public Vector3 green;
        public Vector3 blue;
    }

    [Serializable]
    public struct ShadowsMidtonesHighLightsSettings //调整阴影与高光的中间区域并使之解耦
    {
        [ColorUsage(false, true)]
        public Color shadow;

        [ColorUsage(false, true)]
        public Color midTone;

        [ColorUsage(false, true)]
        public Color highLights;

        [Range(0f, 2f)]
        public float shadowsStart;

        [Range(0f, 2f)]
        public float shadowsEnd;

        [Range(0f, 2f)]
        public float highLightsStart;

        [Range(0f, 2f)]
        public float highLightsEnd;
    }

    [SerializeField]
    private BloomSettings bloomSettings = new BloomSettings()
    {
        scatter = 0.7f,
    };

    [SerializeField]
    private ToneMappingSettings toneMappingSetting;

    [SerializeField]
    private ColorAdjustmentsSettings colorAdjustmentsSettings = new ColorAdjustmentsSettings()
    {
        colorFilter = Color.white,
    };

    [SerializeField]
    private WhiteBalanceSettings whiteBalanceSettings = default;

    [SerializeField]
    private SplitToningSettings splitToningSettings = new SplitToningSettings()
    {
        shadows = Color.gray,
        highlights = Color.gray,
    };

    [SerializeField]
    private ChannelMixerSettings channelMixerSettings = new ChannelMixerSettings()
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    [SerializeField]
    private ShadowsMidtonesHighLightsSettings shadowsMidtonesHighLightsSettings =
        new ShadowsMidtonesHighLightsSettings()
        {
            shadow = Color.white,
            midTone = Color.white,
            highLights = Color.white,
            shadowsStart = 0f,
            shadowsEnd = 0.3f,
            highLightsStart = 0.55f,
            highLightsEnd = 1f
        };

    public BloomSettings BloomSetting => bloomSettings;

    public ToneMappingSettings ToneMappingSetting => toneMappingSetting;

    public ColorAdjustmentsSettings ColorAdjustmentsSetting => colorAdjustmentsSettings;

    public WhiteBalanceSettings WhiteBalanceSetting => whiteBalanceSettings;

    public SplitToningSettings SplitToningSetting => splitToningSettings;

    public ChannelMixerSettings ChannelMixerSetting => channelMixerSettings;

    public ShadowsMidtonesHighLightsSettings ShadowsMidtonesHighLightsSetting => shadowsMidtonesHighLightsSettings;
}