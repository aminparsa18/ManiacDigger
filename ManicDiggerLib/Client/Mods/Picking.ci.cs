using ManicDigger;
using OpenTK.Mathematics;

/// <summary>
/// Handles block and entity picking (ray-casting from the player's view),
/// block placement/destruction, weapon shooting, grenade throwing, and the
/// cuboid fill-area tool.
/// </summary>
public class ModPicking : ModBase
{
    /// <summary>Reusable viewport rectangle passed to <see cref="VectorUtils.UnProject"/>.</summary>
    private readonly int[] _tempViewport;

    /// <summary>
    /// Tracks which world blocks were overwritten by the fill-area tool so they
    /// can be restored if the fill is cancelled.
    /// Keyed by (x, y, z) block position; value is the original block ID.
    /// </summary>
    internal Dictionary<(int x, int y, int z), float> fillarea;

    /// <summary>First corner of the pending cuboid fill selection, or <see langword="null"/> when unset.</summary>
    internal Vector3i? fillstart;

    /// <summary>Second corner of the pending cuboid fill selection, or <see langword="null"/> when unset.</summary>
    internal Vector3i? fillend;

    /// <summary>Timestamp (ms) of the last block build/destroy action, used to enforce <see cref="BuildDelay"/>.</summary>
    internal int lastbuildMilliseconds;

    /// <summary>
    /// <see langword="true"/> when the mouse button was released between clicks,
    /// allowing instant action on the next press.
    /// </summary>
    internal bool fastclicking;

    public ModPicking()
    {
        _tempViewport = new int[4];
        fillarea = new();
    }

    /// <inheritdoc/>
    public override void OnNewFrameReadOnlyMainThread(Game game, float deltaTime)
    {
        if (game.GuiState == GuiState.Normal) { UpdatePicking(game); }
    }

    /// <inheritdoc/>
    public override void OnMouseUp(Game game, MouseEventArgs args)
    {
        if (game.GuiState == GuiState.Normal) { UpdatePicking(game); }
    }

    /// <inheritdoc/>
    public override void OnMouseDown(Game game, MouseEventArgs args)
    {
        if (game.GuiState == GuiState.Normal)
        {
            UpdatePicking(game);
            UpdateEntityHit(game);
        }
    }

    /// <summary>
    /// Main picking entry point. Clears the selected block when the player is
    /// following an entity (spectator), otherwise begins the bullet/block trace.
    /// </summary>
    internal void UpdatePicking(Game game)
    {
        if (game.FollowId() != null)
        {
            game.SelectedBlockPositionX = -1;
            game.SelectedBlockPositionY = -1;
            game.SelectedBlockPositionZ = -1;
            return;
        }
        NextBullet(game, bulletsShot: 0);
    }

