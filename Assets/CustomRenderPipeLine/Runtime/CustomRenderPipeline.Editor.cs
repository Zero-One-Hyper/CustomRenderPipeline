using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.GlobalIllumination;
using Lightmapping = UnityEngine.Experimental.GlobalIllumination.Lightmapping;

public partial class CustomRenderPipeline
{
#if UNITY_EDITOR

    //重写光照贴图器以解决如何设置光照数据
    private static Lightmapping.RequestLightsDelegate _lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            var lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                switch (light.type)
                {
                    case UnityEngine.LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.Init(ref directionalLight);
                        break;
                    case UnityEngine.LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
#if UNITY_2019_4_OR_NEWER
                        //在光照贴图中使用聚光灯的内角衰减角度
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
#endif
                        lightData.Init(ref spotLight);
                        break;
                    case UnityEngine.LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case UnityEngine.LightType.Area:
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        rectangleLight.mode = LightMode.Baked; //暂时不支持区域实时光照
                        lightData.Init(ref rectangleLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                lightData.falloff = FalloffType.InverseSquared;
                output[i] = lightData;
            }
        };

    private partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(_lightsDelegate);
    }

    private partial void DisposeForEditor()
    {
        //清理委托
        Lightmapping.ResetDelegate();
    }
#endif
}