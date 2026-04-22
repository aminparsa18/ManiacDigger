public partial class Game
{
    // ── Fixed-timestep constants ──────────────────────────────────────────────

    /// <summary>
    /// Number of fixed-logic ticks per second.
    /// Appears in two places: the tick delta-time and the velocity calculation
    /// that converts per-tick displacement to units/second.
    /// </summary>
    private const int FixedTickRate = 75;

    /// <summary>Delta time (seconds) passed to each <see cref="FrameTick"/> call.</summary>
    private const float FixedTickDt = 1f / FixedTickRate;

    // ── Entry point (called by platform / scheduler) ──────────────────────────

    /// <summary>
    /// Top-level per-frame entry point. Delegates to the task scheduler which
    /// orchestrates background and main-thread mod hooks.
    /// </summary>
    public void OnRenderFrame(float deltaTime)
    {
        taskScheduler.Update(this, deltaTime);
    }

    /// <summary>
    /// Main-thread render path. Drives the fixed-update accumulator, dispatches
    /// 3D and 2D draw hooks to all registered mods, and handles the map-loading
    /// screen as a fast-exit special case.
    /// </summary>
    internal void MainThreadOnRenderFrame(float deltaTime)
    {
        UpdateResize();
        UpdateClearColor();
        UpdateMouseSmoothing(deltaTime);

        // Required in Mono for running the terrain background thread.
        platform.ApplicationDoEvents();

        // Fixed-timestep accumulator — capped at 1 s to prevent a spiral of death
        // when the renderer stalls (e.g. window resize, focus loss).
        accumulator = Math.Min(accumulator + deltaTime, 1f);
        while (accumulator >= FixedTickDt)
        {
            FrameTick(FixedTickDt);
            accumulator -= FixedTickDt;
        }

        // During map loading only the 2D pass (progress bar, status text) runs.
        if (guistate == GuiState.MapLoading)
        {
            GotoDraw2d(deltaTime);
            return;
        }

        if (ENABLE_LAG == 2)
            platform.ThreadSpinWait(20_000_000); // simulate ~20 ms frame lag

        SetAmbientLight(Terraincolor());
        platform.GlClearColorBufferAndDepthBuffer();
        platform.BindTexture2d(d_TerrainTextures.TerrainTexture);

        for (int i = 0; i < clientmods.Count; i++)
            clientmods[i]?.OnBeforeNewFrameDraw3d(this, deltaTime);

        GLMatrixModeModelView();
        GLLoadMatrix(camera);
        CameraMatrix.LastModelViewMatrix = camera;
        d_FrustumCulling.CalcFrustumEquations();

        platform.GlEnableDepthTest();
        for (int i = 0; i < clientmods.Count; i++)
            clientmods[i]?.OnNewFrameDraw3d(this, deltaTime);

        GotoDraw2d(deltaTime);
    }

    // ── Fixed update tick ─────────────────────────────────────────────────────

    /// <summary>
    /// Advances game logic by one fixed time step.
    /// Runs mod fixed hooks, entity scripts, physics revert, audio listener
    /// positioning, and player velocity estimation.
    /// </summary>
    internal void FrameTick(float dt)
    {
        for (int i = 0; i < clientmods.Count; i++)
            clientmods[i].OnNewFrameFixed(this, dt);

        for (int i = 0; i < entities.Count; i++)
        {
            Entity e = entities[i];
            if (e == null) continue;
            for (int k = 0; k < e.scriptsCount; k++)
                e.scripts[k].OnNewFrameFixed(this, i, dt);
        }

        RevertSpeculative(dt);

        if (guistate == GuiState.MapLoading)
            return;

        float orientationX = MathF.Sin(player.position.roty);
        float orientationZ = -MathF.Cos(player.position.roty);
        platform.AudioUpdateListener(
            EyesPosX(), EyesPosY(), EyesPosZ(),
            orientationX, 0, orientationZ);

        // Estimate velocity in world units/second from per-tick displacement.
        playervelocity.X = (player.position.x - lastplayerpositionX) * FixedTickRate;
        playervelocity.Y = (player.position.y - lastplayerpositionY) * FixedTickRate;
        playervelocity.Z = (player.position.z - lastplayerpositionZ) * FixedTickRate;
        lastplayerpositionX = player.position.x;
        lastplayerpositionY = player.position.y;
        lastplayerpositionZ = player.position.z;
    }

    // ── 2D pass + end-of-frame work ───────────────────────────────────────────

    /// <summary>
    /// Runs the 2D overlay pass, fires the end-of-frame mod hook, resets click
    /// state, and initiates the server connection on the first eligible frame.
    /// </summary>
    internal void GotoDraw2d(float dt)
    {
        SetAmbientLight(ColorUtils.ColorFromArgb(255, 255, 255, 255));
        Draw2d(dt);

        for (int i = 0; i < clientmods.Count; i++)
            clientmods[i]?.OnNewFrame(this, dt);

        mouseleftclick = mouserightclick = false;
        mouseleftdeclick = mouserightdeclick = false;

        if (!issingleplayer
         || platform.SinglePlayerServerLoaded()
         || !platform.SinglePlayerServerAvailable())
        {
            if (!startedconnecting)
            {
                startedconnecting = true;
                Connect__();
            }
        }
    }

    // ── Resize / viewport ─────────────────────────────────────────────────────

    /// <summary>
    /// Detects canvas size changes and triggers a viewport + projection update.
    /// Caches both dimensions in locals to avoid double platform calls on change.
    /// </summary>
    private void UpdateResize()
    {
        int w = platform.GetCanvasWidth();
        int h = platform.GetCanvasHeight();
        if (lastWidth == w && lastHeight == h) return;

        lastWidth = w;
        lastHeight = h;
        OnResize();
    }

    /// <summary>Updates the OpenGL viewport and projection matrix after a resize.</summary>
    internal void OnResize()
    {
        platform.GlViewport(0, 0, Width(), Height());
        Set3dProjection2();
        if (sendResize)
            SendGameResolution();
    }

    // ── Per-frame helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Sets the GL clear colour. During map loading the screen is pure black;
    /// otherwise uses the current sky/terrain fog colour.
    /// </summary>
    private void UpdateClearColor()
    {
        if (guistate == GuiState.MapLoading)
        {
            platform.GlClearColorRgbaf(0, 0, 0, 1);
        }
        else
        {
            platform.GlClearColorRgbaf(
                clearcolorR / 255f,
                clearcolorG / 255f,
                clearcolorB / 255f,
                clearcolorA / 255f);
        }
    }

    /// <summary>
    /// Advances the mouse-smoothing accumulator and fires viewport control
    /// updates at a fixed rate of 300 Hz, independent of frame rate.
    /// </summary>
    private void UpdateMouseSmoothing(float deltaTime)
    {
        const float MouseSmoothingDt = 1f / 300f;
        mouseSmoothingAccum += deltaTime;
        while (mouseSmoothingAccum > MouseSmoothingDt)
        {
            mouseSmoothingAccum -= MouseSmoothingDt;
            UpdateMouseViewportControl(MouseSmoothingDt);
        }
    }
}