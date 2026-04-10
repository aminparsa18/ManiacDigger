using OpenTK.Mathematics;
using System.Numerics;
using Vector3 = OpenTK.Mathematics.Vector3;

public partial class Game
{
    public void MapLoadingStart()
    {
        guistate = GuiState.MapLoading;
        SetFreeMouse(true);
        maploadingprogress = new MapLoadingProgressEventArgs();
        fontMapLoading = new Font("Arial", 14, FontStyle.Regular);
    }

    internal int Xcenter(float width)
    {
        return platform.GetCanvasWidth() / 2 - (int)width / 2;
    }

    internal int Ycenter(float height)
    {
        return platform.GetCanvasHeight() / 2 - (int)height / 2;
    }

    public int Width()
    {
        return platform.GetCanvasWidth();
    }

    public int Height()
    {
        return platform.GetCanvasHeight();
    }

    public void Set3dProjection(float zfar, float fov)
    {
        float aspect_ratio = 1f * Width() / Height();
        Matrix4.CreatePerspectiveFieldOfView(fov, aspect_ratio, znear, zfar, out Matrix4 projection);
        CameraMatrix.LastProjectionMatrix = projection;
        GLMatrixModeProjection();
        GLLoadMatrix(projection);
        SetMatrixUniformProjection();
    }

    internal float Zfar()
    {
        if (d_Config3d.viewdistance >= 256)
        {
            return d_Config3d.viewdistance * 2;
        }
        return ENABLE_ZFAR ? d_Config3d.viewdistance : 99999;
    }

    internal float DecodeFixedPoint(int value)
    {
        return one * value / 32;
    }

    public static int EncodeFixedPoint(float p)
    {
        return (int)(p * 32);
    }

    public void Draw2dBitmapFile(string filename, float x, float y, float w, float h)
    {
        Draw2dTexture(GetTexture(filename), x, y, w, h, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);
    }

    internal string ValidFont(string family)
    {
        return AllowedFonts.Contains(family) ? family : AllowedFonts.First();
    }

    public int MaterialSlots_(int i)
    {
        Packet_Item item = d_Inventory.RightHand[i];
        int m = d_Data.BlockIdDirt();
        if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
        {
            m = d_Inventory.RightHand[i].BlockId;
        }
        return m;
    }

    internal int DialogsCount => dialogs.Count(d => d != null);

    internal int GetDialogId(string name)
    {
        for (int i = 0; i < dialogs.Length; i++)
        {
            if (dialogs[i] == null)
            {
                continue;
            }
            if (dialogs[i].key == name)
            {
                return i;
            }
        }
        return -1;
    }

    internal static bool EnablePlayerUpdatePosition(int kKey)
    {
        return true;
    }

    internal static bool EnablePlayerUpdatePositionContainsKey(int kKey)
    {
        return false;
    }

    public static byte HeadingByte(float orientationX, float orientationY, float orientationZ) =>
     (byte)(int)(orientationY % (2 * MathF.PI) / (2 * MathF.PI) * 256);

    public static byte PitchByte(float orientationX, float orientationY, float orientationZ)
    {
        float xx = (orientationX + MathF.PI) % (2 * MathF.PI);
        return (byte)(int)(xx / (2 * MathF.PI) * 256);
    }

    internal void InvokeMapLoadingProgress(int progressPercent, int progressBytes, string status)
    {
        maploadingprogress = new MapLoadingProgressEventArgs
        {
            ProgressPercent = progressPercent,
            ProgressBytes = progressBytes,
            ProgressStatus = status
        };
    }

    internal void Log(string p)
    {
        AddChatline(p);
    }

