using System.Security.Cryptography;

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

/// <summary>
/// Scans one or more directories and loads all files as <see cref="Asset"/> instances.
/// Each asset is fingerprinted with an MD5 hash for server-side deduplication and caching.
/// </summary>
public class AssetLoader
{
    private readonly string[] datapaths;
    private readonly MD5 md5 = MD5.Create();

    public AssetLoader(string[] datapaths)
    {
        this.datapaths = datapaths;
    }

    public List<Asset> LoadAssetsAsync(out float progress)
    {
        List<Asset> assets = [];

        foreach (string path in datapaths)
        {
            if (!Directory.Exists(path))
                continue;

            foreach (string file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    FileInfo f = new(file);
                    if (f.Name.Equals("thumbs.db", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    byte[] data = File.ReadAllBytes(file);
                    assets.Add(new Asset
                    {
                        data = data,
                        dataLength = data.Length,
                        name = f.Name.ToLowerInvariant(),
                        md5 = ComputeMd5(data)
                    });
                }
                catch
                {
                    // Skip unreadable files.
                }
            }
        }

        progress = 1;
        return assets;
    }

    private string ComputeMd5(byte[] data) =>
        Convert.ToHexString(md5.ComputeHash(data)).ToLowerInvariant();
}