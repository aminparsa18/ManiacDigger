using OpenTK.Mathematics;

public partial class Game
{
    // ── Fixed-timestep constants ──────────────────────────────────────────────

    /// <summary>Number of fixed-logic ticks per second.</summary>
    private const int FixedTickRate = 75;

    /// <summary>Delta time (seconds) passed to each <see cref="FrameTick"/> call.</summary>
    private const float FixedTickDt = 1f / FixedTickRate;

    /// <summary>Value of <see cref="EnableLog"/> that simulates ~20 ms frame lag.</summary>
    private const int EnableLogSimulateLag = 2;

    // Recomputed only when the colour changes, not every frame.
    private float _clearColorRf, _clearColorGf, _clearColorBf, _clearColorAf;
    private int _lastClearColorR = -1, _lastClearColorG = -1,
                  _lastClearColorB = -1, _lastClearColorA = -1;

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>
    /// Top-level per-frame entry point. Delegates to the task scheduler which
    /// orchestrates background and main-thread mod hooks.
    /// </summary>
    public void OnRenderFrame(float deltaTime)
        => taskScheduler.Update(deltaTime);

    /// <summary>
    /// Main-thread render path. Drives the fixed-update accumulator, dispatches
    /// 3D and 2D draw hooks to all registered mods, and handles the map-loading
    /// screen as a fast-exit special case.
    /// </summary>
    public void MainThreadOnRenderFrame(float deltaTime)
    {
        UpdateResize();
        UpdateClearColor();
        UpdateMouseSmoothing(deltaTime);

        // Required in Mono for running the terrain background thread.
        Platform.ApplicationDoEvents();

        // Fixed-timestep accumulator — capped at 1 s to prevent spiral-of-death
        // when the renderer stalls (e.g. window resize, focus loss).
        accumulator = Math.Min(accumulator + deltaTime, 1f);
        int maxTicksPerFrame = 3; // never run more than 3 physics ticks per render frame
        while (accumulator >= FixedTickDt && maxTicksPerFrame-- > 0)
        {
            FrameTick(FixedTickDt);
            accumulator -= FixedTickDt;
        }

        // During map loading only the 2D pass (progress bar, status text) runs.
        if (GuiState == GuiState.MapLoading)
        {
            RunDraw2dAndEndFrame(deltaTime);
            return;
        }

        // Fix #4: named constant instead of magic number.
        if (EnableLog == EnableLogSimulateLag)
           Thread.SpinWait(20_000_000);

        SetAmbientLight(Terraincolor());
        Platform.GlClearColorBufferAndDepthBuffer();
        Platform.BindTexture2d(TerrainTexture);

        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.OnBeforeNewFrameDraw3d(deltaTime);

        GLMatrixModeModelView();
        GLLoadMatrix(Camera);
        CameraMatrix.LastModelViewMatrix = Camera;
        FrustumCulling.CalcFrustumEquations();

        Platform.GlEnableDepthTest();
        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.OnNewFrameDraw3d(deltaTime);

        RunDraw2dAndEndFrame(deltaTime);
    }

    // ── Fixed update tick ─────────────────────────────────────────────────────

    /// <summary>
    /// Advances game logic by one fixed time step.
    /// Runs mod fixed hooks, entity scripts, physics revert, audio listener
    /// positioning, and player velocity estimation.
    /// </summary>
    internal void FrameTick(float dt)
    {
        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i].OnNewFrameFixed(dt);

        for (int i = 0; i < Entities.Count; i++)
        {
            Entity e = Entities[i];
            if (e == null) continue;
            for (int k = 0; k < e.scriptsCount; k++)
                e.scripts[k].OnNewFrameFixed(i, dt);
        }

        RevertSpeculative(dt);

        if (GuiState == GuiState.MapLoading) return;

        float orientationX = MathF.Sin(Player.position.roty);
        float orientationZ = -MathF.Cos(Player.position.roty);
        Platform.AudioUpdateListener(
            EyesPosX, EyesPosY, EyesPosZ,
            orientationX, 0, orientationZ);

        Vector3 vel;
        vel.X = (Player.position.x - lastplayerpositionX) * FixedTickRate;
        vel.Y = (Player.position.y - lastplayerpositionY) * FixedTickRate;
        vel.Z = (Player.position.z - lastplayerpositionZ) * FixedTickRate;
        playervelocity = vel;

