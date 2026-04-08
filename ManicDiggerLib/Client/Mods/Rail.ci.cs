using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles minecart rail riding: entering and exiting a minecart, movement along
/// rail tracks (including slopes and corners), speed control, direction reversal,
/// and rail sound effects.
/// </summary>
public class ModRail : ModBase
{
    /// <summary>
    /// Floating-point literal <c>1.0f</c> stored in a field to prevent the Cito
    /// transpiler from truncating integer-division expressions to integer arithmetic.
    /// </summary>
    private readonly float _one;

    /// <summary>
    /// Height above the rail block at which the player sits inside the minecart
    /// (0.5 blocks by default, adjusted by <see cref="MinecartHeight"/>).
    /// </summary>
    private readonly float _railHeight;

    /// <summary>The local minecart entity added to the world when rail riding begins.</summary>
    internal Entity localMinecart;

    /// <summary>Rail map utility used to query slope and direction data.</summary>
    internal RailMapUtil d_RailMapUtil;

    /// <summary>Player eye height saved when entering a minecart, restored on exit.</summary>
    internal float originalmodelheight;

    /// <summary><see langword="true"/> while the player is riding a rail.</summary>
    internal bool railriding;

    /// <summary>Current forward speed of the minecart in blocks per second.</summary>
    internal float currentvehiclespeed;

    /// <summary>Chunk-grid X coordinate of the rail block the minecart currently occupies.</summary>
    internal int currentrailblockX;

    /// <summary>Chunk-grid Y coordinate of the rail block the minecart currently occupies.</summary>
    internal int currentrailblockY;

    /// <summary>Chunk-grid Z coordinate of the rail block the minecart currently occupies.</summary>
    internal int currentrailblockZ;

    /// <summary>
    /// Progress through the current rail block in the range [0, 1).
    /// Incremented each frame by <c>speed × dt</c>.
    /// </summary>
    internal float currentrailblockprogress;

    /// <summary>The direction in which the minecart is currently travelling.</summary>
    internal VehicleDirection12 currentdirection;

    /// <summary>The direction the minecart was travelling in the previous block.</summary>
    internal VehicleDirection12 lastdirection;

    /// <summary>Whether Q was held on the previous frame (used for edge-trigger detection).</summary>
    internal bool wasqpressed;

    /// <summary>Whether E was held on the previous frame (used for edge-trigger detection).</summary>
    internal bool wasepressed;

    /// <summary>Timestamp (ms) of the last clack rail sound effect.</summary>
    private int _lastRailSoundTimeMs;

    /// <summary>Index (0–3) of the last rail clack sound played, cycled round-robin.</summary>
    private int _lastRailSoundIndex;

    /// <summary>Returns the height of the minecart seat above the rail block origin.</summary>
    internal float MinecartHeight() => _one / 2;

    public ModRail()
    {
        _one = 1;
        _railHeight = _one * 3 / 10;
    }

