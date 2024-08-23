using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class SetUpPass
{
    //手动添加内敛采样器 用于记录通道(不设置的话会自动生成但是会计算哈希)
    private static ProfilingSampler _setupSampler = new ProfilingSampler("SetUp");
    // private CameraRender _render; 消除对CameraRender的依赖

    private bool _useIntermediateAttanchments;
    private TextureHandle _colorAttanchment;
    private TextureHandle _depthAttanchment;
    private Vector2Int _attanchmentSize;
    private Camera _renderCamera;
    private CameraClearFlags _cameraClearFlags;

    //unity的_ScreenParams中的值与Camera的width和height绑定，若要使用RenderScale需要调整
    private static int _bufferSizeID = UnityEngine.Shader.PropertyToID("_CameraBufferSize");


    private void Render(RenderGraphContext context)
    {
        //_render.SetUp();
        //这一步用来将摄像机的属性应用到context上，渲染天空盒的时候主要是设置VP矩阵（unity_MatrixVP）
        //有了这个指令在scene中选中摄像机时才不会黑屏
        context.renderContext.SetupCameraProperties(this._renderCamera);
        //清除标志位 在Recode中处理了
        CommandBuffer cmd = context.cmd;

        if (_useIntermediateAttanchments)
        {
            //颜色缓冲和深度缓冲 在Recode中用builder.WriteTexture(renderGraph.CreateTexture(desc));创建了
            cmd.SetRenderTarget(
                //颜色
                _colorAttanchment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                //深度
                _depthAttanchment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        //清除可能对接下来要画的东西有干扰的旧的内容
        cmd.ClearRenderTarget(
            _cameraClearFlags <= CameraClearFlags.Depth,
            _cameraClearFlags <= CameraClearFlags.Color,
            _cameraClearFlags == CameraClearFlags.Color
                ? _renderCamera.backgroundColor.linear
                : Color.clear);

        //延迟设置Camera缓冲区大小到setup结束
        cmd.SetGlobalVector(_bufferSizeID, new Vector4(
            1f / _attanchmentSize.x, 1f / _attanchmentSize.y,
            _attanchmentSize.x, _attanchmentSize.y));

        context.renderContext.ExecuteCommandBuffer(cmd);
        context.cmd.Clear();
    }

    public static CameraRendererTextures Recode(RenderGraph renderGraph, Camera renderCamera,
        Vector2Int attanchmentSize, bool useIntermediateAttanchments, bool useHDR,
        bool copyColor, bool copyDepth)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            "Set Up", out SetUpPass setUpPass, _setupSampler);
        builder.AllowPassCulling(false); //Setup负责清除渲染目标，永远不应该剔除

        setUpPass._useIntermediateAttanchments = useIntermediateAttanchments;
        setUpPass._attanchmentSize = attanchmentSize;
        setUpPass._renderCamera = renderCamera;
        setUpPass._cameraClearFlags = renderCamera.clearFlags;

        TextureHandle colorAttanchments;
        TextureHandle depthAttanchments;
        TextureHandle colorCopy = default;
        TextureHandle depthCopy = default;
        if (useIntermediateAttanchments)
        {
            if (setUpPass._cameraClearFlags > CameraClearFlags.Color)
            {
                setUpPass._cameraClearFlags = CameraClearFlags.Color;
            }

            //创建纹理desc
            //颜色
            var desc = new TextureDesc(attanchmentSize.x, attanchmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(
                    useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
                name = "Color Attanchment",
            };
            setUpPass._colorAttanchment = builder.WriteTexture(renderGraph.CreateTexture(desc));
            colorAttanchments = setUpPass._colorAttanchment;
            if (copyColor)
            {
                desc.name = "Color Copy";
                colorCopy = renderGraph.CreateTexture(desc);
            }

            //深度贴图
            desc.depthBufferBits = DepthBits.Depth32;
            desc.name = "Depth Attanchment";
            setUpPass._depthAttanchment = builder.WriteTexture(renderGraph.CreateTexture(desc));
            depthAttanchments = setUpPass._depthAttanchment;
            if (copyDepth)
            {
                desc.name = "Depth Copy";
                depthCopy = renderGraph.CreateTexture(desc);
            }
        }
        else
        {
            setUpPass._colorAttanchment = setUpPass._depthAttanchment =
                builder.WriteTexture(renderGraph.ImportBackbuffer(
                    BuiltinRenderTextureType.CameraTarget));
            colorAttanchments = setUpPass._colorAttanchment;
            depthAttanchments = setUpPass._depthAttanchment;
        }

        builder.SetRenderFunc<SetUpPass>((pass, context) => pass.Render(context));

        return new CameraRendererTextures(colorAttanchments, depthAttanchments,
            colorCopy, depthCopy);
    }
}