Shader "Hidden/CustomRenderPipeLine/CustomCameraRender"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/Core/CustomRenderPipelineCommon.hlsl"
        #include "Assets/CustomRenderPipeLine/ShaderLibrary/PostProcess/CameraRender/CameraRenderPass.hlsl"
        ENDHLSL

        Pass
        {
            Name "Camera Copy"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment CopyPassFragment
            ENDHLSL
        }
        Pass
        {
            Name "Camera Copy Depth"
            ColorMask 0
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultFXVert
            #pragma fragment CopyDepthFragment
            ENDHLSL
        }
    }
}