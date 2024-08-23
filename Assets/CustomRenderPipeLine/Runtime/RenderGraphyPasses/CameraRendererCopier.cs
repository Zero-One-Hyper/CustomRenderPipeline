using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public readonly struct CameraRendererCopier
{
    private static readonly int _sourceTextureID = UnityEngine.Shader.PropertyToID("_SourceTexture");

    //最终混合模式
    private static readonly int _finalSrcBlendID = UnityEngine.Shader.PropertyToID("_FinalSrcBlend");
    private static readonly int _finalDstBlendID = UnityEngine.Shader.PropertyToID("_FinalDstBlend");

    private readonly Material _cameraRenderMaterial;
    private readonly Camera _renderCamera;
    private readonly CameraSettings.FinalBlendMode _finalBlendMode;

    //默认的相机rect（位置）大小
    private static Rect _fullViewRect = new Rect(0f, 0f, 1f, 1f);

    //诸如WebGL上不能使用CopyTexture的平台使用着色器复制
    private static bool _copyTextureSupported =
        SystemInfo.copyTextureSupport > CopyTextureSupport.None;


    public static bool requiresRenderTargetResetAfterCopy = _copyTextureSupported;
    public readonly Camera Camera => _renderCamera;


    public CameraRendererCopier(Material material, Camera renderCamera,
        CameraSettings.FinalBlendMode finalBlendMode)
    {
        this._cameraRenderMaterial = material;
        this._renderCamera = renderCamera;
        this._finalBlendMode = finalBlendMode;
    }

    public readonly void Copy(CommandBuffer buffer,
        RenderTargetIdentifier from, RenderTargetIdentifier to,
        bool isDepth)
    {
        buffer.SetGlobalTexture(_sourceTextureID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.SetViewport(_renderCamera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, _cameraRenderMaterial, isDepth ? 1 : 0,
            MeshTopology.Triangles, 3);
    }

    public readonly void CopyByDrawing(CommandBuffer buffer, RenderTargetIdentifier from,
        RenderTargetIdentifier to, bool isDepth)
    {
        buffer.SetGlobalTexture(_sourceTextureID, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, _cameraRenderMaterial,
            isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    public readonly void CopyToCameraTarget(CommandBuffer buffer, RenderTargetIdentifier from)
    {
        buffer.SetGlobalFloat(_finalSrcBlendID, (float)_finalBlendMode.source);
        buffer.SetGlobalFloat(_finalDstBlendID, (float)_finalBlendMode.destination);
        buffer.SetGlobalTexture(_sourceTextureID, from);
        buffer.SetRenderTarget(
            BuiltinRenderTextureType.CameraTarget,
            _finalBlendMode.destination == BlendMode.Zero && _renderCamera.rect == _fullViewRect
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store
        );
        buffer.SetViewport(_renderCamera.pixelRect);
        buffer.DrawProcedural(
            Matrix4x4.identity, _cameraRenderMaterial, 0, MeshTopology.Triangles, 3
        );
        buffer.SetGlobalFloat(_finalSrcBlendID, 1f);
        buffer.SetGlobalFloat(_finalDstBlendID, 0f);
    }
}