    /// <inheritdoc/>
    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        d_RailMapUtil ??= new RailMapUtil { game = game };
        RailOnNewFrame(game, args.GetDt());
    }

    /// <summary>
    /// Main per-fixed-frame update: syncs the minecart entity, handles player
    /// input, advances rail progress, and manages enter/exit logic.
    /// </summary>
    internal void RailOnNewFrame(Game game, float dt)
    {
        EnsureMinecartEntity(game);
        SyncMinecartEntity();

        game.localplayeranimationhint.InVehicle = railriding;
        game.localplayeranimationhint.DrawFixX = 0;
        game.localplayeranimationhint.DrawFixY = railriding ? -(_one * 7 / 10) : 0;
        game.localplayeranimationhint.DrawFixZ = 0;

        bool turnRight = game.keyboardState[game.GetKey(Keys.D)];
        bool turnLeft = game.keyboardState[game.GetKey(Keys.A)];

        RailSound(game);

        if (railriding)
        {
            AdvanceRailRiding(game, dt, turnLeft, turnRight);
        }

        HandleSpeedInput(game, dt);
        HandleReverseInput(game);
        HandleEnterExitInput(game, turnLeft, turnRight);

        wasqpressed = game.keyboardState[game.GetKey(Keys.Q)] && game.GuiTyping != TypingState.Typing;
        wasepressed = game.keyboardState[game.GetKey(Keys.E)] && game.GuiTyping != TypingState.Typing;
    }

    /// <summary>
    /// Creates the local minecart entity on the first frame if it does not yet exist.
    /// </summary>
    private void EnsureMinecartEntity(Game game)
    {
        if (localMinecart != null) { return; }
        localMinecart = new Entity { minecart = new Minecart() };
        game.EntityAddLocal(localMinecart);
    }

    /// <summary>Copies current rail state onto the minecart entity for rendering.</summary>
    private void SyncMinecartEntity()
    {
        localMinecart.minecart.enabled = railriding;
        if (!railriding) { return; }

        Minecart m = localMinecart.minecart;
        m.positionX = localMinecart.position?.x ?? 0;
        m.positionY = localMinecart.position?.y ?? 0;
        m.positionZ = localMinecart.position?.z ?? 0;
        m.direction = currentdirection;
        m.lastdirection = lastdirection;
        m.progress = currentrailblockprogress;
    }

    /// <summary>
    /// Moves the player along the rail, advances block progress, and transitions
    /// to the next block when progress reaches 1.
    /// </summary>
    private void AdvanceRailRiding(Game game, float dt, bool turnLeft, bool turnRight)
    {
        game.controls.freemove = true;
        game.enable_move = false;

        Vector3 railPos = CurrentRailPos(game);
        game.player.position.x = railPos.X;
        game.player.position.y = railPos.Y;
        game.player.position.z = railPos.Z;

        currentrailblockprogress += currentvehiclespeed * dt;

        if (currentrailblockprogress >= 1)
        {
            AdvanceToNextBlock(game, turnLeft, turnRight);
        }
    }

    /// <summary>
    /// Moves the minecart into the next rail block, resolving slopes and turn
    /// direction. Reverses direction if no valid next block exists.
    /// </summary>
    private void AdvanceToNextBlock(Game game, bool turnLeft, bool turnRight)
    {
        lastdirection = currentdirection;
        currentrailblockprogress = 0;

        TileEnterData newenter = new();
        Vector3i? nexttile = NextTile(currentdirection, currentrailblockX, currentrailblockY, currentrailblockZ);
        newenter.BlockPositionX = nexttile.Value.X;
        newenter.BlockPositionY = nexttile.Value.Y;
        newenter.BlockPositionZ = nexttile.Value.Z;

        TileEnterDirection enterDir = DirectionUtils.ResultEnter(DirectionUtils.ResultExit(currentdirection));

        // Slope: ascend if the current block ramps up in the exit direction.
        if (GetUpDownMove(game, currentrailblockX, currentrailblockY, currentrailblockZ, enterDir) == UpDown.Up)
        {
            newenter.BlockPositionZ++;
        }
        // Slope: descend if the next block ramps down in the enter direction.
        if (GetUpDownMove(game, newenter.BlockPositionX, newenter.BlockPositionY, newenter.BlockPositionZ - 1, enterDir) == UpDown.Down)
        {
            newenter.BlockPositionZ--;
        }

        newenter.EnterDirection = enterDir;

        VehicleDirection12 newDir = BestNewDirection(PossibleRails(game, newenter), turnLeft, turnRight, out bool found);
        if (!found)
        {
            currentdirection = DirectionUtils.Reverse(currentdirection);
        }
        else
        {
            currentdirection = newDir;
            currentrailblockX = newenter.BlockPositionX;
            currentrailblockY = newenter.BlockPositionY;
            currentrailblockZ = newenter.BlockPositionZ;
        }
    }

    /// <summary>
    /// Reads W/S keys to accelerate or brake the minecart.
    /// Speed is clamped to a minimum of zero.
    /// </summary>
    private void HandleSpeedInput(Game game, float dt)
    {
        if (game.GuiTyping == TypingState.Typing) { return; }

        if (game.keyboardState[game.GetKey(Keys.W)]) { currentvehiclespeed += 1 * dt; }
        if (game.keyboardState[game.GetKey(Keys.S)]) { currentvehiclespeed -= 5 * dt; }
        if (currentvehiclespeed < 0) { currentvehiclespeed = 0; }
    }

    /// <summary>
    /// Edge-triggers the Q key to reverse the minecart's direction while riding.
    /// </summary>
    private void HandleReverseInput(Game game)
    {
        bool qPressed = game.keyboardState[game.GetKey(Keys.Q)] && game.GuiTyping != TypingState.Typing;
        if (!wasqpressed && qPressed) { Reverse(); }
    }

    /// <summary>
    /// Edge-triggers the E key to enter an idle minecart or exit the current one.
    /// </summary>
    private void HandleEnterExitInput(Game game, bool turnLeft, bool turnRight)
    {
        bool ePressed = game.keyboardState[game.GetKey(Keys.E)] && game.GuiTyping != TypingState.Typing;
        if (wasepressed || !ePressed) { return; }

        if (!railriding && !game.controls.freemove)
        {
            TryEnterMinecart(game);
        }
        else if (railriding)
        {
            ExitVehicle(game);
            game.player.position.y += _one * 7 / 10;
        }
    }

    /// <summary>
    /// Attempts to mount the minecart on the rail block directly below the player.
    /// Sets the initial direction based on the rail type at that position.
    /// Exits immediately if no rail is found or the position is invalid.
    /// </summary>
    private void TryEnterMinecart(Game game)
    {
        currentrailblockX = (int)game.player.position.x;
        currentrailblockY = (int)game.player.position.z;
        currentrailblockZ = (int)game.player.position.y - 1;

        if (!game.map.IsValidPos(currentrailblockX, currentrailblockY, currentrailblockZ))
        {
            ExitVehicle(game);
            return;
        }

        int railUnder = game.d_Data.Rail()[game.map.GetBlock(currentrailblockX, currentrailblockY, currentrailblockZ)];

        railriding = true;
        originalmodelheight = game.GetCharacterEyesHeight();
        game.SetCharacterEyesHeight(MinecartHeight());
        currentvehiclespeed = 0;

        if ((railUnder & RailDirectionFlags.Horizontal) != 0) { currentdirection = VehicleDirection12.HorizontalRight; }
        else if ((railUnder & RailDirectionFlags.Vertical) != 0) { currentdirection = VehicleDirection12.VerticalUp; }
        else if ((railUnder & RailDirectionFlags.UpLeft) != 0) { currentdirection = VehicleDirection12.UpLeftUp; }
        else if ((railUnder & RailDirectionFlags.UpRight) != 0) { currentdirection = VehicleDirection12.UpRightUp; }
        else if ((railUnder & RailDirectionFlags.DownLeft) != 0) { currentdirection = VehicleDirection12.DownLeftLeft; }
        else if ((railUnder & RailDirectionFlags.DownRight) != 0) { currentdirection = VehicleDirection12.DownRightRight; }
        else
        {
            ExitVehicle(game);
            return;
        }

        lastdirection = currentdirection;
    }

    /// <summary>
    /// Reverses the minecart's travel direction and mirrors progress within the
    /// current block so the cart appears to turn around smoothly.
    /// </summary>
    internal void Reverse()
    {
        currentdirection = DirectionUtils.Reverse(currentdirection);
        currentrailblockprogress = 1 - currentrailblockprogress;
        lastdirection = currentdirection;
    }

    /// <summary>
    /// Exits the minecart: restores the player's original eye height, re-enables
    /// standard movement, and clears the rail-riding flag.
    /// </summary>
    internal void ExitVehicle(Game game)
    {
        game.SetCharacterEyesHeight(originalmodelheight);
        railriding = false;
        game.controls.freemove = false;
        game.enable_move = true;
    }

    /// <summary>
    /// Computes the world-space position the player should occupy given the current
    /// rail block, direction, progress, and slope.
    /// The result is offset upward by <see cref="_railHeight"/> + 1 so the player
    /// sits above the rail block without clipping into it.
    /// </summary>
    internal Vector3 CurrentRailPos(Game game)
    {
        RailSlope slope = d_RailMapUtil.GetRailSlope(currentrailblockX, currentrailblockY, currentrailblockZ);
        float aX = currentrailblockX;
        float aY = currentrailblockY;
        float aZ = currentrailblockZ;
        float half = _one / 2;
        float p = currentrailblockprogress;
        float xc = 0, yc = 0, zc = 0;

        switch (currentdirection)
        {
            case VehicleDirection12.HorizontalRight:
                xc += p; yc += half;
                if (slope == RailSlope.TwoRightRaised) { zc += p; }
                if (slope == RailSlope.TwoLeftRaised) { zc += 1 - p; }
                break;
            case VehicleDirection12.HorizontalLeft:
                xc += 1 - p; yc += half;
                if (slope == RailSlope.TwoRightRaised) { zc += 1 - p; }
                if (slope == RailSlope.TwoLeftRaised) { zc += p; }
                break;
            case VehicleDirection12.VerticalDown:
                xc += half; yc += p;
                if (slope == RailSlope.TwoDownRaised) { zc += p; }
                if (slope == RailSlope.TwoUpRaised) { zc += 1 - p; }
                break;
            case VehicleDirection12.VerticalUp:
                xc += half; yc += 1 - p;
                if (slope == RailSlope.TwoDownRaised) { zc += 1 - p; }
                if (slope == RailSlope.TwoUpRaised) { zc += p; }
                break;
            case VehicleDirection12.UpLeftLeft: xc += half * (1 - p); yc += half * p; break;
            case VehicleDirection12.UpLeftUp: xc += half * p; yc += half - half * p; break;
            case VehicleDirection12.UpRightRight: xc += half + half * p; yc += half * p; break;
            case VehicleDirection12.UpRightUp: xc += 1 - half * p; yc += half - half * p; break;
            case VehicleDirection12.DownLeftLeft: xc += half * (1 - p); yc += 1 - half * p; break;
            case VehicleDirection12.DownLeftDown: xc += half * p; yc += half + half * p; break;
            case VehicleDirection12.DownRightRight: xc += half + half * p; yc += 1 - half * p; break;
            case VehicleDirection12.DownRightDown: xc += 1 - half * p; yc += half + half * p; break;
        }

        // +1 so the player sits above the rail block and picking still works.
        return new Vector3(aX + xc, aZ + _railHeight + 1 + zc, aY + yc);
    }

    /// <summary>
    /// Returns whether the minecart will ascend, descend, or travel flat when
    /// entering the given rail block from the specified direction.
    /// </summary>
    /// <returns>One of <see cref="UpDown.Up"/>, <see cref="UpDown.Down"/>, or <see cref="UpDown.None"/>.</returns>
    internal int GetUpDownMove(Game game, int railblockX, int railblockY, int railblockZ, TileEnterDirection dir)
    {
        if (!game.map.IsValidPos(railblockX, railblockY, railblockZ)) { return UpDown.None; }

        RailSlope slope = d_RailMapUtil.GetRailSlope(railblockX, railblockY, railblockZ);

        if (slope == RailSlope.TwoDownRaised && dir == TileEnterDirection.Up) { return UpDown.Up; }
        if (slope == RailSlope.TwoUpRaised && dir == TileEnterDirection.Down) { return UpDown.Up; }
        if (slope == RailSlope.TwoLeftRaised && dir == TileEnterDirection.Right) { return UpDown.Up; }
        if (slope == RailSlope.TwoRightRaised && dir == TileEnterDirection.Left) { return UpDown.Up; }

        if (slope == RailSlope.TwoDownRaised && dir == TileEnterDirection.Down) { return UpDown.Down; }
        if (slope == RailSlope.TwoUpRaised && dir == TileEnterDirection.Up) { return UpDown.Down; }
        if (slope == RailSlope.TwoLeftRaised && dir == TileEnterDirection.Left) { return UpDown.Down; }
        if (slope == RailSlope.TwoRightRaised && dir == TileEnterDirection.Right) { return UpDown.Down; }

        return UpDown.None;
    }

    /// <summary>
    /// Plays looping rail noise and periodic clack sounds scaled to the current speed.
    /// </summary>
    internal void RailSound(Game game)
    {
        float soundRate = Math.Min(currentvehiclespeed, 10f);
        game.AudioPlayLoop("railnoise.wav", railriding && soundRate > _one * 1 / 10, false);

        if (!railriding || soundRate <= 0) { return; }

        if ((game.platform.TimeMillisecondsFromStart() - _lastRailSoundTimeMs) > 1000 / soundRate)
        {
            _lastRailSoundIndex = (_lastRailSoundIndex + 1) % 4;
            game.PlayAudio(string.Format("rail{0}.wav", _lastRailSoundIndex.ToString()));
            _lastRailSoundTimeMs = game.platform.TimeMillisecondsFromStart();
        }
    }

    /// <summary>
    /// Returns the world-block coordinates of the next rail block in the given
    /// <paramref name="direction"/> from the current block.
    /// </summary>
    public static Vector3i? NextTile(VehicleDirection12 direction, int currentTileX, int currentTileY, int currentTileZ)
        => NextTileByExit(DirectionUtils.ResultExit(direction), currentTileX, currentTileY, currentTileZ);

    /// <summary>
    /// Returns the neighbour block in the given exit direction.
    /// Returns <see langword="null"/> for unrecognised directions.
    /// </summary>
    public static Vector3i? NextTileByExit(TileExitDirection direction, int x, int y, int z)
        => direction switch
        {
            TileExitDirection.Left => new Vector3i(x - 1, y, z),
            TileExitDirection.Right => new Vector3i(x + 1, y, z),
            TileExitDirection.Up => new Vector3i(x, y - 1, z),
            TileExitDirection.Down => new Vector3i(x, y + 1, z),
            _ => null,
        };

    /// <summary>
    /// Returns a bitmask of the <see cref="VehicleDirection12"/> values that are
    /// valid exits from the block described by <paramref name="enter"/>.
    /// </summary>
    internal static int PossibleRails(Game game, TileEnterData enter)
    {
        if (!game.map.IsValidPos(enter.BlockPositionX, enter.BlockPositionY, enter.BlockPositionZ))
        {
            return 0;
        }

        int railFlags = game.d_Data.Rail()[
            game.map.GetBlock(enter.BlockPositionX, enter.BlockPositionY, enter.BlockPositionZ)];

        VehicleDirection12[] candidates = new VehicleDirection12[3];
        int candidateCount = 0;
        VehicleDirection12[] possible3 = DirectionUtils.PossibleNewRails3(enter.EnterDirection);

        for (int i = 0; i < 3; i++)
        {
            VehicleDirection12 dir = possible3[i];
            if ((railFlags & DirectionUtils.ToRailDirectionFlags(DirectionUtils.ToRailDirection(dir))) != 0)
            {
                candidates[candidateCount++] = dir;
            }
        }

        return DirectionUtils.ToVehicleDirection12Flags_(candidates, candidateCount);
    }

    /// <summary>
    /// Selects the best exit direction from a bitmask of possible directions,
    /// honouring the player's turn input if it matches a corner.
    /// Straight rails take priority over corners when no turn input is given.
    /// </summary>
    /// <param name="dirFlags">Bitmask of available <see cref="VehicleDirection12"/> exits.</param>
    /// <param name="turnLeft"><see langword="true"/> when the player is steering left.</param>
    /// <param name="turnRight"><see langword="true"/> when the player is steering right.</param>
    /// <param name="retFound"><see langword="false"/> when no direction is available (end of rail).</param>
    internal static VehicleDirection12 BestNewDirection(int dirFlags, bool turnLeft, bool turnRight, out bool retFound)
    {
        retFound = true;

        if (turnRight)
        {
            if ((dirFlags & VehicleDirection12Flags.DownRightRight) != 0) { return VehicleDirection12.DownRightRight; }
            if ((dirFlags & VehicleDirection12Flags.UpRightUp) != 0) { return VehicleDirection12.UpRightUp; }
            if ((dirFlags & VehicleDirection12Flags.UpLeftLeft) != 0) { return VehicleDirection12.UpLeftLeft; }
            if ((dirFlags & VehicleDirection12Flags.DownLeftDown) != 0) { return VehicleDirection12.DownLeftDown; }
        }

        if (turnLeft)
        {
            if ((dirFlags & VehicleDirection12Flags.DownRightDown) != 0) { return VehicleDirection12.DownRightDown; }
            if ((dirFlags & VehicleDirection12Flags.UpRightRight) != 0) { return VehicleDirection12.UpRightRight; }
            if ((dirFlags & VehicleDirection12Flags.UpLeftUp) != 0) { return VehicleDirection12.UpLeftUp; }
            if ((dirFlags & VehicleDirection12Flags.DownLeftLeft) != 0) { return VehicleDirection12.DownLeftLeft; }
        }

        // Straight directions take priority.
        if ((dirFlags & VehicleDirection12Flags.VerticalDown) != 0) { return VehicleDirection12.VerticalDown; }
        if ((dirFlags & VehicleDirection12Flags.VerticalUp) != 0) { return VehicleDirection12.VerticalUp; }
        if ((dirFlags & VehicleDirection12Flags.HorizontalLeft) != 0) { return VehicleDirection12.HorizontalLeft; }
        if ((dirFlags & VehicleDirection12Flags.HorizontalRight) != 0) { return VehicleDirection12.HorizontalRight; }

        if ((dirFlags & VehicleDirection12Flags.DownLeftDown) != 0) { return VehicleDirection12.DownLeftDown; }
        if ((dirFlags & VehicleDirection12Flags.DownLeftLeft) != 0) { return VehicleDirection12.DownLeftLeft; }
        if ((dirFlags & VehicleDirection12Flags.DownRightDown) != 0) { return VehicleDirection12.DownRightDown; }
        if ((dirFlags & VehicleDirection12Flags.DownRightRight) != 0) { return VehicleDirection12.DownRightRight; }
        if ((dirFlags & VehicleDirection12Flags.UpLeftLeft) != 0) { return VehicleDirection12.UpLeftLeft; }
        if ((dirFlags & VehicleDirection12Flags.UpLeftUp) != 0) { return VehicleDirection12.UpLeftUp; }
        if ((dirFlags & VehicleDirection12Flags.UpRightRight) != 0) { return VehicleDirection12.UpRightRight; }
        if ((dirFlags & VehicleDirection12Flags.UpRightUp) != 0) { return VehicleDirection12.UpRightUp; }

        retFound = false;
        return VehicleDirection12.DownLeftDown; // sentinel — caller checks retFound
    }
}