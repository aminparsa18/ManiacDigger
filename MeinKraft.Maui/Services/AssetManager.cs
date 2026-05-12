using System.Security.Cryptography;

/// <summary>
/// Scans one or more directories and loads all files as <see cref="Asset"/> instances.
/// Each asset is fingerprinted with an MD5 hash for server-side deduplication and caching.
/// </summary>
public class AssetManager : IAssetManager
{
    private readonly MD5 _md5 = MD5.Create();

    public List<Asset> Assets { get; set; } = [];
    public float AssetsLoadProgress { get; set; }

    public void LoadAssets()
    {
        if (Assets.Count > 0) return;

        // Read manifest — works identically on Windows and Android
        IEnumerable<string> files = ReadManifest();

        foreach (string filename in files)
        {
            using Stream stream = FileSystem.OpenAppPackageFileAsync(filename)
                                            .GetAwaiter().GetResult();
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            byte[] data = ms.ToArray();

            Assets.Add(new Asset
            {
                data = data,
                dataLength = data.Length,
                name = Path.GetFileName(filename).ToLowerInvariant(), // just filename for lookups
                md5 = ComputeMd5(data),
            });
        }

        AssetsLoadProgress = 1;
    }

    private static IEnumerable<string> ReadManifest()
    {
        using Stream s = FileSystem.OpenAppPackageFileAsync("assets_manifest.txt")
                                   .GetAwaiter().GetResult();
        using var sr = new StreamReader(s);
        return sr.ReadToEnd()
                 .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                 .Select(line => line.Replace('\\', '/'));
    }

    private string ComputeMd5(byte[] data)
        => Convert.ToHexString(_md5.ComputeHash(data)).ToLowerInvariant();
}