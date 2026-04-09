using OpenTK.Mathematics;

/// <summary>
/// Client-side mod that renders the player's held item (or empty hand) as a 3-D model
/// in the bottom-right corner of the viewport.
/// Handles idle bob animation, attack swing, and block-placement swing.
/// </summary>
public class ModDrawHand3d : ModBase
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>Maximum light level used to normalise light values to [0, 1].</summary>
    private const int MaxLight = 15;

    /// <summary>
    /// Angular speed of the hand bob animation (radians per second multiplier).
    /// </summary>
    private const float BobSpeed = 5f;

    /// <summary>
    /// Half-amplitude of the hand bob displacement in world units.
    /// Stored as <c>7 / 100</c> to avoid integer division; see <see cref="_one"/>.
    /// </summary>
    private const float BobRange = 7f / 100f;

    /// <summary>
    /// Floating-point literal <c>1.0f</c> stored in a field to prevent the Cito
    /// transpiler from truncating integer-division expressions to integer arithmetic.
    /// </summary>
    private readonly float _one;

    /// <summary>Reference to the current game instance, set each frame in <see cref="OnNewFrameDraw3d"/>.</summary>
    internal Game game;

    /// <summary>Torch block renderer used to draw held torches and the empty-hand model.</summary>
    internal BlockRendererTorch d_BlockRendererTorch;

    /// <summary>
    /// Progress of the current attack or build swing in radians.
    /// <c>-1</c> means no swing is active.
    /// </summary>
    private float _attack;

    /// <summary>
    /// <see langword="true"/> when the active swing is a block-placement action;
    /// <see langword="false"/> when it is a destroy/attack action.
    /// </summary>
    private bool _isBuildSwing;

    /// <summary>Horizontal displacement applied to the hand during a destroy/attack swing.</summary>
    private float _attackOffset;

    /// <summary>Vertical displacement applied to the hand during a block-placement swing.</summary>
    private float _buildOffset;

    /// <summary>Accumulated time used as the input to the bob sine functions.</summary>
    private float _bobTime;

    /// <summary>
    /// Countdown timer that keeps the bob animation running for a short while after
    /// the player stops moving, then smoothly decays to zero.
    /// Initialised to <see cref="_slowdownTimerMax"/> when movement begins.
    /// </summary>
    private float _slowdownTimer;

    /// <summary>
    /// Large sentinel value assigned to <see cref="_slowdownTimer"/> the moment movement
    /// starts so that the first check after stopping can detect the transition.
    /// </summary>
    private readonly float _slowdownTimerMax;

    /// <summary>Period of one full bob cycle in radians.</summary>
    private readonly float _animPeriod;

    /// <summary>
    /// Lateral (Z-axis) bob offset derived from <see cref="BobSide"/>.
    /// Applied as a translation along the hand's local Z axis each frame.
    /// </summary>
    private float _bobOffsetZ;

    /// <summary>
    /// Vertical (X-axis in view space) bob offset derived from <see cref="BobVertical"/>.
    /// Applied as a translation along the hand's local X axis each frame.
    /// </summary>
    private float _bobOffsetX;

    /// <summary>
    /// Constant X-axis rotation of the hand rest pose (degrees).
    /// Controls the inward tilt of the hand model.
    /// </summary>
    private readonly float _restRotateX;

    /// <summary>
    /// Constant Y-axis rotation of the hand rest pose (degrees).
    /// Controls the sideways tilt of the hand model.
    /// </summary>
    private readonly float _restRotateY;

    /// <summary>
    /// Constant Y-axis translation offset of the hand rest pose.
    /// Positions the hand vertically on screen.
    /// </summary>
    private readonly float _restOffsetY;

    /// <summary>Cached geometry for the currently held item.</summary>
    private ModelData _modelData;

    /// <summary>Block ID of the item that was used to build <see cref="_modelData"/>.</summary>
    private int _cachedMaterial;

    /// <summary>Light level that was used to build <see cref="_modelData"/>.</summary>
    private float _cachedLight;

    /// <summary>Player X position recorded at the end of the previous frame.</summary>
    private float _prevPlayerX;

    /// <summary>Player Y position recorded at the end of the previous frame.</summary>
    private float _prevPlayerY;

    /// <summary>Player Z position recorded at the end of the previous frame.</summary>
    private float _prevPlayerZ;

    /// <summary>
    /// Initialises all animation parameters and creates the torch renderer dependency.
    /// </summary>
    public ModDrawHand3d()
    {
        _one = 1;
        _attack = -1;
        _attackOffset = 0;
        _buildOffset = 0;
        _bobTime = 0;
        _bobOffsetZ = 0;
        _slowdownTimerMax = 32 * 1000;
        _animPeriod = MathF.PI / (BobSpeed / 2);

        // Rest-pose transform constants — tweak these to reposition the hand on screen.
        _restRotateX = -27f;
        _restRotateY = _one * -137f / 10f;    // -13.7 degrees
        _restOffsetY = _one * -2f / 10f;     // -0.2 units
        _bobOffsetX = _one * -4f / 10f;     // -0.4 units (initial rest)

        d_BlockRendererTorch = new BlockRendererTorch();
    }

    /// <inheritdoc/>
    public override void OnNewFrameDraw3d(Game game_, float deltaTime)
    {
        if (!ModDrawHand2d.ShouldDrawHand(game_))
        {
            return;
        }

        // A 2-D hand image overrides the 3-D model entirely.
        string img = ModDrawHand2d.HandImage2d(game_);
        if (img != null)
        {
            return;
        }

        game = game_;

        // Consume pending swing triggers set by the game engine.
        if (game.handSetAttackBuild)
        {
            SetAttack(isAttack: true, isBuild: true);
            game.handSetAttackBuild = false;
        }
        if (game.handSetAttackDestroy)
        {
            SetAttack(isAttack: true, isBuild: false);
            game.handSetAttackDestroy = false;
        }

        DrawWeapon(deltaTime);
    }

    /// <summary>Returns the OpenGL texture ID of the terrain texture atlas.</summary>
    public int TerrainTexture => game.terrainTexture;

    /// <summary>Returns the number of textures packed per row/column in the terrain atlas.</summary>
    public static int TexturesPacked => Game.TexturesPacked;

    /// <summary>
    /// Returns the atlas texture ID for the given face of the currently held item.
    /// Falls back to the empty-hand block texture when the player holds nothing,
    /// holds a compass, or holds air (block ID 0).
    /// </summary>
    /// <param name="side">The block face whose texture ID is requested.</param>
    /// <returns>Texture atlas index for that face.</returns>
    public int GetWeaponTextureId(TileSide side)
    {
        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];

        if (item == null || IsCompass() || item.BlockId == 0)
        {
            // Empty hand — use the designated empty-hand block texture.
            if (side == TileSide.Top)
            {
                return game.TextureId[game.d_Data.BlockIdEmptyHand()][(int)TileSide.Top];
            }
            return game.TextureId[game.d_Data.BlockIdEmptyHand()][(int)TileSide.Front];
        }

        if (item.ItemClass == Packet_ItemClassEnum.Block)
        {
            return game.TextureId[item.BlockId][(int)side];
        }

        // TODO: return texture for non-block items.
        return 0;
    }

    /// <summary>
    /// Returns the ambient light level at the player's current position, normalised
    /// to the range [0, 1].
    /// </summary>
    public float Light()
    {
        float posx = game.player.position.x;
        float posy = game.player.position.y;
        float posz = game.player.position.z;
        int light = game.GetLight((int)posx, (int)posz, (int)posy);
        return (_one * light) / MaxLight;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the player is currently holding a torch block.
    /// </summary>
    public bool IsTorch()
    {
        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        return item != null
            && item.ItemClass == Packet_ItemClassEnum.Block
            && game.blocktypes[item.BlockId].DrawType == Packet_DrawTypeEnum.Torch;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the player is currently holding a compass block.
    /// </summary>
    public bool IsCompass()
    {
        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        return item != null
            && item.ItemClass == Packet_ItemClassEnum.Block
            && item.BlockId == game.d_Data.BlockIdCompass();
    }

    /// <summary>
    /// Returns <see langword="true"/> when the player's right-hand slot is empty
    /// (null item or block ID 0).
    /// </summary>
    public bool IsEmptyHand()
    {
        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        return item == null || item.BlockId == 0;
    }

    /// <summary>
    /// Triggers or cancels a swing animation on the held item.
    /// </summary>
    /// <param name="isAttack">
    /// <see langword="true"/> to start a swing; <see langword="false"/> to cancel it.
    /// </param>
    /// <param name="isBuild">
    /// <see langword="true"/> for a block-placement swing (vertical);
    /// <see langword="false"/> for a destroy/attack swing (horizontal).
    /// </param>
    public void SetAttack(bool isAttack, bool isBuild)
    {
        _isBuildSwing = isBuild;
        if (isAttack)
        {
            if (_attack == -1)
            {
                _attack = 0;
            }
        }
        else
        {
            _attack = -1;
        }
    }

    /// <summary>
    /// Main per-frame method: rebuilds the hand geometry when the held item or light
    /// level changes, advances all animations, then submits the model to the GPU.
    /// </summary>
    /// <param name="dt">Real-time delta for the current frame in seconds.</param>
    public void DrawWeapon(float dt)
    {
        int lightByte = IsTorch() ? 255 : Math.Clamp((int)(Light() * 256), 0, 255);

        game.platform.BindTexture2d(TerrainTexture);

        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];

        // Normalise block ID — block 151 is remapped to 128 for rendering purposes.
        int curMaterial = item == null ? 0
                        : item.BlockId == 151 ? 128
                        : item.BlockId;

        float curLight = Light();

        if (curMaterial != _cachedMaterial || curLight != _cachedLight || _modelData == null || game.handRedraw)
        {
            RebuildHandModel(lightByte);
            game.platform.UpdateModel(_modelData); // sync rebuilt geometry to GPU
            game.handRedraw = false;
        }

        _cachedMaterial = curMaterial;
        _cachedLight = curLight;

        // Push an isolated model-view matrix for the hand so it always renders in
        // front of the world geometry regardless of camera distance.
        game.platform.GlClearDepthBuffer();
        game.GLMatrixModeModelView();
        game.GLPushMatrix();
        game.GLLoadIdentity();

        // Position and orient the hand in view space.
        game.GLTranslate(
            (_one * 3 / 10) + _bobOffsetZ - _attackOffset * 5,
            -(_one * 15 / 10) + _bobOffsetX - _buildOffset * 10,
            -(_one * 15 / 10) + _restOffsetY);

        game.GLRotate(30 + _restRotateX - _attackOffset * 300, 1, 0, 0);
        game.GLRotate(60 + _restRotateY, 0, 1, 0);
        game.GLScale(_one * 8 / 10, _one * 8 / 10, _one * 8 / 10);

        AdvanceBobAnimation(dt);
        AdvanceSwingAnimation(dt);

        game.platform.GlEnableTexture2d();
        game.platform.BindTexture2d(TerrainTexture);
        game.DrawModelData(_modelData);

        game.GLPopMatrix();
    }

    /// <summary>
    /// Allocates a fresh <see cref="ModelData"/> buffer and populates it with the
    /// geometry for the currently held item.
    /// Called whenever the held item, light level, or a forced-redraw flag changes.
    /// </summary>
    /// <param name="lightByte">
    /// Light intensity in [0, 255] baked into the vertex colours.
    /// </param>
    private void RebuildHandModel(int lightByte)
    {
        _modelData = new ModelData
        {
            indices = new int[128],
            xyz = new float[128],
            uv = new float[128],
            rgba = new byte[128]
        };

        const int x = 0, y = 0, z = 0;

        if (IsEmptyHand() || IsCompass() || IsTorch())
        {
            // All three cases use the torch renderer: the empty-hand and compass use
            // a normal torch shape, and a real torch uses its own texture on the same shape.
            d_BlockRendererTorch.TopTexture = GetWeaponTextureId(TileSide.Top);
            d_BlockRendererTorch.SideTexture = GetWeaponTextureId(TileSide.Front);
            d_BlockRendererTorch.AddTorch(game.d_Data, game, _modelData, x, y, z, TorchType.Normal);
        }
        else
        {
            DrawCube(_modelData, x, y, z, Game.ColorFromArgb(255, lightByte, lightByte, lightByte));
        }
    }

    /// <summary>
    /// Advances the idle bob animation by <paramref name="dt"/> seconds.
    /// The bob runs while the player is moving and decays gracefully once they stop.
    /// Updates <see cref="_bobOffsetX"/> and <see cref="_bobOffsetZ"/>.
    /// </summary>
    /// <param name="dt">Frame delta time in seconds.</param>
    private void AdvanceBobAnimation(float dt)
    {
        bool moved = _prevPlayerX != game.player.position.x
                  || _prevPlayerY != game.player.position.y
                  || _prevPlayerZ != game.player.position.z;

        _prevPlayerX = game.player.position.x;
        _prevPlayerY = game.player.position.y;
        _prevPlayerZ = game.player.position.z;

        if (moved)
        {
            _bobTime += dt;
            _slowdownTimer = _slowdownTimerMax;
        }
        else
        {
            // First frame after stopping: snap the slowdown timer to the remaining
            // half-period so the bob finishes its current half-cycle cleanly.
            if (_slowdownTimer == _slowdownTimerMax)
            {
                _slowdownTimer = _animPeriod / 2 - (_bobTime % (_animPeriod / 2));
            }

            _slowdownTimer -= dt;
            if (_slowdownTimer < 0)
            {
                _bobTime = 0;
            }
            else
            {
                _bobTime += dt;
            }
        }

        _bobOffsetX = BobVertical(_bobTime);
        _bobOffsetZ = BobSide(_bobTime);
    }

    /// <summary>
    /// Advances the attack/build swing animation by <paramref name="dt"/> seconds.
    /// Updates <see cref="_attackOffset"/> and <see cref="_buildOffset"/>,
    /// and resets <see cref="_attack"/> to <c>-1</c> when the swing completes.
    /// </summary>
    /// <param name="dt">Frame delta time in seconds.</param>
    private void AdvanceSwingAnimation(float dt)
    {
        if (_attack == -1)
        {
            return;
        }

        _attack += dt * 7;

        if (_attack > MathF.PI / 2)
        {
            // Swing complete — reset whichever offset was active.
            _attack = -1;
            if (_isBuildSwing) { _buildOffset = 0; }
            else { _attackOffset = 0; }
        }
        else
        {
            if (_isBuildSwing)
            {
                _buildOffset = BobVertical(_attack / 5);
                _attackOffset = 0;
            }
            else
            {
                _attackOffset = BobVertical(_attack / 5);
                _buildOffset = 0;
            }
        }
    }

    /// <summary>
    /// Returns the vertical component of the hand bob at time <paramref name="t"/>.
    /// Uses a sine wave at twice the base speed.
    /// </summary>
    /// <param name="t">Accumulated animation time in seconds.</param>
    private static float BobVertical(float t) => MathF.Sin(t * 2 * BobSpeed) * BobRange;

    /// <summary>
    /// Returns the lateral (side-to-side) component of the hand bob at time <paramref name="t"/>.
    /// Uses a sine wave at the base speed, offset by π so it is out of phase with the vertical bob.
    /// </summary>
    /// <param name="t">Accumulated animation time in seconds.</param>
    private static float BobSide(float t) => MathF.Sin((t + MathF.PI) * BobSpeed) * BobRange;

    /// <summary>
    /// Appends a full axis-aligned unit cube to <paramref name="m"/>, sampling each
    /// face texture from <see cref="GetWeaponTextureId"/>.
    /// </summary>
    /// <param name="m">Model data buffer to append into.</param>
    /// <param name="x">Cube origin X in local model space.</param>
    /// <param name="y">Cube origin Y in local model space.</param>
    /// <param name="z">Cube origin Z in local model space.</param>
    /// <param name="c">Packed ARGB vertex colour applied to every vertex.</param>
    private void DrawCube(ModelData m, int x, int y, int z, int c)
    {
        AddFace(m, x, y, z, c, TileSide.Top, windingCw: false);
        AddFace(m, x, y, z, c, TileSide.Bottom, windingCw: true);
        AddFace(m, x, y, z, c, TileSide.Front, windingCw: false);
        AddFace(m, x, y, z, c, TileSide.Back, windingCw: true);   // TODO: fix texture coords
        AddFace(m, x, y, z, c, TileSide.Left, windingCw: false);
        AddFace(m, x, y, z, c, TileSide.Right, windingCw: true);   // TODO: fix texture coords
    }

    /// <summary>
    /// Appends four vertices and two triangles for one face of the cube.
    /// </summary>
    /// <param name="m">Target model data buffer.</param>
    /// <param name="x">Cube origin X.</param>
    /// <param name="y">Cube origin Y.</param>
    /// <param name="z">Cube origin Z.</param>
    /// <param name="c">Packed ARGB vertex colour.</param>
    /// <param name="side">Which face of the cube to emit.</param>
    /// <param name="windingCw">
    /// <see langword="true"/> to emit indices in clockwise winding (back-facing normals);
    /// <see langword="false"/> for counter-clockwise (front-facing normals).
    /// </param>
    private void AddFace(ModelData m, int x, int y, int z, int c, TileSide side, bool windingCw)
    {
        int tex = GetWeaponTextureId(side);
        RectangleF r = TextureAtlas.TextureCoords2d(tex, TexturesPacked);
        int base_ = m.GetVerticesCount();

        switch (side)
        {
            case TileSide.Top:
                AddVertex(m, x + 0, z + 1, y + 0, r.Left, r.Top, c);
                AddVertex(m, x + 0, z + 1, y + 1, r.Left, r.Bottom, c);
                AddVertex(m, x + 1, z + 1, y + 0, r.Right, r.Top, c);
                AddVertex(m, x + 1, z + 1, y + 1, r.Right, r.Bottom, c);
                break;
            case TileSide.Bottom:
                AddVertex(m, x + 0, z + 0, y + 0, r.Left, r.Top, c);
                AddVertex(m, x + 0, z + 0, y + 1, r.Left, r.Bottom, c);
                AddVertex(m, x + 1, z + 0, y + 0, r.Right, r.Top, c);
                AddVertex(m, x + 1, z + 0, y + 1, r.Right, r.Bottom, c);
                break;
            case TileSide.Front:
                AddVertex(m, x + 0, z + 0, y + 0, r.Left, r.Bottom, c);
                AddVertex(m, x + 0, z + 0, y + 1, r.Right, r.Bottom, c);
                AddVertex(m, x + 0, z + 1, y + 0, r.Left, r.Top, c);
                AddVertex(m, x + 0, z + 1, y + 1, r.Right, r.Top, c);
                break;
            case TileSide.Back:
                AddVertex(m, x + 1, z + 0, y + 0, r.Left, r.Bottom, c);
                AddVertex(m, x + 1, z + 0, y + 1, r.Right, r.Bottom, c);
                AddVertex(m, x + 1, z + 1, y + 0, r.Left, r.Top, c);
                AddVertex(m, x + 1, z + 1, y + 1, r.Right, r.Top, c);
                break;
            case TileSide.Left:
                AddVertex(m, x + 0, z + 0, y + 0, r.Left, r.Bottom, c);
                AddVertex(m, x + 0, z + 1, y + 0, r.Left, r.Top, c);
                AddVertex(m, x + 1, z + 0, y + 0, r.Right, r.Bottom, c);
                AddVertex(m, x + 1, z + 1, y + 0, r.Right, r.Top, c);
                break;
            case TileSide.Right:
                AddVertex(m, x + 0, z + 0, y + 1, r.Left, r.Bottom, c);
                AddVertex(m, x + 0, z + 1, y + 1, r.Left, r.Top, c);
                AddVertex(m, x + 1, z + 0, y + 1, r.Right, r.Bottom, c);
                AddVertex(m, x + 1, z + 1, y + 1, r.Right, r.Top, c);
                break;
        }

        if (!windingCw)
        {
            m.indices[m.indicesCount++] = base_ + 0;
            m.indices[m.indicesCount++] = base_ + 1;
            m.indices[m.indicesCount++] = base_ + 2;
            m.indices[m.indicesCount++] = base_ + 1;
            m.indices[m.indicesCount++] = base_ + 3;
            m.indices[m.indicesCount++] = base_ + 2;
        }
        else
        {
            m.indices[m.indicesCount++] = base_ + 1;
            m.indices[m.indicesCount++] = base_ + 0;
            m.indices[m.indicesCount++] = base_ + 2;
            m.indices[m.indicesCount++] = base_ + 3;
            m.indices[m.indicesCount++] = base_ + 1;
            m.indices[m.indicesCount++] = base_ + 2;
        }
    }

    /// <summary>
    /// Appends a single vertex (position, UV, and colour) to <paramref name="model"/>.
    /// </summary>
    /// <param name="model">Target model data buffer.</param>
    /// <param name="x">Vertex X position in local model space.</param>
    /// <param name="y">Vertex Y position in local model space.</param>
    /// <param name="z">Vertex Z position in local model space.</param>
    /// <param name="u">Horizontal texture coordinate.</param>
    /// <param name="v">Vertical texture coordinate.</param>
    /// <param name="color">Packed ARGB colour applied to the vertex.</param>
    public static void AddVertex(ModelData model, float x, float y, float z, float u, float v, int color)
    {
        int xyzOffset = model.GetXyzCount();
        int uvOffset = model.GetUvCount();
        int rgbaOffset = model.GetRgbaCount();

        model.xyz[xyzOffset] = x;
        model.xyz[xyzOffset + 1] = y;
        model.xyz[xyzOffset + 2] = z;

        model.uv[uvOffset] = u;
        model.uv[uvOffset + 1] = v;

        model.rgba[rgbaOffset] = (byte)Game.ColorR(color);
        model.rgba[rgbaOffset + 1] = (byte)Game.ColorG(color);
        model.rgba[rgbaOffset + 2] = (byte)Game.ColorB(color);
        model.rgba[rgbaOffset + 3] = (byte)Game.ColorA(color);

        model.verticesCount++;
    }
}


