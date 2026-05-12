
/// <summary>
/// Represents a named game asset (texture, sound, etc.) loaded from disk or received from a server.
/// </summary>
public class Asset
{
    /// <summary>Lowercase filename, e.g. "grass.png".</summary>
    public string name;

    /// <summary>MD5 hex string of <see cref="data"/>, used for caching and deduplication.</summary>
    public string md5;

    /// <summary>Raw file bytes.</summary>
    public byte[] data;

    /// <summary>Valid byte count in <see cref="data"/>.</summary>
    public int dataLength;
}
