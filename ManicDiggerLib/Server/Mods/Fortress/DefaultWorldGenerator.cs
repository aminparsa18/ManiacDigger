using LibNoise;
using System.Diagnostics;

namespace ManicDigger.Mods;

public class DefaultWorldGenerator : IMod
{
    public void PreStart(IModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IModManager manager)
    {
        m = manager;
        chunksize = m.GetChunkSize();
        mapSizeX = m.GetMapSizeX();
        mapSizeY = m.GetMapSizeY();
        int seed = m.Seed;

        InitNoise(seed);

        BLOCK_AIR = m.GetBlockId("Empty");
        BLOCK_ADMINIUM = m.GetBlockId("Adminium");
        BLOCK_STONE = m.GetBlockId("Stone");
        BLOCK_DIRT = m.GetBlockId("Dirt");
        BLOCK_GRASS = m.GetBlockId("Grass");
        BLOCK_WATER = m.GetBlockId("Water");
        BLOCK_SAND = m.GetBlockId("Sand");
        BLOCK_GRAVEL = m.GetBlockId("Gravel");
        BLOCK_CLAY = m.GetBlockId("Clay");
        BLOCK_REDSAND = m.GetBlockId("RedSand");
        BLOCK_SANDSTONE = m.GetBlockId("Sandstone");
        BLOCK_REDSANDSTONE = m.GetBlockId("RedSandstone");
        BLOCK_CACTUS = m.GetBlockId("Cactus");
        BLOCK_DEADPLANT = m.GetBlockId("DeadPlant");
        BLOCK_GRASSPLANT = m.GetBlockId("GrassPlant");
        //BLOCK_SNOW = m.GetBlockId("Snow");
        //BLOCK_MUD = m.GetBlockId("Mud");

        m.RegisterWorldGenerator(GetChunk);
        m.RegisterPopulateChunk(PopulateChunk);
        m.RegisterOnSave(DisplayTimes);
        SaveImage();
    }

    // =========================================================================
    //  State
    // =========================================================================

    IModManager m;
    int chunksize, mapSizeX, mapSizeY;

    int BLOCK_AIR, BLOCK_ADMINIUM, BLOCK_STONE, BLOCK_DIRT, BLOCK_GRASS;
    int BLOCK_WATER, BLOCK_SAND, BLOCK_GRAVEL, BLOCK_CLAY;
    int BLOCK_REDSAND, BLOCK_SANDSTONE, BLOCK_REDSANDSTONE;
    int BLOCK_CACTUS, BLOCK_DEADPLANT, BLOCK_GRASSPLANT;
    int BLOCK_SNOW, BLOCK_MUD;

    // Tuned for 256x256 map — biome transitions every ~60-100 blocks
    const double CONTINENT_SCALE = 180.0;
    const double HEIGHT_SCALE = 120.0;
    const double TEMP_SCALE = 220.0;
    const double HUMIDITY_SCALE = 160.0;

    // Water sits at block 30. Land biomes always generate above this.
    const int WATER_LEVEL = 30;
    const int LAND_MIN_HEIGHT = 33;   // guaranteed minimum for any land biome surface

    int getChunkCalls, populateChunkCalls;
    long totalGetChunkMs, totalPopulateMs;
    readonly Stopwatch watch = new();

    // =========================================================================
    //  Noise — four fully independent layers
    // =========================================================================

    // Layer 1: Continent — decides ocean vs land
    // Billow with few octaves = large smooth blobs, always has + and - regions
    Billow continentNoise = new();

    // Layer 2: Height — two generators, blended by how far inland we are
    RidgedMultifractal heightRidged = new();   // sharp mountain ridges
    Billow heightSmooth = new();   // rolling hills / lowlands

    // Layer 3: Temperature — independent Perlin
    Perlin tempNoise = new();

    // Layer 4: Humidity — FastNoise at a different scale
    FastNoise humidityNoise = new();

    // Vegetation scatter
    FastNoise vegetationNoise = new();

    Perlin heightDetail = new();

