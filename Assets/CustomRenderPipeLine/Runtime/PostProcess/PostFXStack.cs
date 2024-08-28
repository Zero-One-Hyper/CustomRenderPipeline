using UnityEngine;
using UnityEngine.Rendering;

public class PostFXStack
{
    //用于寻找pass
    public enum FXPass
    {
        Copy,
        BloomHorizontal,
        BloomVertical,
        BloomAdd,
        BloomScatter,
        BloomScatterFinal,
        BloomPrefilter,
        BloomPrefilterFireflies,
        ToneMappingNone,
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard,
        ApplyColorGrading,
        ApplyColorGradingWithAlpha,
        FinalRescale,
        FXAA,
        FXAAWithLuma,
    }

    private int _fxSourceID = UnityEngine.Shader.PropertyToID("_PostFXSource");

    //最终混合模式
    public static readonly int FinalSrcBlendID = UnityEngine.Shader.PropertyToID("_FinalSrcBlend");
    public static readonly int FinalDstBlendID = UnityEngine.Shader.PropertyToID("_FinalDstBlend");


    private static Rect _fullViewRect = new Rect(0f, 0f, 1f, 1f);

    public CameraBufferSettings CameraBufferSettings { get; set; }
    public Vector2Int BufferSize { get; set; }
    public Camera Camera { get; set; }
    public CameraSettings.FinalBlendMode FinalBlendMode { get; set; }
    public PostFXSettings PostFXSettings { get; set; }

    public void Draw(CommandBuffer cmd, RenderTargetIdentifier to, FXPass pass)
    {
        cmd.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cmd.DrawProcedural(Matrix4x4.identity, PostFXSettings.Material, (int)pass,
            MeshTopology.Triangles, 3);
    }

    public void Draw(CommandBuffer cmd, RenderTargetIdentifier from, RenderTargetIdentifier to, FXPass pass)
    {
        cmd.SetGlobalTexture(_fxSourceID, from);
        //使用SetRenderTarget重置视口以覆盖整个目标
        cmd.SetRenderTarget(to,
            //FinalBlendMode.destination == BlendMode.Zero ?
            RenderBufferLoadAction.DontCare,
            //: RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);
        cmd.DrawProcedural(Matrix4x4.identity, PostFXSettings.Material, (int)pass,
            MeshTopology.Triangles, 3);
    }

    public void DrawFinal(CommandBuffer cmd, RenderTargetIdentifier from, FXPass pass)
    {
        cmd.SetGlobalFloat(FinalSrcBlendID, (float)FinalBlendMode.source);
        cmd.SetGlobalFloat(FinalDstBlendID, (float)FinalBlendMode.destination);

        cmd.SetGlobalTexture(_fxSourceID, from);
        //使用SetRenderTarget重置视口以覆盖整个目标
        cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
            FinalBlendMode.destination == BlendMode.Zero && Camera.rect == _fullViewRect
                ? RenderBufferLoadAction.DontCare
                : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store); //为了使PostFX可以使用图层透明度 选择RenderBufferLoadAction.Load加载目标缓冲
        //再设置渲染目标后，最终绘制之前设置视口
        cmd.SetViewport(Camera.pixelRect);
        cmd.DrawProcedural(Matrix4x4.identity, PostFXSettings.Material,
            (int)pass, MeshTopology.Triangles, 3);
    }
}