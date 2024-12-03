using UnityEngine.Rendering.RenderGraphModule;

public readonly ref struct ShadowResource
{
    public readonly TextureHandle directionalAtlas;
    public readonly TextureHandle otherAtlas;

    public readonly BufferHandle directionShadowCascadesBuffer;
    public readonly BufferHandle directionShadowMatricesBuffer;
    public readonly BufferHandle otherShadowDataBuffer;

    public ShadowResource(TextureHandle directionalAtlas, TextureHandle otherAtlas,
        BufferHandle directionShadowCascadesBuffer,
        BufferHandle directionShadowMatricesBuffer,
        BufferHandle otherShadowDataBuffer)
    {
        this.directionalAtlas = directionalAtlas;
        this.otherAtlas = otherAtlas;
        this.directionShadowCascadesBuffer = directionShadowCascadesBuffer;
        this.directionShadowMatricesBuffer = directionShadowMatricesBuffer;
        this.otherShadowDataBuffer = otherShadowDataBuffer;
    }
}