
using ManicDigger;
using OpenTK.Mathematics;

/// <summary>
/// Provides mods with controlled access to game state, rendering, chat,
/// and player controls. Implemented by <see cref="Game"/>.
/// </summary>
public interface IGameClient
{
    // -------------------------------------------------------------------------
    // Platform
    // -------------------------------------------------------------------------

    /// <summary>The host platform abstraction (screenshot, canvas size, etc.).</summary>
    IGamePlatform Platform { get; set; }

    // -------------------------------------------------------------------------
    // Player
    // -------------------------------------------------------------------------

    /// <summary>Local player's world-space X position.</summary>
    float LocalPositionX { get; set; }

    /// <summary>Local player's world-space Y position.</summary>
    float LocalPositionY { get; set; }

    /// <summary>Local player's world-space Z position.</summary>
    float LocalPositionZ { get; set; }

    /// <summary>Local player's X orientation (pitch/yaw/roll component).</summary>
    float LocalOrientationX { get; set; }

    /// <summary>Local player's Y orientation component.</summary>
    float LocalOrientationY { get; set; }

    /// <summary>Local player's Z orientation component.</summary>
    float LocalOrientationZ { get; set; }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the current freemove level.
    /// See <see cref="FreemoveLevelEnum"/> for valid values.
    /// </summary>
    int FreemoveLevel { get; set; }

    /// <summary>Whether freemove is permitted by the current server.</summary>
    bool AllowFreeMove { get; set; }

    // -------------------------------------------------------------------------
    // Camera / GUI
    // -------------------------------------------------------------------------

    /// <summary>When <c>false</c>, the 2-D HUD is hidden.</summary>
    bool EnableDraw2d { get; set; }

    /// <summary>Enables or disables player camera control.</summary>
    bool EnableCameraControl { set; }

    // -------------------------------------------------------------------------
    // Chat
    // -------------------------------------------------------------------------

    /// <summary>Appends a message to the local chat log.</summary>
    void AddChatLine(string message);

    /// <summary>Sends a chat message to the server.</summary>
    void SendChat(string message);

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    /// <summary>Returns the OpenGL ID of the engine's built-in white texture.</summary>
    int WhiteTexture();

    /// <inheritdoc cref="Game.Draw2dTexture"/>
    void Draw2dTexture(int textureid, float x, float y, float width, float height,
                       int? inAtlasId, int atlasIndex, int color, bool blend);

