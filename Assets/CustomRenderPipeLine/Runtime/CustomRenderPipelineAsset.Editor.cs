using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class CustomRenderPipelineAsset
{
#if UNITY_EDITOR

    private static string[] _renderLayerNames;

    static CustomRenderPipelineAsset()
    {
        _renderLayerNames = new string[31]; //减少到31个来解决光的渲染层掩码在内部用无符号整型的问题uint
        for (int i = 0; i < 31; i++)
        {
            _renderLayerNames[i] = "Layer" + (i + 1);
        }
    }

    public override string[] renderingLayerMaskNames => _renderLayerNames;

#endif
}