/// <summary>
/// Specifies the wall the torch is mounted on.
/// <see cref="Normal"/> means the torch stands upright on a flat surface.
/// The remaining values tilt the torch toward the named face of the block.
/// </summary>
public enum TorchType
{
    /// <summary>Torch stands vertically on a floor or horizontal surface.</summary>
    Normal,

    /// <summary>Torch is mounted on the left wall, leaning rightward.</summary>
    Left,

    /// <summary>Torch is mounted on the right wall, leaning leftward.</summary>
    Right,

    /// <summary>Torch is mounted on the front wall, leaning backward.</summary>
    Front,

    /// <summary>Torch is mounted on the back wall, leaning forward.</summary>
    Back,
}

/// <summary>
/// Builds the six-faced torch geometry and appends it to a <see cref="ModelData"/> buffer.
/// The torch is rendered as a thin rectangular prism whose top cap uses
/// <see cref="TopTexture"/> and whose four sides and bottom use <see cref="SideTexture"/>.
/// Wall-mounted types tilt the prism by offsetting the base corners away from the wall.
/// </summary>
public class BlockRendererTorch
{
    /// <summary>
    /// Cross-section width and depth of the torch shaft in world units (16 % of a block).
    /// </summary>
    private const float TorchSizeXY = 16f / 100f;