    void InitNoise(int seed)
    {
        // Continent — very few octaves so features are large and definite
        continentNoise.Seed = seed + 3;
        continentNoise.Frequency = 1.0;
        continentNoise.OctaveCount = 8;
        continentNoise.Persistence = 0.4;
        continentNoise.Lacunarity = 2.1;
        continentNoise.NoiseQuality = NoiseQuality.Standard;

        // Ridged for mountain peaks
        heightRidged.Seed = seed + 1;
        heightRidged.Frequency = 1.0;
        heightRidged.OctaveCount = 7;
        heightRidged.Lacunarity = 2.4;
        heightRidged.NoiseQuality = NoiseQuality.Standard;

        // Smooth for lowland variety
        heightSmooth.Seed = seed + 2;
        heightSmooth.Frequency = 1.0;
        heightSmooth.OctaveCount = 5;
        heightSmooth.Persistence = 0.5;
        heightSmooth.Lacunarity = 2.0;
        heightSmooth.NoiseQuality = NoiseQuality.Standard;

        // Temperature — wider zones than humidity
        tempNoise.Seed = seed * 3 + 137;
        tempNoise.Frequency = 1.0;
        tempNoise.OctaveCount = 3;
        tempNoise.Persistence = 0.5;
        tempNoise.Lacunarity = 2.0;
        tempNoise.NoiseQuality = NoiseQuality.Standard;

        // Humidity — tighter zones, decorrelated from temperature
        humidityNoise.Seed = seed * 7 + 53;
        humidityNoise.Frequency = 1.0;
        humidityNoise.OctaveCount = 4;
        humidityNoise.Persistence = 0.55;
        humidityNoise.Lacunarity = 2.0;

        vegetationNoise.Seed = seed + 7;
    }

    // =========================================================================
    //  Biome
    // =========================================================================

    enum Biome
    {
        DeepOcean,
        Ocean,
        Shore,
        Plains,
        Forest,
        Swamp,
        Savanna,
        Desert,
        Dunes,
        Canyon,
        Hills,
        Mountains,
        SnowyMountains,
        DesertMountains,
    }

    /// <summary>
    /// Radial bias — pushes map center above ocean threshold so spawn is always land.
    /// Returns up to +0.6 at center, fading to 0 at the edges.
    /// </summary>
    double ContinentBias(int wx, int wy)
    {
        double cx = mapSizeX * 0.5;
        double cy = mapSizeY * 0.5;
        double dx = (wx - cx) / cx;
        double dy = (wy - cy) / cy;
        double dist = System.Math.Sqrt(dx * dx + dy * dy); // 0 at center, ~1.4 at corner
        return System.Math.Max(0.0, 0.6 - dist * 0.55);
    }

    /// <summary>
    /// Returns raw continent noise + center bias, normalised to [0, 1].
    /// Values above 0.5 are land, below 0.5 are ocean.
    /// </summary>
    double GetContinent(int wx, int wy)
    {
        double raw = continentNoise.GetValue(wx / CONTINENT_SCALE, 0, wy / CONTINENT_SCALE);
        if (!double.IsFinite(raw)) raw = 0.0;
        double biased = raw + ContinentBias(wx, wy);
        // Billow output ≈ [-0.5, 1.5] after bias. Normalise to [0,1].
        return System.Math.Clamp((biased + 0.3) / 1.6, 0.0, 1.0);
    }

    Biome GetBiome(int wx, int wy)
    {
        double cont = GetContinent(wx, wy);

        // Ocean zones — continent mask only, no wasted temp/humidity sampling
        if (cont < 0.18) return Biome.DeepOcean;
        if (cont < 0.28) return Biome.Ocean;
        if (cont < 0.36) return Biome.Shore;

        // Land — sample height, temperature, humidity
        double normH = GetNormHeight(wx, wy, cont);

        double rawTemp = tempNoise.GetValue(wx / TEMP_SCALE, 0, wy / TEMP_SCALE);
        double temp = System.Math.Clamp((rawTemp + 0.8) / 1.6, 0.0, 1.0);
        temp = System.Math.Max(0.0, temp - normH * 0.45); // cold at altitude

        double rawHum = humidityNoise.GetValue(wx / HUMIDITY_SCALE, 0, wy / HUMIDITY_SCALE);
        double humidity = System.Math.Clamp((rawHum + 0.8) / 1.6, 0.0, 1.0);

        return DetermineBiome(normH, temp, humidity);
    }