    /// <summary>
    /// Performs a single picking trace, handles mouse-button state, weapon logic,
    /// grenade cooking, block interaction, and entity hit detection.
    /// Calls itself recursively for burst-fire weapons.
    /// </summary>
    /// <param name="game">Current game instance.</param>
    /// <param name="bulletsShot">Number of bullets already fired in this burst.</param>
    internal void NextBullet(Game game, int bulletsShot)
    {
        bool left = game.mouseLeft;
        bool middle = game.mouseMiddle;
        bool right = game.mouseRight;
        bool isNextShot = bulletsShot != 0;

        // Latch left-mouse so that held-down is treated as continuous fire.
        if (!game.leftpressedpicking)
        {
            if (game.MouseLeftClick) { game.leftpressedpicking = true; }
            else { left = false; }
        }
        else
        {
            if (game.mouseleftdeclick)
            {
                game.leftpressedpicking = false;
                left = false;
            }
        }
        if (!left) { game.CurrentAttackedBlock = null; }

        Packet_Item item = game.Inventory.RightHand[game.ActiveMaterial];
        bool isPistol = item != null && game.BlockTypes[item.BlockId].IsPistol;
        bool isGrenade = isPistol && game.BlockTypes[item.BlockId].PistolType == PistolType.Grenade;
        bool isPistolShoot = isPistol && left;
        if (isPistol && isGrenade) { isPistolShoot = game.mouseleftdeclick; }

        // Grenade cooking — start timer on left-click, auto-fire when cooked.
        // TODO: fix instant explosion when closing ESC menu.
        if (game.MouseLeftClick)
        {
            game.grenadecookingstartMilliseconds = game.Platform.TimeMillisecondsFromStart;
            if (isPistol && isGrenade && game.BlockTypes[item.BlockId].Sounds.Shoot.Length > 0)
            {
                game.PlayAudio(string.Format("{0}.ogg", game.BlockTypes[item.BlockId].Sounds.Shoot[0]));
            }
        }

        float cookWait = (game.Platform.TimeMillisecondsFromStart - game.grenadecookingstartMilliseconds) / 1000f;

        if (isGrenade && left)
        {
            if (cookWait >= game.grenadetime && game.grenadecookingstartMilliseconds != 0)
            {
                isPistolShoot = true;
                game.mouseleftdeclick = true;
            }
            else { return; }
        }
        else
        {
            game.grenadecookingstartMilliseconds = 0;
        }

        // Iron sights toggle (right-click with pistol, 500 ms cooldown).
        if (isPistol && game.mouserightclick
         && (game.Platform.TimeMillisecondsFromStart - game.lastironsightschangeMilliseconds) >= 500)
        {
            game.IronSights = !game.IronSights;
            game.lastironsightschangeMilliseconds = game.Platform.TimeMillisecondsFromStart;
        }

        Line3D pick = new();
        GetPickingLine(game, pick, isPistolShoot);
        ArraySegment<BlockPosSide> pick2 = game.Pick(game.s, pick, out int pick2count);

        if (left) { game.handSetAttackDestroy = true; }
        else if (right) { game.handSetAttackBuild = true; }

        // Overhead camera: walk toward clicked block.
        if (game.OverheadCamera && pick2count > 0 && left && game.Follow == null)
        {
            game.PlayerDestination = new Vector3(pick2[0].blockPos[0], pick2[0].blockPos[1] + 1, pick2[0].blockPos[2]);
        }

        // Distance check.
        bool pickDistanceOk = pick2count > 0;
        if (pickDistanceOk)
        {
            float pickDist = Vector3.Distance(
                new Vector3(pick2[0].blockPos[0] + 0.5f, pick2[0].blockPos[1] + 0.5f, pick2[0].blockPos[2] + 0.5f),
                new Vector3(pick.Start[0], pick.Start[1], pick.Start[2]));
            if (pickDist > CurrentPickDistance(game)) { pickDistanceOk = false; }
        }

        bool playerTileEmpty = game.IsTileEmptyForPhysics(
            (int)game.Player.position.x, (int)game.Player.position.z, (int)(game.Player.position.y + 0.5f));
        bool playerTileEmptyClose = game.IsTileEmptyForPhysicsClose(
            (int)game.Player.position.x, (int)game.Player.position.z, (int)(game.Player.position.y + 0.5f));

        BlockPosSide pick0 = new();
        if (pick2count > 0 && ((pickDistanceOk && (playerTileEmpty || playerTileEmptyClose)) || game.OverheadCamera))
        {
            game.SelectedBlockPositionX = (int)pick2[0].Current()[0];
            game.SelectedBlockPositionY = (int)pick2[0].Current()[1];
            game.SelectedBlockPositionZ = (int)pick2[0].Current()[2];
            pick0 = pick2[0];
        }
        else
        {
            game.SelectedBlockPositionX = -1;
            game.SelectedBlockPositionY = -1;
            game.SelectedBlockPositionZ = -1;
            pick0.blockPos = Vector3.Zero;
            pick0.blockPos[0] = -1;
            pick0.blockPos[1] = -1;
            pick0.blockPos[2] = -1;
        }

        PickEntity(game, pick, pick2, pick2count);

        if (game.CameraType == CameraType.Fpp || game.CameraType == CameraType.Tpp)
        {
            int ntileX = (int)pick0.Current()[0];
            int ntileY = (int)pick0.Current()[1];
            int ntileZ = (int)pick0.Current()[2];
            if (game.IsUsableBlock(game.VoxelMap.GetBlock(ntileX, ntileZ, ntileY)))
            {
                game.CurrentAttackedBlock = new Vector3i(ntileX, ntileZ, ntileY);
            }
        }

        if (game.GetFreeMouse())
        {
            if (pick2count > 0) { OnPick_(pick0); }
            return;
        }

        bool buildDelayElapsed = (game.Platform.TimeMillisecondsFromStart - lastbuildMilliseconds) / 1000f >= BuildDelay(game);
        if (!buildDelayElapsed && !isNextShot) { PickingEnd(left, right, middle, isPistol); return; }

        if (left && game.Inventory.RightHand[game.ActiveMaterial] == null)
        {
            game.SendPacketClient(ClientPackets.MonsterHit(2 + game.rnd.Next() * 4));
        }

        if ((left || right || middle) && !isGrenade)
        {
            lastbuildMilliseconds = game.Platform.TimeMillisecondsFromStart;
        }
        if (isGrenade && game.mouseleftdeclick)
        {
            lastbuildMilliseconds = game.Platform.TimeMillisecondsFromStart;
        }

        if (game.ReloadStartMilliseconds != 0)
        {
            PickingEnd(left, right, middle, isPistol);
            return;
        }

        if (isPistolShoot)
        {
            if (!(game.LoadedAmmo[item.BlockId] > 0) || !(game.TotalAmmo[item.BlockId] > 0))
            {
                game.PlayAudio("Dry Fire Gun-SoundBible.com-2053652037.ogg");
                PickingEnd(left, right, middle, isPistol);
                return;
            }

            FirePistol(game, pick, pick2, pick2count, item, isGrenade, cookWait, ref bulletsShot);
            PickingEnd(left, right, middle, isPistol);
            return;
        }

        if (isPistol && right) { PickingEnd(left, right, middle, isPistol); return; }

        if (pick2count > 0)
        {
            HandleBlockInteraction(game, pick0, pick2, pick2count, left, right, middle, isPistol, isGrenade);
        }

        PickingEnd(left, right, middle, isPistol);
    }

