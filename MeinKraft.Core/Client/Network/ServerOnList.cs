/// <summary>
/// Represents a single server entry returned from the server list.
/// </summary>
public class ServerOnList
{
    public string Hash { get; set; }
    public string Name { get; set; }
    public string Motd { get; set; }
    public int Port { get; set; }
    public string Ip { get; set; }
    public string Version { get; set; }
    public int Users { get; set; }
    public int Max { get; set; }
    public string GameMode { get; set; }
    public string Players { get; set; }
    public bool ThumbnailDownloading { get; set; }
    public bool ThumbnailError { get; set; }
    public bool ThumbnailFetched { get; set; }
}