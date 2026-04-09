/// <summary>
/// Renders an animated sky sphere whose colors are driven by sun position and sky/glow textures.
/// </summary>
public class ModSkySphereAnimated : ModBase
{
    private const int TextureSize = 512;
    private const int FancySegments = 64;
    private const int NormalSegments = 20;

    private readonly ModBase stars = new ModSkySphereStatic();
    private ModelData skyModel;
    private int[] skyPixels;
    private int[] glowPixels;
    private bool started;

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        game.SkySphereNight = false;
        stars.OnNewFrameDraw3d(game, deltaTime);
        game.platform.GlDisableFog();
        DrawSkySphere(game);
        game.SetFog();
    }

    internal void DrawSkySphere(Game game)
    {
        if (!started)
        {
            started = true;
            LoadPixels(game, "sky.png", ref skyPixels);
            LoadPixels(game, "glow.png", ref glowPixels);
        }

        game.platform.GLDisableAlphaTest();
        game.platform.GlDisableDepthTest();
        Draw(game, game.CurrentFov());
        game.platform.GLEnableAlphaTest();
        game.platform.GlEnableDepthTest();
    }

    private void LoadPixels(Game game, string filename, ref int[] pixels)
    {
        Bitmap bmp = game.platform.BitmapCreateFromPng(game.GetAssetFile(filename), game.GetAssetFileLength(filename));
        pixels = new int[TextureSize * TextureSize * 4];
        game.platform.BitmapGetPixelsArgb(bmp, pixels);
        game.platform.BitmapDelete(bmp);
    }

    public void Draw(Game game, float fov)
    {
        int size = 1000;
        int segments = game.fancySkysphere ? FancySegments : NormalSegments;

        skyModel = GetSphereModelData2(skyModel, game.platform, size, size, segments, segments,
            skyPixels, glowPixels, game.sunPositionX, game.sunPositionY, game.sunPositionZ);
        
        game.platform.UpdateModel(skyModel);
        game.Set3dProjection(size * 2, fov);
        game.GLMatrixModeModelView();
        game.GLPushMatrix();
        game.GLTranslate(game.player.position.x, game.player.position.y, game.player.position.z);
        game.platform.BindTexture2d(0);
        game.DrawModelData(skyModel);
        game.GLPopMatrix();
        game.Set3dProjection(game.Zfar(), fov);
    }

    public static ModelData GetSphereModelData2(ModelData data,
        IGamePlatform platform,
        float radius, float height, int segments, int rings,
        int[] skyPixels, int[] glowPixels,
        float sunX, float sunY, float sunZ)
    {
        if (data == null)
        {
            data = new ModelData
            {
                xyz = new float[rings * segments * 3],
                uv = new float[rings * segments * 2],
                rgba = new byte[rings * segments * 4]
            };
            data.SetVerticesCount(segments * rings);
            data.SetIndicesCount(segments * rings * 6);
            data.setIndices(CalculateElements(segments, rings));
        }

        // Normalize sun direction once outside the vertex loop
        float sunLength = MathF.Sqrt(sunX * sunX + sunY * sunY + sunZ * sunZ);
        if (sunLength == 0) sunLength = 1;
        float sunXN = sunX / sunLength;
        float sunYN = sunY / sunLength;
        float sunZN = sunZ / sunLength;

        int i = 0;
        for (int y = 0; y < rings; y++)
        {
            float phi = (y / (float)(rings - 1)) * MathF.PI;
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            for (int x = 0; x < segments; x++)
            {
                float theta = (x / (float)(segments - 1)) * 2 * MathF.PI;
                float vx = radius * sinPhi * MathF.Cos(theta);
                float vy = height * cosPhi;
                float vz = radius * sinPhi * MathF.Sin(theta);

                data.xyz[i * 3] = vx;
                data.xyz[i * 3 + 1] = vy;
                data.xyz[i * 3 + 2] = vz;
                data.uv[i * 2] = x / (float)(segments - 1);
                data.uv[i * 2 + 1] = y / (float)(rings - 1);

                float vertLen = MathF.Sqrt(vx * vx + vy * vy + vz * vz);
                float vxN = vx / vertLen, vyN = vy / vertLen, vzN = vz / vertLen;

                float dx = vxN - sunXN, dy = vyN - sunYN, dz = vzN - sunZN;
                float proximityToSun = 1f - MathF.Sqrt(dx * dx + dy * dy + dz * dz) / 2f;

                int skyColor = Texture2d(platform, skyPixels, (sunYN + 2f) / 4f, 1f - (vyN + 1f) / 2f);
                int glowColor = Texture2d(platform, glowPixels, (sunYN + 1f) / 2f, 1f - proximityToSun);

                float skyA = Game.ColorA(skyColor) / 255f;
                float skyR = Game.ColorR(skyColor) / 255f;
                float skyG = Game.ColorG(skyColor) / 255f;
                float skyB = Game.ColorB(skyColor) / 255f;
                float glowA = Game.ColorA(glowColor) / 255f;
                float glowR = Game.ColorR(glowColor) / 255f;
                float glowG = Game.ColorG(glowColor) / 255f;
                float glowB = Game.ColorB(glowColor) / 255f;

                // Blend sky and glow
                data.rgba[i * 4] = (byte)(Math.Min(1f, skyR + glowR * glowA) * 255);
                data.rgba[i * 4 + 1] = (byte)(Math.Min(1f, skyG + glowG * glowA) * 255);
                data.rgba[i * 4 + 2] = (byte)(Math.Min(1f, skyB + glowB * glowA) * 255);
                data.rgba[i * 4 + 3] = (byte)(Math.Min(1f, skyA) * 255);
                i++;
            }
        }

        return data;
    }

    /// <summary>
    /// Generates the triangle index buffer for a UV-sphere with the given tessellation.
    /// Each quad cell in the ring/segment grid is split into two triangles.
    /// </summary>
    /// <param name="segments">Number of subdivisions around the equator.</param>
    /// <param name="rings">Number of subdivisions from pole to pole.</param>
    private static int[] CalculateElements(int segments, int rings)
    {
        int[] indices = new int[segments * rings * 6];
        int i = 0;
        for (int y = 0; y < rings - 1; y++)
        {
            for (int x = 0; x < segments - 1; x++)
            {
                int bl = y * segments + x;
                int tl = (y + 1) * segments + x;
                int tr = (y + 1) * segments + x + 1;
                int br = y * segments + x + 1;

                indices[i++] = bl; indices[i++] = tl; indices[i++] = tr;
                indices[i++] = tr; indices[i++] = br; indices[i++] = bl;
            }
        }
        return indices;
    }

    private static int Texture2d(IGamePlatform platform, int[] pixelsArgb, float x, float y)
    {
        int px = PositiveMod((int)(x * (TextureSize - 1)), TextureSize - 1);
        int py = PositiveMod((int)(y * (TextureSize - 1)), TextureSize - 1);
        return pixelsArgb[MapUtilCi.Index2d(px, py, TextureSize)];
    }

    private static int PositiveMod(int i, int n) => (i % n + n) % n;
}