        lastplayerpositionX = Player.position.x;
        lastplayerpositionY = Player.position.y;
        lastplayerpositionZ = Player.position.z;
    }

    // ── 2D pass + end-of-frame work ───────────────────────────────────────────

    /// <summary>
    /// Runs the 2D overlay pass, fires the end-of-frame mod hook, resets click
    /// state, and initiates the server connection on the first eligible frame.
    /// </summary>
    internal void RunDraw2dAndEndFrame(float dt)
    {
        // ── Drain the commit queue ────────────────────────────────────────────
        // Cap at 32 actions per frame so a sudden flood can't stall rendering.
        // Unprocessed actions stay in the queue and drain over subsequent frames.
        int maxCommitsPerFrame = 32;
        while (maxCommitsPerFrame-- > 0 && commitActions.TryDequeue(out Action action))
            action();

        SetAmbientLight(ColorUtils.ColorFromArgb(255, 255, 255, 255));
        Draw2d(dt);

        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.OnNewFrame(dt);

        MouseLeftClick = false;
        mouserightclick = false;
        mouseleftdeclick = false;
        mouserightdeclick = false;

        TryInitialiseConnection();
    }

    /// <summary>
    /// Initiates the server connection on the first eligible frame.
    /// Extracted from <see cref="RunDraw2dAndEndFrame"/> — starting a network
    /// connection has nothing to do with drawing.
    /// </summary>
    private void TryInitialiseConnection()
    {
        if (startedconnecting) return;

        if (!IsSinglePlayer
         || Platform.SinglePlayerServerLoaded()
         || !Platform.SinglePlayerServerAvailable())
        {
            startedconnecting = true;
            Connect();
        }
    }

    // ── Resize / viewport ─────────────────────────────────────────────────────

    /// <summary>
    /// Detects canvas size changes and triggers a viewport + projection update.
    /// </summary>
    private void UpdateResize()
    {
        int w = Platform.GetCanvasWidth();
        int h = Platform.GetCanvasHeight();
        if (lastWidth == w && lastHeight == h) return;

        lastWidth = w;
        lastHeight = h;
        OnResize();
    }

    /// <summary>Updates the OpenGL viewport and projection matrix after a resize.</summary>
    internal void OnResize()
    {
        Platform.GlViewport(0, 0, Platform.GetCanvasWidth(), Platform.GetCanvasHeight());
        Set3dProjection2();
        if (sendResize)
            SendGameResolution();
    }

    // ── Per-frame helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Sets the GL clear colour.
    /// </summary>
    private void UpdateClearColor()
    {
        if (GuiState == GuiState.MapLoading)
        {
            Platform.GlClearColorRgbaf(0f, 0f, 0f, 1f);
            return;
        }

        // Recompute only when any component has changed.
        if (clearcolorR != _lastClearColorR
         || clearcolorG != _lastClearColorG
         || clearcolorB != _lastClearColorB
         || clearcolorA != _lastClearColorA)
        {
            _clearColorRf = clearcolorR / 255f;
            _clearColorGf = clearcolorG / 255f;
            _clearColorBf = clearcolorB / 255f;
            _clearColorAf = clearcolorA / 255f;
            _lastClearColorR = clearcolorR;
            _lastClearColorG = clearcolorG;
            _lastClearColorB = clearcolorB;
            _lastClearColorA = clearcolorA;
        }

        Platform.GlClearColorRgbaf(_clearColorRf, _clearColorGf, _clearColorBf, _clearColorAf);
    }

    /// <summary>
    /// Advances the mouse-smoothing accumulator and fires viewport control
    /// updates at a fixed rate of 300 Hz, independent of frame rate.
    /// </summary>
    private void UpdateMouseSmoothing(float deltaTime)
    {
        const float MouseSmoothingDt = 1f / 75f;
        mouseSmoothingAccum += deltaTime;
        while (mouseSmoothingAccum > MouseSmoothingDt)
        {
            mouseSmoothingAccum -= MouseSmoothingDt;
            UpdateMouseViewportControl(MouseSmoothingDt);
        }
    }
}