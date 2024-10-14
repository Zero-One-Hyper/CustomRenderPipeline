using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SetUpPass
{
    //手动添加内敛采样器 用于记录通道(不设置的话会自动生成但是会计算哈希)
    private static ProfilingSampler _setupSampler = new ProfilingSampler("SetUp");

    //private bool _useIntermediateAttachments;//现在总是使用中间缓冲区
    private TextureHandle _colorAttachment;
    private TextureHandle _depthAttachment;
    private Vector2Int _attachmentSize;
    private Camera _renderCamera;
    private CameraClearFlags _cameraClearFlags;

    //unity的_ScreenParams中的值与Camera的width和height绑定，若要使用RenderScale需要调整
    private static int _bufferSizeID = UnityEngine.Shader.PropertyToID("_CameraBufferSize");

    private void Render(RenderGraphContext context)
    {
        //这一步用来将摄像机的属性应用到context上，渲染天空盒的时候主要是设置VP矩阵（unity_MatrixVP）
        //有了这个指令在scene中选中摄像机时才不会黑屏
        context.renderContext.SetupCameraProperties(this._renderCamera);
        //清除标志位 在Recode中处理了
        CommandBuffer cmd = context.cmd;

        //if (_useIntermediateAttachments)
        //{
        //颜色缓冲和深度缓冲 在Recode中用builder.WriteTexture(renderGraph.CreateTexture(desc));创建了
        cmd.SetRenderTarget(
            //颜色
            _colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            //深度
            _depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //}

        //清除可能对接下来要画的东西有干扰的旧的内容
        cmd.ClearRenderTarget(
            _cameraClearFlags <= CameraClearFlags.Depth,
            _cameraClearFlags <= CameraClearFlags.Color,
            _cameraClearFlags == CameraClearFlags.Color
                ? _renderCamera.backgroundColor.linear
                : Color.clear);

        //延迟设置Camera缓冲区大小到setup结束
        cmd.SetGlobalVector(_bufferSizeID, new Vector4(
            1f / (float)_attachmentSize.x, 1f / (float)_attachmentSize.y,
            _attachmentSize.x, _attachmentSize.y));

        context.renderContext.ExecuteCommandBuffer(cmd);
        context.cmd.Clear();
    }

    public static CameraRendererTextures Recode(RenderGraph renderGraph, Camera renderCamera,
        Vector2Int attachmentSize, bool useHDR, //bool useIntermediateAttachments,
        bool copyColor, bool copyDepth)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Set Up", out SetUpPass setUpPass, _setupSampler);
        builder.AllowPassCulling(false); //Setup负责清除渲染目标，永远不应该剔除

        //setUpPass._useIntermediateAttachments = useIntermediateAttachments;
        setUpPass._attachmentSize = attachmentSize;
        setUpPass._renderCamera = renderCamera;
        setUpPass._cameraClearFlags = renderCamera.clearFlags;

        //TextureHandle colorAttachments;
        //TextureHandle depthAttachments;
        TextureHandle colorCopy = default;
        TextureHandle depthCopy = default;
        //if (useIntermediateAttachments)
        //{
        if (setUpPass._cameraClearFlags > CameraClearFlags.Color)
        {
            setUpPass._cameraClearFlags = CameraClearFlags.Color;
        }

        //创建纹理desc
        //颜色
        var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
        {
            colorFormat = SystemInfo.GetGraphicsFormat(
                useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
            name = "Color Attachment",
        };
        setUpPass._colorAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
        TextureHandle colorAttachments = setUpPass._colorAttachment;
        if (copyColor)
        {
            desc.name = "Color Copy";
            colorCopy = renderGraph.CreateTexture(desc);
        }

        //深度贴图
        desc.depthBufferBits = DepthBits.Depth32;
        desc.name = "Depth Attachment";
        setUpPass._depthAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
        TextureHandle depthAttachments = setUpPass._depthAttachment;
        if (copyDepth)
        {
            desc.name = "Depth Copy";
            depthCopy = renderGraph.CreateTexture(desc);
        }
        //}
        //lse
        //
        //   setUpPass._colorAttachment = setUpPass._depthAttachment =
        //       builder.WriteTexture(renderGraph.ImportBackbuffer(
        //           BuiltinRenderTextureType.CameraTarget));
        //   colorAttachments = setUpPass._colorAttachment;
        //   depthAttachments = setUpPass._depthAttachment;
        //

        builder.SetRenderFunc<SetUpPass>(
            static (pass, context) => pass.Render(context));

        return new CameraRendererTextures(colorAttachments, depthAttachments,
            colorCopy, depthCopy);
    }
}