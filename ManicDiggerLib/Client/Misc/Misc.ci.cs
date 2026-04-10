public class TextureAtlasConverter
{
    /// <summary>
    /// Splits a square 2-D texture atlas into one or more 1-D (vertical strip) atlases,
    /// each no taller than <paramref name="atlasSizeLimit"/> pixels.
    /// </summary>
    /// <param name="p">The platform instance (unused directly; passed for context).</param>
    /// <param name="atlas2d">The source square atlas. Must be <paramref name="tiles"/> × <paramref name="tiles"/> tiles.</param>
    /// <param name="tiles">Number of tiles along each axis of the source atlas (e.g. 16 for a 16×16 atlas).</param>
    /// <param name="atlasSizeLimit">Maximum height in pixels of each output 1-D atlas.</param>
    /// <param name="retCount">Returns the number of 1-D atlases produced.</param>
    /// <returns>
    /// An array of <see cref="Bitmap"/> strips. Only the first <paramref name="retCount"/>
    /// entries are valid; the rest are <see langword="null"/>.
    /// </returns>
    public static Bitmap[] Atlas2dInto1d(IGamePlatform p, Bitmap atlas2d, int tiles, int atlasSizeLimit, out int retCount)
    {
        PixelBuffer orig = PixelBuffer.FromBitmap(atlas2d);

        int tilesize = orig.Width / tiles;
        int totalTiles = tiles * tiles;
        int atlasesCount = Math.Max(1, (totalTiles * tilesize) / atlasSizeLimit);
        int tilesPerAtlas = totalTiles / atlasesCount;

        Bitmap[] atlases = new Bitmap[128];
        int atlasIndex = 0;
        PixelBuffer atlas1d = null;

        for (int i = 0; i < totalTiles; i++)
        {
            int x = i % tiles;
            int y = i / tiles;

            if (i % tilesPerAtlas == 0)
            {
                if (atlas1d != null)
                    atlases[atlasIndex++] = atlas1d.ToBitmap();

                atlas1d = PixelBuffer.Create(tilesize, atlasSizeLimit);
            }

            int destY = (i % tilesPerAtlas) * tilesize;
            for (int yy = 0; yy < tilesize; yy++)
                for (int xx = 0; xx < tilesize; xx++)
                    atlas1d.SetPixel(xx, destY + yy, orig.GetPixel(x * tilesize + xx, y * tilesize + yy));
        }

        atlases[atlasIndex++] = atlas1d.ToBitmap();
        retCount = atlasesCount;
        return atlases;
    }
}

public class VecCito3i
{
    public int x;
    public int y;
    public int z;

    public static VecCito3i CitoCtr(int _x, int _y, int _z)
    {
        VecCito3i v = new()
        {
            x = _x,
            y = _y,
            z = _z
        };

        return v;
    }

    public void Add(int _x, int _y, int _z, VecCito3i result)
    {
        result.x = x + _x;
        result.y = y + _y;
        result.z = z + _z;
    }
}

public class GameVersionHelper
{
    public static bool ServerVersionAtLeast(IGamePlatform platform, string serverGameVersion, int year, int month, int day)
    {
        if (serverGameVersion == null)
        {
            return true;
        }
        if (VersionToInt(platform, serverGameVersion) < DateToInt(year, month, day))
        {
            return false;
        }
        return true;
    }

    private static bool IsVersionDate(IGamePlatform platform, string version)
    {
        if (version.Length >= 10)
        {
            if (version[4] == 45 && version[7] == 45) // '-'
            {
                return true;
            }
        }
        return false;
    }

    private static int VersionToInt(IGamePlatform platform, string version)
    {
        int max = 1000 * 1000 * 1000;
        if (!IsVersionDate(platform, version))
        {
            return max;
        }
        if (DateTime.TryParseExact(version[..10], "yyyy.MM.dd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
        {
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }
        return max;
    }

    private static int DateToInt(int year, int month, int day)
    {
        return year * 10000 + month * 100 + day;
    }
}