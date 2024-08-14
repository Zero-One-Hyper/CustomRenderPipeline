#ifndef _CUSTOM_BRDF_INCLUDE_
#define _CUSTOM_BRDF_INCLUDE_

#define MIN_REFLECTIVITY 0.04 //非金属反射率平均为0.04，这里设置一个最小的反射率
#include "Assets/CustomRenderPipeLine/ShaderLibrary/Lighting/CustomLight.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

struct BRDF
{
    float3 diffuse;
    float3 specular;
    float roughness;
    float perceptualRoughness;
    float fresnel;
};

float OneMinusReflectivity(float metallic)
{
    //一些光还会从电介质表面反射回来，从而使得表面具有亮点
    float range = 1 - MIN_REFLECTIVITY;
    return range * (1 - metallic); //修改金属度描述的反射率映射范围（0~0.96）
}

BRDF GetBRDF(Surface surface)
{
    BRDF brdf;
    //金属通常会通过镜面反射反射所有光，且漫反射为0，所以金属度越高，漫反射越少
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color * oneMinusReflectivity;
    #ifdef _PREMULTIPLYALPHA_ON
    //为漫反射预乘一个alpha，而不是依赖GPU混合，保证在One OneMinusSrcAlpha保留镜面反射时漫反射依旧变淡
    brdf.diffuse * surface.alpha;
    //brdf.diffuse = 0;
    #endif
    //以一种方式被反射的光线不会再以另一种方式反射
    brdf.specular = surface.color - brdf.diffuse; //等价于surface.color * (1 - oneMinusReflectivity);
    //光滑度和粗糙度是反义词二者相加为， Perceptual：感性的 adj
    //对值进行一个平方的处理可以使得编辑器上的值更加符合直觉（即值越大，越smothness）
    brdf.perceptualRoughness = PerceptualSmoothnessToRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    brdf.fresnel = saturate(surface.smoothness + 1 - oneMinusReflectivity);

    return brdf;
}

float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
    float3 halfVL = SafeNormalize(light.direction + surface.viewDirection);
    float nDotH2 = Square(saturate(dot(surface.normal, halfVL)));
    float lDOtH2 = Square(saturate(dot(light.direction, halfVL)));
    float roughness2 = Square(brdf.roughness);
    //这里的PBR公式的unityURP中使用的(D * V * F) / 4.0化简后的计算方法 与unity的URP一致
    float d2 = Square(nDotH2 * (roughness2 - 1.0) + 1.000001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return roughness2 / (d2 * max(0.1, lDOtH2) * normalization);
}

float3 DirectBRDF(Surface surface, BRDF brdf, Light light)
{
    //这里其实可以为了更能展示出高光与漫反射的关系 让diffuse在这里乘占比的
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

float3 IndirectBRDF(Surface surface, BRDF brdf, float3 giDiffuse, float3 giSpecular)
{
    float fresnelStrength = Pow4(1 - saturate(dot(surface.normal, surface.viewDirection)));
    float3 reflection = giSpecular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    //粗糙度会散射反射，最终减少看到的镜面反射
    reflection /= brdf.roughness * brdf.roughness + 1.0;
    return (giDiffuse * brdf.diffuse + reflection) * surface.occlusion;
}
#endif
