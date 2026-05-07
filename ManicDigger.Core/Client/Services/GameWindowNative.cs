using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

public class GameWindowNative : GameWindow
{
    public GameWindowNative()
        : base(
            new GameWindowSettings
            {
                UpdateFrequency = 60.0 // Cap at 60 updates/sec
            },
            new NativeWindowSettings
            {
                ClientSize = new Vector2i(1280, 720),
                Title = "",
                WindowState = WindowState.Maximized,
                Profile = ContextProfile.Compatability
                // APIVersion = new Version(3, 3),
            })
    {
    }
}