    /// <summary>
    /// Height of the torch top cap above the block origin in world units (90 % of a block).
    /// </summary>
    private const float TorchTopZ = 9f / 10f;

    /// <summary>
    /// Vertical drop applied to two of the top-cap corners for wall-mounted torches,
    /// creating the illusion of a tilt (10 % of a block).
    /// </summary>
    private const float TiltDrop = 1f / 10f;

    /// <summary>Fully opaque white, used as the default vertex colour for the torch.</summary>
    private static readonly int White = Game.ColorFromArgb(255, 255, 255, 255);

    /// <summary>Atlas texture index used for the top (flame) cap of the torch.</summary>
    internal int TopTexture;

    /// <summary>Atlas texture index used for the four sides and the bottom of the torch shaft.</summary>
    internal int SideTexture;

    /// <summary>
    /// Appends all six faces of a torch to <paramref name="m"/> at block position
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    /// <param name="d_Data">Game data provider (currently unused but reserved for future use).</param>
    /// <param name="d_TerrainRenderer">Game instance used to query the packed-texture count.</param>
    /// <param name="m">Model data buffer to append geometry into.</param>
    /// <param name="x">Block-grid X origin of the torch.</param>
    /// <param name="y">Block-grid Y origin of the torch.</param>
    /// <param name="z">Block-grid Z (vertical) origin of the torch.</param>
    /// <param name="type">Mount type that determines the tilt direction.</param>
    public void AddTorch(GameData d_Data, Game d_TerrainRenderer, ModelData m,
                         int x, int y, int z, TorchType type)
    {
        // --- Compute top-cap corners ---
        // The top cap is always centred in X/Y regardless of mount type.
        float centreOffset = 0.5f - TorchSizeXY / 2f;
        float topX = centreOffset + x;
        float topY = centreOffset + y;

        // --- Compute bottom-cap corners ---
        // For wall-mounted types the base is shifted to the wall side so the shaft leans.
        float bottomX = centreOffset + x;
        float bottomY = centreOffset + y;

        if (type == TorchType.Front) { bottomX = x - TorchSizeXY; }
        if (type == TorchType.Back) { bottomX = x + 1f; }
        if (type == TorchType.Left) { bottomY = y - TorchSizeXY; }
        if (type == TorchType.Right) { bottomY = y + 1f; }

        // Top-cap quad — four corners at height TorchTopZ.
        // Corner layout (viewed from above, X right, Y into screen):
        //   00 --- 10
        //   |       |
        //   01 --- 11
        Vector3 top00 = new(topX, z + TorchTopZ, topY);
        Vector3 top01 = new(topX, z + TorchTopZ, topY + TorchSizeXY);
        Vector3 top10 = new(topX + TorchSizeXY, z + TorchTopZ, topY);
        Vector3 top11 = new(topX + TorchSizeXY, z + TorchTopZ, topY + TorchSizeXY);

        // Apply tilt to the top cap: two corners drop by TiltDrop on the wall side.
        ApplyTopTilt(type, ref top00, ref top01, ref top10, ref top11);

        // Bottom-cap quad — four corners at height 0.
        Vector3 bottom00 = new(bottomX, z, bottomY);
        Vector3 bottom01 = new(bottomX, z, bottomY + TorchSizeXY);
        Vector3 bottom10 = new(bottomX + TorchSizeXY, z, bottomY);
        Vector3 bottom11 = new(bottomX + TorchSizeXY, z, bottomY + TorchSizeXY);

        // --- Emit faces ---
        AddTopFace(m, top00, top01, top10, top11);
        AddBottomFace(m, bottom00, bottom01, bottom10, bottom11);
        AddFrontFace(m, bottom00, bottom01, top00, top01);
        AddBackFace(m, bottom10, bottom11, top10, top11);
        AddLeftFace(m, bottom00, bottom10, top00, top10);
        AddRightFace(m, bottom01, bottom11, top01, top11);
    }

