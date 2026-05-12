
/// <summary>
/// Holds 3-D rendering configuration: culling, transparency, mipmap,
/// visibility, and view-distance settings.
/// </summary>
public class Config3d
{
    /// <summary>Whether back-face culling is enabled. Default: <c>true</c>.</summary>
    public bool EnableBackfaceCulling { get; set; } = true;

    /// <summary>Whether transparency rendering is enabled. Default: <c>true</c>.</summary>
    public bool EnableTransparency { get; set; } = true;

    /// <summary>Whether mipmapping is enabled. Default: <c>true</c>.</summary>
    public bool EnableMipmaps { get; set; } = true;

    /// <summary>Whether visibility/occlusion culling is enabled. Default: <c>false</c>.</summary>
    public bool EnableVisibilityCulling { get; set; } = false;

    /// <summary>Maximum view distance in world units. Default: <c>128</c>.</summary>
    public float ViewDistance { get; set; } = 1024;
}
