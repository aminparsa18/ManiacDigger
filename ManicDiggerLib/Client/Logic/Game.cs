using ManicDigger;
using OpenTK.Mathematics;
using System.Numerics;
using Vector3 = OpenTK.Mathematics.Vector3;

public partial class Game : IMeshDrawer
{
    // ── Map loading ───────────────────────────────────────────────────────────

    /// <summary>Enters map-loading state, locks the mouse, and initialises progress tracking.</summary>
    public void MapLoadingStart()
    {
        GuiState = GuiState.MapLoading;
        maploadingprogress = new MapLoadingProgressEventArgs();
        FontMapLoading = new Font("Arial", 14, FontStyle.Regular);
        SetFreeMouse(true);
    }

    /// <summary>
    /// Updates the map-loading progress in-place rather than allocating a new
    /// <see cref="MapLoadingProgressEventArgs"/> on every incoming chunk.
    /// </summary>
    public void InvokeMapLoadingProgress(int progressPercent, int progressBytes, string status)
    {
        maploadingprogress.ProgressPercent = progressPercent;
        maploadingprogress.ProgressBytes = progressBytes;
        maploadingprogress.ProgressStatus = status;
    }

    // ── Screen / layout helpers ───────────────────────────────────────────────

    /// <summary>Returns the X coordinate that centres a region of <paramref name="width"/> pixels.</summary>
    public int Xcenter(float width) => Platform.GetCanvasWidth() / 2 - (int)width / 2;

    /// <summary>Returns the Y coordinate that centres a region of <paramref name="height"/> pixels.</summary>
    public int Ycenter(float height) => Platform.GetCanvasHeight() / 2 - (int)height / 2;

    /// <summary>Current canvas width in pixels.</summary>
    public int Width() => Platform.GetCanvasWidth();

    /// <summary>Current canvas height in pixels.</summary>
    public int Height() => Platform.GetCanvasHeight();

    /// <summary>
    /// UI scale factor. Returns a width-relative scale on small screens
    /// (mobile) and 1 on desktop.
    /// </summary>
    public float Scale() =>
        Platform.IsSmallScreen() ? Width() / 1280f : 1f;

    // ── Projection ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a perspective projection matrix from <paramref name="fov"/> and
    /// <paramref name="zfar"/>, uploads it to the GPU, and caches it in
    /// <see cref="CameraMatrix"/>.
    /// </summary>
    public void Set3dProjection(float zfar, float fov)
    {
        float aspect = Width() / (float)Height();
        Matrix4.CreatePerspectiveFieldOfView(fov, aspect, znear, zfar, out Matrix4 projection);
        CameraMatrix.LastProjectionMatrix = projection;
        GLMatrixModeProjection();
        GLLoadMatrix(projection);
        SetMatrixUniformProjection();
    }

    /// <summary>Returns the far-clip distance for the current view distance setting.</summary>
    public float Zfar() =>
        Config3d.ViewDistance >= 256
            ? Config3d.ViewDistance * 2
            : ENABLE_ZFAR ? Config3d.ViewDistance : 99999;

    /// <summary>Sets the 3D projection using the current far-clip and FOV.</summary>
    internal void Set3dProjection1(float zfar_) => Set3dProjection(zfar_, CurrentFov());

    /// <summary>Sets the 3D projection using <see cref="Zfar"/> and the current FOV.</summary>
    internal void Set3dProjection2() => Set3dProjection1(Zfar());

    // ── Fixed-point encoding ──────────────────────────────────────────────────

    /// <summary>Decodes a Q5 fixed-point integer (value / 32) to a float.</summary>
    public float DecodeFixedPoint(int value) => value / 32f;

    /// <summary>Encodes a float to Q5 fixed-point (value × 32).</summary>
    public static int EncodeFixedPoint(float p) => (int)(p * 32);

    // ── Orientation encoding ──────────────────────────────────────────────────

    /// <summary>Encodes a yaw angle (radians) as a 0–255 byte.</summary>
    public static byte HeadingByte(float orientationX, float orientationY, float orientationZ) =>
        (byte)(int)(orientationY % (2 * MathF.PI) / (2 * MathF.PI) * 256);

