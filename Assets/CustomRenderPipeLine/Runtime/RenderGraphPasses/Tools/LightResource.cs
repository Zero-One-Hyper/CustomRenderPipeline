using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct LightResource
{
    public readonly ComputeBufferHandle directionLightDataBuffer;
    public readonly ComputeBufferHandle otherLightDataBuffer;

    public readonly ShadowResource shadowResource;

    public LightResource(ComputeBufferHandle directionLightDataBuffer,
        ComputeBufferHandle otherLightDataBuffer, ShadowResource shadowResource)
    {
        this.directionLightDataBuffer = directionLightDataBuffer;
        this.otherLightDataBuffer = otherLightDataBuffer;
        this.shadowResource = shadowResource;
    }
}