    /// <summary>
    /// Pure lookup table. No noise inside.
    /// normH    [0..1] : terrain elevation
    /// temp     [0..1] : 0=frozen, 1=scorching
    /// humidity [0..1] : 0=dry, 1=wet
    /// </summary>
    Biome DetermineBiome(double normH, double temp, double humidity)
    {
        // High altitude
        if (normH > 58)
        {
            if (temp < 0.30) return Biome.SnowyMountains;
            if (humidity < 0.30) return Biome.DesertMountains;
            return Biome.Mountains;
        }

        // Mid elevation
        if (normH > 0.35)
        {
            if (humidity < 0.30 && temp > 0.60) return Biome.Canyon;
            if (humidity < 0.40 && temp > 0.50) return Biome.Dunes;
            return Biome.Hills;
        }

        // Lowlands
        if (temp > 0.65 && humidity < 0.30) return Biome.Desert;
        if (temp > 0.60 && humidity < 0.50) return Biome.Savanna;
        if (humidity > 0.72) return Biome.Swamp;
        if (humidity > 0.52) return Biome.Forest;
        return Biome.Plains;
    }

    // =========================================================================
    //  Terrain height
    // =========================================================================

    /// <summary>
    /// Normalised height [0..1] for land tiles.
    /// Mountain noise weight rises inland (away from coast).
    /// </summary>
    double GetNormHeight(int wx, int wy, double cont)
    {
        double hx = wx / HEIGHT_SCALE;
        double hy = wy / HEIGHT_SCALE;

        double inland = System.Math.Clamp((cont - 0.36) / 0.64, 0.0, 1.0);
        double mw = System.Math.Max(inland * inland, 0.3);

        double rRaw = heightRidged.GetValue(hx, 0, hy);
        double sRaw = heightSmooth.GetValue(hx, 0, hy);
        double dRaw = heightDetail.GetValue(hx * 2, 0, hy * 2);

        if (!double.IsFinite(rRaw)) rRaw = 0.0;
        if (!double.IsFinite(sRaw)) sRaw = 0.0;
        if (!double.IsFinite(dRaw)) dRaw = 0.0;

        double rNorm = System.Math.Clamp((rRaw + 1.0) / 2.0, 0.0, 1.0);
        double sNorm = System.Math.Clamp((sRaw + 0.5) / 2.0, 0.0, 1.0);
        double dNorm = System.Math.Clamp((dRaw + 0.8) / 1.6, 0.0, 1.0);

        double baseH = double.Lerp(sNorm, rNorm, mw);
        return System.Math.Clamp(baseH * 0.82 + dNorm * 0.18, 0.0, 1.0);
    }

    /// <summary>
    /// Converts biome + normalised height to an absolute world block height.
    /// Land biomes are clamped to always be above LAND_MIN_HEIGHT.
    /// Ocean biomes are clamped to always be below WATER_LEVEL.
    /// </summary>
    int GetSurfaceZ(Biome biome, double normH)
    {
        (int baseH, int amp) = biome switch
        {
            Biome.DeepOcean => (8, 10),
            Biome.Ocean => (14, 12),
            Biome.Shore => (27, 5),
            Biome.Plains => (34, 10),
            Biome.Forest => (34, 12),
            Biome.Swamp => (32, 5),
            Biome.Savanna => (34, 12),
            Biome.Desert => (34, 14),
            Biome.Dunes => (36, 26),
            Biome.Canyon => (52, 28),
            Biome.Hills => (36, 34),
            Biome.Mountains => (44, 58),
            Biome.SnowyMountains => (46, 64),
            Biome.DesertMountains => (40, 48),
            _ => (34, 10),
        };

        int h = (int)(baseH + normH * amp);

        bool isOcean = biome == Biome.DeepOcean || biome == Biome.Ocean;
        if (isOcean) return System.Math.Min(h, WATER_LEVEL - 2);
        if (biome == Biome.Shore) return System.Math.Clamp(h, WATER_LEVEL - 1, WATER_LEVEL + 2);

        h = System.Math.Max(h, LAND_MIN_HEIGHT);
        return System.Math.Min(h, 90);  // ← add this — hard ceiling below map top
    }