    /// <summary>
    /// Handles all block interactions: middle-click clone, left-click destroy,
    /// and right-click place.
    /// </summary>
    private void HandleBlockInteraction(Game game, BlockPosSide pick0,
        ArraySegment<BlockPosSide> pick2, int pick2count,
        bool left, bool right, bool middle, bool isPistol, bool isGrenade)
    {
        if (middle)
        {
            HandleMiddleClickClone(game, pick0);
        }

        if (left || right)
        {
            int newtileX = right ? (int)pick0.Translated()[0] : (int)pick0.Current()[0];
            int newtileY = right ? (int)pick0.Translated()[1] : (int)pick0.Current()[1];
            int newtileZ = right ? (int)pick0.Translated()[2] : (int)pick0.Current()[2];

            if (!game.VoxelMap.IsValidPos(newtileX, newtileZ, newtileY)) { return; }

            bool pickIsInvalid = pick0.blockPos[0] == -1 && pick0.blockPos[1] == -1 && pick0.blockPos[2] == -1;
            if (!pickIsInvalid)
            {
                int blocktype = left
                    ? game.VoxelMap.GetBlock(newtileX, newtileZ, newtileY)
                    : (game.BlockInHand() == null ? 1 : game.BlockInHand() ?? -1);

                if (left && blocktype == game.BlockRegistry.BlockIdAdminium)
                {
                    PickingEnd(left, right, middle, isPistol);
                    return;
                }

                string[] sound = left ? game.BlockRegistry.BreakSound[blocktype] : game.BlockRegistry.BuildSound[blocktype];
                if (sound != null) { game.PlayAudio(sound[0]); } // TODO: sound cycle
            }

            if (!right)
            {
                HandleAttack(game, pick0, newtileX, newtileZ, newtileY, left, right, middle, isPistol);
                return;
            }

            if (!game.VoxelMap.IsValidPos(newtileX, newtileZ, newtileY))
            {
                throw new ArgumentException("Error in picking - NextBullet()");
            }
            OnPick(game,
                newtileX, newtileZ, newtileY,
                (int)pick0.Current()[0], (int)pick0.Current()[2], (int)pick0.Current()[1],
                pick0.collisionPos, right);
        }
    }

    /// <summary>
    /// Handles a left-click attack on a block: reduces block health and destroys
    /// it when health reaches zero.
    /// </summary>
    private void HandleAttack(Game game, BlockPosSide tile,
        int newtileX, int newtileY, int newtileZ,
        bool left, bool right, bool middle, bool isPistol)
    {
        int posx = newtileX;
        int posy = newtileY;
        int posz = newtileZ;
        game.CurrentAttackedBlock = new Vector3i(posx, posy, posz);
        var key = (posx, posy, posz);

        if (!game.blockHealth.ContainsKey(key))
        {
            game.blockHealth[key] = game.GetCurrentBlockHealth(posx, posy, posz);
        }

        game.blockHealth[key] -= game.WeaponAttackStrength();

        if (game.GetCurrentBlockHealth(posx, posy, posz) <= 0)
        {
            game.blockHealth.Remove(key);
            game.CurrentAttackedBlock = null;
            OnPick(game,
                newtileX, posy, posz,
                (int)tile.Current()[0], (int)tile.Current()[2], (int)tile.Current()[1],
                tile.collisionPos, right: false);
        }

        PickingEnd(left, right, middle, isPistol);
    }

