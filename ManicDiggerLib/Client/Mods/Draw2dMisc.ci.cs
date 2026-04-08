using OpenTK.Mathematics;

public class ModDraw2dMisc : ModBase
{
    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        if (game.guistate == GuiState.Normal)
        {
            DrawAim(game);
        }
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

    public void DrawBlockInfo(Game game)
    {
        if (!game.drawblockinfo)
        {
            return;
        }
        int x = game.SelectedBlockPositionX;
        int y = game.SelectedBlockPositionZ;
        int z = game.SelectedBlockPositionY;
        //string info = "None";
        if (!game.map.IsValidPos(x, y, z))
        {
            return;
        }
        int blocktype = game.map.GetBlock(x, y, z);
        if (!game.IsValid(blocktype))
        {
            return;
        }
        game.currentAttackedBlock = new Vector3i(x, y, z);
        DrawEnemyHealthBlock(game);
    }

    internal static void DrawMouseCursor(Game game)
    {
        if (!game.GetFreeMouse())
        {
            return;
        }
        if (!game.platform.MouseCursorIsVisible())
        {
            game.Draw2dBitmapFile("mousecursor.png", game.mouseCurrentX, game.mouseCurrentY, 32, 32);
        }
    }

    internal void DrawEnemyHealthBlock(Game game)
    {
        if (game.currentAttackedBlock != null)
        {
            int x = game.currentAttackedBlock.Value.X;
            int y = game.currentAttackedBlock.Value.Y;
            int z = game.currentAttackedBlock.Value.Z;
            int blocktype = game.map.GetBlock(x, y, z);
            float health = game.GetCurrentBlockHealth(x, y, z);
            float progress = health / game.d_Data.Strength()[blocktype];
            if (game.IsUsableBlock(blocktype))
            {
                DrawEnemyHealthUseInfo(game, game.language.Get(string.Concat("Block_", game.blocktypes[blocktype].Name)), progress, true);
            }
            DrawEnemyHealthCommon(game, game.language.Get(string.Concat("Block_", game.blocktypes[blocktype].Name)), progress);
        }
        if (game.currentlyAttackedEntity != -1)
        {
            Entity e = game.entities[game.currentlyAttackedEntity];
            if (e == null)
            {
                return;
            }
            float health;
            if (e.playerStats != null)
            {
                health = game.one * e.playerStats.CurrentHealth / e.playerStats.MaxHealth;
            }
            else
            {
                health = 1;
            }
            string name = "Unknown";
            if (e.drawName != null)
            {
                name = e.drawName.Name;
            }
            if (e.usable)
            {
                DrawEnemyHealthUseInfo(game, game.language.Get(name), health, true);
            }
            DrawEnemyHealthCommon(game, game.language.Get(name), health);
        }
    }

    internal void DrawEnemyHealthCommon(Game game, string name, float progress)
    {
        DrawEnemyHealthUseInfo(game, name, 1, false);
    }

    internal static void DrawEnemyHealthUseInfo(Game game, string name, float progress, bool useInfo)
    {
        int y = useInfo ? 55 : 35;
        game.Draw2dTexture(game.WhiteTexture(), game.Xcenter(300), 40, 300, y, null, 0, Game.ColorFromArgb(255, 0, 0, 0), false);
        game.Draw2dTexture(game.WhiteTexture(), game.Xcenter(300), 40, 300 * progress, y, null, 0, Game.ColorFromArgb(255, 255, 0, 0), false);
        FontCi font = new()
        {
            family = "Arial",
            size = 14
        };
        game.platform.TextSize(name, 14, out int w, out int h);
        game.Draw2dText(name, font, game.Xcenter(w), 40, null, false);
        if (useInfo)
        {
            name = string.Format(game.language.PressToUse(), "E");
            game.platform.TextSize(name, 10, out w, out h);
            FontCi font2 = new()
            {
                family = "Arial",
                size = 10
            };
            game.Draw2dText(name, font2, game.Xcenter(w), 70, null, false);
        }
    }

