using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct ShadowResource
{
    public readonly TextureHandle directionalAtlas;
    public readonly TextureHandle otherAtlas;

    public readonly ComputeBufferHandle directionShadowCascadesBuffer;
    public readonly ComputeBufferHandle directionShadowMatricesBuffer;
    public readonly ComputeBufferHandle otherShadowDataBuffer;

    public ShadowResource(TextureHandle directionalAtlas, TextureHandle otherAtlas,
        ComputeBufferHandle directionShadowCascadesBuffer,
        ComputeBufferHandle directionShadowMatricesBuffer,
        ComputeBufferHandle otherShadowDataBuffer)
    {
        this.directionalAtlas = directionalAtlas;
        this.otherAtlas = otherAtlas;
        this.directionShadowCascadesBuffer = directionShadowCascadesBuffer;
        this.directionShadowMatricesBuffer = directionShadowMatricesBuffer;
        this.otherShadowDataBuffer = otherShadowDataBuffer;
    }
}