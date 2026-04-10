public partial class Game
{
    // -------------------------------------------------------------------------
    // Block geometry
    // -------------------------------------------------------------------------

    public int Blockheight(int x, int y, int z_)
    {
        for (int z = z_; z >= 0; z--)
        {
            if (VoxelMap.GetBlock(x, y, z) != 0)
                return z + 1;
        }
        return 0;
    }

    public float Getblockheight(int x, int y, int z)
    {
        float RailHeight = one * 3 / 10;
        if (!VoxelMap.IsValidPos(x, y, z))
            return 1;

        int block = VoxelMap.GetBlock(x, y, z);
        if (blocktypes[block].Rail != 0)
            return RailHeight;
        if (blocktypes[block].DrawType == Packet_DrawTypeEnum.HalfHeight)
            return one / 2;
        if (blocktypes[block].DrawType == Packet_DrawTypeEnum.Flat)
            return one / 20;

        return 1;
    }

    // -------------------------------------------------------------------------
    // 2D drawing
    // -------------------------------------------------------------------------

    private GeometryModel quadModel;

    public void Draw2dTexture(int textureid, float x1, float y1, float width, float height, int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (color == ColorUtils.ColorFromArgb(255, 255, 255, 255) && inAtlasId == null)
            Draw2dTextureSimple(textureid, x1, y1, width, height, enabledepthtest);
        else
            Draw2dTextureInAtlas(textureid, x1, y1, width, height, inAtlasId, atlastextures, color, enabledepthtest);
    }

    private void Draw2dTextureSimple(int textureid, float x1, float y1, float width, float height, bool enabledepthtest)
    {
        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);

        if (!enabledepthtest)
            platform.GlDisableDepthTest();

        quadModel ??= platform.CreateModel(Quad.Create());

        GLPushMatrix();
        GLTranslate(x1, y1, 0);
        GLScale(width, height, 0);
        GLScale(one / 2, one / 2, 0);
        GLTranslate(one, one, 0);
        DrawModel(quadModel);
        GLPopMatrix();

        if (!enabledepthtest)
            platform.GlEnableDepthTest();

        platform.GlEnableCullFace();
    }

    private void Draw2dTextureInAtlas(int textureid, float x1, float y1, float width, float height, int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (inAtlasId == null)
            return;

        RectangleF rect = TextureAtlasCi.TextureCoords2d(inAtlasId ?? 0, atlastextures);

        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);

        if (!enabledepthtest)
            platform.GlDisableDepthTest();

        GeometryModel data = Quad.CreateColored(
            rect.X, rect.Y, rect.Width, rect.Height,
            x1, y1, width, height,
            (byte)ColorUtils.ColorR(color), (byte)ColorUtils.ColorG(color), (byte)ColorUtils.ColorB(color), (byte)ColorUtils.ColorA(color));
        data.Mode=(int)DrawMode.Triangles;
        platform.UpdateModel(data);
        DrawModelData(data);

        if (!enabledepthtest)
            platform.GlEnableDepthTest();

        platform.GlEnableCullFace();
    }

    public void Draw2dTexturePart(int textureid, float srcwidth, float srcheight, float dstx, float dsty, float dstwidth, float dstheight, int color, bool enabledepthtest)
    {
        RectangleF rect = new(0, 0, srcwidth, srcheight);

        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);

        if (!enabledepthtest)
            platform.GlDisableDepthTest();

        GeometryModel data = Quad.CreateColored(
            rect.X, rect.Y, rect.Width, rect.Height,
            dstx, dsty, dstwidth, dstheight,
            (byte)ColorUtils.ColorR(color), (byte)ColorUtils.ColorG(color), (byte)ColorUtils.ColorB(color), (byte)ColorUtils.ColorA(color));
        data.Mode = (int)DrawMode.Triangles;
        platform.UpdateModel(data);
        DrawModelData(data);

        if (!enabledepthtest)
            platform.GlEnableDepthTest();

        platform.GlEnableCullFace();
    }

    public void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureid)
    {
        GeometryModel[] modelDatas = new GeometryModel[512];
        int modelDatasCount = 0;

        for (int i = 0; i < todrawLength; i++)
        {
            Draw2dData d = todraw[i];
            RectangleF rect = TextureAtlasCi.TextureCoords2d(d.inAtlasId, TexturesPacked);

            modelDatas[modelDatasCount++] = Quad.CreateColored(
                rect.X, rect.Y, rect.Width, rect.Height,
                d.x1, d.y1, d.width, d.height,
                (byte)ColorUtils.ColorR(d.color), (byte)ColorUtils.ColorG(d.color), (byte)ColorUtils.ColorB(d.color), (byte)ColorUtils.ColorA(d.color));
        }

        GeometryModel combined = CombineModelData(modelDatas, modelDatasCount);
        combined.Mode = (int)DrawMode.Triangles;

        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);
        platform.GlDisableDepthTest();
        platform.UpdateModel(combined);
        DrawModelData(combined);
        platform.GlEnableDepthTest();
        platform.GlEnableCullFace();
    }

    internal void Draw2d(float dt)
    {
        if (!ENABLE_DRAW2D)
            return;

        OrthoMode(Width(), Height());

        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null)
                continue;
            clientmods[i].OnNewFrameDraw2d(this, dt);
        }

        PerspectiveMode();
    }

    public static GeometryModel CombineModelData(GeometryModel[] modelDatas, int count)
    {
        int totalIndices = 0;
        int totalVertices = 0;
        for (int i = 0; i < count; i++)
        {
            totalIndices += modelDatas[i].IndicesCount;
            totalVertices += modelDatas[i].VerticesCount;
        }

        GeometryModel ret = new()
        {
            Indices = new int[totalIndices],
            Xyz = new float[totalVertices * 3],
            Uv = new float[totalVertices * 2],
            Rgba = new byte[totalVertices * 4]
        };

        for (int i = 0; i < count; i++)
        {
            GeometryModel m = modelDatas[i];
            int baseVertex = ret.VerticesCount;
            int baseIndex = ret.IndicesCount;

            for (int k = 0; k < m.IndicesCount; k++)
                ret.Indices[baseIndex + k] = m.Indices[k] + baseVertex;
            ret.IndicesCount += m.IndicesCount; 
            for (int k = 0; k < m.VerticesCount * 3; k++)
                ret.Xyz[baseVertex * 3 + k] = m.Xyz[k];

            for (int k = 0; k < m.VerticesCount * 2; k++)
                ret.Uv[baseVertex * 2 + k] = m.Uv[k];
            for (int k = 0; k < m.VerticesCount * 4; k++)
                ret.Rgba[baseVertex * 4 + k] = m.Rgba[k];

            ret.VerticesCount += m.VerticesCount;
        }

        return ret;
    }

    // -------------------------------------------------------------------------
    // 2D text
    // -------------------------------------------------------------------------

    internal void Draw2dText1(string text, int x, int y, int fontsize, int? color, bool enabledepthtest)
    {
        Font font = new("Arial", fontsize, FontStyle.Regular);
        Draw2dText(text, font, x, y, color, enabledepthtest);
    }

    // -------------------------------------------------------------------------
    // Primitives
    // -------------------------------------------------------------------------
    private const int CircleSegments = 32;

    public void Circle3i(float x, float y, float radius)
    {
        if (circleModelData == null)
        {
            circleModelData = new GeometryModel
            {
                Mode = (int)DrawMode.Lines,
                Indices = new int[CircleSegments * 2],
                Xyz = new float[CircleSegments * 3],
                Rgba = new byte[CircleSegments * 4],
                Uv = new float[CircleSegments * 2],
                IndicesCount = CircleSegments * 2,
                VerticesCount = CircleSegments
            };

            // Indices and uv/rgba never change — build once.
            for (int i = 0; i < CircleSegments; i++)
            {
                circleModelData.Indices[i * 2] = i;
                circleModelData.Indices[i * 2 + 1] = (i + 1) % CircleSegments;
            }
            for (int i = 0; i < CircleSegments * 4; i++)
                circleModelData.Rgba[i] = 255;
            // uv is already zero-initialised by default
        }

        // Only xyz changes per call — re-upload positions to GPU.
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * 2 * MathF.PI / CircleSegments;
            circleModelData.Xyz[i * 3 + 0] = x + MathF.Cos(angle) * radius;
            circleModelData.Xyz[i * 3 + 1] = y + MathF.Sin(angle) * radius;
            circleModelData.Xyz[i * 3 + 2] = 0;
        }

        platform.UpdateModel(circleModelData);

        GLPushMatrix();
        GLLoadIdentity();
        DrawModelData(circleModelData);
        GLPopMatrix();
    }

    public void Draw2dText(string text, Font font, float x, float y, int? color, bool enabledepthtest)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        color ??= ColorUtils.ColorFromArgb(255, 255, 255, 255);
        Text_ t = new()
        {
            text = text,
            color = color.Value,
            fontsize = font.Size,
            fontfamily = font.FontFamily.Name,
            fontstyle = (int)font.Style
        };

        if (GetCachedTextTexture(t) == null)
        {
            CachedTexture ct = MakeTextTexture(t);
            if (ct == null)
                return;

            cachedTextTextures.Add(new CachedTextTexture { text = t, texture = ct });
        }

        CachedTexture cached = GetCachedTextTexture(t);
        cached.lastuseMilliseconds = platform.TimeMillisecondsFromStart;
        platform.GLDisableAlphaTest();
        Draw2dTexture(cached.textureId, x, y, cached.sizeX, cached.sizeY, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), enabledepthtest);
        platform.GLEnableAlphaTest();
        DeleteUnusedCachedTextTextures();
    }
}