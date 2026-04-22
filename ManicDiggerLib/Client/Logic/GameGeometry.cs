using ManicDigger;

public partial class Game
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Height of a rail block as a fraction of a full block.</summary>
    private const float RailBlockHeight = 3f / 10f;

    /// <summary>Number of line segments used to approximate a circle.</summary>
    private const int CircleSegments = 32;

    // ── Pre-allocated 2D draw models ──────────────────────────────────────────

    /// <summary>
    /// Reusable model for <see cref="Draw2dTextureSimple"/> (full-texture quad).
    /// Allocated on first use and reused every subsequent call.
    /// </summary>
    private GeometryModel _quadModel;

    /// <summary>
    /// Reusable model for atlas-sourced quad draws.
    /// <see cref="Draw2dTextureInAtlas"/> and <see cref="Draw2dTexturePart"/>
    /// previously called <see cref="Quad.CreateColored"/> on every draw call,
    /// allocating Xyz <c>float[12]</c>, Uv <c>float[8]</c>, and Rgba <c>byte[16]</c>
    /// every frame per 2D element. This model is allocated once and its arrays
    /// are overwritten in-place before each GPU upload.
    /// </summary>
    private GeometryModel _atlasQuadModel;

    /// <summary>
    /// Scratch array for <see cref="Draw2dTextures"/>. Pre-allocated to avoid
    /// a <c>new GeometryModel[512]</c> allocation on every batch draw call.
    /// </summary>
    private readonly GeometryModel[] _batchModelScratch = new GeometryModel[512];

    /// <summary>
    /// Lazy-initialised per-radius circle geometry.
    /// Indices, Rgba and Uv are built once; only Xyz is updated per call.
    /// </summary>
    private GeometryModel _circleModelData;

    /// <summary>
    /// Font cache keyed by point size. Avoids allocating a new <see cref="Font"/>
    /// object on every <see cref="Draw2dText1"/> call (called per-frame for every
    /// visible text element).
    /// </summary>
    private readonly Dictionary<float, Font> _fontCache = new();

    // ── Block geometry ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the height (in blocks) of the highest non-air block at
    /// (<paramref name="x"/>, <paramref name="y"/>) below or at <paramref name="z_"/>.
    /// Returns 0 when the column is entirely air.
    /// </summary>
    public int Blockheight(int x, int y, int z_)
    {
        for (int z = z_; z >= 0; z--)
        {
            if (VoxelMap.GetBlock(x, y, z) != 0)
                return z + 1;
        }
        return 0;
    }

    /// <summary>
    /// Returns the visual height of the block at (<paramref name="x"/>,
    /// <paramref name="y"/>, <paramref name="z"/>) as a fraction of a full block.
    /// Used for physics and camera positioning on partial-height blocks.
    /// Returns 1 for out-of-bounds positions (treat as solid).
    /// </summary>
    public float Getblockheight(int x, int y, int z)
    {
        if (!VoxelMap.IsValidPos(x, y, z))
            return 1f;

        int block = VoxelMap.GetBlock(x, y, z);
        if (blocktypes[block].Rail != 0) return RailBlockHeight;
        if (blocktypes[block].DrawType == DrawType.HalfHeight) return 0.5f;
        if (blocktypes[block].DrawType == DrawType.Flat) return 0.05f;
        return 1f;
    }

    // ── 2D drawing ────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a 2D texture at the given screen position.
    /// Routes to the simple (full-texture) or atlas path depending on whether
    /// <paramref name="inAtlasId"/> is specified and whether the colour is plain white.
    /// </summary>
    public void Draw2dTexture(int textureid, float x1, float y1, float width, float height,
        int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (color == ColorUtils.ColorFromArgb(255, 255, 255, 255) && inAtlasId == null)
            Draw2dTextureSimple(textureid, x1, y1, width, height, enabledepthtest);
        else
            Draw2dTextureInAtlas(textureid, x1, y1, width, height, inAtlasId, atlastextures, color, enabledepthtest);
    }

    /// <summary>
    /// Draws a full-texture quad using the cached <see cref="_quadModel"/>.
    /// No per-call allocation — the model is created once and reused.
    /// </summary>
    private void Draw2dTextureSimple(int textureid, float x1, float y1,
        float width, float height, bool enabledepthtest)
    {
        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);

        if (!enabledepthtest)
            platform.GlDisableDepthTest();

        _quadModel ??= platform.CreateModel(Quad.Create());

        GLPushMatrix();
        GLTranslate(x1, y1, 0);
        GLScale(width, height, 0);
        GLScale(0.5f, 0.5f, 0);
        GLTranslate(1f, 1f, 0);
        DrawModel(_quadModel);
        GLPopMatrix();

        if (!enabledepthtest)
            platform.GlEnableDepthTest();

        platform.GlEnableCullFace();
    }

    /// <summary>
    /// Draws a sub-region of a texture atlas at the given screen position.
    /// Uses a single pre-allocated <see cref="_atlasQuadModel"/> updated in-place,
    /// avoiding the per-call Xyz/Uv/Rgba array allocations that
    /// <see cref="Quad.CreateColored"/> would otherwise produce.
    /// </summary>
    private void Draw2dTextureInAtlas(int textureid, float x1, float y1,
        float width, float height, int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (inAtlasId == null) return;

        RectangleF rect = TextureAtlasCi.TextureCoords2d(inAtlasId.Value, atlastextures);
        FillAtlasQuadModel(rect.X, rect.Y, rect.Width, rect.Height,
            x1, y1, width, height, color);

        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);
        if (!enabledepthtest) platform.GlDisableDepthTest();

        platform.UpdateModel(_atlasQuadModel);
        DrawModelData(_atlasQuadModel);

        if (!enabledepthtest) platform.GlEnableDepthTest();
        platform.GlEnableCullFace();
    }

    /// <summary>
    /// Draws a portion of a texture (defined by source UV extents) onto a
    /// destination rectangle in screen space.
    /// Uses the same pre-allocated <see cref="_atlasQuadModel"/> as
    /// <see cref="Draw2dTextureInAtlas"/>.
    /// </summary>
    public void Draw2dTexturePart(int textureid, float srcwidth, float srcheight,
        float dstx, float dsty, float dstwidth, float dstheight, int color, bool enabledepthtest)
    {
        FillAtlasQuadModel(0f, 0f, srcwidth, srcheight,
            dstx, dsty, dstwidth, dstheight, color);

        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);
        if (!enabledepthtest) platform.GlDisableDepthTest();

        platform.UpdateModel(_atlasQuadModel);
        DrawModelData(_atlasQuadModel);

        if (!enabledepthtest) platform.GlEnableDepthTest();
        platform.GlEnableCullFace();
    }

    /// <summary>
    /// Initialises <see cref="_atlasQuadModel"/> on first call and overwrites its
    /// Xyz, Uv and Rgba arrays in-place for the given source UV and destination
    /// rectangle. The index array (two triangles, never changes) is set once.
    /// </summary>
    private void FillAtlasQuadModel(float sx, float sy, float sw, float sh,
        float dx, float dy, float dw, float dh, int color)
    {
        if (_atlasQuadModel == null)
        {
            _atlasQuadModel = new GeometryModel
            {
                Xyz = new float[4 * 3],
                Uv = new float[4 * 2],
                Rgba = new byte[4 * 4],
                Indices = [0, 1, 2, 0, 2, 3],
                VerticesCount = 4,
                IndicesCount = 6,
                Mode = (int)DrawMode.Triangles,
            };
        }

        // Xyz — screen-space corners (Z = 0)
        float[] xyz = _atlasQuadModel.Xyz;
        xyz[0] = dx; xyz[1] = dy; xyz[2] = 0f;
        xyz[3] = dx + dw; xyz[4] = dy; xyz[5] = 0f;
        xyz[6] = dx + dw; xyz[7] = dy + dh; xyz[8] = 0f;
        xyz[9] = dx; xyz[10] = dy + dh; xyz[11] = 0f;

        // Uv — atlas texture coordinates
        float[] uv = _atlasQuadModel.Uv;
        uv[0] = sx; uv[1] = sy;
        uv[2] = sx + sw; uv[3] = sy;
        uv[4] = sx + sw; uv[5] = sy + sh;
        uv[6] = sx; uv[7] = sy + sh;

        // Rgba — same colour for all 4 vertices
        byte r = (byte)ColorUtils.ColorR(color);
        byte g = (byte)ColorUtils.ColorG(color);
        byte b = (byte)ColorUtils.ColorB(color);
        byte a = (byte)ColorUtils.ColorA(color);
        byte[] rgba = _atlasQuadModel.Rgba;
        for (int i = 0; i < 4; i++)
        {
            rgba[i * 4 + 0] = r;
            rgba[i * 4 + 1] = g;
            rgba[i * 4 + 2] = b;
            rgba[i * 4 + 3] = a;
        }
    }

    /// <summary>
    /// Draws a batch of textured quads in a single GPU call by combining their
    /// geometry into one model. Uses <see cref="_batchModelScratch"/> to avoid
    /// a per-call <c>new GeometryModel[512]</c> allocation.
    /// </summary>
    public void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureid)
    {
        int count = 0;
        for (int i = 0; i < todrawLength; i++)
        {
            Draw2dData d = todraw[i];
            RectangleF rect = TextureAtlasCi.TextureCoords2d(d.inAtlasId, TexturesPacked);
            _batchModelScratch[count++] = Quad.CreateColored(
                rect.X, rect.Y, rect.Width, rect.Height,
                d.x1, d.y1, d.width, d.height,
                (byte)ColorUtils.ColorR(d.color),
                (byte)ColorUtils.ColorG(d.color),
                (byte)ColorUtils.ColorB(d.color),
                (byte)ColorUtils.ColorA(d.color));
        }

        GeometryModel combined = CombineModelData(_batchModelScratch, count);
        combined.Mode = (int)DrawMode.Triangles;

        platform.GlDisableCullFace();
        platform.BindTexture2d(textureid);
        platform.GlDisableDepthTest();
        platform.UpdateModel(combined);
        DrawModelData(combined);
        platform.GlEnableDepthTest();
        platform.GlEnableCullFace();
    }

    /// <summary>Runs the 2D draw pass for all registered mods.</summary>
    internal void Draw2d(float dt)
    {
        if (!ENABLE_DRAW2D) return;

        OrthoMode(Width(), Height());

        for (int i = 0; i < clientmods.Count; i++)
            clientmods[i]?.OnNewFrameDraw2d(this, dt);

        PerspectiveMode();
    }

    /// <summary>
    /// Merges <paramref name="count"/> geometry models into a single combined model
    /// by concatenating their vertex and index data.
    /// Xyz, Uv and Rgba arrays are copied with <see cref="Span{T}.CopyTo"/> rather
    /// than manual element loops. Index values are offset by the running vertex base.
    /// </summary>
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
            Rgba = new byte[totalVertices * 4],
        };

        for (int i = 0; i < count; i++)
        {
            GeometryModel m = modelDatas[i];
            int baseVertex = ret.VerticesCount;
            int baseIndex = ret.IndicesCount;

            // Indices — each index must be shifted by the current vertex base.
            for (int k = 0; k < m.IndicesCount; k++)
                ret.Indices[baseIndex + k] = m.Indices[k] + baseVertex;
            ret.IndicesCount += m.IndicesCount;

            // Xyz / Uv / Rgba — contiguous float/byte blocks; Span.CopyTo is faster
            // than manual element loops and avoids bounds-check overhead in the JIT.
            m.Xyz.AsSpan(0, m.VerticesCount * 3)
                  .CopyTo(ret.Xyz.AsSpan(baseVertex * 3));

            m.Uv.AsSpan(0, m.VerticesCount * 2)
                 .CopyTo(ret.Uv.AsSpan(baseVertex * 2));

            m.Rgba.AsSpan(0, m.VerticesCount * 4)
                   .CopyTo(ret.Rgba.AsSpan(baseVertex * 4));

            ret.VerticesCount += m.VerticesCount;
        }

        return ret;
    }

    // ── 2D text ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Convenience overload that draws <paramref name="text"/> using Arial at the
    /// given point size. Fonts are cached by size to avoid allocating a new
    /// <see cref="Font"/> object on every call (this method is called per-frame
    /// for every visible text element).
    /// </summary>
    internal void Draw2dText1(string text, int x, int y, int fontsize, int? color, bool enabledepthtest)
    {
        if (!_fontCache.TryGetValue(fontsize, out Font font))
        {
            font = new Font("Arial", fontsize, FontStyle.Regular);
            _fontCache[fontsize] = font;
        }
        Draw2dText(text, font, x, y, color, enabledepthtest);
    }

    /// <summary>
    /// Draws <paramref name="text"/> to the screen using a cached texture.
    /// The texture is created on first use and evicted when not accessed for
    /// a configurable duration by <c>DeleteUnusedCachedTextTextures</c>.
    /// </summary>
    public void Draw2dText(string text, Font font, float x, float y, int? color, bool enabledepthtest)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        color ??= ColorUtils.ColorFromArgb(255, 255, 255, 255);
        TextStyle t = new()
        {
            Text = text,
            Color = color.Value,
            FontSize = font.Size,
            FontFamily = font.FontFamily.Name,
            FontStyle = font.Style,
        };

        // Cache lookup — retrieve once and reuse rather than calling
        // GetCachedTextTexture twice (check-null then retrieve).
        CachedTexture cached = GetCachedTextTexture(t);
        if (cached == null)
        {
            CachedTexture ct = MakeTextTexture(t);
            if (ct == null) return;

            cachedTextTextures.Add(new CachedTextTexture { text = t, texture = ct });
            cached = ct;
        }

        cached.lastuseMilliseconds = platform.TimeMillisecondsFromStart;
        Draw2dTexture(cached.textureId, x, y, cached.sizeX, cached.sizeY,
            null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), enabledepthtest);
        DeleteUnusedCachedTextTextures();
    }

    // ── Primitives ────────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a 2D circle outline at screen position (<paramref name="x"/>,
    /// <paramref name="y"/>) with the given <paramref name="radius"/>.
    /// Uses a pre-allocated <see cref="_circleModelData"/> whose Xyz array is
    /// updated in-place on each call; only the vertex positions change.
    /// </summary>
    public void Circle3i(float x, float y, float radius)
    {
        if (_circleModelData == null)
        {
            _circleModelData = new GeometryModel
            {
                Mode = (int)DrawMode.Lines,
                Indices = new int[CircleSegments * 2],
                Xyz = new float[CircleSegments * 3],
                Rgba = new byte[CircleSegments * 4],
                Uv = new float[CircleSegments * 2],
                IndicesCount = CircleSegments * 2,
                VerticesCount = CircleSegments,
            };
            // Indices and Rgba never change — built once.
            for (int i = 0; i < CircleSegments; i++)
            {
                _circleModelData.Indices[i * 2] = i;
                _circleModelData.Indices[i * 2 + 1] = (i + 1) % CircleSegments;
            }
            _circleModelData.Rgba.AsSpan().Fill(255);
            // Uv is zero-initialised by default.
        }

        // Update only the Xyz positions — everything else is already correct.
        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * 2f * MathF.PI / CircleSegments;
            _circleModelData.Xyz[i * 3 + 0] = x + MathF.Cos(angle) * radius;
            _circleModelData.Xyz[i * 3 + 1] = y + MathF.Sin(angle) * radius;
            _circleModelData.Xyz[i * 3 + 2] = 0f;
        }

        platform.UpdateModel(_circleModelData);

        GLPushMatrix();
        GLLoadIdentity();
        DrawModelData(_circleModelData);
        GLPopMatrix();
    }
}