    /// <summary>
    /// Handles middle-click: finds the pointed-at block type in the hotbar or
    /// inventory and selects or moves it to the active material slot.
    /// </summary>
    private static void HandleMiddleClickClone(Game game, BlockPosSide pick0)
    {
        int newtileX = (int)pick0.Current()[0];
        int newtileY = (int)pick0.Current()[1];
        int newtileZ = (int)pick0.Current()[2];

        if (!game.VoxelMap.IsValidPos(newtileX, newtileZ, newtileY)) { return; }

        int cloneSource = game.VoxelMap.GetBlock(newtileX, newtileZ, newtileY);
        int cloneSource2 = game.BlockRegistry.WhenPlayerPlacesGetsConvertedTo[cloneSource];

        // Search the hotbar first.
        bool found = false;
        for (int i = 0; i < 10; i++)
        {
            if (game.Inventory.RightHand[i]?.ItemClass == ItemClass.Block
             && game.Inventory.RightHand[i].BlockId == cloneSource2)
            {
                game.ActiveMaterial = i;
                found = true;
            }
        }

        if (!found)
        {
            int freeHand = game.InventoryUtil.FreeHand(game.ActiveMaterial) ?? -1;

            for (int i = 0; i < game.Inventory.Items.Length; i++)
            {
                Packet_PositionItem k = game.Inventory.Items[i];
                if (k == null) { continue; }
                if (k.Value_.ItemClass != ItemClass.Block || k.Value_.BlockId != cloneSource2) { continue; }

                if (freeHand != -1)
                {
                    game.WearItem(InventoryPositionMainArea(k.X, k.Y),
                                  InventoryPositionMaterialSelector(freeHand));
                    break;
                }

                if (game.Inventory.RightHand[game.ActiveMaterial]?.ItemClass == ItemClass.Block)
                {
                    game.MoveToInventory(InventoryPositionMaterialSelector(game.ActiveMaterial));
                    game.WearItem(InventoryPositionMainArea(k.X, k.Y),
                                  InventoryPositionMaterialSelector(game.ActiveMaterial));
                }
            }
        }

        string[] sound = game.BlockRegistry.CloneSound[cloneSource];
        if (sound != null) { game.PlayAudio(sound[0]); } // TODO: sound cycle
    }

    /// <summary>
    /// Fires a single pistol bullet or grenade, performs entity hit detection,
    /// spawns bullet/grenade entities, decrements ammo, applies recoil, and
    /// triggers the next shot in a burst if required.
    /// </summary>
    private void FirePistol(Game game, Line3D pick,
        ArraySegment<BlockPosSide> pick2, int pick2count,
        Packet_Item item, bool isGrenade, float cookWait, ref int bulletsShot)
    {
        float toX = pick.End[0], toY = pick.End[1], toZ = pick.End[2];
        if (pick2count > 0) { toX = pick2[0].blockPos[0]; toY = pick2[0].blockPos[1]; toZ = pick2[0].blockPos[2]; }

        Packet_ClientShot shot = new()
        {
            FromX = Game.EncodeFixedPoint(pick.Start[0]),
            FromY = Game.EncodeFixedPoint(pick.Start[1]),
            FromZ = Game.EncodeFixedPoint(pick.Start[2]),
            ToX = Game.EncodeFixedPoint(toX),
            ToY = Game.EncodeFixedPoint(toY),
            ToZ = Game.EncodeFixedPoint(toZ),
            HitPlayer = -1
        };

        CheckEntityHitsForShot(game, pick, pick2, pick2count, isGrenade, ref shot);

        shot.WeaponBlock = item.BlockId;
        game.LoadedAmmo[item.BlockId]--;
        game.TotalAmmo[item.BlockId]--;

        float projectileSpeed = game.DecodeFixedPoint(game.BlockTypes[item.BlockId].ProjectileSpeedFloat);
        if (projectileSpeed == 0)
        {
            game.EntityAddLocal(Game.CreateBulletEntity(pick.Start[0], pick.Start[1], pick.Start[2], toX, toY, toZ, 150));
        }
        else
        {
            SpawnGrenadeEntity(game, pick, item, toX, toY, toZ, projectileSpeed, cookWait, ref shot);
        }

        game.SendPacketClient(new Packet_Client { Id = PacketType.Shot, Shot = shot });

        if (game.BlockTypes[item.BlockId].Sounds.ShootEnd.Length > 0)
        {
            game.pistolcycle = game.rnd.Next() % game.BlockTypes[item.BlockId].Sounds.ShootEnd.Length;
            game.PlayAudio(string.Format("{0}.ogg", game.BlockTypes[item.BlockId].Sounds.ShootEnd[game.pistolcycle]));
        }

        // Apply recoil.
        game.Player.position.rotx -= game.rnd.Next() * game.CurrentRecoil();
        game.Player.position.roty += game.rnd.Next() * game.CurrentRecoil() * 2 - game.CurrentRecoil();

        // Burst fire.
        bulletsShot++;
        if (bulletsShot < game.DecodeFixedPoint(game.BlockTypes[item.BlockId].BulletsPerShotFloat))
        {
            NextBullet(game, bulletsShot);
        }
    }

