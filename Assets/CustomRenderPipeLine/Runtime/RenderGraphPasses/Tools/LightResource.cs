using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct LightResource
{
    public readonly ComputeBufferHandle directionLightDataBuffer;
    public readonly ComputeBufferHandle otherLightDataBuffer;
    public readonly ComputeBufferHandle tilesBuffer;

    public readonly ShadowResource shadowResource;

    public LightResource(ComputeBufferHandle directionLightDataBuffer,
        ComputeBufferHandle otherLightDataBuffer, ComputeBufferHandle tilesBuffer
        , ShadowResource shadowResource)
    {
        this.directionLightDataBuffer = directionLightDataBuffer;
        this.otherLightDataBuffer = otherLightDataBuffer;
        this.tilesBuffer = tilesBuffer;
        this.shadowResource = shadowResource;
    }
}