    /// <summary>
    /// Drops two of the top-cap corners by <see cref="TiltDrop"/> on the wall side
    /// to create the leaning illusion for wall-mounted torch types.
    /// </summary>
    private static void ApplyTopTilt(TorchType type,
        ref Vector3 top00, ref Vector3 top01,
        ref Vector3 top10, ref Vector3 top11)
    {
        switch (type)
        {
            case TorchType.Left:
                top01.Y -= TiltDrop;
                top11.Y -= TiltDrop;
                break;
            case TorchType.Right:
                top00.Y -= TiltDrop;
                top10.Y -= TiltDrop;
                break;
            case TorchType.Front:
                top10.Y -= TiltDrop;
                top11.Y -= TiltDrop;
                break;
            case TorchType.Back:
                top00.Y -= TiltDrop;
                top01.Y -= TiltDrop;
                break;
        }
    }

    /// <summary>Emits the top (flame cap) face using <see cref="TopTexture"/>.</summary>
    private void AddTopFace(ModelData m,
        Vector3 v00, Vector3 v01, Vector3 v10, Vector3 v11)
    {
        RectangleF r = GetTexRect(TopTexture);
        int b = m.GetVerticesCount();
        AddVertex(m, v00, r.Left, r.Top, White);
        AddVertex(m, v01, r.Left, r.Bottom, White);
        AddVertex(m, v10, r.Right, r.Top, White);
        AddVertex(m, v11, r.Right, r.Bottom, White);
        EmitQuadCcw(m, b);
    }

