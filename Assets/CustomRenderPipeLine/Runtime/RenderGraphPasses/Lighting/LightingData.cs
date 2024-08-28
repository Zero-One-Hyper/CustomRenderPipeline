using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class LightingPass
{
    [StructLayout(LayoutKind.Sequential)]
    struct DirectionalLightData
    {
        public const int Stride = 4 * 4 * 3;
        public Vector4 color;
        public Vector4 directionAndMask;
        public Vector4 shadowData;

        public DirectionalLightData(Light light, Vector4 shadowData, ref VisibleLight visibleLight)
        {
            color = visibleLight.finalColor;
            directionAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
            this.shadowData = shadowData;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct OtherLightData
    {
        public const int Stride = 4 * 4 * 5;
        public Vector4 color;
        public Vector4 position;
        public Vector4 directionAndMask;
        public Vector4 spotAngle;
        public Vector4 shadowData;
    }

    private OtherLightData CreatePointLight(Light light, Vector4 shadowData, ref VisibleLight visibleLight)
    {
        OtherLightData pointLightData;
        pointLightData.color = visibleLight.finalColor;
        pointLightData.position = visibleLight.localToWorldMatrix.GetColumn(3);
        //光的范围 使用衰减距离来平滑淡入淡出光线 max(0, 1 - (d^2/r^2)^2)^2
        //把坟墓范围放到position的w分量中去
        pointLightData.position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.000001f);
        //_otherLightPosition[index] = position;
        pointLightData.spotAngle = new Vector4(0.0f, 1.0f);
        pointLightData.directionAndMask = Vector4.zero; //点光源没有方向
        pointLightData.directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        //_otherLightDirectionsAndMasks[index] = dirAndMask;
        pointLightData.shadowData = shadowData;
        return pointLightData;
    }

    private OtherLightData CreateSpotLight(Light light, Vector4 shadowData, ref VisibleLight visibleLight)
    {
        OtherLightData spotLightData;
        spotLightData.color = visibleLight.finalColor;
        spotLightData.position = visibleLight.localToWorldMatrix.GetColumn(3);
        spotLightData.position.w = 1.0f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        //_otherLightPosition[index] = position;
        spotLightData.directionAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        spotLightData.directionAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        //_otherLightDirectionsAndMasks[index] = dirAndMask;

        //计算聚光灯的角度
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1.0f / Mathf.Max(innerCos - outerCos, 0.00001f);
        spotLightData.spotAngle = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        spotLightData.shadowData = shadowData;
        return spotLightData;
    }
}