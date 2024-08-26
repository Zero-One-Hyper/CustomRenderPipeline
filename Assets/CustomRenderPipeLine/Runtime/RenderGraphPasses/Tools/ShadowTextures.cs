using UnityEngine.Experimental.Rendering.RenderGraphModule;

public class ShadowTextures
{
    public TextureHandle directionalAtlas;
    public TextureHandle otherAtlas;

    public ShadowTextures(TextureHandle directionalAtlas, TextureHandle otherAtlas)
    {
        this.directionalAtlas = directionalAtlas;
        this.otherAtlas = otherAtlas;
    }
}