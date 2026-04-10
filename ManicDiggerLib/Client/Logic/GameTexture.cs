public partial class Game
{
    // -------------------------------------------------------------------------
    // White texture
    // -------------------------------------------------------------------------

    public int WhiteTexture()
    {
        if (whitetexture == -1)
        {
            PixelBuffer buf = PixelBuffer.Create(1, 1);
            buf.SetPixel(0, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255));
            Bitmap bmp = buf.ToBitmap();
            whitetexture = platform.LoadTextureFromBitmap(bmp);
        }
        return whitetexture;
    }

    // -------------------------------------------------------------------------
    // Texture cache (text rendering)
    // -------------------------------------------------------------------------

    public void DeleteUnusedCachedTextTextures()
    {
        int now = platform.TimeMillisecondsFromStart;
        for (int i = 0; i < cachedTextTextures.Count; i++)
        {
            CachedTextTexture t = cachedTextTextures[i];
            if (t == null)
                continue;

            if ((now - t.texture.lastuseMilliseconds) / 1000f > 1)
            {
                platform.GLDeleteTexture(t.texture.textureId);
                cachedTextTextures[i] = null;
            }
        }
    }

    private CachedTexture GetCachedTextTexture(Text_ t)
    {
        for (int i = 0; i < cachedTextTextures.Count; i++)
        {
            CachedTextTexture ct = cachedTextTextures[i];
            if (ct == null)
                continue;

            if (ct.text.Equals_(t))
                return ct.texture;
        }
        return null;
    }

    private CachedTexture MakeTextTexture(Text_ t)
    {
        Bitmap bmp = textColorRenderer.CreateTextTexture(t);
        CachedTexture ct = new()
        {
            sizeX = bmp.Width,
            sizeY = bmp.Height,
            textureId = platform.LoadTextureFromBitmap(bmp)
        };
        platform.BitmapDelete(bmp);
        return ct;
    }

    public void UpdateTextRendererFont()
    {
        platform.SetTextRendererFont(Font);
    }

    // -------------------------------------------------------------------------
    // Named texture cache
    // -------------------------------------------------------------------------

    internal int GetTexture(string p)
    {
        if (!textures.ContainsKey(p))
        {
            Bitmap bmp = platform.BitmapCreateFromPng(GetAssetFile(p), GetAssetFileLength(p));
            textures[p] = platform.LoadTextureFromBitmap(bmp);
            platform.BitmapDelete(bmp);
        }
        return textures[p];
    }

    internal int GetTextureOrLoad(string name, Bitmap bmp)
    {
        if (!textures.ContainsKey(name))
            textures[name] = platform.LoadTextureFromBitmap(bmp);

        return textures[name];
    }

    internal bool DeleteTexture(string name)
    {
        if (name != null && textures.TryGetValue(name, out int id))
        {
            textures.Remove(name);
            platform.GLDeleteTexture(id);
            return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Terrain texture atlas
    // -------------------------------------------------------------------------

    internal void UseTerrainTextureAtlas2d(Bitmap atlas2d, int atlas2dWidth)
    {
        terrainTexture = platform.LoadTextureFromBitmap(atlas2d);

        terrainTexturesPerAtlas = Atlas1dheight() / (atlas2dWidth / Atlas2DTiles);
        Bitmap[] atlases1d = TextureAtlasConverter.Atlas2dInto1d(platform, atlas2d, Atlas2DTiles, Atlas1dheight(), out int atlasesidCount);

        terrainTextures1d = new int[atlasesidCount];
        int count = 0;
        for (int i = 0; i < atlasesidCount; i++)
        {
            terrainTextures1d[count++] = platform.LoadTextureFromBitmap(atlases1d[i]);
            platform.BitmapDelete(atlases1d[i]);
        }
    }

    /// <summary>
    /// Builds a 2-D texture atlas from the given terrain texture IDs and uploads it to the GPU.
    /// Each texture is loaded from a PNG asset file and copied into a <see cref="PixelBuffer"/> atlas.
    /// Textures that are missing, unresolvable, or not exactly <c>32×32</c> pixels are skipped.
    /// </summary>
    /// <param name="textureIds">
    /// Array of texture asset identifiers (without the <c>.png</c> extension).
    /// Entries may be <see langword="null"/> to leave a slot empty.
    /// </param>
    /// <param name="textureIdsCount">Number of entries in <paramref name="textureIds"/> to process.</param>
    internal void UseTerrainTextures(string[] textureIds, int textureIdsCount)
    {
        // TODO: support tile sizes other than 32x32
        const int tilesize = 32;
        PixelBuffer atlas2d = PixelBuffer.Create(tilesize * Atlas2DTiles, tilesize * Atlas2DTiles);

        for (int i = 0; i < textureIdsCount; i++)
        {
            if (textureIds[i] == null) continue;

            byte[] fileData = GetAssetFile(string.Concat(textureIds[i], ".png")) ?? GetAssetFile("Unknown.png");
            if (fileData == null) continue;

            Bitmap bmp = platform.BitmapCreateFromPng(fileData, fileData.Length);
            if (bmp.Width != tilesize || bmp.Height != tilesize)
            {
                platform.BitmapDelete(bmp);
                continue;
            }

            PixelBuffer tile = PixelBuffer.FromBitmap(bmp);
            platform.BitmapDelete(bmp);

            int x = (i % TexturesPacked) * tilesize;
            int y = (i / TexturesPacked) * tilesize;
            for (int yy = 0; yy < tilesize; yy++)
                for (int xx = 0; xx < tilesize; xx++)
                    atlas2d.SetPixel(x + xx, y + yy, tile.GetPixel(xx, yy));
        }

        Bitmap bitmap = atlas2d.ToBitmap();
        UseTerrainTextureAtlas2d(bitmap, atlas2d.Width);
    }
}