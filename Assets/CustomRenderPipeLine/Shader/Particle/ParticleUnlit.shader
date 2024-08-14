Shader "Custom/CustomRenderPipeLine/Particles/Unlit"
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

        [Toggle(_VERTEXCOLORS)]
        _VertexColors("Vertex Color", float) = 0
        [Toggle(_FLIPBOOKBLENDING)]
        _FlipBoolBlending("FlipBoolBlending", Float) = 0

        [Toggle(_NEARFADE)]
        _NearFade("Near Fade", Float) = 1
        _NearFadeDistance("Near Fade Distance", Range(0, 10)) = 1
        _NearFadeRange("Near Fade Range", Range(0.01, 10)) = 1

        //软粒子 -- 透过粒子看到背后一定深度范围的物体
        [Toggle(_SOFTPARTIClES)]
        _SoftParticles("Soft Particles", Float) = 1
        _SoftParticlesDistance("Soft Particles Distance", Range(0.0, 10.0)) = 1
        _SoftParticlesRange("Soft Particles Range", Range(0.01, 10.0)) = 1

        //热扭曲粒子
        [Toggle(_DISTORTION)]
        _Distortion("Distortion", Float) = 1
        [NoScaleOffset][Normal]
        _DistortionNormal("Distortion Normal", 2D) = "bumb"{}
        _DistortionStrength("Distortion Strength", Range(0, 0.2)) = 0.1
        _DistortionBlend("Distortion Blend", Range(0.0, 1.0)) = 0

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
        //ulit粒子shader是由unlit拷贝而来
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
            #pragma shader_feature _ _VERTEXCOLORS
            #pragma shader_feature _ _FLIPBOOKBLENDING
            #pragma shader_feature _ _NEARFADE
            #pragma shader_feature _ _SOFTPARTIClES
            #pragma shader_feature _ _DISTORTION
            #pragma shader_feature _ _ALPHATEST
            #pragma multi_compile_instancing

            #pragma vertex UnLitVert
            #pragma fragment UnLitFrag

            #include "Assets/CustomRenderPipeLine/Shader/Particle/ParticleUnLitPass.hlsl"
            ENDHLSL
        }
    }
}