    internal int? FollowId()
    {
        if (Follow == null)
        {
            return null;
        }
        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] == null)
            {
                continue;
            }
            if (entities[i].drawName == null)
            {
                continue;
            }
            DrawName p = entities[i].drawName;
            if (p.Name == Follow)
            {
                return i;
            }
        }
        return null;
    }

    internal int TextSizeWidth(string s, int size)
    {
        platform.TextSize(s, size, out int width, out _);
        return width;
    }

    internal int TextSizeHeight(string s, int size)
    {
        platform.TextSize(s, size, out _, out int height);
        return height;
    }

    internal void EntityAddLocal(Entity entity)
    {
        entities.Add(entity);
    }

    internal static Entity CreateBulletEntity(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float speed)
    {
        Entity entity = new();

        Bullet bullet = new()
        {
            fromX = fromX,
            fromY = fromY,
            fromZ = fromZ,
            toX = toX,
            toY = toY,
            toZ = toZ,
            speed = speed
        };
        entity.bullet = bullet;

        entity.sprite = new Sprite
        {
            image = "Sponge.png",
            size = 4,
            animationcount = 0
        };

        return entity;
    }

    internal int Terraincolor()
    {
        if (WaterSwimmingCamera())
        {
            return ColorUtils.ColorFromArgb(255, 78, 95, 140);
        }
        else if (LavaSwimmingCamera())
        {
            return ColorUtils.ColorFromArgb(255, 222, 101, 46);
        }
        else
        {
            return ColorUtils.ColorFromArgb(255, 255, 255, 255);
        }
    }

    internal void SetAmbientLight(int color)
    {
        int r = ColorUtils.ColorR(color);
        int g = ColorUtils.ColorG(color);
        int b = ColorUtils.ColorB(color);
        platform.GlLightModelAmbient(r, g, b);
    }

    internal void UseVsync()
    {
        platform.SetVSync((ENABLE_LAG == 1) ? false : true);
    }

    internal void ToggleVsync()
    {
        ENABLE_LAG++;
        ENABLE_LAG = ENABLE_LAG % 3;
        UseVsync();
    }

    internal void GuiStateBackToGame()
    {
        guistate = GuiState.Normal;
        SetFreeMouse(false);
    }
    
    public const int clearcolorR = 0;
    public const int clearcolorG = 0;
    public const int clearcolorB = 0;
    public const int clearcolorA = 255;

    internal BlockPosSide Nearest(ArraySegment<BlockPosSide> pick2, int pick2Count, Vector3 target)
    {
        float minDist = 1000 * 1000;
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

    internal BlockOctreeSearcher s;

    internal void UseInventory(Packet_Inventory packet_Inventory)
    {
        d_Inventory = packet_Inventory;
        d_InventoryUtil.UpdateInventory(packet_Inventory);
    }

    internal void Set3dProjection1(float zfar_)
    {
        Set3dProjection(zfar_, CurrentFov());
    }

    internal void Set3dProjection2()
    {
        Set3dProjection1(Zfar());
    }

    private bool sendResize;

    public void EscapeMenuStart()
    {
        guistate = GuiState.EscapeMenu;
        menustate = new MenuState();
        platform.ExitMousePointerLock();
        escapeMenuRestart = true;
    }

    public void ShowEscapeMenu()
    {
        guistate = GuiState.EscapeMenu;
        menustate = new MenuState();
        SetFreeMouse(true);
    }

    public void ShowInventory()
    {
        guistate = GuiState.Inventory;
        menustate = new MenuState();
        SetFreeMouse(true);
    }

    public void Update(float dt)
    {
        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnNewFrameReadOnlyMainThread(this, dt);
        }
    }

    public ArraySegment<BlockPosSide> Pick(BlockOctreeSearcher s_, Line3D line, out int retCount)
    {
        int minX = (int)Math.Min(line.Start[0], line.End[0]);
        int minY = (int)Math.Min(line.Start[1], line.End[1]);
        int minZ = (int)Math.Min(line.Start[2], line.End[2]);

        if (minX < 0) { minX = 0; }
        if (minY < 0) { minY = 0; }
        if (minZ < 0) { minZ = 0; }

        int maxX = (int)Math.Max(line.Start[0], line.End[0]);
        int maxY = (int)Math.Max(line.Start[1], line.End[1]);
        int maxZ = (int)Math.Max(line.Start[2], line.End[2]);

        if (maxX > VoxelMap.MapSizeX) { maxX = VoxelMap.MapSizeX; }
        if (maxY > VoxelMap.MapSizeZ) { maxY = VoxelMap.MapSizeZ; }
        if (maxZ > VoxelMap.MapSizeY) { maxZ = VoxelMap.MapSizeY; }

        int sizex = maxX - minX + 1;
        int sizey = maxY - minY + 1;
        int sizez = maxZ - minZ + 1;

        int size = (int)BitOperations.RoundUpToPowerOf2(
            (uint)Math.Max(sizex, Math.Max(sizey, sizez))
        );

        s_.StartBox = new Box3(new Vector3(minX, minY, minZ), new Vector3(minX + size, minY + size, minZ + size));

        var pick2 = s_.LineIntersection(
            IsTileEmptyForPhysics,
            Getblockheight,
            line,
            out retCount
        );

        PickSort(pick2, retCount, line.Start);

        return pick2;
    }

    private void PickSort(ArraySegment<BlockPosSide> pick, int pickCount, Vector3 start)
    {
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < pickCount - 1; i++)
            {
                float dist = Vector3.Distance(pick[i].blockPos, start);
                float distNext = Vector3.Distance(pick[i + 1].blockPos, start);
                if (dist > distNext)
                {
                    changed = true;

                    BlockPosSide swapTemp = pick[i];
                    pick[i] = pick[i + 1];
                    pick[i + 1] = swapTemp;
                }
            }
        }
        while (changed);
    }

    public IGamePlatform GetPlatform()
    {
        return platform;
    }

    public void SetPlatform(IGamePlatform value)
    {
        platform = value;
    }

    public float Scale()
    {
        //Only scale things on mobile devices
        if (platform.IsSmallScreen())
        {
            float scale = one * Width() / 1280;
            return scale;
        }
        else
        {
            return one;
        }
    }
  
    public void QueueActionCommit(Action action)
    {
        commitActions.Add(action);
    }

    public void DrawModel(GeometryModel model)
    {
        SetMatrixUniformModelView();
        platform.DrawModel(model);
    }

    public void DrawModels(List<GeometryModel> model, int count)
    {
        SetMatrixUniformModelView();
        platform.DrawModels(model, count);
    }

    public void DrawModelData(GeometryModel data)
    {
        SetMatrixUniformModelView();
        platform.DrawModelData(data);
    }

    public void Dispose()
    {
        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].Dispose(this);
        }
        foreach (int id in textures.Values)
        {
            platform.GLDeleteTexture(id);
        }
        for (int i = 0; i < cachedTextTextures.Count; i++)
        {
            if (cachedTextTextures[i] == null)
            {
                continue;
            }
            if (cachedTextTextures[i].texture == null)
            {
                continue;
            }
            platform.GLDeleteTexture(cachedTextTextures[i].texture.textureId);
        }
    }
}