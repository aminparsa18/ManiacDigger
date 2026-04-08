public partial class Game
{
    // -------------------------------------------------------------------------
    // Entry point (called by platform/scheduler)
    // -------------------------------------------------------------------------

    public void OnRenderFrame(float deltaTime)
    {
        taskScheduler.Update(this, deltaTime);
    }

    internal void MainThreadOnRenderFrame(float deltaTime)
    {
        UpdateResize();
        UpdateClearColor();

        UpdateMouseSmoothing(deltaTime);

        // Required in Mono for running the terrain background thread.
        platform.ApplicationDoEvents();

        accumulator = Math.Min(accumulator + deltaTime, 1f);
        float dt = one / 75;
        while (accumulator >= dt)
        {
            FrameTick(dt);
            accumulator -= dt;
        }

        if (guistate == GuiState.MapLoading)
        {
            GotoDraw2d(deltaTime);
            return;
        }

        if (ENABLE_LAG == 2)
            platform.ThreadSpinWait(20 * 1000 * 1000);

        SetAmbientLight(Terraincolor());
        platform.GlClearColorBufferAndDepthBuffer();
        platform.BindTexture2d(d_TerrainTextures.TerrainTexture);

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnBeforeNewFrameDraw3d(this, deltaTime);
        }

        GLMatrixModeModelView();
        GLLoadMatrix(camera);
        CameraMatrix.LastModelViewMatrix = camera;
        d_FrustumCulling.CalcFrustumEquations();

        platform.GlEnableDepthTest();
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnNewFrameDraw3d(this, deltaTime);
        }

        GotoDraw2d(deltaTime);
    }

    // -------------------------------------------------------------------------
    // Fixed update tick
    // -------------------------------------------------------------------------

    internal void FrameTick(float dt)
    {
        NewFrameEventArgs args_ = new();
        args_.SetDt(dt);

        for (int i = 0; i < clientmodsCount; i++)
            clientmods[i].OnNewFrameFixed(this, args_);

        for (int i = 0; i < entitiesCount; i++)
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
        platform.AudioUpdateListener(EyesPosX(), EyesPosY(), EyesPosZ(), orientationX, 0, orientationZ);

        playervelocity.X = (player.position.x - lastplayerpositionX) * 75;
        playervelocity.Y = (player.position.y - lastplayerpositionY) * 75;
        playervelocity.Z = (player.position.z - lastplayerpositionZ) * 75;
        lastplayerpositionX = player.position.x;
        lastplayerpositionY = player.position.y;
        lastplayerpositionZ = player.position.z;
    }

    // -------------------------------------------------------------------------
    // 2D pass + end-of-frame work
    // -------------------------------------------------------------------------

    internal void GotoDraw2d(float dt)
    {
        SetAmbientLight(ColorFromArgb(255, 255, 255, 255));
        Draw2d(dt);

        NewFrameEventArgs args_ = new();
        args_.SetDt(dt);
        for (int i = 0; i < clientmodsCount; i++)
            clientmods[i].OnNewFrame(this, args_);

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

    // -------------------------------------------------------------------------
    // Resize / viewport
    // -------------------------------------------------------------------------

    private void UpdateResize()
    {
        if (lastWidth != platform.GetCanvasWidth() || lastHeight != platform.GetCanvasHeight())
        {
            lastWidth = platform.GetCanvasWidth();
            lastHeight = platform.GetCanvasHeight();
            OnResize();
        }
    }

    internal void OnResize()
    {
        platform.GlViewport(0, 0, Width(), Height());
        Set3dProjection2();
        if (sendResize)
            SendGameResolution();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void UpdateClearColor()
    {
        if (guistate == GuiState.MapLoading)
            platform.GlClearColorRgbaf(0, 0, 0, 1);
        else
            platform.GlClearColorRgbaf(
                one * clearcolorR / 255,
                one * clearcolorG / 255,
                one * clearcolorB / 255,
                one * clearcolorA / 255);
    }

    private void UpdateMouseSmoothing(float deltaTime)
    {
        const float constMouseDt = 1f / 300;
        mouseSmoothingAccum += deltaTime;
        while (mouseSmoothingAccum > constMouseDt)
        {
            mouseSmoothingAccum -= constMouseDt;
            UpdateMouseViewportControl(constMouseDt);
        }
    }
}