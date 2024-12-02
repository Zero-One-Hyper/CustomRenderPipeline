using UnityEngine.Rendering.RenderGraphModule;

public readonly ref struct LightResource
{
    public readonly BufferHandle directionLightDataBuffer;
    public readonly BufferHandle otherLightDataBuffer;
    public readonly BufferHandle tilesBuffer;

    public readonly ShadowResource shadowResource;

    public LightResource(BufferHandle directionLightDataBuffer,
        BufferHandle otherLightDataBuffer, BufferHandle tilesBuffer
        , ShadowResource shadowResource)
    {
        this.directionLightDataBuffer = directionLightDataBuffer;
        this.otherLightDataBuffer = otherLightDataBuffer;
        this.tilesBuffer = tilesBuffer;
        this.shadowResource = shadowResource;
    }
}