    /// <summary>Encodes a pitch angle (radians) as a 0–255 byte.</summary>
    public static byte PitchByte(float orientationX, float orientationY, float orientationZ)
    {
        float xx = (orientationX + MathF.PI) % (2 * MathF.PI);
        return (byte)(int)(xx / (2 * MathF.PI) * 256);
    }

    // ── Texture helpers ───────────────────────────────────────────────────────

    /// <summary>Draws a full-size 2D quad using a named PNG asset.</summary>
    public void Draw2dBitmapFile(string filename, float x, float y, float w, float h) =>
        Draw2dTexture(GetTexture(filename), x, y, w, h, null, 0,
            ColorUtils.ColorFromArgb(255, 255, 255, 255), false);

    /// <summary>
    /// Returns <paramref name="family"/> if it is in the allowed font list,
    /// otherwise falls back to the first allowed font.
    /// Uses index access instead of <c>First()</c> to avoid allocating an enumerator.
    /// </summary>
    public string ValidFont(string family) =>
        AllowedFonts.Contains(family) ? family : AllowedFonts[0];

    // ── Inventory ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the block ID in the given hotbar slot, or
    /// <see cref="BlockRegistry.BlockIdDirt"/> when the slot is empty.
    /// </summary>
    public int MaterialSlots(int i)
    {
        Packet_Item item = Inventory.RightHand[i];
        if (item != null && item.ItemClass == ItemClass.Block)
            return item.BlockId;
        return BlockRegistry.BlockIdDirt;
    }

    /// <summary>Replaces the active inventory with the server-sent packet and notifies the util layer.</summary>
    public void UseInventory(Packet_Inventory packet_Inventory)
    {
        Inventory = packet_Inventory;
        InventoryUtil.UpdateInventory(packet_Inventory);
    }

    // ── Dialog helpers ────────────────────────────────────────────────────────

    /// <summary>Number of currently active (non-null) dialogs.</summary>
    internal int DialogsCount => Dialogs.Count(d => d != null);

    public int MapSizeX => VoxelMap.MapSizeX;
    public int MapSizeY => VoxelMap.MapSizeY;
    public int MapSizeZ => VoxelMap.MapSizeZ;
    public int TerrainTexturesPerAtlas { get; set; }

    public bool EnableDraw2d { get => ENABLE_DRAW2D; set => ENABLE_DRAW2D = value; }


    /// <summary>
    /// Returns the index of the dialog with key <paramref name="name"/>,
    /// or -1 if no such dialog is active.
    /// </summary>
    public int GetDialogId(string name)
    {
        for (int i = 0; i < Dialogs.Length; i++)
        {
            if (Dialogs[i]?.key == name)
                return i;
        }
        return -1;
    }

    // ── Entity helpers ────────────────────────────────────────────────────────

    /// <summary>Appends <paramref name="entity"/> to the local entity list.</summary>
    public void EntityAddLocal(Entity entity) => Entities.Add(entity);

    /// <summary>
    /// Returns the entity index of the followed player, or
    /// <see langword="null"/> when no follow target is set or found.
    /// </summary>
    public int? FollowId()
    {
        if (Follow == null) return null;

        for (int i = 0; i < Entities.Count; i++)
        {
            if (Entities[i]?.drawName?.Name == Follow)
                return i;
        }
        return null;
    }

    /// <summary>Creates a bullet entity travelling from <paramref name="fromX/Y/Z"/> to <paramref name="toX/Y/Z"/>.</summary>
    internal static Entity CreateBulletEntity(
        float fromX, float fromY, float fromZ,
        float toX, float toY, float toZ,
        float speed)
    {
        return new Entity
        {
            bullet = new Bullet
            {
                fromX = fromX,
                fromY = fromY,
                fromZ = fromZ,
                toX = toX,
                toY = toY,
                toZ = toZ,
                speed = speed,
            },
            sprite = new Sprite
            {
                image = "Sponge.png",
                size = 4,
                animationcount = 0,
            },
        };
    }

    // ── Lighting / colour ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the ambient terrain tint colour for the current camera position
    /// (blue underwater, orange in lava, white normally).
    /// </summary>
    internal int Terraincolor()
    {
        if (WaterSwimmingCamera()) return ColorUtils.ColorFromArgb(255, 78, 95, 140);
        if (LavaSwimmingCamera()) return ColorUtils.ColorFromArgb(255, 222, 101, 46);
        return ColorUtils.ColorFromArgb(255, 255, 255, 255);
    }

