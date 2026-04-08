using System.Security.Cryptography;
using System.Text;

public class AssetLoader
{
    public AssetLoader(string[] datapaths_)
    {
        this.datapaths = datapaths_;
    }
    private readonly string[] datapaths;
    public void LoadAssetsAsync(AssetList list, out float progress)
    {
        List<Asset> assets = new();
        foreach (string path in datapaths)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }
                foreach (string s in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileInfo f = new(s);
                        if (f.Name.Equals("thumbs.db", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                        Asset a = new()
                        {
                            data = File.ReadAllBytes(s)
                        };
                        a.dataLength = a.data.Length;
                        a.name = f.Name.ToLowerInvariant();
                        a.md5 = Md5(a.data);
                        assets.Add(a);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
        progress = 1;
        list.count = assets.Count;
        list.items = new Asset[2048];
        for (int i = 0; i < assets.Count; i++)
        {
            list.items[i] = assets[i];
        }
    }

    private readonly MD5 sha1 = MD5.Create();
    private string Md5(byte[] data)
    {
        string hash = ToHex(sha1.ComputeHash(data), false);
        return hash;
    }

    public static string ToHex(byte[] bytes, bool upperCase)
    {
        StringBuilder result = new(bytes.Length * 2);

        for (int i = 0; i < bytes.Length; i++)
        {
            result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
        }

        return result.ToString();
    }
}
