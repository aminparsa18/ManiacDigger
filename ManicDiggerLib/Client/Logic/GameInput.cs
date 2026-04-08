using OpenTK.Windowing.Common;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Mouse button events
    // -------------------------------------------------------------------------

    internal void MouseDown(MouseEventArgs args)
    {
        int btn = args.GetButton();

        if (btn == MouseButtonEnum.Left) { mouseLeft = true; mouseleftclick = true; }
        if (btn == MouseButtonEnum.Middle) mouseMiddle = true;
        if (btn == MouseButtonEnum.Right) { mouseRight = true; mouserightclick = true; }

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseDown(this, args);
        }

        if (mousePointerLockShouldBe)
        {
            platform.RequestMousePointerLock();
            mouseDeltaX = 0;
            mouseDeltaY = 0;
        }

        InvalidVersionAllow();
    }

    internal void MouseUp(MouseEventArgs args)
    {
        int btn = args.GetButton();

        if (btn == MouseButtonEnum.Left) { mouseLeft = false; mouseleftdeclick = true; }
        if (btn == MouseButtonEnum.Middle) mouseMiddle = false;
        if (btn == MouseButtonEnum.Right) { mouseRight = false; mouserightdeclick = true; }

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseUp(this, args);
        }
    }

    // -------------------------------------------------------------------------
    // Mouse move / wheel
    // -------------------------------------------------------------------------

    public void MouseMove(MouseEventArgs e)
    {
        if (!e.GetEmulated() || e.GetForceUsage())
        {
            // Set position only for real MouseMove events.
            mouseCurrentX = e.GetX();
            mouseCurrentY = e.GetY();
        }
        if (e.GetEmulated() || e.GetForceUsage())
        {
            // Accumulate delta only from emulated events (actual events negate previous ones).
            mouseDeltaX += e.GetMovementX();
            mouseDeltaY += e.GetMovementY();
        }

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseMove(this, e);
        }
    }

    internal void MouseWheelChanged(MouseWheelEventArgs e)
    {
        float delta = e.OffsetY;

        if (keyboardState[GetKey(Keys.LeftShift)])
        {
            if (cameratype == CameraType.Overhead)
                overheadcameradistance = Math.Clamp(overheadcameradistance - delta, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);

            if (cameratype == CameraType.Tpp)
                tppcameradistance = Math.Clamp(tppcameradistance - delta, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);
        }

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseWheelChanged(this, e);
        }
    }

    internal void UpdateMouseViewportControl(float dt)
    {
        if (mouseSmoothing)
        {
            const float smoothing1 = 0.85f;
            const float smoothing2 = 0.8f;
            float scale = smoothing2 / (300f / 75);
            mouseSmoothingVelX = (mouseSmoothingVelX + mouseDeltaX * scale) * smoothing1;
            mouseSmoothingVelY = (mouseSmoothingVelY + mouseDeltaY * scale) * smoothing1;
        }
        else
        {
            mouseSmoothingVelX = mouseDeltaX;
            mouseSmoothingVelY = mouseDeltaY;
        }

        if (guistate == GuiState.Normal && enableCameraControl && platform.Focused())
        {
            if (!overheadcamera && platform.IsMousePointerLocked())
            {
                float rotScale = rotationspeed / 75f;
                player.position.roty += mouseSmoothingVelX * rotScale;
                player.position.rotx += mouseSmoothingVelY * rotScale;
                player.position.rotx = Math.Clamp(player.position.rotx,
                    MathF.PI / 2 + (one * 15 / 1000),
                    MathF.PI / 2 + MathF.PI - (one * 15 / 1000));
            }

            if (!overheadcamera)
            {
                float touchScale = constRotationSpeed * (one / 75);
                player.position.rotx += touchOrientationDy * touchScale;
                player.position.roty += touchOrientationDx * touchScale;
                touchOrientationDx = 0;
                touchOrientationDy = 0;
            }

            if (cameratype == CameraType.Overhead && (mouseMiddle || mouseRight))
            {
                overheadcameraK.TurnLeft(mouseDeltaX / 70);
                overheadcameraK.TurnUp(mouseDeltaY / 3);
            }
        }

        mouseDeltaX = 0;
        mouseDeltaY = 0;
    }

    // -------------------------------------------------------------------------
    // Free mouse / pointer lock
    // -------------------------------------------------------------------------

    public bool GetFreeMouse() => overheadcamera || !platform.IsMousePointerLocked();

    public void SetFreeMouse(bool value)
    {
        mousePointerLockShouldBe = !value;
        if (value)
            platform.ExitMousePointerLock();
        else
            platform.RequestMousePointerLock();
    }

    // -------------------------------------------------------------------------
    // Touch events
    // -------------------------------------------------------------------------

    public void OnTouchStart(TouchEventArgs e)
    {
        InvalidVersionAllow();
        mouseCurrentX = e.GetX();
        mouseCurrentY = e.GetY();
        mouseleftclick = true;

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnTouchStart(this, e);
            if (e.GetHandled()) return;
        }
    }

    public void OnTouchMove(TouchEventArgs e)
    {
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnTouchMove(this, e);
            if (e.GetHandled()) return;
        }
    }

    public void OnTouchEnd(TouchEventArgs e)
    {
        mouseCurrentX = 0;
        mouseCurrentY = 0;

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnTouchEnd(this, e);
            if (e.GetHandled()) return;
        }
    }

    public static void OnBackPressed() { }

    // -------------------------------------------------------------------------
    // Keyboard events
    // -------------------------------------------------------------------------

    internal void KeyUp(int eKey)
    {
        keyboardStateRaw[eKey] = false;

        for (int i = 0; i < clientmodsCount; i++)
        {
            KeyEventArgs args_ = new();
            args_.SetKeyCode(eKey);
            clientmods[i].OnKeyUp(this, args_);
            if (args_.GetHandled()) return;
        }

        keyboardState[eKey] = false;

        if (eKey == GetKey(Keys.LeftShift) || eKey == GetKey(Keys.RightShift))
            IsShiftPressed = false;
    }

    internal void KeyPress(int eKeyChar)
    {
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) continue;
            KeyPressEventArgs args_ = new();
            args_.SetKeyChar(eKeyChar);
            clientmods[i].OnKeyPress(this, args_);
            if (args_.GetHandled()) return;
        }
    }

    internal void KeyDown(int eKey)
    {
        keyboardStateRaw[eKey] = true;

        if (guistate != GuiState.MapLoading)
        {
            for (int i = 0; i < clientmodsCount; i++)
            {
                KeyEventArgs args_ = new();
                args_.SetKeyCode(eKey);
                clientmods[i].OnKeyDown(this, args_);
                if (args_.GetHandled()) return;
            }
        }

        keyboardState[eKey] = true;
        InvalidVersionAllow();

        if (eKey == GetKey(Keys.LeftShift) || eKey == GetKey(Keys.RightShift))
            IsShiftPressed = true;

        // F6 outside of Normal state: reconnect if lagging or map loading.
        if (eKey == GetKey(Keys.F6))
        {
            float lagSeconds = one * (platform.TimeMillisecondsFromStart() - LastReceivedMilliseconds) / 1000;
            if (lagSeconds >= DISCONNECTED_ICON_AFTER_SECONDS || guistate == GuiState.MapLoading)
                Reconnect();
        }

        if (guistate == GuiState.Normal)
            KeyDownNormal(eKey);

        if (guistate == GuiState.Inventory)
        {
            if (eKey == GetKey(Keys.B) || eKey == GetKey(Keys.Escape))
                GuiStateBackToGame();
            return;
        }

        if (guistate == GuiState.MapLoading)
        {
            if (eKey == GetKey(Keys.Escape))
                ExitToMainMenu_();
        }

        if (guistate == GuiState.CraftingRecipes)
        {
            if (eKey == GetKey(Keys.Escape))
                GuiStateBackToGame();
        }

        if (guistate == GuiState.Normal)
        {
            if (eKey == GetKey(Keys.Escape))
            {
                EscapeMenuStart();
                return;
            }
        }
    }

    private void KeyDownNormal(int eKey)
    {
        const string strFreemoveNotAllowed = "You are not allowed to enable freemove.";

        if (eKey == GetKey(Keys.F1))
        {
            if (!AllowFreemove) { Log(strFreemoveNotAllowed); return; }
            movespeed = basemovespeed * 1;
            Log("Move speed: 1x.");
        }
        if (eKey == GetKey(Keys.F2))
        {
            if (!AllowFreemove) { Log(strFreemoveNotAllowed); return; }
            movespeed = basemovespeed * 10;
            Log(string.Format(language.MoveSpeed(), "10"));
        }
        if (eKey == GetKey(Keys.F3))
        {
            if (!AllowFreemove) { Log(strFreemoveNotAllowed); return; }
            stopPlayerMove = true;
            if (!controls.freemove)
            {
                controls.freemove = true;
                Log(language.MoveFree());
            }
            else if (controls.freemove && !controls.noclip)
            {
                controls.noclip = true;
                Log(language.MoveFreeNoclip());
            }
            else
            {
                controls.freemove = false;
                controls.noclip = false;
                Log(language.MoveNormal());
            }
        }
        if (eKey == GetKey(Keys.I))
            drawblockinfo = !drawblockinfo;

        int playerx = (int)player.position.x;
        int playery = (int)player.position.z;
        if (playerx >= 0 && playerx < map.MapSizeX && playery >= 0 && playery < map.MapSizeY)
            performanceinfo["height"] = string.Format("height:{0}", d_Heightmap.GetBlock(playerx, playery).ToString());

        if (eKey == GetKey(Keys.F5))
            CameraChange();

        if (eKey == GetKey(Keys.Equal))
        {
            if (cameratype == CameraType.Overhead) overheadcameradistance -= 1;
            else if (cameratype == CameraType.Tpp) tppcameradistance -= 1;
        }
        if (eKey == GetKey(Keys.Minus) || eKey == GetKey(Keys.KeyPadSubtract))
        {
            if (cameratype == CameraType.Overhead) overheadcameradistance += 1;
            else if (cameratype == CameraType.Tpp) tppcameradistance += 1;
        }

        overheadcameradistance = Math.Clamp(overheadcameradistance, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);
        tppcameradistance = Math.Clamp(tppcameradistance, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);

        if (eKey == GetKey(Keys.F6))
            RedrawAllBlocks();

        if (eKey == (int)Keys.F8)
        {
            ToggleVsync();
            if (ENABLE_LAG == 0) Log(language.FrameRateVsync());
            if (ENABLE_LAG == 1) Log(language.FrameRateUnlimited());
            if (ENABLE_LAG == 2) Log(language.FrameRateLagSimulation());
        }

        if (eKey == GetKey(Keys.Tab))
            SendPacketClient(ClientPackets.SpecialKeyTabPlayerList());

        if (eKey == GetKey(Keys.E))
            KeyDownUse();

        if (eKey == GetKey(Keys.O))
            Respawn();

        if (eKey == GetKey(Keys.L))
            SendPacketClient(ClientPackets.SpecialKeySelectTeam());

        if (eKey == GetKey(Keys.P))
        {
            SendPacketClient(ClientPackets.SpecialKeySetSpawn());
            playerPositionSpawnX = player.position.x;
            playerPositionSpawnY = player.position.y;
            playerPositionSpawnZ = player.position.z;
            player.position.x = (int)player.position.x + one / 2;
            player.position.z = (int)player.position.z + one / 2;
        }

        if (eKey == GetKey(Keys.F))
        {
            ToggleFog();
            Log(string.Format(language.FogDistance(), ((int)d_Config3d.viewdistance).ToString()));
            OnResize();
        }

        if (eKey == GetKey(Keys.B))
        {
            ShowInventory();
            return;
        }

        HandleMaterialKeys(eKey);
    }

    private void KeyDownUse()
    {
        if (currentAttackedBlock != null)
        {
            int posX = currentAttackedBlock.Value.X;
            int posY = currentAttackedBlock.Value.Y;
            int posZ = currentAttackedBlock.Value.Z;
            int blocktype = map.GetBlock(posX, posY, posZ);

            if (IsUsableBlock(blocktype))
            {
                if (d_Data.IsRailTile(blocktype))
                {
                    player.position.x = posX + (one / 2);
                    player.position.y = posZ + 1;
                    player.position.z = posY + (one / 2);
                    controls.freemove = false;
                }
                else
                {
                    SendSetBlock(posX, posY, posZ, Packet_BlockSetModeEnum.Use, 0, ActiveMaterial);
                }
            }
        }

        if (currentlyAttackedEntity != -1 && entities[currentlyAttackedEntity].usable)
        {
            OnUseEntityArgs args = new() { entityId = currentlyAttackedEntity };
            for (int i = 0; i < clientmodsCount; i++)
            {
                if (clientmods[i] == null) continue;
                clientmods[i].OnUseEntity(this, args);
            }
            SendPacketClient(ClientPackets.UseEntity(currentlyAttackedEntity));
        }
    }

    internal void HandleMaterialKeys(int eKey)
    {
        if (eKey == GetKey(Keys.KeyPad1)) { ActiveMaterial = 0; }
        if (eKey == GetKey(Keys.KeyPad2)) { ActiveMaterial = 1; }
        if (eKey == GetKey(Keys.KeyPad3)) { ActiveMaterial = 2; }
        if (eKey == GetKey(Keys.KeyPad4)) { ActiveMaterial = 3; }
        if (eKey == GetKey(Keys.KeyPad5)) { ActiveMaterial = 4; }
        if (eKey == GetKey(Keys.KeyPad6)) { ActiveMaterial = 5; }
        if (eKey == GetKey(Keys.KeyPad7)) { ActiveMaterial = 6; }
        if (eKey == GetKey(Keys.KeyPad8)) { ActiveMaterial = 7; }
        if (eKey == GetKey(Keys.KeyPad9)) { ActiveMaterial = 8; }
        if (eKey == GetKey(Keys.KeyPad0)) { ActiveMaterial = 9; }
    }

    internal int GetKey(Keys key)
    {
        if (options == null)
        {
            return (int)key;
        }
        if (options.Keys[(int)key] != 0)
        {
            return options.Keys[(int)key];
        }
        return (int)key;
    }
}