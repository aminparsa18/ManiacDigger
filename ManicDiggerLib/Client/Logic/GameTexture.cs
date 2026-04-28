//This partial Game class manages all GPU texture loading and caching:
//White texture — lazily creates a 1×1 white pixel texture used as a neutral tint when no colour modulation is needed.
//Text texture cache — DeleteUnusedCachedTextTextures evicts text textures that haven't been used for over a second,
//releasing their GPU handles. MakeTextTexture renders a TextStyle to a bitmap and uploads it to the GPU.
//Named texture cache — GetTexture and GetTextureOrLoad load PNG assets into GPU textures and cache them by name.
//DeleteTexture removes a texture and releases its GPU handle.
//Terrain texture atlas — UseTerrainTextures builds a 2D atlas by loading all terrain tile PNGs
//and blitting them into a large atlas bitmap row-by-row. UseTerrainTextureAtlas2d uploads 
//that atlas and splits it into 1D strips for the tessellator.

public partial class Game
{
    // ── White texture ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns (and lazily creates) a 1×1 white GPU texture.
    /// Used as a neutral tint when no colour modulation is needed.
    /// Named <c>GetOrCreate</c> to signal that this has a side effect on first call.
    /// </summary>
    public int GetOrCreateWhiteTexture()
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

    private readonly List<TextStyle> _textStylesToRemove = new();

    /// <summary>
    /// Evicts text texture cache entries that have not been used for more than
    /// one second, releasing their GPU texture handles.
    /// Fix #1: uses a pre-allocated <see cref="_textStylesToRemove"/> list
    /// instead of allocating a new one on every eviction frame.
    /// </summary>
    public void DeleteUnusedCachedTextTextures()
    {
        int now = Platform.TimeMillisecondsFromStart;

        _textStylesToRemove.Clear();
        foreach (var (style, tex) in CachedTextTextures)
        {
            if ((now - tex.lastuseMilliseconds) / 1000f > 1f)
                _textStylesToRemove.Add(style);
        }

        foreach (TextStyle key in _textStylesToRemove)
        {
            Platform.GLDeleteTexture(CachedTextTextures[key].textureId);
            CachedTextTextures.Remove(key);
        }
    }

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
    /// on first access.
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
    /// Returns the cached GPU texture for <paramref name="name"/>,
    /// uploading <paramref name="bmp"/> on first access.
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
    /// calling it twice.
    /// </summary>
    internal void UseTerrainTextureAtlas2d(Bitmap atlas2d, int atlas2dWidth)
    {
        TerrainTexture = Platform.LoadTextureFromBitmap(atlas2d);

        // Fix #5: call Atlas1dheight() once and reuse the result.
        int atlas1dHeight = Atlas1dheight();
        TerrainTexturesPerAtlas = atlas1dHeight / (atlas2dWidth / Atlas2DTiles);

        Bitmap[] atlases1d = PixelBuffer.Atlas2dInto1d(atlas2d, Atlas2DTiles, atlas1dHeight);

        TerrainTextures1d = new int[atlases1d.Length];
        for (int i = 0; i < atlases1d.Length; i++)
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
    /// <param name="textureIds">Texture asset names (without <c>.png</c>). Null entries are skipped.</param>
    /// <param name="textureIdsCount">Number of valid entries to process.</param>
    public void UseTerrainTextures(string[] textureIds, int textureIdsCount)
    {
        const int tilesize = 32; // TODO: support tile sizes other than 32×32.

        PixelBuffer atlas2d = PixelBuffer.Create(tilesize * Atlas2DTiles, tilesize * Atlas2DTiles);

        byte[] unknownPng = GetAssetFile("Unknown.png");

        for (int i = 0; i < textureIdsCount; i++)
        {
            if (textureIds[i] == null) continue;

            byte[] fileData = GetAssetFile(string.Concat(textureIds[i], ".png")) ?? unknownPng;
            if (fileData == null) continue;

            using Bitmap bmp = PixelBuffer.BitmapFromPng(fileData, fileData.Length);

            if (bmp.Width != tilesize || bmp.Height != tilesize)
            {
                Console.WriteLine(
                    $"[Terrain] Skipping '{textureIds[i]}': expected {tilesize}×{tilesize}, got {bmp.Width}×{bmp.Height}.");
                continue;
            }

            PixelBuffer tile = PixelBuffer.FromBitmap(bmp);

            int destX = i % TexturesPacked * tilesize;
            int destY = i / TexturesPacked * tilesize;

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