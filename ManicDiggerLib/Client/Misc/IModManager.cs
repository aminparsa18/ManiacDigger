public interface IModManager
{
    public void MakeScreenshot();
    public void SetLocalPosition(float glx, float gly, float glz);
    public float GetLocalPositionX();
    public float GetLocalPositionY();
    public float GetLocalPositionZ();
    public void SetLocalOrientation(float glx, float gly, float glz);
    public float GetLocalOrientationX();
    public float GetLocalOrientationY();
    public float GetLocalOrientationZ();
    public void DisplayNotification(string message);
    public void SendChatMessage(string message);
    public IGamePlatform GetPlatform();
    public void ShowGui(int level);
    public void SetFreemove(int level);
    public int GetFreemove();
    public Bitmap GrabScreenshot();
    public AviWriterCi AviWriterCreate();
    public int GetWindowWidth();
    public int GetWindowHeight();
    public bool IsFreemoveAllowed();
    public void EnableCameraControl(bool enable);
    public int WhiteTexture();
    public void Draw2dTexture(int textureid, float x1, float y1, float width, float height, int inAtlasId, int color);
    public void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId);
    public void Draw2dText(string text, float x, float y, float fontsize);
    public void OrthoMode();
    public void PerspectiveMode();
    public Dictionary<string, string> GetPerformanceInfo();
}
