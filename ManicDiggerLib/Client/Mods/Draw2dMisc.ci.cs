using OpenTK.Mathematics;

public class ModDraw2dMisc : ModBase
{
    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        if (game.guistate == GuiState.Normal)
            DrawAim(game);

        if (game.guistate != GuiState.MapLoading)
        {
            DrawEnemyHealthBlock(game);
            DrawAmmo(game);
            DrawLocalPosition(game);
            DrawBlockInfo(game);
        }

        DrawMouseCursor(game);
        DrawDisconnected(game);
    }

    // ── Block / entity health display ─────────────────────────────────────────

    public static void DrawBlockInfo(Game game)
    {
        if (!game.drawblockinfo) return;

        int x = game.SelectedBlockPositionX;
        int y = game.SelectedBlockPositionZ;
        int z = game.SelectedBlockPositionY;

        if (!game.VoxelMap.IsValidPos(x, y, z)) return;

        int blocktype = game.VoxelMap.GetBlock(x, y, z);
        if (!game.IsValid(blocktype)) return;

        game.currentAttackedBlock = new Vector3i(x, y, z);
        DrawEnemyHealthBlock(game);
    }

    internal static void DrawEnemyHealthBlock(Game game)
    {
        if (game.currentAttackedBlock != null)
        {
            int x = game.currentAttackedBlock.Value.X;
            int y = game.currentAttackedBlock.Value.Y;
            int z = game.currentAttackedBlock.Value.Z;
            int blocktype = game.VoxelMap.GetBlock(x, y, z);
            float health = game.GetCurrentBlockHealth(x, y, z);
            float progress = health / game.BlockRegistry.Strength[blocktype];

            // Cache the translated name — used in up to two calls below.
            string name = game.language.Get("Block_" + game.blocktypes[blocktype].Name);

            if (game.IsUsableBlock(blocktype))
                DrawEnemyHealthUseInfo(game, name, progress, useInfo: true);

            DrawEnemyHealthBackground(game, name);
        }

        if (game.currentlyAttackedEntity != -1)
        {
            Entity e = game.entities[game.currentlyAttackedEntity];
            if (e == null) return;

            float health = e.playerStats != null
                ? (float)e.playerStats.CurrentHealth / e.playerStats.MaxHealth
                : 1f;

            string name = e.drawName?.Name ?? "Unknown";
            string translatedName = game.language.Get(name);

            if (e.usable)
                DrawEnemyHealthUseInfo(game, translatedName, health, useInfo: true);

            DrawEnemyHealthBackground(game, translatedName);
        }
    }

    /// <summary>
    /// Draws the full-width background bar and name label (no progress, no use hint).
    /// Replaces the old <c>DrawEnemyHealthCommon</c> which accepted a <c>progress</c>
    /// parameter it silently ignored.
    /// </summary>
    internal static void DrawEnemyHealthBackground(Game game, string name)
        => DrawEnemyHealthUseInfo(game, name, progress: 1f, useInfo: false);

    internal static void DrawEnemyHealthUseInfo(Game game, string name, float progress, bool useInfo)
    {
        int barHeight = useInfo ? 55 : 35;
        int whiteTexId = game.WhiteTexture(); // cache — was called twice

        game.Draw2dTexture(whiteTexId, game.Xcenter(300), 40, 300, barHeight, null, 0,
            ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
        game.Draw2dTexture(whiteTexId, game.Xcenter(300), 40, 300 * progress, barHeight, null, 0,
            ColorUtils.ColorFromArgb(255, 255, 0, 0), false);

        game.platform.TextSize(name, 14, out int w, out _);
        game.Draw2dText1(name, game.Xcenter(w), 40, 14, null, false);

        if (useInfo)
        {
            string hint = string.Format(game.language.PressToUse(), "E");
            game.platform.TextSize(hint, 10, out w, out _);
            game.Draw2dText1(hint, game.Xcenter(w), 70, 10, null, false);
        }
    }

    // ── Crosshair ─────────────────────────────────────────────────────────────

    internal static void DrawAim(Game game)
    {
        if (game.cameratype == CameraType.Overhead) return;

        const int AimSize = 32;
        game.platform.BindTexture2d(0);

        if (game.CurrentAimRadius() > 1)
        {
            float fov_ = game.CurrentFov();
            game.Circle3i(game.Width() / 2, game.Height() / 2,
                game.CurrentAimRadius() * game.fov / fov_);
        }

        game.Draw2dBitmapFile("target.png",
            game.Width() / 2 - AimSize / 2,
            game.Height() / 2 - AimSize / 2,
            AimSize, AimSize);
    }

    // ── Mouse cursor ──────────────────────────────────────────────────────────

    internal static void DrawMouseCursor(Game game)
    {
        if (!game.GetFreeMouse()) return;
        if (!game.platform.MouseCursorIsVisible())
            game.Draw2dBitmapFile("mousecursor.png", game.mouseCurrentX, game.mouseCurrentY, 32, 32);
    }

    // ── Ammo counter ──────────────────────────────────────────────────────────

    internal static void DrawAmmo(Game game)
    {
        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block) return;
        if (!game.blocktypes[item.BlockId].IsPistol) return;

        int loaded = game.LoadedAmmo[item.BlockId];
        int total = game.TotalAmmo[item.BlockId];
        string s = $"{loaded}/{total - loaded}";
        int color = loaded == 0
            ? ColorUtils.ColorFromArgb(255, 255, 0, 0)
            : ColorUtils.ColorFromArgb(255, 255, 255, 255);

        game.Draw2dText1(s, game.Width() - game.TextSizeWidth(s, 18) - 50,
            game.Height() - game.TextSizeHeight(s, 18) - 50, 18, color, false);

        if (loaded == 0)
        {
            const string PressR = "Press R to reload"; // TODO: move to game.language
            game.Draw2dText1(PressR,
                game.Width() - game.TextSizeWidth(PressR, 14) - 50,
                game.Height() - game.TextSizeHeight(s, 14) - 80,
                14, ColorUtils.ColorFromArgb(255, 255, 0, 0), false);
        }
    }

    // ── Debug position overlay ────────────────────────────────────────────────

    private static void DrawLocalPosition(Game game)
    {
        if (!game.ENABLE_DRAWPOSITION) return;

        float heading = Game.HeadingByte(
            game.player.position.rotx, game.player.position.roty, game.player.position.rotz);
        float pitch = Game.PitchByte(
            game.player.position.rotx, game.player.position.roty, game.player.position.rotz);

        // Single interpolated string replaces seven string.Concat calls.
        string postext =
            $"X: {MathF.Floor(game.player.position.x)},\t" +
            $"Y: {MathF.Floor(game.player.position.z)},\t" +
            $"Z: {MathF.Floor(game.player.position.y)}\n" +
            $"Heading: {MathF.Floor(heading)}\n" +
            $"Pitch: {MathF.Floor(pitch)}";

        game.Draw2dText1(postext, 100, 460, Game.ChatFontSize, null, false);
    }

    // ── Disconnected overlay ──────────────────────────────────────────────────

    private static void DrawDisconnected(Game game)
    {
        float lagSeconds =
            (game.platform.TimeMillisecondsFromStart - game.LastReceivedMilliseconds) / 1000f;

        if (lagSeconds < Game.DISCONNECTED_ICON_AFTER_SECONDS) return;
        if (lagSeconds >= 60 * 60 * 24) return;
        if (game.invalidVersionDrawMessage != null) return;
        if (game.issingleplayer && !game.platform.SinglePlayerServerLoaded()) return;

        game.Draw2dBitmapFile("disconnected.png", game.Width() - 100, 50, 50, 50);

        game.Draw2dText1(((int)lagSeconds).ToString(),
            game.Width() - 100, 50 + 50 + 10, 12, null, false);

        const string Reconnect = "Press F6 to reconnect";
        game.Draw2dText1(Reconnect,
            game.Width() / 2 - 200 / 2, 50, 12, null, false);
    }
}