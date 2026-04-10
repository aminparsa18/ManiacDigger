public class ClientModManager : IModManager
{
    internal Game game;

    public void MakeScreenshot()
    {
        game.platform.SaveScreenshot();
    }

    public void SetLocalPosition(float glx, float gly, float glz)
    {
        game.player.position.x = glx;
        game.player.position.y = gly;
        game.player.position.z = glz;
    }

    public float GetLocalPositionX()
    {
        return game.player.position.x;
    }

    public float GetLocalPositionY()
    {
        return game.player.position.y;
    }

    public float GetLocalPositionZ()
    {
        return game.player.position.z;
    }

    public void SetLocalOrientation(float glx, float gly, float glz)
    {
        game.player.position.rotx = glx;
        game.player.position.roty = gly;
        game.player.position.rotz = glz;
    }

    public float GetLocalOrientationX()
    {
        return game.player.position.rotx;
    }

    public float GetLocalOrientationY()
    {
        return game.player.position.roty;
    }

    public float GetLocalOrientationZ()
    {
        return game.player.position.rotz;
    }

    public void DisplayNotification(string message)
    {
        game.AddChatline(message);
    }

    public void SendChatMessage(string message)
    {
        game.SendChat(message);
    }

    public IGamePlatform GetPlatform()
    {
        return game.platform;
    }

    public void ShowGui(int level)
    {
        if (level == 0)
        {
            game.ENABLE_DRAW2D = false;
        }
        else
        {
            game.ENABLE_DRAW2D = true;
        }
    }

    public void SetFreemove(int level)
    {
        if (level == FreemoveLevelEnum.None)
        {
            game.controls.freemove = false;
            game.controls.noclip = false;
        }

        if (level == FreemoveLevelEnum.Freemove)
        {
            game.controls.freemove = true;
            game.controls.noclip = false;
        }

        if (level == FreemoveLevelEnum.Noclip)
        {
            game.controls.freemove = true;
            game.controls.noclip = true;
        }
    }

    public int GetFreemove()
    {
        if (!game.controls.freemove)
        {
            return FreemoveLevelEnum.None;
        }
        if (game.controls.noclip)
        {
            return FreemoveLevelEnum.Noclip;
        }
        else
        {
            return FreemoveLevelEnum.Freemove;
        }
    }

    public Bitmap GrabScreenshot()
    {
        return game.platform.GrabScreenshot();
    }

    public AviWriterCi AviWriterCreate()
    {
        return new AviWriterCiCs();
    }

    public int GetWindowWidth()
    {
        return game.platform.GetCanvasWidth();
    }

    public int GetWindowHeight()
    {
        return game.platform.GetCanvasHeight();
    }

    public bool IsFreemoveAllowed()
    {
        return game.AllowFreemove;
    }

    public void EnableCameraControl(bool enable)
    {
        game.enableCameraControl = enable;
    }

    public int WhiteTexture()
    {
        return game.WhiteTexture();
    }

    public void Draw2dTexture(int textureid, float x1, float y1, float width, float height, int inAtlasId, int color)
    {
        int a = ColorUtils.ColorA(color);
        int r = ColorUtils.ColorR(color);
        int g = ColorUtils.ColorG(color);
        int b = ColorUtils.ColorB(color);
        game.Draw2dTexture(textureid, (int)x1, (int)y1,
            (int)width, (int)height,
             inAtlasId, 0, ColorUtils.ColorFromArgb(a, r, g, b), false);
    }

    public void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId)
    {
        game.Draw2dTextures(todraw, todrawLength, textureId);
    }

    public void Draw2dText(string text, float x, float y, float fontsize)
    {
        Font font = new("Arial", fontsize);
        game.Draw2dText(text, font, x, y, null, false);
    }

    public void OrthoMode()
    {
        game.OrthoMode(GetWindowWidth(), GetWindowHeight());
    }

    public void PerspectiveMode()
    {
        game.PerspectiveMode();
    }

    public Dictionary<string, string> GetPerformanceInfo()
    {
        return game.performanceinfo;
    }
}
