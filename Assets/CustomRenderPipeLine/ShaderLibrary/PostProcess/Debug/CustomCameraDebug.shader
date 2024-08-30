Shader "Hidden/CustomRenderPipeLine/CustomCameraDebug"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/Debug/CameraDebugPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Forward+ Tiles"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DefaultPassVert
            #pragma fragment ForwardPlusTilesPassFragment
            ENDHLSL
        }
    }
}