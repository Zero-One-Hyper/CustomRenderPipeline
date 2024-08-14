#ifndef _CUSTOM_SURFACE_INCLUDE_
#define _CUSTOM_SURFACE_INCLUDE_

struct Surface
{
    float3 position;
    float3 normal;
    float3 viewDirection;
    float depth; //基于观察空间的深度 这里用于阴影最大距离 
    float3 color;
    float alpha;
    float metallic;
    float smoothness;
    float occlusion;
    float dither; //用于阴影抖动过渡
    float3 interpolatedNormal; //插值法线
    uint renderingLayerMask;
};


#endif