    /// <inheritdoc cref="Game.Draw2dTextures"/>
    void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId);

    /// <inheritdoc cref="Game.Draw2dText"/>
    void Draw2dText(string text, Font font, float x, float y, int? color, bool shadow);

    void Draw2dText1(string text, int x, int y, int fontsize, int? color, bool enabledepthtest);

    /// <summary>Switches the GL projection to orthographic for 2-D rendering.</summary>
    void OrthoMode(int width, int height);

    /// <summary>Restores the GL projection to perspective for 3-D rendering.</summary>
    void PerspectiveMode();

    // -------------------------------------------------------------------------
    // Diagnostics
    // -------------------------------------------------------------------------

    /// <summary>Live performance counters exposed to mods for overlay display.</summary>
    Dictionary<string, string> PerformanceInfo { get; }

    List<Entity> Entities { get; }

    void MainThreadOnRenderFrame(float deltaTime);

    void Update(float dt);

    MeshBatcher Batcher { get; }

    bool ShouldRedrawAllBlocks { get; set; }

    void QueueActionCommit(Action action);

    /// <summary>X dimension of the voxel map in blocks.</summary>
    int MapSizeX { get; }

    /// <summary>Y dimension of the voxel map in blocks.</summary>
    int MapSizeY { get; }

    /// <summary>Z dimension of the voxel map in blocks.</summary>
    int MapSizeZ { get; }

    /// <summary>Number of terrain textures packed into a single atlas.</summary>
    int TerrainTexturesPerAtlas { get; }

    /// <summary>
    /// OpenGL texture handles for each atlas slice, indexed by atlas index.
    /// </summary>
    int[] TerrainTextures1d { get; }

    /// <summary>
    /// Block type definitions indexed by block type ID.
    /// May contain <c>null</c> entries for unregistered IDs.
    /// </summary>
    Packet_BlockType[] BlockTypes { get; set; }

    /// <summary>
    /// Per-block, per-side texture IDs. Indexed as <c>[blockTypeId][sideIndex]</c>.
    /// </summary>
    int[][] TextureId { get; }

    bool IsValidPos(int x, int y, int z);

    int GetBlock(int x, int y, int z);

    TerrainChunkTesselator TerrainChunkTesselator { get; }

    VoxelMap VoxelMap { get; set; }

    int LastplacedblockX { get; set; }
    int LastplacedblockY { get; set; }
    int LastplacedblockZ { get; set; }

    // -------------------------------------------------------------------------
    // Player state
    // -------------------------------------------------------------------------

    /// <summary>The local player's entity ID.</summary>
    int LocalPlayerId { get; }

    /// <summary>Current GUI/loading state of the game.</summary>
    GuiState GuiState { get; set; }

    /// <summary>
    /// Returns the entity ID being followed in spectator mode,
    /// or <c>null</c> if not spectating.
    /// </summary>
    int? FollowId();

    /// <summary>Whether the player's eyes are currently submerged in water.</summary>
    bool WaterSwimmingEyes();

    /// <summary>Live player statistics: health, oxygen, etc.</summary>
    Packet_ServerPlayerStats PlayerStats { get; set; }

    /// <summary>Eye height of the local player's draw model, used for block sampling.</summary>
    float LocalEyeHeight { get; }

    // -------------------------------------------------------------------------
    // Block registry
    // -------------------------------------------------------------------------

    /// <summary>
    /// Provides per-block-type data such as damage dealt to the player on contact.
    /// </summary>
    BlockTypeRegistry BlockRegistry { get; }

    // -------------------------------------------------------------------------
    // Damage
    // -------------------------------------------------------------------------

    /// <summary>Applies <paramref name="damage"/> to the local player.</summary>
    /// <param name="damage">Hit points to subtract.</param>
    /// <param name="reason">Cause of death shown if the player dies.</param>
    /// <param name="sourceBlock">Block type that caused the damage, or 0 if none.</param>
    void ApplyDamageToPlayer(int damage, DeathReason reason, int sourceBlock);

    // -------------------------------------------------------------------------
    // Network
    // -------------------------------------------------------------------------

    /// <summary>Current server version string, used for protocol compatibility checks.</summary>
    string ServerGameVersion { get; set; }

    /// <summary>Sends a pre-serialised packet to the server.</summary>
    void SendPacketClient(Packet_Client packet);


    float[] LightLevels { get; set; }

    Language Language { get; set; }
    Config3d Config3d { get; set; }

    int CurrentTimeMilliseconds { get; set; }

    NetClient NetClient { get; set; }

    Packet_Server InvalidVersionPacketIdentification { get; set; }
    int LastReceivedMilliseconds { get; set; }

    void ChatLog(string p);
    int ReceivedMapLength { get; set; }
    ChunkedMap2d<int> Heightmap { get; set; }

    ClientPacketHandler[] PacketHandlers { get; set; }

    string InvalidVersionDrawMessage { get; set; }

    void ProcessServerIdentification(Packet_Server packet);
    void SendPingReply();

    ServerInformation ServerInfo { get; set; }

    void InvokeMapLoadingProgress(int progressPercent, int progressBytes, string status);
    void SetTileAndUpdate(int x, int y, int z, int type);
    int FillAreaLimit { get; set; }
    Controls Controls { get; set; }
    float MoveSpeed { get; set; }
    float Basemovespeed { get; set; }

    float PlayerPositionSpawnX { get; set; }
    float PlayerPositionSpawnY { get; set; }
    float PlayerPositionSpawnZ { get; set; }

    void ExitToMainMenu();
    void UseInventory(Packet_Inventory packet_Inventory);
    int[] NightLevels { get; set; }
    bool SkySphereNight { get; set; }
    SunMoonRenderer SunMoonRenderer { get; set; }
    int Sunlight { get; set; }
    void RedrawAllBlocks();

    string BlobDownloadName { get; set; }
    string BlobDownloadMd5 { get; set; }
    MemoryStream BlobDownload { get; set; }
    void SetFile(string name, string md5, byte[] downloaded, int downloadedLength);
    void PlayAudio(string name, float x, float y, float z);
    bool soundnow { get; set; }
    Packet_BlockType[] NewBlockTypes { get; set; }
    float DecodeFixedPoint(int value);
    string Follow { get; set; }
    void SetCamera(CameraType type);
    void GuiStateBackToGame();
    void EntityAddLocal(Entity entity);
    bool AmmoStarted { get; set; }
    int[] TotalAmmo { get; set; }
    int[] LoadedAmmo { get; set; }
    int[] TextureIdForInventory { get; set; }
    void UseTerrainTextures(string[] textureIds, int textureIdsCount);
    bool HandRedraw { get; set; }
    void SendLeave(PacketLeaveReason reason);
    void ExitAndSwitchServer(Packet_ServerRedirect newServer);
    int GetDialogId(string name);
    VisibleDialog[] Dialogs { get; set; }
    void SetFreeMouse(bool value);
    string ValidFont(string family);
    int GetTexture(string p);
    bool DeleteTexture(string name);
    Entity Player { get; set; }
    bool Spawned { get; set; }
    void MapLoaded();

    bool EnableDrawTestCharacter { get; set; }
    bool BoolCommandArgument(string arguments);
    float AssetsLoadProgress { get; set; }

    AudioControl Audio { get; set; }
    byte[] GetAssetFile(string p);
    int GetAssetFileLength(string p);
    List<Asset> Assets { get; set; }
    int TotalTimeMilliseconds { get; set; }
    bool IsSinglePlayer { get; set; }
    int GetTextureOrLoad(string name, Bitmap bmp);
    int LastPositionSentMilliseconds { get; set; }
    byte LocalStance { get; set; }

    TypingState GuiTyping { get; set; }
    bool OverheadCamera { get; set; }
    bool EnableMove { get; set; }
    bool[] KeyboardState { get; set; }
    int GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys key);
    bool ReachedWall1BlockHigh { get; set; }
    bool AutoJumpEnabled { get; set; }
    bool ReachedHalfBlock { get; set; }
    Camera OverheadCameraK { get; set; }
    float OverHeadCameraDistance { get; set; }
    Vector3 PlayerDestination { get; set; }
    bool ReachedWall { get; set; }

    AnimationHint LocalPlayerAnimationHint { get; set; }
    float TouchMoveDx { get; set; }
    float TouchMoveDy { get; set; }

    bool SwimmingEyes();

    void PlayAudioAt(string file, float x, float y, float z);
    float MovedZ { get; set; }

    int PlayerEyesBlockX { get; }

    int PlayerEyesBlockY { get; }
    int PlayerEyesBlockZ { get; }
    void AudioPlayLoop(string file, bool play, bool restart);

    int Blockheight(int x, int y, int z);
    bool IsWater(int blockType);
    bool IsTileEmptyForPhysics(int x, int y, int z);
    int Ycenter(float height);

    int MouseCurrentY { get; set; }
    bool MouseLeftClick { get; set; }

    float PushX { get; set; }
    float PushY { get; set; }
    float PushZ { get; set; }
    void PlayAudio(string file);
    void SetCharacterEyesHeight(float value);
    float GetCharacterEyesHeight();

    int ReloadStartMilliseconds { get; set; }

    int ReloadBlock { get; set; }

    Packet_Inventory Inventory { get; set; }
    InventoryUtilClient InventoryUtil { get; set; }
    int ActiveMaterial { get; set; }
    bool IsPlayerOnGround { get; set; }
    int BlockUnderPlayer();
    int MaterialSlots(int i);

    void GLPushMatrix();
    void GLPopMatrix();
    void GLRotate(float angle, float x, float y, float z);
    void GLTranslate(float x, float y, float z);
    bool DrawBlockInfo { get; set; }
    int SelectedBlockPositionX { get; set; }
    int SelectedBlockPositionY { get; set; }
    int SelectedBlockPositionZ { get; set; }

    bool IsValid(int blocktype);
    Vector3i? CurrentAttackedBlock { get; set; }
    float GetCurrentBlockHealth(int x, int y, int z);
    bool IsUsableBlock(int blocktype);
    int CurrentlyAttackedEntity { get; set; }
    int Xcenter(float width);
    CameraType CameraType { get; set; }
    float CurrentAimRadius();
    float CurrentFov();
    void Circle3i(float x, float y, float radius);
    void Draw2dBitmapFile(string filename, float x, float y, float w, float h);
    int MouseCurrentX { get; set; }
    bool GetFreeMouse();
    int TextSizeWidth(string s, int size);
    int TextSizeHeight(string s, int size);
    bool EnableDrawPosition { get; set; }
    float Scale();
    List<Chatline> ChatLines { get; set; }
    int ChatLinesCount { get; set; }
    string GuiTypingBuffer { get; set; }
    bool IsTeamchat { get; set; }
    bool IsTyping { get; set; }
    bool IsShiftPressed { get; set; }
    List<string> TypingLog { get; set; }
    int TypingLogPos { get; set; }
    void ExecuteChat(string s_);
    void StartTyping();

    bool[] KeyboardStateRaw { get; set; }
    void SetBlock(int x, int y, int z, int tileType);
    int TerrainTexture { get; set; }
    GameOption options { get; set; }
    void ToggleFog();

    void ToggleVsync();

    void InventoryClick(Packet_InventoryPosition pos);
    void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to);
    void MoveToInventory(Packet_InventoryPosition from);
    MapLoadingProgressEventArgs maploadingprogress { get; set; }
    Font FontMapLoading { get; set; }
    void Draw2dTexturePart(int textureid, float srcwidth, float srcheight,
        float dstx, float dsty, float dstwidth, float dstheight, int color, bool enabledepthtest);

    void EscapeMenuStart();

    /// <summary>Shows the escape menu in free-mouse mode.</summary>
    public void ShowEscapeMenu();

    /// <summary>Opens the inventory screen in free-mouse mode.</summary>
    public void ShowInventory();

    void CameraChange();
    void StopTyping();

    float TouchOrientationDx { get; set; }
    float TouchOrientationDy { get; set; }
    bool AudioEnabled { get; set; }
    int EnableLog { get; set; }
    int Font { get; set; }
    Dictionary<TextStyle, CachedTexture> CachedTextTextures { get; set; }
    bool EscapeMenuRestart { get; set; }
    void UseVsync();
}