    /// <summary>
    /// Emits the bottom face using <see cref="SideTexture"/>.
    /// Winding is reversed (CW) so the normal points downward.
    /// </summary>
    private void AddBottomFace(ModelData m,
        Vector3 v00, Vector3 v01, Vector3 v10, Vector3 v11)
    {
        RectangleF r = GetTexRect(SideTexture);
        int b = m.GetVerticesCount();
        AddVertex(m, v00, r.Left, r.Top, White);
        AddVertex(m, v01, r.Left, r.Bottom, White);
        AddVertex(m, v10, r.Right, r.Top, White);
        AddVertex(m, v11, r.Right, r.Bottom, White);
        EmitQuadCw(m, b);
    }

    /// <summary>Emits the front side face (−X direction) using <see cref="SideTexture"/>.</summary>
    private void AddFrontFace(ModelData m,
        Vector3 b00, Vector3 b01, Vector3 t00, Vector3 t01)
    {
        RectangleF r = GetTexRect(SideTexture);
        int b = m.GetVerticesCount();
        AddVertex(m, b00, r.Left, r.Bottom, White);
        AddVertex(m, b01, r.Right, r.Bottom, White);
        AddVertex(m, t00, r.Left, r.Top, White);
        AddVertex(m, t01, r.Right, r.Top, White);
        EmitQuadCcw(m, b);
    }