    /// <summary>Uploads <paramref name="color"/> as the OpenGL ambient light value.</summary>
    internal void SetAmbientLight(int color) =>
        Platform.GlLightModelAmbient(
            ColorUtils.ColorR(color),
            ColorUtils.ColorG(color),
            ColorUtils.ColorB(color));

    // ── Sky clear colour ──────────────────────────────────────────────────────

    // These are compile-time constants (black sky, full alpha).
    // UpdateClearColor in GameLoop uses them; the compiler folds the / 255f
    // divisions to 0f / 0f / 0f / 1f at JIT time.
    public const int clearcolorR = 0;
    public const int clearcolorG = 0;
    public const int clearcolorB = 0;
    public const int clearcolorA = 255;

    // ── VSync / lag simulation ────────────────────────────────────────────────

    /// <summary>Applies the current VSync setting (disabled only when lag simulation is active).</summary>
    public void UseVsync() => Platform.SetVSync(EnableLog != 1);

    /// <summary>Cycles through lag-simulation modes (0 = off, 1 = no vsync, 2 = spin-wait).</summary>
    public void ToggleVsync()
    {
        EnableLog = (EnableLog + 1) % 3;
        UseVsync();
    }

    // ── GUI state ─────────────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, the next <see cref="OnResize"/> call will
    /// send the current resolution to the server.
    /// Set to <see langword="true"/> in <c>ProcessServerIdentification</c> once
    /// the connection is established.
    /// </summary>
    private bool sendResize;

    /// <summary>Returns to in-game GUI state and releases the free mouse.</summary>
    public void GuiStateBackToGame()
    {
        GuiState = GuiState.Normal;
        SetFreeMouse(false);
    }

    /// <summary>Opens the escape menu and releases the mouse pointer lock.</summary>
    public void EscapeMenuStart()
    {
        GuiState = GuiState.EscapeMenu;
        MenuState = new MenuState();
        EscapeMenuRestart = true;
        Platform.ExitMousePointerLock();
    }

    /// <summary>Shows the escape menu in free-mouse mode.</summary>
    public void ShowEscapeMenu()
    {
        GuiState = GuiState.EscapeMenu;
        MenuState = new MenuState();
        SetFreeMouse(true);
    }

    /// <summary>Opens the inventory screen in free-mouse mode.</summary>
    public void ShowInventory()
    {
        GuiState = GuiState.Inventory;
        MenuState = new MenuState();
        SetFreeMouse(true);
    }

    // ── Text measurement ──────────────────────────────────────────────────────

    /// <summary>Returns the rendered width of <paramref name="s"/> at the given point size.</summary>
    public int TextSizeWidth(string s, int size)
    {
        TextRenderer.TextSize(s, size, out int width, out _);
        return width;
    }

    /// <summary>Returns the rendered height of <paramref name="s"/> at the given point size.</summary>
    public int TextSizeHeight(string s, int size)
    {
        TextRenderer.TextSize(s, size, out _, out int height);
        return height;
    }

    // ── Action queue ──────────────────────────────────────────────────────────

    /// <summary>
    /// Enqueues <paramref name="action"/> for execution on the main thread at
    /// the end of the next frame. Thread-safe — see <see cref="ConcurrentQueue{T}"/>.
    /// </summary>
    public void QueueActionCommit(Action action) => commitActions.Enqueue(action);

    // ── Draw dispatch ─────────────────────────────────────────────────────────

    /// <summary>Sets the model-view matrix uniform and draws <paramref name="model"/>.</summary>
    public void DrawModel(GeometryModel model)
    {
        SetMatrixUniformModelView();
        Platform.DrawModel(model);
    }

    /// <summary>Sets the model-view matrix uniform and draws a list of models.</summary>
    public void DrawModels(List<GeometryModel> model, int count)
    {
        SetMatrixUniformModelView();
        Platform.DrawModels(model, count);
    }

    /// <summary>Sets the model-view matrix uniform and draws raw geometry data.</summary>
    public void DrawModelData(GeometryModel data)
    {
        SetMatrixUniformModelView();
        Platform.DrawModelData(data);
    }

    // ── Per-frame update ──────────────────────────────────────────────────────