    // =========================================================================
    //  Chunk generation
    // =========================================================================

    void GetChunk(int cx, int cy, int cz, ushort[] chunk)
    {
        getChunkCalls++;
        watch.Restart();

        int ox = cx * chunksize;
        int oy = cy * chunksize;
        int oz = cz * chunksize;

        for (int xx = 0; xx < chunksize; xx++)
            for (int yy = 0; yy < chunksize; yy++)
            {
                int wx = ox + xx;
                int wy = oy + yy;

                double cont = GetContinent(wx, wy);
                Biome biome = GetBiome(wx, wy);
                double normH = GetSmoothedNormHeight(wx, wy, cont, biome);
                int surfaceZ = GetSurfaceZ(biome, normH);

                for (int zz = 0; zz < chunksize; zz++)
                {
                    int wz = oz + zz;
                    int block;

                    if (wz == 0)
                    {
                        block = BLOCK_ADMINIUM;
                    }
                    else if (wz < surfaceZ - 5)
                    {
                        // Deep underground — always solid stone, no biome logic needed
                        block = BLOCK_STONE;
                    }
                    else
                    {
                        try { block = BuildBlock(biome, wz, surfaceZ, wx, wy); }
                        catch { block = BLOCK_STONE; }
                    }

                    chunk[m.Index3d(xx, yy, zz, chunksize, chunksize)] = (ushort)block;
                }
            }

        totalGetChunkMs += watch.ElapsedMilliseconds;
        watch.Stop();
    }

    double GetSmoothedNormHeight(int wx, int wy, double cont, Biome biome)
    {
        bool isOcean = biome == Biome.DeepOcean || biome == Biome.Ocean || biome == Biome.Shore;
        if (isOcean)
            return System.Math.Clamp(cont / 0.36, 0.0, 1.0);

        // Average over a 5x5 neighbourhood to smooth out sharp biome-boundary jumps
        const int R = 2;
        double sum = 0;
        for (int dx = -R; dx <= R; dx++)
            for (int dy = -R; dy <= R; dy++)
            {
                double nc = GetContinent(wx + dx, wy + dy);
                sum += GetNormHeight(wx + dx, wy + dy, nc);
            }
        return sum / ((2 * R + 1) * (2 * R + 1));
    }

