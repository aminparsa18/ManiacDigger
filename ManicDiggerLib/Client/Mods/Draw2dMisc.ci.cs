using ManicDigger;
using OpenTK.Mathematics;

public class ModDraw2dMisc : ModBase
{
    private readonly IGameClient game;
    private readonly IGamePlatform platform;
    public ModDraw2dMisc(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    public override void OnNewFrameDraw2d(float deltaTime)
    {
        if (game.GuiState == GuiState.Normal)
            DrawAim();

        if (game.GuiState != GuiState.MapLoading)
        {
            DrawEnemyHealthBlock();
            DrawAmmo();
            DrawLocalPosition();
            DrawBlockInfo();
        }

        DrawMouseCursor();
        DrawDisconnected();
    }

    // ── Block / entity health display ─────────────────────────────────────────

    public void DrawBlockInfo()
    {
        if (!game.DrawBlockInfo) return;

        int x = game.SelectedBlockPositionX;
        int y = game.SelectedBlockPositionZ;
        int z = game.SelectedBlockPositionY;

        if (!game.VoxelMap.IsValidPos(x, y, z)) return;

        int blocktype = game.VoxelMap.GetBlock(x, y, z);
        if (!game.IsValid(blocktype)) return;

        game.CurrentAttackedBlock = new Vector3i(x, y, z);
        DrawEnemyHealthBlock();
    }

    internal void DrawEnemyHealthBlock()
    {
        if (game.CurrentAttackedBlock != null)
        {
            int x = game.CurrentAttackedBlock.Value.X;
            int y = game.CurrentAttackedBlock.Value.Y;
            int z = game.CurrentAttackedBlock.Value.Z;
            int blocktype = game.VoxelMap.GetBlock(x, y, z);
            float health = game.GetCurrentBlockHealth(x, y, z);
            float progress = health / game.BlockRegistry.Strength[blocktype];

            // Cache the translated name — used in up to two calls below.
            string name = game.Language.Get("Block_" + game.BlockTypes[blocktype].Name);

            if (game.IsUsableBlock(blocktype))
                DrawEnemyHealthUseInfo(name, progress, true);

            DrawEnemyHealthBackground(name);
        }

        if (game.CurrentlyAttackedEntity != -1)
        {
            Entity e = game.Entities[game.CurrentlyAttackedEntity];
            if (e == null) return;

            float health = e.playerStats != null
                ? (float)e.playerStats.CurrentHealth / e.playerStats.MaxHealth
                : 1f;

            string name = e.drawName?.Name ?? "Unknown";
            string translatedName = game.Language.Get(name);

            if (e.usable)
                DrawEnemyHealthUseInfo(translatedName, health, useInfo: true);

            DrawEnemyHealthBackground(translatedName);
        }
    }

    /// <summary>
    /// Draws the full-width background bar and name label (no progress, no use hint).
    /// Replaces the old <c>DrawEnemyHealthCommon</c> which accepted a <c>progress</c>
    /// parameter it silently ignored.
    /// </summary>
    internal void DrawEnemyHealthBackground(string name)
        => DrawEnemyHealthUseInfo(name, progress: 1f, useInfo: false);

    internal void DrawEnemyHealthUseInfo(string name, float progress, bool useInfo)
    {
        int barHeight = useInfo ? 55 : 35;
        int whiteTexId = game.GetOrCreateWhiteTexture(); // cache — was called twice

        game.Draw2dTexture(whiteTexId, game.Xcenter(300), 40, 300, barHeight, null, 0,
            ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
        game.Draw2dTexture(whiteTexId, game.Xcenter(300), 40, 300 * progress, barHeight, null, 0,
            ColorUtils.ColorFromArgb(255, 255, 0, 0), false);

        TextRenderer.TextSize(name, 14, out int w, out _);
        game.Draw2dText1(name, game.Xcenter(w), 40, 14, null, false);

        if (useInfo)
        {
            string hint = string.Format(game.Language.PressToUse(), "E");
            TextRenderer.TextSize(hint, 10, out w, out _);
            game.Draw2dText1(hint, game.Xcenter(w), 70, 10, null, false);
        }
    }

    // ── Crosshair ─────────────────────────────────────────────────────────────

    internal void DrawAim()
    {
        if (game.CameraType == CameraType.Overhead) return;

        const int AimSize = 32;
        platform.BindTexture2d(0);

        if (game.CurrentAimRadius() > 1)
        {
            float fov_ = game.CurrentFov();
            game.Circle3i(platform.GetCanvasWidth() / 2, platform.GetCanvasHeight() / 2,
                game.CurrentAimRadius() * game.CurrentFov() / fov_);
        }

        game.Draw2dBitmapFile("target.png",
            platform.GetCanvasWidth() / 2 - AimSize / 2,
            platform.GetCanvasHeight() / 2 - AimSize / 2,
            AimSize, AimSize);
    }

    // ── Mouse cursor ──────────────────────────────────────────────────────────

    internal void DrawMouseCursor()
    {
        if (!game.GetFreeMouse()) return;
        if (!platform.MouseCursorIsVisible())
            game.Draw2dBitmapFile("mousecursor.png", game.MouseCurrentX, game.MouseCurrentY, 32, 32);
    }

    // ── Ammo counter ──────────────────────────────────────────────────────────

    internal void DrawAmmo()
    {
        Packet_Item item = game.Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block) return;
        if (!game.BlockTypes[item.BlockId].IsPistol) return;

        int loaded = game.LoadedAmmo[item.BlockId];
        int total = game.TotalAmmo[item.BlockId];
        string s = $"{loaded}/{total - loaded}";
        int color = loaded == 0
            ? ColorUtils.ColorFromArgb(255, 255, 0, 0)
            : ColorUtils.ColorFromArgb(255, 255, 255, 255);

        game.Draw2dText1(s, platform.GetCanvasWidth() - game.TextSizeWidth(s, 18) - 50,
            platform.GetCanvasWidth() - game.TextSizeHeight(s, 18) - 50, 18, color, false);

        if (loaded == 0)
        {
            const string PressR = "Press R to reload"; // TODO: move to game.language
            game.Draw2dText1(PressR,
                platform.GetCanvasWidth() - game.TextSizeWidth(PressR, 14) - 50,
                platform.GetCanvasHeight() - game.TextSizeHeight(s, 14) - 80,
                14, ColorUtils.ColorFromArgb(255, 255, 0, 0), false);
        }
    }

    // ── Debug position overlay ────────────────────────────────────────────────

    private void DrawLocalPosition()
    {
        if (!game.EnableDrawPosition) return;

        float heading = Game.HeadingByte(
            game.Player.position.rotx, game.Player.position.roty, game.Player.position.rotz);
        float pitch = Game.PitchByte(
            game.Player.position.rotx, game.Player.position.roty, game.Player.position.rotz);

        // Single interpolated string replaces seven string.Concat calls.
        string postext =
            $"X: {MathF.Floor(game.Player.position.x)},\t" +
            $"Y: {MathF.Floor(game.Player.position.z)},\t" +
            $"Z: {MathF.Floor(game.Player.position.y)}\n" +
            $"Heading: {MathF.Floor(heading)}\n" +
            $"Pitch: {MathF.Floor(pitch)}";

        game.Draw2dText1(postext, 100, 460, Game.ChatFontSize, null, false);
    }

    // ── Disconnected overlay ──────────────────────────────────────────────────

    private void DrawDisconnected()
    {
        float lagSeconds =
            (platform.TimeMillisecondsFromStart - game.LastReceivedMilliseconds) / 1000f;

        if (lagSeconds < Game.DISCONNECTED_ICON_AFTER_SECONDS) 
            return;
        if (lagSeconds >= 60 * 60 * 24) 
            return;
        if (game.InvalidVersionDrawMessage != null) 
            return;
        if (game.IsSinglePlayer && !platform.SinglePlayerServerLoaded())
            return;

        game.Draw2dBitmapFile("disconnected.png", platform.GetCanvasWidth() - 100, 50, 50, 50);

        game.Draw2dText1(((int)lagSeconds).ToString(),
            platform.GetCanvasWidth() - 100, 50 + 50 + 10, 12, null, false);

        const string Reconnect = "Press F6 to reconnect";
        game.Draw2dText1(Reconnect,
            platform.GetCanvasWidth() / 2 - 200 / 2, 50, 12, null, false);
    }
}