using UnityEngine;
using UnityEngine.Rendering;

//使用一个CameraRender类来专门对每个摄像机进行渲染
//目的为了将摄像机能看到的东西画出来
public partial class CameraRender
{
#if UNITY_EDITOR
    //private partial void PrepareForSceneWindow()
    //{
    //    if (camera.cameraType == CameraType.SceneView)
    //    {
    //        //当摄像机类型为场景相机时，使用这个场景相机渲染
    //        //将 UI 几何体发出到 Scene 视图中进行渲染。
    //        ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
    //        _useRenderScaledRendering = false; //不希望渲染缩放影响scene相机
    //    }
    //}

#endif
}