    int BuildBlock(Biome biome, int wz, int surfaceZ, int wx, int wy)
    {
        if (wz == 0) return BLOCK_ADMINIUM;

        // ── Solid terrain ─────────────────────────────────────────────────────
        if (wz <= surfaceZ)
        {
            switch (biome)
            {
                case Biome.DeepOcean:
                case Biome.Ocean:
                    return wz < surfaceZ ? BLOCK_STONE : BLOCK_GRAVEL;

                case Biome.Shore:
                    return wz < surfaceZ - 1 ? BLOCK_STONE : BLOCK_SAND;

                case Biome.Swamp:
                    if (wz < surfaceZ - 3) return BLOCK_STONE;
                    if (wz < surfaceZ) return BLOCK_MUD;
                    return BLOCK_GRASS;

                case Biome.Plains:
                case Biome.Forest:
                case Biome.Savanna:
                    if (wz < surfaceZ - 4) return BLOCK_STONE;
                    if (wz < surfaceZ) return BLOCK_DIRT;
                    return BLOCK_GRASS;

                case Biome.Desert:
                    return wz < surfaceZ - 4 ? BLOCK_SANDSTONE : BLOCK_SAND;

                case Biome.Dunes:
                    return wz < surfaceZ - 6 ? BLOCK_SANDSTONE : BLOCK_SAND;

                case Biome.Canyon:
                    {
                        if (wz >= surfaceZ - 1) return BLOCK_REDSAND;
                        int d = surfaceZ - wz;
                        if (d == 7 || d == 18) return BLOCK_CLAY;
                        if (d == 8 || d == 20) return BLOCK_SANDSTONE;
                        if (d == 19) return BLOCK_SAND;
                        if (d < 28) return BLOCK_REDSANDSTONE;
                        return BLOCK_STONE;
                    }

                case Biome.Hills:
                    if (wz < surfaceZ - 3) return BLOCK_STONE;
                    if (wz < surfaceZ) return BLOCK_DIRT;
                    return BLOCK_GRASS;

                case Biome.Mountains:
                    if (wz < surfaceZ - 3) return BLOCK_STONE;
                    if (wz < surfaceZ) return BLOCK_DIRT;
                    return wz > 60 ? BLOCK_STONE : BLOCK_GRASS;

                case Biome.SnowyMountains:
                    if (wz < surfaceZ - 2) return BLOCK_STONE;
                    if (wz < surfaceZ) return BLOCK_DIRT;
                    return BLOCK_SNOW;

                case Biome.DesertMountains:
                    if (wz < surfaceZ - 4) return BLOCK_STONE;
                    if (wz < surfaceZ) return BLOCK_SANDSTONE;
                    return BLOCK_SAND;

                default: return BLOCK_STONE;
            }
        }

        // ── Above surface ─────────────────────────────────────────────────────

        // Water fill for ocean and low terrain
        if (wz <= WATER_LEVEL) return BLOCK_WATER;

        // Swamp: shallow water sits just above mud surface
        if (biome == Biome.Swamp && wz <= surfaceZ + 1) return BLOCK_WATER;

        // Sparse inline grass plants on plains/forest
        if (wz == surfaceZ + 1 &&
            (biome == Biome.Plains || biome == Biome.Forest || biome == Biome.Savanna))
        {
            if (vegetationNoise.GetValue(wx / 3.0, wy / 3.0, 0) > 0.45)
                return BLOCK_GRASSPLANT;
        }

        return BLOCK_AIR;
    }

    // =========================================================================
    //  Chunk population
    // =========================================================================

    readonly Random rnd = new();

    void PopulateChunk(int cx, int cy, int cz)
    {
        populateChunkCalls++;
        watch.Restart();

        // Skip chunks outside safe bounds entirely
        if (cx < 0 || cy < 0 || cx * chunksize >= mapSizeX || cy * chunksize >= mapSizeY)
            return;

        int ox = cx * chunksize;
        int oy = cy * chunksize;
        int oz = cz * chunksize;

        for (int i = 0; i < rnd.Next(30, 120); i++)
        {
            int x = ox + rnd.Next(chunksize);
            int y = oy + rnd.Next(chunksize);

            if (!m.IsValidPos(x, y, oz)) continue; // skip whole column if base invalid

            int surfaceZ = -1;
            for (int z = oz + chunksize - 1; z >= oz; z--)
            {
                if (!m.IsValidPos(x, y, z)) break; // break not continue — if one is invalid, rest below are too
                int b = m.GetBlock(x, y, z);
                if (b != BLOCK_AIR && b != BLOCK_WATER &&
                    m.IsValidPos(x, y, z + 1) && m.GetBlock(x, y, z + 1) == BLOCK_AIR)
                {
                    surfaceZ = z;
                    break;
                }
            }
            if (surfaceZ == -1) continue;

            int at = m.GetBlock(x, y, surfaceZ);
            if (at == BLOCK_GRASS) PlaceGrass(x, y, surfaceZ);
            else if (at == BLOCK_SAND || at == BLOCK_REDSAND) PlaceDesert(x, y, surfaceZ);
            else if (at == BLOCK_MUD) PlaceSwamp(x, y, surfaceZ);
        }

        totalPopulateMs += watch.ElapsedMilliseconds;
        watch.Stop();
    }

