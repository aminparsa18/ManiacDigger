/// <summary>
/// Renders a static textured sky sphere, switching between day and night textures based on game state.
/// </summary>
public class ModSkySphereStatic : ModBase
{
    private const int SphereSize = 1000;
    private const int SphereSegments = 20;

    internal int SkyTexture = -1;
    private int skySphereTexture = -1;
    private int skySphereNightTexture = -1;
    private Model skyModel;

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        game.platform.GlDisableFog();
        DrawSkySphere(game);
        game.SetFog();
    }

    internal void DrawSkySphere(Game game)
    {
        if (skySphereTexture == -1)
        {
            skySphereTexture = LoadTexture(game, "skysphere.png");
            skySphereNightTexture = LoadTexture(game, "skyspherenight.png");
        }

        // Simple shadows always use the day texture
        SkyTexture = (!game.SkySphereNight || game.shadowssimple)
            ? skySphereTexture
            : skySphereNightTexture;

        Draw(game, game.CurrentFov());
    }

    public void Draw(Game game, float fov)
    {
        if (SkyTexture == -1)
            game.platform.ThrowException("InvalidOperationException");

        skyModel ??= game.platform.CreateModel(SphereModelData.GetSphereModelData(SphereSize, SphereSize, SphereSegments, SphereSegments));

        game.Set3dProjection(SphereSize * 2, fov);
        game.GLMatrixModeModelView();
        game.GLPushMatrix();
        game.GLTranslate(game.player.position.x, game.player.position.y, game.player.position.z);
        game.platform.BindTexture2d(SkyTexture);
        game.DrawModel(skyModel);
        game.GLPopMatrix();
        game.Set3dProjection(game.Zfar(), fov);
    }

    private static int LoadTexture(Game game, string filename)
    {
        Bitmap bmp = game.platform.BitmapCreateFromPng(game.GetAssetFile(filename), game.GetAssetFileLength(filename));
        int texture = game.platform.LoadTextureFromBitmap(bmp);
        game.platform.BitmapDelete(bmp);
        return texture;
    }
}