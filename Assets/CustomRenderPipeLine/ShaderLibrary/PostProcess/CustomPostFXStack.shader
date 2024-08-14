Shader "Hidden/CustomRenderPipeLine/Custom Post FX Stack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        HLSLINCLUDE
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/CustomPostFXPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Copy"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment CopyPassFrag
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Horizontal"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Bloom/CustomBloom.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Vertical"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Bloom/CustomBloom.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Add"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Bloom/CustomBloom.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment BloomAddPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Scatter"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Bloom/CustomBloom.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment BloomScatterPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Scatter Final"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Bloom/CustomBloom.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment BloomScatterFinalPassFragment
            ENDHLSL

        }
        Pass
        {
            Name "Bloom Prefilter"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Bloom/CustomBloom.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Bloom Prefilter Fireflies"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Bloom/CustomBloom.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment BloomPrefilterFirefliesPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Tone Mapping None"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment ToneMappingNoneFragment
            ENDHLSL
        }
        Pass
        {
            Name "Tone Mapping ACES"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment ToneMappingACESFragment
            ENDHLSL
        }
        Pass
        {
            Name "Tone Mapping Neutral"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment ToneMappingNeutralFragment
            ENDHLSL
        }
        Pass
        {
            Name "Tone Mapping Reinhard"
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment ToneMappingReinhardFragment
            ENDHLSL
        }
        Pass
        {
            Name "Apply Color Grading"
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment ApplyColorGradingPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Apply Color Grading with luma"//将亮度储存在a通道中(就可以不用每次采样都计算它了)
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment ApplyColorGradingWithLumaPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Final Copy Scale"
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment FinalCopyScalePassFragment
            ENDHLSL
        }
        Pass
        {
            Name "FXAA"
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/AA/FXAA.hlsl"
            #pragma multi_compile_fragment _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment FXAAPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "FXAA With Luma"
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            HLSLPROGRAM
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/ToneMapping/ToneMapping.hlsl"
            #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/AA/FXAA.hlsl"
            #pragma multi_compile_fragment _ _FXAA_ALPHA_CONTANTS_LUMA_
            #pragma multi_compile_fragment _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment FXAAPassFragment
            ENDHLSL
        }

    }
}