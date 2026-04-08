public partial class Game
{
    // -------------------------------------------------------------------------
    // Asset lookup
    // -------------------------------------------------------------------------

    private bool HasAsset(string md5, string name)
    {
        for (int i = 0; i < assets.count; i++)
        {
            Asset a = assets.items[i];
            // Check both MD5 and name as there might be files with same content.
            if (a.md5 == md5 && a.name == name)
                return true;
        }
        return false;
    }

    internal byte[] GetAssetFile(string p)
    {
        string pLower = p.ToLowerInvariant();
        for (int i = 0; i < assets.count; i++)
        {
            if (assets.items[i].name == pLower)
                return assets.items[i].data;
        }
        return null;
    }

    internal int GetAssetFileLength(string p)
    {
        string pLower = p.ToLowerInvariant();
        for (int i = 0; i < assets.count; i++)
        {
            if (assets.items[i].name == pLower)
                return assets.items[i].dataLength;
        }
        return 0;
    }

    // -------------------------------------------------------------------------
    // Asset cache
    // -------------------------------------------------------------------------

    private void CacheAsset(Asset asset)
    {
        // Prevent crash on old servers that don't send a checksum.
        if (asset.md5 == null)
            return;

        if (!platform.IsChecksum(asset.md5))
            return;

        if (!platform.IsCached(asset.md5))
            platform.SaveAssetToCache(asset);
    }

    public void SetFile(string name, string md5, byte[] downloaded, int downloadedLength)
    {
        string nameLower = name.ToLowerInvariant();

        // Update mouse cursor if the cursor asset changed.
        if (nameLower == "mousecursor.png")
            platform.SetWindowCursor(0, 0, 32, 32, downloaded, downloadedLength);

        Asset newAsset = new()
        {
            data = downloaded,
            dataLength = downloadedLength,
            name = nameLower,
            md5 = md5
        };

        for (int i = 0; i < assets.count; i++)
        {
            if (assets.items[i] == null)
                continue;

            if (assets.items[i].name == nameLower)
            {
                if (options.UseServerTextures)
                    assets.items[i] = newAsset;

                CacheAsset(newAsset);
                return;
            }
        }

        assets.items[assets.count++] = newAsset;
        CacheAsset(newAsset);
    }
}