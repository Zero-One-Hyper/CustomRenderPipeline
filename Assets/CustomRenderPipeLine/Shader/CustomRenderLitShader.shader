Shader "Custom/CustomRenderPipeLine/LitShader"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcBlend("SrcBlend", float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstBlend("DstBlend", float) = 0

        [Space(10)]
        [Enum(Off, 0, On, 1)]
        _ZWrite("ZWrite", Float) = 1

        [Space(10)]
        [Toggle]_ALPHATEST("__clip", Float) = 0.0
        _CutOff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Space(10)]
        [Toggle]_PREMULTIPLYALPHA("PreMultiply Alpha", float) = 0

        [Space(20)]
        [MainTexture]
        _MainTex ("Texture", 2D) = "white" {}
        [MainColor]
        _MainColor("MainColor", Color) = (0, 1, 1, 1)

        [Toggle(_MASK_MAP_ON)] _MaskMapToggle("Use Mask Tex", Float) = 0
        [NoScalOffset]
        _MaskTex("Mask", 2D) = "white"{}

        [Space(10)]
        _Metallic("Metallic", Range(0, 1)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Occlusion("Occlusin", Range(0, 1)) = 0.5

        [Space(10)]
        [Toggle(_NORMAL_MAP_ON)] _NormalMapToggle("Normal maps", float) = 0
        [NoScaleOffset]
        _NormalMap("NormalMap", 2D) = "bump"{}
        _NormalScale("NormalScale", Range(0, 1)) = 1

        [Space(10)]
        [NoScaleOffset]
        _EmissionMap("EmissionMap", 2D) = "black"{}
        [HDR]
        _EmissionColor("EmissionColor", Color) = (0, 0, 0, 1)

        [Toggle(_DETAIL_MAP_ON)] _DetailMapToggle("Use Detail Tex", Float) = 0
        _DetailMap("Detail", 2D) = "linearGray"{}
        _DetailNormalMap("DetailNormal", 2D) = "bump"{}
        _DetailNormalScale("DetailNormalScale", Range(0, 1)) = 1
        _DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
        _DetailSmoothness("Detail Smoothness", Range(0, 1)) = 0.5


        //用于烘培光照贴图（只需声明，须在GUI代码中与MainTex和MainColor连接）
        [HideInInspector]
        _BaseMap("BaseMap for LightMap", 2D) = "white"{}
        [HideInInspector]
        _BaseColor("BaseColor for lightMap", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }

        Blend [_SrcBlend] [_DstBlend]
        HLSLINCLUDE
        #pragma shader_feature _ _ALPHATEST_ON
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
        #include "Assets/CustomRenderPipeLine/Shader/CustomRenderLit.hlsl"
        ENDHLSL
        //这里本来想直接用URP的shader，但是由于Pipeline还在早期阶段，似乎部分URP的变体啥的还没有定义，无法正常工作
        //因此这里选择使用了buildin管线的shader
        Pass
        {
            Name "Forward"
            Tags
            {
                "LightMode"="CustomRenderPipelineLightMode"
            }
            ZWrite [_ZWrite]


            HLSLPROGRAM
            //着色器目标级别
            //#pragma target 3.5
            //使用了计算缓冲（ComputerBuffer) 现在再WebGl上不能使用了
            #pragma target 4.5

            //为玻璃材质准备的漫反射预乘alpha
            #pragma shader_feature_fragment _ _PREMULTIPLYALPHA_ON
            //软阴影采样大小
            //平行光
            //#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            //其他光源 点光源及聚光灯
            //#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
            #pragma multi_compile _ _SHADOW_FILTER_MEDIUM _SHADOW_FILTER_HIGH

            //软阴影、抖动混合 (软阴影)
            //#pragma multi_compile _  _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _SOFT_CASCADE_BLEND

            //光照贴图
            #pragma multi_compile _ LIGHTMAP_ON
            //使用ShadowMask
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE

            //著对象光照 使用灯光索引
            //#pragma multi_compile _ _LIGHTS_PER_OBJECT

            //LOD级别的交叉混合
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing

            #pragma shader_feature _ _NORMAL_MAP_ON
            #pragma shader_feature _ _MASK_MAP_ON
            #pragma shader_feature _ _DETAIL_MAP_ON

            #pragma vertex LitVert
            #pragma fragment LitFrag

            #include "Assets/CustomRenderPipeLine/Shader/CustomRenderLit.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode"="ShadowCaster"
            }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            //#pragma target 3.5
            #pragma target 4.5

            #pragma multi_compile _ _CASCADE_BLEND_DITHER
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing

            #pragma vertex ShadowCasterVert
            #pragma fragment ShadowCasterFrag

            #include "Assets/CustomRenderPipeLine/Shader/CustomRenderShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name"Custom Meta"
            Tags
            {
                //unity使用特殊的pass meta来确定烘培时的反射光
                "LightMode"="Meta"
            }
            Cull Off

            HLSLPROGRAM
            //#pragma target 3.5
            #pragma target 4.5
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #include "Assets/CustomRenderPipeLine/Shader/CustomRenderMetaPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "CustomRenderLitGUI"
}