    internal static void DrawAim(Game game)
    {
        if (game.cameratype == CameraType.Overhead)
        {
            return;
        }
        int aimwidth = 32;
        int aimheight = 32;
        game.platform.BindTexture2d(0);
        if (game.CurrentAimRadius() > 1)
        {
            float fov_ = game.CurrentFov();
            game.Circle3i(game.Width() / 2, game.Height() / 2, game.CurrentAimRadius() * game.fov / fov_);
        }
        game.Draw2dBitmapFile("target.png", game.Width() / 2 - aimwidth / 2, game.Height() / 2 - aimheight / 2, aimwidth, aimheight);
    }

    internal static void DrawAmmo(Game game)
    {
        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
        {
            if (game.blocktypes[item.BlockId].IsPistol)
            {
                int loaded = game.LoadedAmmo[item.BlockId];
                int total = game.TotalAmmo[item.BlockId];
                string s = string.Format("{0}/{1}", loaded.ToString(), (total - loaded).ToString());
                FontCi font = new()
                {
                    family = "Arial",
                    size = 18
                };
                game.Draw2dText(s, font, game.Width() - game.TextSizeWidth(s, 18) - 50,
                    game.Height() - game.TextSizeHeight(s, 18) - 50, loaded == 0 ? Game.ColorFromArgb(255, 255, 0, 0) 
                    : Game.ColorFromArgb(255, 255, 255, 255), false);
                if (loaded == 0)
                {
                    font.size = 14;
                    string pressR = "Press R to reload";
                    game.Draw2dText(pressR, font, game.Width() - game.TextSizeWidth(pressR, 14) - 50,
                        game.Height() - game.TextSizeHeight(s, 14) - 80, Game.ColorFromArgb(255, 255, 0, 0), false);
                }
            }
        }
    }

    private static void DrawLocalPosition(Game game)
    {
        float one = 1;
        if (game.ENABLE_DRAWPOSITION)
        {
            float heading = one * Game.HeadingByte(game.player.position.rotx, game.player.position.roty, game.player.position.rotz);
            float pitch = one * Game.PitchByte(game.player.position.rotx, game.player.position.roty, game.player.position.rotz);
            string postext = string.Format("X: {0}", MathF.Floor(game.player.position.x).ToString());
            postext = string.Concat(postext, ",\tY: ");
            postext = string.Concat(postext, MathF.Floor(game.player.position.z).ToString());
            postext = string.Concat(postext, ",\tZ: ");
            postext = string.Concat(postext, MathF.Floor(game.player.position.y).ToString());
            postext = string.Concat(postext, "\nHeading: ");
            postext = string.Concat(postext, MathF.Floor(heading).ToString());
            postext = string.Concat(postext, "\nPitch: ");
            postext = string.Concat(postext, MathF.Floor(pitch).ToString());
            FontCi font = new()
            {
                family = "Arial",
                size = Game.ChatFontSize
            };
            game.Draw2dText(postext, font, 100, 460, null, false);
        }
    }

    private static void DrawDisconnected(Game game)
    {
        float one = 1;
        float lagSeconds = one * (game.platform.TimeMillisecondsFromStart() - game.LastReceivedMilliseconds) / 1000;
        if ((lagSeconds >= Game.DISCONNECTED_ICON_AFTER_SECONDS && lagSeconds < 60 * 60 * 24)
            && game.invalidVersionDrawMessage == null && !(game.issingleplayer && (!game.platform.SinglePlayerServerLoaded())))
        {
            game.Draw2dBitmapFile("disconnected.png", game.Width() - 100, 50, 50, 50);
            FontCi font = new()
            {
                family = "Arial",
                size = 12
            };
            game.Draw2dText(((int)(lagSeconds)).ToString(), font, game.Width() - 100, 50 + 50 + 10, null, false);
            game.Draw2dText("Press F6 to reconnect", font, game.Width() / 2 - 200 / 2, 50, null, false);
        }
    }
}