    /// <summary>
    /// Iterates all entities, checks the picking ray against their head and body
    /// boxes, and records any hit (preventing shots through terrain).
    /// Spawns a blood-splatter sprite for non-grenade hits.
    /// </summary>
    private static void CheckEntityHitsForShot(Game game, Line3D pick,
        ArraySegment<BlockPosSide> pick2, int pick2count,
        bool isGrenade, ref Packet_ClientShot shot)
    {
        float eyeX = game.EyesPosX(), eyeY = game.EyesPosY(), eyeZ = game.EyesPosZ();

        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity entity = game.Entities[i];
            if (entity?.drawModel == null || entity.networkPosition == null) { continue; }
            if (!entity.networkPosition.PositionLoaded) { continue; }

            float fx = entity.position.x, fy = entity.position.y, fz = entity.position.z;
            float headSize = (entity.drawModel.ModelHeight - entity.drawModel.eyeHeight) * 2;
            float bodyH = entity.drawModel.ModelHeight - headSize;
            const float r = 0.35f;

            Box3 bodyBox = new(new Vector3(fx - r, fy, fz - r), new Vector3(fx + r, fy + bodyH, fz + r));
            Box3 headBox = new(new Vector3(fx - r, fy + bodyH, fz - r), new Vector3(fx + r, fy + bodyH + headSize, fz + r));

            Vector3? hit = Intersection.CheckLineBoxExact(pick, headBox);
            bool isHead = hit != null;
            if (hit == null) { hit = Intersection.CheckLineBoxExact(pick, bodyBox); }
            if (hit == null) { continue; }

            // Do not allow shooting through terrain.
            bool blockedByTerrain = pick2count > 0
                && Vector3.Distance(new Vector3(pick2[0].blockPos[0], pick2[0].blockPos[1], pick2[0].blockPos[2]), new Vector3(eyeX, eyeY, eyeZ))
                <= Vector3.Distance(new Vector3(hit.Value.X, hit.Value.Y, hit.Value.Z), new Vector3(eyeX, eyeY, eyeZ));
            if (blockedByTerrain) { continue; }

            if (!isGrenade)
            {
                Entity blood = new()
                {
                    sprite = new Sprite { positionX = hit.Value.X, positionY = hit.Value.Y, positionZ = hit.Value.Z, image = "blood.png" },
                    expires = Expires.Create(0.2f)
                };
                game.EntityAddLocal(blood);
            }

            shot.HitPlayer = i;
            shot.IsHitHead = isHead ? 1 : 0;
        }
    }

    /// <summary>
    /// Creates and spawns a grenade entity with the correct velocity, fuse time,
    /// and sprite, and writes the explosion timer into <paramref name="shot"/>.
    /// </summary>
    private static void SpawnGrenadeEntity(Game game, Line3D pick, Packet_Item item,
        float toX, float toY, float toZ, float projectileSpeed, float cookWait,
        ref Packet_ClientShot shot)
    {
        float vX = toX - pick.Start[0];
        float vY = toY - pick.Start[1];
        float vZ = toZ - pick.Start[2];
        float len = new Vector3(vX, vY, vZ).Length;
        vX = vX / len * projectileSpeed;
        vY = vY / len * projectileSpeed;
        vZ = vZ / len * projectileSpeed;

        float fuseRemaining = game.grenadetime - cookWait;
        shot.ExplodesAfter = Game.EncodeFixedPoint(fuseRemaining);

        Entity grenadeEntity = new()
        {
            sprite = new Sprite
            {
                image = "ChemicalGreen.png",
                size = 14,
                animationcount = 0,
                positionX = pick.Start[0],
                positionY = pick.Start[1],
                positionZ = pick.Start[2]
            },
            grenade = new Grenade
            {
                velocityX = vX,
                velocityY = vY,
                velocityZ = vZ,
                block = item.BlockId,
                sourcePlayer = game.LocalPlayerId
            },
            expires = Expires.Create(fuseRemaining)
        };
        game.EntityAddLocal(grenadeEntity);
    }

    /// <summary>
    /// Resets <see cref="fastclicking"/> and clears <see cref="lastbuildMilliseconds"/>
    /// when no mouse button is held and the held item is not a pistol.
    /// </summary>
    internal void PickingEnd(bool left, bool right, bool middle, bool isPistol)
    {
        fastclicking = false;
        if (!(left || right || middle) && !isPistol)
        {
            lastbuildMilliseconds = 0;
            fastclicking = true;
        }
    }

    /// <summary>
    /// Applies a block-set action at the picked position, taking into account
    /// rail direction snapping, the cuboid fill tool, and the fill-start marker.
    /// </summary>
    internal void OnPick(Game game,
        int blockposX, int blockposY, int blockposZ,
        int blockposOldX, int blockposOldY, int blockposOldZ,
        Vector3 collisionPos, bool right)
    {
        float xFract = collisionPos[0] - MathF.Floor(collisionPos[0]);
        float zFract = collisionPos[2] - MathF.Floor(collisionPos[2]);

        int activeMaterial = game.MaterialSlots(game.ActiveMaterial);
        int railStart = game.BlockRegistry.BlockIdRailStart;

        if (activeMaterial == railStart + RailDirectionFlags.TwoHorizontalVertical
         || activeMaterial == railStart + RailDirectionFlags.Corners)
        {
            RailDirection dirNew = activeMaterial == railStart + RailDirectionFlags.TwoHorizontalVertical
                ? PickHorizontalVertical(xFract, zFract)
                : PickCorners(xFract, zFract);

            int dir = game.BlockRegistry.Rail[game.VoxelMap.GetBlock(blockposOldX, blockposOldY, blockposOldZ)];
            if (dir != 0)
            {
                blockposX = blockposOldX;
                blockposY = blockposOldY;
                blockposZ = blockposOldZ;
            }
            activeMaterial = railStart + (dir | DirectionUtils.ToRailDirectionFlags(dirNew));
        }

        int x = blockposX;
        int y = blockposY;
        int z = blockposZ;
        PacketBlockSetMode mode = right ? PacketBlockSetMode.Create : PacketBlockSetMode.Destroy;

        if (game.IsAnyPlayerInPos(x, y, z) || activeMaterial == 151 /* Compass */) { return; }

        Vector3i v = new(x, y, z);

        if (mode == PacketBlockSetMode.Create)
        {
            if (game.BlockTypes[activeMaterial].IsTool)
            {
                OnPickUseWithTool(game, blockposX, blockposY, blockposZ);
                return;
            }
            if (activeMaterial == game.BlockRegistry.BlockIdCuboid)
            {
                ClearFillArea(game);
                if (fillstart != null)
                {
                    Vector3i f = fillstart.Value;
                    if (!game.IsFillBlock(game.VoxelMap.GetBlock(f.X, f.Y, f.Z)))
                    {
                        fillarea[(f.X, f.Y, f.Z)] = game.VoxelMap.GetBlock(f.X, f.Y, f.Z);
                    }
                    game.SetBlock(f.X, f.Y, f.Z, game.BlockRegistry.BlockIdFillStart);
                    FillFill(game, v, fillstart);
                }
                if (!game.IsFillBlock(game.VoxelMap.GetBlock(v.X, v.Y, v.Z)))
                {
                    fillarea[(v.X, v.Y, v.Z)] = game.VoxelMap.GetBlock(v.X, v.Y, v.Z);
                }
                game.SetBlock(v.X, v.Y, v.Z, game.BlockRegistry.BlockIdCuboid);
                fillend = v;
                game.RedrawBlock(v.X, v.Y, v.Z);
                return;
            }
            if (activeMaterial == game.BlockRegistry.BlockIdFillStart)
            {
                ClearFillArea(game);
                if (!game.IsFillBlock(game.VoxelMap.GetBlock(v.X, v.Y, v.Z)))
                {
                    fillarea[(v.X, v.Y, v.Z)] = game.VoxelMap.GetBlock(v.X, v.Y, v.Z);
                }
                game.SetBlock(v.X, v.Y, v.Z, game.BlockRegistry.BlockIdFillStart);
                fillstart = v;
                fillend = null;
                game.RedrawBlock(v.X, v.Y, v.Z);
                return;
            }
            if (fillarea.ContainsKey((v.X, v.Y, v.Z)))
            {
                game.SendFillArea(fillstart.Value.X, fillstart.Value.Y, fillstart.Value.Z,
                                   fillend.Value.X, fillend.Value.Y, fillend.Value.Z,
                                   activeMaterial);
                ClearFillArea(game);
                fillstart = null;
                fillend = null;
                return;
            }
        }
        else
        {
            if (game.BlockTypes[activeMaterial].IsTool)
            {
                OnPickUseWithTool(game, blockposX, blockposY, blockposOldZ);
                return;
            }
            if (fillstart?.X == v.X && fillstart?.Y == v.Y && fillstart?.Z == v.Z)
            {
                ClearFillArea(game);
                fillstart = null;
                fillend = null;
                return;
            }
            if (fillend?.X == v.X && fillend?.Y == v.Y && fillend?.Z == v.Z)
            {
                ClearFillArea(game);
                fillend = null;
                return;
            }
        }

        game.SendSetBlockAndUpdateSpeculative(activeMaterial, x, y, z, mode);
    }

    /// <summary>
    /// Restores all blocks overwritten by the fill-area tool to their original
    /// values and clears the fill-area dictionary.
    /// </summary>
    internal void ClearFillArea(Game game)
    {
        foreach (var ((x, y, z), value) in fillarea)
        {
            game.SetBlock(x, y, z, (int)value);
            game.RedrawBlock(x, y, z);
        }
        fillarea.Clear();
    }

    /// <summary>
    /// Fills the axis-aligned bounding box between <paramref name="a"/> and
    /// <paramref name="b"/> with fill-area marker blocks, recording each
    /// overwritten block so it can be restored by <see cref="ClearFillArea"/>.
    /// Aborts if the fill would exceed the game's fill-area limit.
    /// </summary>
    internal void FillFill(Game game, Vector3i a, Vector3i? b)
    {
        int startX = Math.Min(a.X, b.Value.X), endX = Math.Max(a.X, b.Value.X);
        int startY = Math.Min(a.Y, b.Value.Y), endY = Math.Max(a.Y, b.Value.Y);
        int startZ = Math.Min(a.Z, b.Value.Z), endZ = Math.Max(a.Z, b.Value.Z);

        for (int x = startX; x <= endX; x++)
            for (int y = startY; y <= endY; y++)
                for (int z = startZ; z <= endZ; z++)
                {
                    if (fillarea.Count > game.FillAreaLimit) { ClearFillArea(game); return; }
                    if (!game.IsFillBlock(game.VoxelMap.GetBlock(x, y, z)))
                    {
                        fillarea[(x, y, z)] = game.VoxelMap.GetBlock(x, y, z);
                        game.SetBlock(x, y, z, game.BlockRegistry.BlockIdFillArea);
                        game.RedrawBlock(x, y, z);
                    }
                }
    }

    /// <summary>Sends a <c>UseWithTool</c> block-set packet for the block at the given position.</summary>
    internal static void OnPickUseWithTool(Game game, int posX, int posY, int posZ)
        => game.SendSetBlock(posX, posY, posZ, PacketBlockSetMode.UseWithTool,
                             game.Inventory.RightHand[game.ActiveMaterial].BlockId,
                             game.ActiveMaterial);

    /// <summary>
    /// Determines the rail direction for a two-state (horizontal/vertical) rail
    /// based on the fractional hit position within the block face.
    /// </summary>
    internal static RailDirection PickHorizontalVertical(float xFract, float yFract)
    {
        if (yFract >= xFract && yFract >= 1 - xFract) { return RailDirection.Vertical; }
        if (yFract < xFract && yFract < 1 - xFract) { return RailDirection.Vertical; }
        return RailDirection.Horizontal;
    }

    /// <summary>
    /// Determines the corner rail direction based on which quadrant of the block
    /// face was hit.
    /// </summary>
    internal static RailDirection PickCorners(float xFract, float zFract)
    {
        if (xFract < 0.5f && zFract < 0.5f) { return RailDirection.UpLeft; }
        if (xFract >= 0.5f && zFract < 0.5f) { return RailDirection.UpRight; }
        if (xFract < 0.5f && zFract >= 0.5f) { return RailDirection.DownLeft; }
        return RailDirection.DownRight;
    }

    /// <summary>
    /// Performs entity-selection ray-casting for the interaction cursor
    /// (distinct from the shooting hit-detection in <see cref="CheckEntityHitsForShot"/>).
    /// Sets <c>game.SelectedEntityId</c> and <c>game.currentlyAttackedEntity</c>.
    /// </summary>
    private static void PickEntity(Game game, Line3D pick,
        ArraySegment<BlockPosSide> pick2, int pick2count)
    {
        game.SelectedEntityId = -1;
        game.CurrentlyAttackedEntity = -1;

        float eyeX = game.EyesPosX(), eyeY = game.EyesPosY(), eyeZ = game.EyesPosZ();

        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity entity = game.Entities[i];
            if (entity?.drawModel == null || i == game.LocalPlayerId) { continue; }
            if (entity.networkPosition == null || !entity.networkPosition.PositionLoaded) { continue; }
            if (!entity.usable) { continue; }

            float fx = entity.position.x, fy = entity.position.y, fz = entity.position.z;
            if (Vector3.Distance(new Vector3(fx, fy, fz), new Vector3(game.Player.position.x, game.Player.position.y, game.Player.position.z)) > 5) { continue; }

            const float r = 0.35f;
            float h = entity.drawModel.ModelHeight;
            Box3 bodyBox = new(new Vector3(fx - r, fy, fz - r), new Vector3(fx + r, fy + h, fz + r));

            Vector3? hit = Intersection.CheckLineBoxExact(pick, bodyBox);
            if (hit == null) { continue; }

            bool blockedByTerrain = pick2count > 0
                && Vector3.Distance(new Vector3(pick2[0].blockPos[0] + 0.5f, pick2[0].blockPos[1] + 0.5f, pick2[0].blockPos[2] + 0.5f), new Vector3(eyeX, eyeY, eyeZ))
                <= Vector3.Distance(new Vector3(hit.Value.X, hit.Value.Y, hit.Value.Z), new Vector3(eyeX, eyeY, eyeZ));
            if (blockedByTerrain) { continue; }

            game.SelectedEntityId = i;
            if (game.CameraType == CameraType.Fpp || game.CameraType == CameraType.Tpp)
            {
                game.CurrentlyAttackedEntity = i;
            }
        }
    }

    /// <summary>
    /// When the player left-clicks and an entity is targeted, notifies all client
    /// mods and sends a hit packet to the server.
    /// </summary>
    private static void UpdateEntityHit(Game game)
    {
        if (game.CurrentlyAttackedEntity == -1 || !game.mouseLeft) { return; }

        for (int i = 0; i < game.clientmods.Count; i++)
        {
            if (game.clientmods[i] == null) { continue; }
            game.clientmods[i].OnHitEntity(game, new OnUseEntityArgs { entityId = game.CurrentlyAttackedEntity });
        }
        game.SendPacketClient(ClientPackets.HitEntity(game.CurrentlyAttackedEntity));
    }

    /// <summary>Placeholder called when the player picks a block in free-mouse mode.</summary>
    internal static void OnPick_(BlockPosSide pick0) { }

    /// <summary>
    /// Returns the minimum seconds between successive block actions for the
    /// currently held item. Derived from the item's <c>DelayFloat</c> field,
    /// or a movement-speed-scaled default when no delay is specified.
    /// </summary>
    internal static float BuildDelay(Game game)
    {
        float defaultDelay = 0.95f / game.Basemovespeed;
        Packet_Item item = game.Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block) { return defaultDelay; }

        float delay = game.DecodeFixedPoint(game.BlockTypes[item.BlockId].DelayFloat);
        return delay == 0 ? defaultDelay : delay;
    }

    /// <summary>
    /// Constructs the picking ray from the camera position through the screen
    /// centre (FPP/TPP) or mouse position (other camera types), optionally
    /// applying weapon spread for pistol shots.
    /// </summary>
    public void GetPickingLine(Game game, Line3D retPick, bool isPistolShoot)
    {
        int mouseX, mouseY;
        if (game.CameraType == CameraType.Fpp || game.CameraType == CameraType.Tpp)
        {
            mouseX = game.Width() / 2;
            mouseY = game.Height() / 2;
        }
        else
        {
            mouseX = game.MouseCurrentX;
            mouseY = game.MouseCurrentY;
        }

        PointF aim = GetAim(game);
        if (isPistolShoot && (aim.X != 0 || aim.Y != 0))
        {
            mouseX += (int)aim.X;
            mouseY += (int)aim.Y;
        }

        _tempViewport[0] = 0;
        _tempViewport[1] = 0;
        _tempViewport[2] = game.Width();
        _tempViewport[3] = game.Height();

        int flippedY = game.Height() - mouseY;
        VectorUtils.UnProject(mouseX, flippedY, 1, game.mvMatrix.Peek(), game.pMatrix.Peek(), _tempViewport, out Vector3 rayEnd);
        VectorUtils.UnProject(mouseX, flippedY, 0, game.mvMatrix.Peek(), game.pMatrix.Peek(), _tempViewport, out Vector3 rayStart);

        float rdX = rayEnd.X - rayStart.X;
        float rdY = rayEnd.Y - rayStart.Y;
        float rdZ = rayEnd.Z - rayStart.Z;
        float len = new Vector3(rdX, rdY, rdZ).Length;
        rdX /= len; rdY /= len; rdZ /= len;

        float pickDist = CurrentPickDistance(game) * (isPistolShoot ? 100 : 1) + 1;

        retPick.Start = new Vector3(rayStart.X, rayStart.Y, rayStart.Z);
        retPick.End = new Vector3(rayStart.X + rdX * pickDist,
                                    rayStart.Y + rdY * pickDist,
                                    rayStart.Z + rdZ * pickDist);
    }

    /// <summary>
    /// Returns a random offset within the weapon's aim circle for spread simulation.
    /// Returns <see cref="PointF.Empty"/> when the aim radius is 1 or less.
    /// </summary>
    internal static PointF GetAim(Game game)
    {
        if (game.CurrentAimRadius() <= 1) { return new PointF(0, 0); }

        float radius = game.CurrentAimRadius();
        float x, y;
        do
        {
            x = (game.rnd.Next() - 0.5f) * radius * 2;
            y = (game.rnd.Next() - 0.5f) * radius * 2;
        }
        while (MathF.Sqrt(x * x + y * y) > radius);

        return new PointF(x, y);
    }

    /// <summary>
    /// Returns the effective pick distance for the current camera type and held item.
    /// Overhead camera uses a fixed or distance-doubled range; TPP adds the camera
    /// offset; FPP uses the item's configured distance or the global default.
    /// </summary>
    private static float CurrentPickDistance(Game game)
    {
        float distance = game.PICK_DISTANCE;
        int? inHand = game.BlockInHand();

        if (inHand.HasValue && game.BlockTypes[inHand.Value].PickDistanceWhenUsedFloat > 0)
        {
            distance = game.DecodeFixedPoint(game.BlockTypes[inHand.Value].PickDistanceWhenUsedFloat);
        }

        if (game.CameraType == CameraType.Tpp)
        {
            distance = game.tppcameradistance + game.PICK_DISTANCE;
        }
        if (game.CameraType == CameraType.Overhead)
        {
            distance = game.Platform.IsFastSystem() ? 100 : game.OverHeadCameraDistance * 2;
        }

        return distance;
    }

    internal static Packet_InventoryPosition InventoryPositionMaterialSelector(int materialId)
    {
        Packet_InventoryPosition pos = new()
        {
            Type = PacketInventoryPositionType.MaterialSelector,
            MaterialId = materialId
        };
        return pos;
    }

    internal static Packet_InventoryPosition InventoryPositionMainArea(int x, int y)
    {
        Packet_InventoryPosition pos = new()
        {
            Type = PacketInventoryPositionType.MainArea,
            AreaX = x,
            AreaY = y
        };
        return pos;
    }

}