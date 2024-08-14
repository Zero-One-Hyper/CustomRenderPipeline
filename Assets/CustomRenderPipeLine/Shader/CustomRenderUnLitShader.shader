Shader "Custom/CustomRenderPipeLine/UnLitShader"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcBlend("SrcBlend", float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstBlend("DstBlend", float) = 1
        [Space(10)]
        [Enum(Off, 0, On, 1)]
        _ZWrite("ZWrite", Float) = 1
        [Space(10)]
        [Toggle]_ALPHATEST("__clip", Float) = 0.0
        _CutOff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Space(20)]
        _MainTex ("Texture", 2D) = "white" {}
        _MainColor("MainColor", Color) = (0, 1, 1, 1)
        [KeywordEnum(On, Clip, Dither, Off)]
        _Shadows ("Shadows", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        HLSLINCLUDE
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
        #include "Assets/CustomRenderPipeLine/Shader/CustomRenderUnLitInput.hlsl"
        ENDHLSL
        //这里本来想直接用URP的shader，但是由于Pipeline还在早期阶段，似乎部分URP的变体啥的还没有定义，无法正常工作
        //因此这里选择使用了buildin管线的shader
        Pass
        {
            Tags
            {
                "LightMode"="CustomRenderPipelineLightMode"
            }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]

            HLSLPROGRAM
            //着色器目标级别
            #pragma target 3.5

            #pragma shader_feature _ _ALPHATEST_ON
            #pragma multi_compile_instancing

            #pragma vertex UnLitVert
            #pragma fragment UnLitFrag

            #include "Assets/CustomRenderPipeLine/Shader/CustomRenderUnLit.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterVert
            #pragma fragment ShadowCasterFrag
            #include "Assets/CustomRenderPipeLine/Shader/CustomRenderShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags
            {
                "LightMode" = "Meta"
            }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #include "Assets/CustomRenderPipeLine/Shader/CustomRenderMetaPass.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "CustomShaderGUI"
}