    /// <summary>Calls the read-only main-thread hook on all registered mods.</summary>
    public void Update(float dt)
    {
        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.OnNewFrameReadOnlyMainThread(dt);
    }

    // ── Block picking ─────────────────────────────────────────────────────────

    /// <summary>Returns the nearest <see cref="BlockPosSide"/> to <paramref name="target"/>.</summary>
    public BlockPosSide Nearest(ArraySegment<BlockPosSide> pick2, int pick2Count, Vector3 target)
    {
        float minDist = float.MaxValue;
        BlockPosSide nearest = null;
        for (int i = 0; i < pick2Count; i++)
        {
            float dist = Vector3.Distance(pick2[i].blockPos, target);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = pick2[i];
            }
        }
        return nearest;
    }

    /// <summary>
    /// Performs a ray–block intersection for <paramref name="line"/>, returning
    /// the hit blocks sorted by distance from the ray origin.
    /// </summary>
    public ArraySegment<BlockPosSide> Pick(BlockOctreeSearcher s_, Line3D line, out int retCount)
    {
        int minX = Math.Max((int)Math.Min(line.Start[0], line.End[0]), 0);
        int minY = Math.Max((int)Math.Min(line.Start[1], line.End[1]), 0);
        int minZ = Math.Max((int)Math.Min(line.Start[2], line.End[2]), 0);

        int maxX = Math.Min((int)Math.Max(line.Start[0], line.End[0]), VoxelMap.MapSizeX);
        int maxY = Math.Min((int)Math.Max(line.Start[1], line.End[1]), VoxelMap.MapSizeZ);
        int maxZ = Math.Min((int)Math.Max(line.Start[2], line.End[2]), VoxelMap.MapSizeY);

        int size = (int)BitOperations.RoundUpToPowerOf2(
            (uint)Math.Max(maxX - minX + 1, Math.Max(maxY - minY + 1, maxZ - minZ + 1)));

        s_.StartBox = new Box3(
            new Vector3(minX, minY, minZ),
            new Vector3(minX + size, minY + size, minZ + size));

        ArraySegment<BlockPosSide> pick2 = s_.LineIntersection(
            IsTileEmptyForPhysics, Getblockheight, line, out retCount);

        PickSort(pick2, retCount, line.Start);
        return pick2;
    }

    /// <summary>
    /// Sorts <paramref name="pick"/> by ascending distance from <paramref name="start"/>
    /// using bubble sort. Suitable for the small result sets (typically &lt;10) produced
    /// by block picking; replace with <c>Span.Sort</c> if larger sets arise.
    /// </summary>
    private void PickSort(ArraySegment<BlockPosSide> pick, int pickCount, Vector3 start)
    {
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < pickCount - 1; i++)
            {
                if (Vector3.Distance(pick[i].blockPos, start) >
                    Vector3.Distance(pick[i + 1].blockPos, start))
                {
                    (pick[i], pick[i + 1]) = (pick[i + 1], pick[i]);
                    changed = true;
                }
            }
        }
        while (changed);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Disposes all mods and releases all cached GPU texture handles.
    /// </summary>
    public void Dispose()
    {
        for (int i = 0; i < ClientMods.Count; i++)
            ClientMods[i]?.Dispose();

        foreach (int id in textures.Values)
            Platform.GLDeleteTexture(id);

        foreach (CachedTexture ct in CachedTextTextures.Values)
            Platform.GLDeleteTexture(ct.textureId);
    }

    // ── Stubs (candidates for removal) ───────────────────────────────────────

    /// <remarks>
    /// Cito stub — always returns <see langword="true"/>.
    /// All call sites can be replaced with a literal <c>true</c> and this method removed.
    /// </remarks>
    internal static bool EnablePlayerUpdatePosition(int kKey) => true;

    /// <remarks>
    /// Cito stub — always returns <see langword="false"/>.
    /// All call sites can be replaced with a literal <c>false</c> and this method removed.
    /// </remarks>
    internal static bool EnablePlayerUpdatePositionContainsKey(int kKey) => false;

    void IGameClient.SendChat(string message)
    {
        SendChat(message);
    }

    public bool IsValidPos(int x, int y, int z) => VoxelMap.IsValidPos(x, y, z);

    public int GetBlock(int x, int y, int z) => VoxelMap.GetBlock(x, y, z);
}