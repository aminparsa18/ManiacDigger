public partial class Game
{
    // -------------------------------------------------------------------------
    // White texture
    // -------------------------------------------------------------------------

    public int WhiteTexture()
    {
        if (whitetexture == -1)
        {
            Bitmap bmp = new(1, 1);
            platform.BitmapSetPixelsArgb(bmp, [ColorFromArgb(255, 255, 255, 255)]);
            whitetexture = platform.LoadTextureFromBitmap(bmp);
        }
        return whitetexture;
    }

    // -------------------------------------------------------------------------
    // Texture cache (text rendering)
    // -------------------------------------------------------------------------

    public void DeleteUnusedCachedTextTextures()
    {
        int now = platform.TimeMillisecondsFromStart();
        for (int i = 0; i < cachedTextTexturesMax; i++)
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
        for (int i = 0; i < cachedTextTexturesMax; i++)
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
            sizeX = platform.BitmapGetWidth(bmp),
            sizeY = platform.BitmapGetHeight(bmp),
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

        terrainTexturesPerAtlas = Atlas1dheight() / (atlas2dWidth / atlas2dtiles());
        Bitmap[] atlases1d = TextureAtlasConverter.Atlas2dInto1d(platform, atlas2d, atlas2dtiles(), Atlas1dheight(), out int atlasesidCount);

        terrainTextures1d = new int[atlasesidCount];
        int count = 0;
        for (int i = 0; i < atlasesidCount; i++)
        {
            terrainTextures1d[count++] = platform.LoadTextureFromBitmap(atlases1d[i]);
            platform.BitmapDelete(atlases1d[i]);
        }
    }

    internal void UseTerrainTextures(string[] textureIds, int textureIdsCount)
    {
        // TODO: support tile sizes other than 32x32
        const int tilesize = 32;
        BitmapData_ atlas2d = BitmapData_.Create(tilesize * atlas2dtiles(), tilesize * atlas2dtiles());

        for (int i = 0; i < textureIdsCount; i++)
        {
            if (textureIds[i] == null)
                continue;

            byte[] fileData = GetAssetFile(string.Concat(textureIds[i], ".png")) ?? GetAssetFile("Unknown.png");
            if (fileData == null)
                continue;

            Bitmap bmp = platform.BitmapCreateFromPng(fileData, fileData.Length);
            if (platform.BitmapGetWidth(bmp) != tilesize || platform.BitmapGetHeight(bmp) != tilesize)
            {
                platform.BitmapDelete(bmp);
                continue;
            }

            int[] bmpPixels = new int[tilesize * tilesize];
            platform.BitmapGetPixelsArgb(bmp, bmpPixels);
            platform.BitmapDelete(bmp);

            int x = i % texturesPacked();
            int y = i / texturesPacked();
            for (int xx = 0; xx < tilesize; xx++)
                for (int yy = 0; yy < tilesize; yy++)
                    atlas2d.SetPixel(x * tilesize + xx, y * tilesize + yy, bmpPixels[xx + yy * tilesize]);
        }

        Bitmap bitmap = new(atlas2d.width, atlas2d.height);
        platform.BitmapSetPixelsArgb(bitmap, atlas2d.argb);
        UseTerrainTextureAtlas2d(bitmap, atlas2d.width);
    }
}