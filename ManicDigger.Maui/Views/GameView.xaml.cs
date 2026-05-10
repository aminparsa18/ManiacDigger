using ManicDigger.Maui.Services;

namespace ManicDigger.Maui.Views;

public partial class GameView : ContentPage
{
    private readonly MauiGameWindowService _gameWindowService;
    private int _frameCount;
    private System.Timers.Timer? _fpsTimer;

    public GameView(MauiGameWindowService gameWindowService)
    {
        InitializeComponent();
        _gameWindowService = gameWindowService;
    }

    // ── Page lifecycle ────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        GlView.Initialise(_gameWindowService);
        // SKGLView creates the EGL context on first paint.
        // Attach wires touch input and starts the game loop.
        _gameWindowService.Attach(GlView);

        StartFpsTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopFpsTimer();
        _gameWindowService.Detach();
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────
    // ContentPage receives keyboard events; SKGLView does not.

    //protected override void OnKeyDown(string keyName)
    //{
    //    base.OnKeyDown(keyName);
    //    if (Enum.TryParse<Keys>(keyName, ignoreCase: true, out Keys key))
    //        _gameWindowService.RaiseKeyDown(new KeyEventArgs { KeyChar = (int)key });
    //}

    // ── FPS counter ───────────────────────────────────────────────────────────

    private void StartFpsTimer()
    {
        _gameWindowService.AddOnNewFrame(_ => Interlocked.Increment(ref _frameCount));

        _fpsTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _fpsTimer.Elapsed += (s, e) =>
        {
            int fps = Interlocked.Exchange(ref _frameCount, 0);
          //  MainThread.BeginInvokeOnMainThread(() => FpsLabel.Text = $"FPS: {fps}");
        };
        _fpsTimer.Start();
    }

    private void StopFpsTimer()
    {
        _fpsTimer?.Stop();
        _fpsTimer?.Dispose();
        _fpsTimer = null;
    }
}