    void PlaceGrass(int x, int y, int z)
    {
        if (rnd.Next(10) == 0) TrySet(x, y, z + 1, BLOCK_GRASSPLANT);
    }

    void PlaceDesert(int x, int y, int z)
    {
        switch (rnd.Next(4))
        {
            case 0:
                int h = rnd.Next(2, 5);
                for (int j = 1; j <= h; j++)
                    if (!TrySet(x, y, z + j, BLOCK_CACTUS)) break;
                break;
            case 1:
                TrySet(x, y, z + 1, BLOCK_DEADPLANT);
                break;
        }
    }

    void PlaceSwamp(int x, int y, int z)
    {
        if (rnd.Next(7) == 0) TrySet(x, y, z + 1, BLOCK_GRASSPLANT);
    }

    bool TrySet(int x, int y, int z, int block)
    {
        if (!m.IsValidPos(x, y, z)) return false;
        if (m.GetBlock(x, y, z) != BLOCK_AIR) return false;
        m.SetBlock(x, y, z, block);
        return true;
    }

    // =========================================================================
    //  Debug / visualisation
    // =========================================================================

    void DisplayTimes()
    {
        if (getChunkCalls > 0)
        {
            long avg = totalGetChunkMs / getChunkCalls;
            Console.WriteLine($"[WorldGen] {getChunkCalls} GetChunk calls, {avg}ms avg");
            m.SendMessageToAll($"{avg}ms generation avg");
            totalGetChunkMs = 0; getChunkCalls = 0;
        }
        if (populateChunkCalls > 0)
        {
            long avg = totalPopulateMs / populateChunkCalls;
            Console.WriteLine($"[WorldGen] {populateChunkCalls} PopulateChunk calls, {avg}ms avg");
            m.SendMessageToAll($"{avg}ms population avg");
            totalPopulateMs = 0; populateChunkCalls = 0;
        }
    }

    void SaveImage()
    {
        var bmp = new Bitmap(mapSizeX, mapSizeY);
        unsafe
        {
            var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                     System.Drawing.Imaging.ImageLockMode.ReadWrite, bmp.PixelFormat);
            int bpp = Image.GetPixelFormatSize(bmp.PixelFormat) / 8;
            int rowB = bd.Width * bpp;
            byte* ptr = (byte*)bd.Scan0;

            Parallel.For(0, bd.Height, y =>
            {
                byte* line = ptr + y * bd.Stride;
                int x2 = 0;
                for (int x = 0; x < rowB; x += bpp)
                {
                    var c = BiomeColor(GetBiome(x2, y));
                    line[x] = (byte)c.B;
                    line[x + 1] = (byte)c.G;
                    line[x + 2] = (byte)c.R;
                    x2++;
                }
            });

            bmp.UnlockBits(bd);
        }
        bmp.Save("biomes.png");
        Console.WriteLine("[WorldGen] biomes.png saved.");
    }

    static Color BiomeColor(Biome b) => b switch
    {
        Biome.DeepOcean => Color.FromArgb(0, 0, 140),
        Biome.Ocean => Color.FromArgb(0, 30, 200),
        Biome.Shore => Color.FromArgb(210, 200, 120),
        Biome.Plains => Color.FromArgb(80, 180, 60),
        Biome.Forest => Color.FromArgb(30, 110, 30),
        Biome.Swamp => Color.FromArgb(40, 80, 50),
        Biome.Savanna => Color.FromArgb(180, 180, 60),
        Biome.Desert => Color.FromArgb(220, 210, 80),
        Biome.Dunes => Color.FromArgb(230, 220, 130),
        Biome.Canyon => Color.FromArgb(180, 90, 20),
        Biome.Hills => Color.FromArgb(100, 160, 60),
        Biome.Mountains => Color.FromArgb(140, 140, 140),
        Biome.SnowyMountains => Color.FromArgb(230, 240, 255),
        Biome.DesertMountains => Color.FromArgb(170, 140, 90),
        _ => Color.Pink,
    };
}