    /// <summary>Emits the back side face (+X direction) using <see cref="SideTexture"/>.</summary>
    private void AddBackFace(ModelData m,
        Vector3 b10, Vector3 b11, Vector3 t10, Vector3 t11)
    {
        RectangleF r = GetTexRect(SideTexture);
        int b = m.GetVerticesCount();
        AddVertex(m, b10, r.Right, r.Bottom, White);
        AddVertex(m, b11, r.Left, r.Bottom, White);
        AddVertex(m, t10, r.Right, r.Top, White);
        AddVertex(m, t11, r.Left, r.Top, White);
        EmitQuadCw(m, b);
    }

    /// <summary>Emits the left side face (−Y direction) using <see cref="SideTexture"/>.</summary>
    private void AddLeftFace(ModelData m,
        Vector3 b00, Vector3 b10, Vector3 t00, Vector3 t10)
    {
        RectangleF r = GetTexRect(SideTexture);
        int b = m.GetVerticesCount();
        AddVertex(m, b00, r.Right, r.Bottom, White);
        AddVertex(m, t00, r.Right, r.Top, White);
        AddVertex(m, b10, r.Left, r.Bottom, White);
        AddVertex(m, t10, r.Left, r.Top, White);
        EmitQuadCcw(m, b);
    }

    /// <summary>Emits the right side face (+Y direction) using <see cref="SideTexture"/>.</summary>
    private void AddRightFace(ModelData m,
        Vector3 b01, Vector3 b11, Vector3 t01, Vector3 t11)
    {
        RectangleF r = GetTexRect(SideTexture);
        int b = m.GetVerticesCount();
        AddVertex(m, b01, r.Left, r.Bottom, White);
        AddVertex(m, t01, r.Left, r.Top, White);
        AddVertex(m, b11, r.Right, r.Bottom, White);
        AddVertex(m, t11, r.Right, r.Top, White);
        EmitQuadCw(m, b);
    }

