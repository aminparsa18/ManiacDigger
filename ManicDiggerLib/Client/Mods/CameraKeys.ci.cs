using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles keyboard input for player movement and camera control each fixed frame.
/// </summary>
public class ModCameraKeys : ModBase
{
    private const float OverheadCameraSpeed = 3f;

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        if (game.guistate == GuiState.MapLoading) return;

        float dt = args.GetDt();
        bool isNormal = game.guistate == GuiState.Normal;
        bool isTyping = game.GuiTyping != TypingState.None;

        UpdateJumpAndShift(game, isNormal, isTyping);

        game.controls.movedx = 0;
        game.controls.movedy = 0;
        game.controls.moveup = false;
        game.controls.movedown = false;

        if (isNormal)
        {
            if (!isTyping)
            {
                UpdateAutoJump(game);

                if (game.overheadcamera)
                    UpdateOverheadCamera(game, dt);
                else if (game.enable_move)
                    UpdateMovementKeys(game);
            }

            UpdateVerticalFreemove(game, isTyping);
        }
    }

    /// <summary>Updates jump and shift control flags based on keyboard state.</summary>
    private static void UpdateJumpAndShift(Game game, bool isNormal, bool isTyping)
    {
        bool canAct = isNormal && !isTyping;
        game.controls.wantsjump = canAct && game.keyboardState[game.GetKey(Keys.Space)];
        game.controls.wantsjumphalf = false;
        game.controls.shiftkeydown = canAct && game.keyboardState[game.GetKey(Keys.LeftShift)];
    }

    /// <summary>Triggers auto-jump when the player walks into a climbable wall or half-block.</summary>
    private static void UpdateAutoJump(Game game)
    {
        if (game.reachedwall_1blockhigh && (game.AutoJumpEnabled || !game.platform.IsMousePointerLocked()))
            game.controls.wantsjump = true;

        if (game.reachedHalfBlock)
            game.controls.wantsjumphalf = true;
    }

    /// <summary>Handles overhead (RTS-style) camera rotation, angle, and click-to-move.</summary>
    private static void UpdateOverheadCamera(Game game, float dt)
    {
        if (game.keyboardState[game.GetKey(Keys.A)]) game.overheadcameraK.TurnRight(dt * OverheadCameraSpeed);
        if (game.keyboardState[game.GetKey(Keys.D)]) game.overheadcameraK.TurnLeft(dt * OverheadCameraSpeed);

        game.overheadcameraK.Center.X = game.player.position.x;
        game.overheadcameraK.Center.Y = game.player.position.y;
        game.overheadcameraK.Center.Z = game.player.position.z;

        CameraMove m = new()
        {
            Distance = game.overheadcameradistance,
            AngleUp = game.keyboardState[game.GetKey(Keys.W)],
            AngleDown = game.keyboardState[game.GetKey(Keys.S)],
        };
        game.overheadcameraK.Move(m, dt);

        // Click-to-move: steer player toward destination
        float toDest = Vector3.Distance(
            new Vector3(game.player.position.x, game.player.position.y, game.player.position.z),
            new Vector3(game.playerdestination.X + 0.5f, game.playerdestination.Y - 0.5f, game.playerdestination.Z + 0.5f));

        if (toDest >= 1)
        {
            game.controls.movedy += 1;
            if (game.reachedwall) game.controls.wantsjump = true;

            float qX = game.playerdestination.X - game.player.position.x;
            float qZ = game.playerdestination.Z - game.player.position.z;
            game.player.position.roty = MathF.PI / 2 + MathF.Atan2(qX, qZ);
            game.player.position.rotx = MathF.PI;
        }
    }

    /// <summary>Handles WASD movement and leaning animation hints in first/third person.</summary>
    private static void UpdateMovementKeys(Game game)
    {
        if (game.keyboardState[game.GetKey(Keys.W)]) game.controls.movedy += 1;
        if (game.keyboardState[game.GetKey(Keys.S)]) game.controls.movedy -= 1;

        bool leanLeft = game.keyboardState[game.GetKey(Keys.A)];
        bool leanRight = game.keyboardState[game.GetKey(Keys.D)];

        if (leanLeft) { game.controls.movedx -= 1; game.localstance = 1; }
        if (leanRight) { game.controls.movedx += 1; game.localstance = 2; }
        if (!leanLeft && !leanRight) game.localstance = 0;

        game.localplayeranimationhint.LeanLeft = leanLeft;
        game.localplayeranimationhint.LeanRight = leanRight;

        game.controls.movedx += game.touchMoveDx;
        game.controls.movedy += game.touchMoveDy;
    }

    /// <summary>Handles vertical movement in freemove mode or while swimming.</summary>
    private static void UpdateVerticalFreemove(Game game, bool isTyping)
    {
        if (!game.controls.freemove && !game.SwimmingEyes()) return;
        if (isTyping) return;

        if (game.keyboardState[game.GetKey(Keys.Space)]) game.controls.moveup = true;
        if (game.keyboardState[game.GetKey(Keys.LeftControl)]) game.controls.movedown = true;
    }
}