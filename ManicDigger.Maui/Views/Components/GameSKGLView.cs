// ManicDigger.Maui/Views/Components/GameSKGLView.cs
//
// Subclasses SKGLView to drive the game render loop.
// SkiaSharp handles all EGL/ANGLE context creation and swap-chain composition
// with WinUI3 — we never touch EGL directly.
//
// OnPaintSurface is called by SkiaSharp each frame. We reset Skia's cached GL
// state, invoke the game's frame handlers (raw ES30 calls), then let SkiaSharp
// present the result. InvalidateSurface() is called at the end of each tick to
// schedule the next frame — this is the game loop.

using ManicDigger.Maui.Services;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ManicDigger.Maui.Views.Components;

public class GameSKGLView : SKGLView
{
    private MauiGameWindowService _gameWindowService;
    private DateTime _lastFrame = DateTime.UtcNow;
    private bool _running;

    public GameSKGLView()
    {
        HasRenderLoop = true;
    }

    // Called from GameView.OnAppearing after DI resolution
    public void Initialise(MauiGameWindowService gameWindowService)
    {
        _gameWindowService = gameWindowService;
    }

    // ── Loop control ──────────────────────────────────────────────────────────

    public void StartLoop()
    {
        _running = true;
        _lastFrame = DateTime.UtcNow;
        InvalidateSurface(); // kick off first frame
    }

    public void StopLoop() => _running = false;

    // ── SKGLView override ─────────────────────────────────────────────────────

    protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        // ── 1. Compute delta time ─────────────────────────────────────────────
        DateTime now = DateTime.UtcNow;
        float dt = (float)(now - _lastFrame).TotalSeconds;
        _lastFrame = now;

        // ── 2. Reset Skia's cached GL state ───────────────────────────────────
        // Skia tracks GL state internally. Before making raw GL calls we must
        // tell it to abandon its assumptions, otherwise it may not re-apply
        // state correctly when it next renders.
        e.Surface.Canvas.Flush();
        e.Surface.Flush();

        // ── 3. Game frame — raw ES30 calls into Skia's active FBO ────────────
        // SkiaSharp has bound its own FBO at this point. Raw GL draw calls
        // write into that FBO, which SkiaSharp then presents via EglSwapBuffers.
        _gameWindowService.RaiseNewFrame(dt);

        // ── 4. Schedule next frame ────────────────────────────────────────────
        if (_running)
            InvalidateSurface();
    }
}