    /// <summary>
    /// Appends two triangles for a quad in counter-clockwise (front-facing) winding.
    /// Assumes the four vertices starting at <paramref name="b"/> are ordered:
    /// 0 = top-left, 1 = top-right (or bottom-left), 2 = bottom-right (or top-right), 3 = bottom-right.
    /// </summary>
    /// <param name="m">Target model data buffer.</param>
    /// <param name="b">Base vertex index of the quad's first vertex.</param>
    private static void EmitQuadCcw(ModelData m, int b)
    {
        m.indices[m.indicesCount++] = b + 0;
        m.indices[m.indicesCount++] = b + 1;
        m.indices[m.indicesCount++] = b + 2;
        m.indices[m.indicesCount++] = b + 1;
        m.indices[m.indicesCount++] = b + 3;
        m.indices[m.indicesCount++] = b + 2;
    }

    /// <summary>
    /// Appends two triangles for a quad in clockwise (back-facing) winding.
    /// Used for faces whose outward normal points away from the viewer
    /// (e.g. the bottom face of the torch).
    /// </summary>
    /// <param name="m">Target model data buffer.</param>
    /// <param name="b">Base vertex index of the quad's first vertex.</param>
    private static void EmitQuadCw(ModelData m, int b)
    {
        m.indices[m.indicesCount++] = b + 1;
        m.indices[m.indicesCount++] = b + 0;
        m.indices[m.indicesCount++] = b + 2;
        m.indices[m.indicesCount++] = b + 3;
        m.indices[m.indicesCount++] = b + 1;
        m.indices[m.indicesCount++] = b + 2;
    }

