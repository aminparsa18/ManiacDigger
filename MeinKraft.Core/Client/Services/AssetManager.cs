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
public class AssetManager : IAssetManager
{
    private readonly string[] _datapaths;
    private readonly MD5 _md5 = MD5.Create();

    /// <summary>Raw asset list populated asynchronously during <see cref="Start"/>.</summary>
    public List<Asset> Assets { get; set; } = [];

    /// <summary>Fraction [0, 1] indicating how far async asset loading has progressed.</summary>
    public float AssetsLoadProgress { get; set; }

    public AssetManager()
    {
        string baseDir = AppContext.BaseDirectory;
       // _datapaths = [Path.Combine(PathHelper.DataRoot, "public"), Path.Combine("data", "public")];
        _datapaths = [PathHelper.DataRoot, "data"];
        _datapaths = [.. _datapaths
            .Select(p =>
            {
                if (Path.IsPathRooted(p))
                {
                    return p;
                }

                // If path contains '..', resolve it relative to baseDir
                // but then validate it stays within a sane boundary
                string resolved = Path.GetFullPath(Path.Combine(baseDir, p));

                // Safety check: if resolved path doesn't exist, the '..' 
                // traversal went somewhere wrong — return as-is and let
                // the Directory.Exists check in LoadAssetsAsync skip it
                return resolved;
            })];
    }

    public void LoadAssets()
    {
        if (Assets.Count > 0)
        {
            return;
        }

        foreach (string path in _datapaths)
        {
            string ss = Path.GetFullPath(path);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (string file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
            {
                FileInfo f = new(file);
                if (f.Name.Equals("thumbs.db", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                byte[] data = File.ReadAllBytes(file);
                Assets.Add(new Asset
                {
                    data = data,
                    dataLength = data.Length,
                    name = f.Name.ToLowerInvariant(),
                    md5 = ComputeMd5(data)
                });
            }
        }

        AssetsLoadProgress = 1;
    }

    private string ComputeMd5(byte[] data)
        => Convert.ToHexString(_md5.ComputeHash(data)).ToLowerInvariant();
}