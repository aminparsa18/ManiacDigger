using LibNoise;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using static ManicDigger.Mods.ModNetworkProcess;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using Math = System.Math;

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
        BLOCK_REDSAND = m.GetBlockId("RedSand");
        BLOCK_CACTUS = m.GetBlockId("Cactus");
        BLOCK_DEADPLANT = m.GetBlockId("DeadPlant");
        BLOCK_GRASSPLANT = m.GetBlockId("GrassPlant");

        m.RegisterWorldGenerator(GetChunk);
        m.RegisterPopulateChunk(PopulateChunk);
        m.RegisterOnSave(DisplayTimes);
    }

    // =========================================================================
    //  State
    // =========================================================================

    private IModManager m;
    private int chunksize, mapSizeX, mapSizeY;

    private int BLOCK_AIR, BLOCK_ADMINIUM, BLOCK_STONE, BLOCK_DIRT, BLOCK_GRASS;
    private int BLOCK_WATER, BLOCK_SAND;
    private int BLOCK_REDSAND;
    private int BLOCK_CACTUS, BLOCK_DEADPLANT, BLOCK_GRASSPLANT;
    private readonly int BLOCK_MUD;

    // Tuned for 256x256 map — biome transitions every ~60-100 blocks
    private const float CONTINENT_SCALE = 260f;
    private const float HEIGHT_SCALE = 140f;
    private const float TEMP_SCALE = 300f;
    private const float HUMIDITY_SCALE = 240f;

    // Water sits at block 30. Land biomes always generate above this.
    private const int WATER_LEVEL = 30;

    private int getChunkCalls, populateChunkCalls;
    private long totalGetChunkMs, totalPopulateMs;
    private readonly Stopwatch watch = new();

    // =========================================================================
    //  Noise — four fully independent layers
    // =========================================================================

    // Layer 1: Continent — decides ocean vs land
    // Billow with few octaves = large smooth blobs, always has + and - regions
    private readonly Billow continentNoise = new();

    // Layer 2: Height — two generators, blended by how far inland we are
    private readonly RidgedMultifractal heightRidged = new();   // sharp mountain ridges
    private readonly Billow heightSmooth = new();   // rolling hills / lowlands

    // Layer 3: Temperature — independent Perlin
    private readonly Perlin tempNoise = new();

    // Layer 4: Humidity — FastNoise at a different scale
    private readonly FastNoise humidityNoise = new();

    // Vegetation scatter
    private readonly FastNoise vegetationNoise = new();

    private readonly Perlin heightDetail = new();

    private readonly FastNoise warpNoise = new();

    private void InitNoise(int seed)
    {
        // Continent — very few octaves so features are large and definite
        continentNoise.Seed = seed + 3;
        continentNoise.Frequency = 1f;
        continentNoise.OctaveCount = 8;
        continentNoise.Persistence = 0.4f;
        continentNoise.Lacunarity = 2.1f;
        continentNoise.NoiseQuality = NoiseQuality.Standard;

        // Ridged for mountain peaks
        heightRidged.Seed = seed + 1;
        heightRidged.Frequency = 1f;
        heightRidged.OctaveCount = 7;
        heightRidged.Lacunarity = 2.4f;
        heightRidged.NoiseQuality = NoiseQuality.Standard;

        // Smooth for lowland variety
        heightSmooth.Seed = seed + 2;
        heightSmooth.Frequency = 1f;
        heightSmooth.OctaveCount = 5;
        heightSmooth.Persistence = 0.5f;
        heightSmooth.Lacunarity = 2.0f;
        heightSmooth.NoiseQuality = NoiseQuality.Standard;

        // Temperature — wider zones than humidity
        tempNoise.Seed = seed * 3 + 137;
        tempNoise.Frequency = 1f;
        tempNoise.OctaveCount = 3;
        tempNoise.Persistence = 0.5f;
        tempNoise.Lacunarity = 2.0f;
        tempNoise.NoiseQuality = NoiseQuality.Standard;

        // Humidity — tighter zones, decorrelated from temperature
        humidityNoise.Seed = seed * 7 + 53;
        humidityNoise.Frequency = 1f;
        humidityNoise.OctaveCount = 4;
        humidityNoise.Persistence = 0.55f;
        humidityNoise.Lacunarity = 2.0f;

        warpNoise.Seed = seed + 999;
        warpNoise.Frequency = 1f;
        warpNoise.OctaveCount = 3;
        warpNoise.Persistence = 0.5f;
        warpNoise.Lacunarity = 2.0f;

        vegetationNoise.Seed = seed + 7;
    }

    /// <summary>
    /// Radial bias — pushes map center above ocean threshold so spawn is always land.
    /// Returns up to +0.6 at center, fading to 0 at the edges.
    /// </summary>
    private float ContinentBias(int wx, int wy)
    {
        float cx = mapSizeX * 0.5f;
        float cy = mapSizeY * 0.5f;
        float dx = (wx - cx) / cx;
        float dy = (wy - cy) / cy;
        float dist = MathF.Sqrt(dx * dx + dy * dy); // 0 at center, ~1.4 at corner
        return MathF.Max(0f, 0.6f - dist * 0.55f);
    }

    /// <summary>
    /// Returns raw continent noise + center bias, normalised to [0, 1].
    /// Values above 0.5 are land, below 0.5 are ocean.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetContinentF(float wx, float wy)
    {
        float c1 = continentNoise.GetValue(wx / CONTINENT_SCALE, 0f, wy / CONTINENT_SCALE);
        float c2 = continentNoise.GetValue(wx / (CONTINENT_SCALE * 3f), 0f, wy / (CONTINENT_SCALE * 3f));

        float raw = c1 * 0.85f + c2 * 0.15f;

        if (!float.IsFinite(raw)) raw = 0f;

        float biased = raw + ContinentBias((int)wx, (int)wy); // bias can stay int-based
        return Math.Clamp((biased + 0.3f) / 1.6f, 0f, 1f);
    }

    // =========================================================================
    //  Terrain height
    // =========================================================================

    // Raw per-point noise sample — separated so the 5×5 loop stays clean
    private float SampleNormHeight(float wx, float wy, float cont)
    {
        float hx = wx / HEIGHT_SCALE;
        float hy = wy / HEIGHT_SCALE;
        float inland = Math.Clamp((cont - 0.36f) / 0.64f, 0f, 1f);

        float rRaw = heightRidged.GetValue(hx, 0f, hy);
        float sRaw = heightSmooth.GetValue(hx, 0f, hy);
        float dRaw = heightDetail.GetValue(hx * 2f, 0f, hy * 2f);

        if (!float.IsFinite(rRaw)) rRaw = 0f;
        if (!float.IsFinite(sRaw)) sRaw = 0f;
        if (!float.IsFinite(dRaw)) dRaw = 0f;

        float rNorm = Math.Clamp((rRaw + 1.0f) / 2.0f, 0f, 1f);
        float sNorm = Math.Clamp((sRaw + 0.5f) / 2.0f, 0f, 1f);
        float dNorm = Math.Clamp((dRaw + 0.8f) / 1.6f, 0f, 1f);

        float mw = MathF.Pow(inland, 2.0f);
        float baseH = float.Lerp(sNorm, rNorm, mw);

        float h = baseH * 0.82f + dNorm * 0.18f;

        // fake erosion: sharpen high areas, flatten low areas
        float erosion = h * h * (3f - 2f * h);   // smooth curve
        h = float.Lerp(h, erosion, 0.5f);

        // optional: exaggerate peaks slightly
        h = MathF.Pow(h, 1.15f);

        return Math.Clamp(h, 0f, 1f);
    }

    // =========================================================================
    //  Chunk generation
    // =========================================================================

    private void GetChunk(int cx, int cy, int cz, ushort[] chunk)
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

                var (wxw, wyw) = Warp(wx, wy);

                float cont = GetContinentF(wxw, wyw);
                float normH = SampleNormHeight(wxw, wyw, cont);

                float rawTemp = tempNoise.GetValue(wxw / TEMP_SCALE, 0f, wyw / TEMP_SCALE);
                float temp = Math.Clamp((rawTemp + 1f) * 0.5f, 0f, 1f);

                // increase contrast (push toward extremes)
                temp = MathF.Pow(temp, 0.85f);

                float rawHum = humidityNoise.GetValue(wxw / HUMIDITY_SCALE, 0f, wyw / HUMIDITY_SCALE);
                float humidity = Math.Clamp((rawHum + 1f) * 0.5f, 0f, 1f);

                // push values away from middle → real dry/wet regions
                temp = MathF.Pow(temp, 0.9f);
                humidity = MathF.Pow(humidity, 1.15f);

                var weights = GetBiomeWeights(normH, temp, humidity);
                int surfaceZ = ComputeSurfaceZ(cont, normH, weights);

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
                        block = BuildBlock(weights, wz, surfaceZ, wx, wy);
                    }

                    chunk[m.Index3d(xx, yy, zz, chunksize, chunksize)] = (ushort)block;
                }
            }

        totalGetChunkMs += watch.ElapsedMilliseconds;
        watch.Stop();
    }

    // ── Continuous base height from continent value ───────────────────────────────
    // This replaces the per-biome (baseH, amp) lookup table entirely.
    // The ocean floor slopes naturally upward through shore and onto land.
    // Water level is 30 — anything below that fills with water automatically.
    private static float ContinentToBaseZ(float cont)
    {
        if (cont < 0.18f) return Lerp(5f, 14f, cont / 0.18f);                    // deep ocean  z= 5→14
        if (cont < 0.28f) return Lerp(14f, 22f, (cont - 0.18f) / 0.10f);          // ocean       z=14→22
        if (cont < 0.36f) return Lerp(22f, 30f, (cont - 0.28f) / 0.08f);          // shore slope z=22→30
        return Lerp(30f, 36f, Math.Clamp((cont - 0.36f) / 0.64f, 0f, 1f));        // land base   z=30→36
    }

    private static BiomeWeights GetBiomeWeights(float normH, float temp, float humidity)
    {
        BiomeWeights w = new();

        // Height influence
        w.Mountains = SmoothStep(0.5f, 0.8f, normH);
        w.Plains = 1f - w.Mountains;

        // Temperature
        float hot = SmoothStep(0.6f, 1f, temp);
        float cold = 1f - temp;

        // Humidity
        float wet = humidity;

        float dryness = SmoothStep(0.5f, 0.9f, 1f - humidity);
        float heat = SmoothStep(0.55f, 0.9f, temp);

        // additive instead of multiplicative bias
        w.Desert = (heat * 0.7f + dryness * 0.6f) * (1f - w.Mountains);
        w.Forest = wet * (1f - hot) * (1f - w.Mountains);
        w.Snow = cold * w.Mountains;

        w.Normalize();
        return w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SmoothStep(float a, float b, float x)
    {
        x = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    private int ComputeSurfaceZ(float cont, float normH, BiomeWeights weights)
    {
        float baseZ = ContinentToBaseZ(cont);

        float inland = Math.Clamp((cont - 0.36f) / 0.64f, 0f, 1f);
        float amp = GetBlendedAmplitude(weights) * inland;

        return Math.Clamp((int)(baseZ + normH * amp), 1, 90);
    }

    private float GetBlendedAmplitude(BiomeWeights w) => w.Plains * 12f +
            w.Forest * 14f +
            w.Desert * 18f +
            w.Mountains * 58f +
            w.Snow * 64f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BuildBlock(BiomeWeights w, int wz, int surfaceZ, int wx, int wy)
    {
        if (wz <= 0)
            return BLOCK_ADMINIUM;

        int depth = surfaceZ - wz;

        // ── BELOW SURFACE ─────────────────────────────────────────────
        if (wz <= surfaceZ)
        {
            // deep underground
            if (depth > 6)
                return BLOCK_STONE;

            // blended material selection
            float sandness = w.Desert;
            float grassness = w.Plains + w.Forest;
            float rockness = w.Mountains + w.Snow;

            // normalize just in case
            float sum = sandness + grassness + rockness;
            if (sum > 0f)
            {
                sandness /= sum;
                grassness /= sum;
                rockness /= sum;
            }

            // surface layer
            if (depth == 0)
            {
                if (sandness > 0.5f)
                {
                    float dune = heightDetail.GetValue(wx * 0.08f, 0f, wy * 0.08f);
                    if (dune > 0.2f)
                        return BLOCK_SAND;
                    else
                        return BLOCK_REDSAND;
                }
                if (rockness > 0.6f) return BLOCK_STONE;
                return BLOCK_GRASS;
            }

            // subsurface
            if (depth <= 3)
            {
                if (sandness > 0.5f) return BLOCK_SAND;
                return BLOCK_DIRT;
            }

            // transition to stone
            if (rockness > 0.5f)
                return BLOCK_STONE;

            return BLOCK_DIRT;
        }

        // ── ABOVE SURFACE ─────────────────────────────────────────────

        if (wz <= WATER_LEVEL)
            return BLOCK_WATER;

        // simple vegetation (can improve later)
        if (wz == surfaceZ + 1)
        {
            float density = vegetationNoise.GetValue(wx / 20f, wy / 20f, 0f);  // large clusters
            float detail = vegetationNoise.GetValue(wx / 3f, wy / 3f, 0f);     // fine variation

            if (density > 0.2f && detail > 0.5f && (w.Plains + w.Forest) > 0.4f)
                return BLOCK_GRASSPLANT;
        }

        return BLOCK_AIR;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (float x, float y) Warp(int wx, int wy)
    {
        float scale = 140f;      // controls size of distortion
        float strength = 30f;   // how strong the warp is

        float nx = wx / scale;
        float ny = wy / scale;

        float dx = warpNoise.GetValue(nx, ny, 0f);
        float dy = warpNoise.GetValue(nx + 31.4f, ny + 47.2f, 0f);

        return (
            wx + dx * strength,
            wy + dy * strength
        );
    }

    // =========================================================================
    //  Chunk population
    // =========================================================================

    private readonly Random rnd = new();

    private void PopulateChunk(int cx, int cy, int cz)
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

    private void PlaceGrass(int x, int y, int z)
    {
        if (rnd.Next(10) == 0) TrySet(x, y, z + 1, BLOCK_GRASSPLANT);
    }

    private void PlaceDesert(int x, int y, int z)
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

    private void PlaceSwamp(int x, int y, int z)
    {
        if (rnd.Next(7) == 0) TrySet(x, y, z + 1, BLOCK_GRASSPLANT);
    }

    private bool TrySet(int x, int y, int z, int block)
    {
        if (!m.IsValidPos(x, y, z)) return false;
        if (m.GetBlock(x, y, z) != BLOCK_AIR) return false;
        m.SetBlock(x, y, z, block);
        return true;
    }

    // =========================================================================
    //  Debug / visualisation
    // =========================================================================

    private void DisplayTimes()
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

    // ── Lerp helper ───────────────────────────────────────────────────────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private struct BiomeWeights
    {
        public float Plains;
        public float Desert;
        public float Forest;
        public float Mountains;
        public float Snow;

        public void Normalize()
        {
            float sum = Plains + Desert + Forest + Mountains + Snow;
            if (sum <= 0f) return;

            Plains /= sum;
            Desert /= sum;
            Forest /= sum;
            Mountains /= sum;
            Snow /= sum;
        }
    }
}