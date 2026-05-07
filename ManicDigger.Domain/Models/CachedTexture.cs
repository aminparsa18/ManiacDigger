public sealed class CachedTexture
{
    public int TextureId {  get; set; }
    public float SizeX { get; set; }
    public float SizeY { get; set; }
    public int LastUseMilliseconds { get; set; }
}
