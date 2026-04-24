public partial class Game
{
    // ── White texture ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns (and lazily creates) a 1×1 white GPU texture.
    /// Used as a neutral tint when no colour modulation is needed.
    /// </summary>
    public int WhiteTexture()
    {
        if (whitetexture == -1)
        {
            PixelBuffer buf = PixelBuffer.Create(1, 1);
            buf.SetPixel(0, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255));
            using Bitmap bmp = buf.ToBitmap();
            whitetexture = Platform.LoadTextureFromBitmap(bmp);
        }
        return whitetexture;
    }

    // ── Text texture cache ────────────────────────────────────────────────────
    //
    // Keyed by TextStyle so lookup is O(1). Previously a List<CachedTextTexture>
    // was scanned linearly on every Draw2dText call, and deleted entries were
    // set to null rather than removed, causing the list to grow indefinitely.
    //
    // PREREQUISITE: TextStyle must override GetHashCode() consistently with its
    // Equals() implementation. If only Equals() is overridden the Dictionary
    // silently falls back to reference equality and caching breaks.

    /// <summary>
    /// Evicts text texture cache entries that have not been used for more than
    /// one second, releasing their GPU texture handles.
    /// Entries are removed from the Dictionary immediately — no null slots accumulate.
    /// </summary>
    public void DeleteUnusedCachedTextTextures()
    {
        int now = Platform.TimeMillisecondsFromStart;

        // Collect keys to remove — cannot mutate a Dictionary while iterating it.
        List<TextStyle> toRemove = null;
        foreach (var (style, tex) in CachedTextTextures)
        {
            if ((now - tex.lastuseMilliseconds) / 1000f > 1f)
            {
                toRemove ??= [];
                toRemove.Add(style);
            }
        }

        if (toRemove == null) return;
        foreach (TextStyle key in toRemove)
        {
            Platform.GLDeleteTexture(CachedTextTextures[key].textureId);
            CachedTextTextures.Remove(key);
        }
    }

    /// <summary>
    /// Returns the cached GPU texture for the given <see cref="TextStyle"/>,
    /// or <see langword="null"/> if it has not yet been rendered.
    /// O(1) Dictionary lookup.
    /// </summary>
    private CachedTexture GetCachedTextTexture(TextStyle t)
        => CachedTextTextures.TryGetValue(t, out CachedTexture ct) ? ct : null;

    /// <summary>
    /// Renders <paramref name="t"/> to a <see cref="Bitmap"/> via
    /// <see cref="TextColorRenderer"/>, uploads it to the GPU, and returns
    /// the resulting <see cref="CachedTexture"/>.
    /// </summary>
    private CachedTexture MakeTextTexture(TextStyle t)
    {
        using Bitmap bmp = TextColorRenderer.CreateTextTexture(t);
        return new CachedTexture
        {
            sizeX = bmp.Width,
            sizeY = bmp.Height,
            textureId = Platform.LoadTextureFromBitmap(bmp),
        };
    }

    // ── Named texture cache ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the GPU texture ID for the named asset, loading and caching it
    /// on first access. Returns a fresh ID on every load for uncached names.
    /// </summary>
    public int GetTexture(string p)
    {
        if (!textures.TryGetValue(p, out int id))
        {
            using Bitmap bmp = PixelBuffer.BitmapFromPng(GetAssetFile(p), GetAssetFileLength(p));
            id = Platform.LoadTextureFromBitmap(bmp);
            textures[p] = id;
        }
        return id;
    }

    /// <summary>
    /// Returns the cached GPU texture for <paramref name="name"/>, uploading
    /// <paramref name="bmp"/> on first access.
    /// Uses a single <see cref="Dictionary{K,V}.TryGetValue"/> call instead of
    /// the previous <c>ContainsKey</c> + indexer double-lookup.
    /// </summary>
    public int GetTextureOrLoad(string name, Bitmap bmp)
    {
        if (!textures.TryGetValue(name, out int id))
        {
            id = Platform.LoadTextureFromBitmap(bmp);
            textures[name] = id;
        }
        return id;
    }

    /// <summary>
    /// Removes the named texture from the cache and releases its GPU handle.
    /// </summary>
    /// <returns><see langword="true"/> if the texture was found and deleted.</returns>
    public bool DeleteTexture(string name)
    {
        if (name != null && textures.TryGetValue(name, out int id))
        {
            textures.Remove(name);
            Platform.GLDeleteTexture(id);
            return true;
        }
        return false;
    }

    // ── Terrain texture atlas ─────────────────────────────────────────────────

    /// <summary>
    /// Uploads <paramref name="atlas2d"/> as the main terrain texture and splits
    /// it into 1-D atlas strips for indexed lookup by the tessellator.
    /// </summary>
    internal void UseTerrainTextureAtlas2d(Bitmap atlas2d, int atlas2dWidth)
    {
        TerrainTexture = Platform.LoadTextureFromBitmap(atlas2d);
        TerrainTexturesPerAtlas = Atlas1dheight() / (atlas2dWidth / Atlas2DTiles);

        Bitmap[] atlases1d = PixelBuffer.Atlas2dInto1d(atlas2d, Atlas2DTiles, Atlas1dheight(), out int atlasesidCount);
        TerrainTextures1d = new int[atlasesidCount];
        for (int i = 0; i < atlasesidCount; i++)
        {
            TerrainTextures1d[i] = Platform.LoadTextureFromBitmap(atlases1d[i]);
            atlases1d[i].Dispose();
        }
    }

    /// <summary>
    /// Builds a 2-D texture atlas from the given terrain texture IDs and uploads
    /// it to the GPU via <see cref="UseTerrainTextureAtlas2d"/>.
    /// Each texture is loaded from a PNG asset file and blitted into the atlas.
    /// Textures that are missing or not exactly 32×32 pixels are skipped.
    /// </summary>
    /// <param name="textureIds">
    /// Texture asset names (without <c>.png</c>). Null entries are skipped.
    /// </param>
    /// <param name="textureIdsCount">Number of valid entries to process.</param>
    public void UseTerrainTextures(string[] textureIds, int textureIdsCount)
    {
        // TODO: support tile sizes other than 32×32.
        const int tilesize = 32;
        PixelBuffer atlas2d = PixelBuffer.Create(tilesize * Atlas2DTiles, tilesize * Atlas2DTiles);

        for (int i = 0; i < textureIdsCount; i++)
        {
            if (textureIds[i] == null) continue;

            byte[] fileData = GetAssetFile(textureIds[i] + ".png") ?? GetAssetFile("Unknown.png");
            if (fileData == null) continue;

            using Bitmap bmp = PixelBuffer.BitmapFromPng(fileData, fileData.Length);
            if (bmp.Width != tilesize || bmp.Height != tilesize) continue;

            PixelBuffer tile = PixelBuffer.FromBitmap(bmp);

            int destX = i % TexturesPacked * tilesize;
            int destY = i / TexturesPacked * tilesize;

            // ── Row-by-row Span copy replaces the inner xx loop ───────────────
            // tile.Argb is laid out as [row0_col0, row0_col1, ...] so each row
            // is a contiguous tilesize-wide slice. The atlas destination rows are
            // at stride atlas2d.Width, so we copy one row at a time.
            for (int row = 0; row < tilesize; row++)
            {
                tile.Argb
                    .AsSpan(row * tilesize, tilesize)
                    .CopyTo(atlas2d.Argb.AsSpan((destY + row) * atlas2d.Width + destX, tilesize));
            }
        }

        using Bitmap bitmap = atlas2d.ToBitmap();
        UseTerrainTextureAtlas2d(bitmap, atlas2d.Width);
    }
}