using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

//使用一个CameraRender类来专门对每个摄像机进行渲染
//目的为了将摄像机能看到的东西画出来
public partial class CameraRender
{
#if UNITY_EDITOR

    //为了支持绘制过时的渲染器
    private static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    private Material _errorMaterial;

    //用于在Editor下存储Camera名为样本名，就不需要多分配内存了
    private string SampleName { get; set; }

    private partial void DrawUnsupportedShaders()
    {
        if (_errorMaterial == null)
        {
            //给一个错误材质
            _errorMaterial = new Material(UnityEngine.Shader.Find("Hidden/Core/FallbackError"));
        }

        var sortingSettings = new SortingSettings(this.camera);
        var filteringSettings = FilteringSettings.defaultValue;
        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], sortingSettings)
        {
            overrideMaterial = _errorMaterial,
        };
        //调用SetShaderPassName来绘制多个通道
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }

        this._context.DrawRenderers(this._cullingResults, ref drawingSettings, ref filteringSettings);
    }

    /*旧的未使用renderGraphy的绘制Gizmos
    private partial void DrawGizmosBeforeFX()
    {
        if (!Handles.ShouldRenderGizmos() || camera.sceneViewFilterMode == Camera.SceneViewFilterMode.ShowFiltered)
        {
            return;
        }

        if (Handles.ShouldRenderGizmos())
        {
            if (useIntermediateBuffer)
            {
                Draw(_depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
                ExecuteCommandBuffer();
            }
        }

        _context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
    }

    private partial void DrawGizmosAfterFX()
    {
        if (!Handles.ShouldRenderGizmos() || camera.sceneViewFilterMode == Camera.SceneViewFilterMode.ShowFiltered)
        {
            return;
        }

        if (Handles.ShouldRenderGizmos())
        {
            if (useIntermediateBuffer)
            {
                Draw(_depthAttachmentID, BuiltinRenderTextureType.CameraTarget, true);
                ExecuteCommandBuffer();
            }
        }

        _context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
    }
    */
    private partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
        {
            //当摄像机类型为场景相机时，使用这个场景相机渲染
            //将 UI 几何体发出到 Scene 视图中进行渲染。
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            _useRenderScaledRendering = false; //不希望渲染缩放影响scene相机
        }
    }

    private partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        _commandBuffer.name = SampleName = this.camera.name;
        Profiler.EndSample();
    }

#else
    private const string SampleName = BUFFER_NAME;
#endif
}