    /// <summary>
    /// Returns the normalised UV rectangle for <paramref name="textureIndex"/> in the terrain atlas.
    /// </summary>
    private static RectangleF GetTexRect(int textureIndex)
        => TextureAtlas.TextureCoords2d(textureIndex, Game.TexturesPacked);

    /// <summary>
    /// Convenience overload of <see cref="AddVertex(ModelData,float,float,float,float,float,int)"/>
    /// that accepts a <see cref="Vector3"/> position.
    /// </summary>
    private static void AddVertex(ModelData model, Vector3 pos, float u, float v, int color)
        => AddVertex(model, pos.X, pos.Y, pos.Z, u, v, color);

    /// <summary>
    /// Appends a single vertex (position, UV, and colour) to <paramref name="model"/>.
    /// </summary>
    /// <param name="model">Target model data buffer.</param>
    /// <param name="x">Vertex X position in local model space.</param>
    /// <param name="y">Vertex Y position in local model space.</param>
    /// <param name="z">Vertex Z position in local model space.</param>
    /// <param name="u">Horizontal texture coordinate.</param>
    /// <param name="v">Vertical texture coordinate.</param>
    /// <param name="color">Packed ARGB colour applied to the vertex.</param>
    public static void AddVertex(ModelData model, float x, float y, float z, float u, float v, int color)
    {
        int xyzOffset = model.GetXyzCount();
        int uvOffset = model.GetUvCount();
        int rgbaOffset = model.GetRgbaCount();

        model.xyz[xyzOffset] = x;
        model.xyz[xyzOffset + 1] = y;
        model.xyz[xyzOffset + 2] = z;

        model.uv[uvOffset] = u;
        model.uv[uvOffset + 1] = v;

        model.rgba[rgbaOffset] = (byte)Game.ColorR(color);
        model.rgba[rgbaOffset + 1] = (byte)Game.ColorG(color);
        model.rgba[rgbaOffset + 2] = (byte)Game.ColorB(color);
        model.rgba[rgbaOffset + 3] = (byte)Game.ColorA(color);

        model.verticesCount++;
    }
}