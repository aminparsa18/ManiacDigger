using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Eyes position
    // -------------------------------------------------------------------------

    public float EyesPosX() => player.position.x;
    public float EyesPosY() => player.position.y + GetCharacterEyesHeight();
    public float EyesPosZ() => player.position.z;

    internal float GetCharacterEyesHeight() => entities[LocalPlayerId].drawModel.eyeHeight;
    internal void SetCharacterEyesHeight(float value) => entities[LocalPlayerId].drawModel.eyeHeight = value;

    public int GetPlayerEyesBlockX() => (int)MathF.Floor(player.position.x);
    public int GetPlayerEyesBlockY() => (int)MathF.Floor(player.position.z);
    public int GetPlayerEyesBlockZ() => (int)MathF.Floor(player.position.y + GetCharacterEyesHeight());

    internal int GetPlayerEyesBlock()
    {
        int bx = (int)MathF.Floor(player.position.x);
        int by = (int)MathF.Floor(player.position.z);
        int bz = (int)MathF.Floor(player.position.y + GetCharacterEyesHeight());

        if (!map.IsValidPos(bx, by, bz))
            return player.position.y < WaterLevel() ? -1 : 0;

        return map.GetBlockValid(bx, by, bz);
    }

    // -------------------------------------------------------------------------
    // Swimming state
    // -------------------------------------------------------------------------

    internal bool SwimmingEyes()
    {
        int eyesBlock = GetPlayerEyesBlock();
        if (eyesBlock == -1) return true;
        return d_Data.WalkableType1()[eyesBlock] == Packet_WalkableTypeEnum.Fluid;
    }

    internal bool SwimmingBody()
    {
        int block = map.GetBlock((int)player.position.x, (int)player.position.z, (int)(player.position.y + 1));
        if (block == -1) return true;
        return d_Data.WalkableType1()[block] == Packet_WalkableTypeEnum.Fluid;
    }

    internal bool WaterSwimmingEyes()
    {
        int block = GetPlayerEyesBlock();
        if (block == -1) return true;
        return IsWater(block);
    }

    // -------------------------------------------------------------------------
    // Block under / in hand
    // -------------------------------------------------------------------------

    internal int BlockUnderPlayer()
    {
        if (!map.IsValidPos((int)player.position.x, (int)player.position.z, (int)player.position.y - 1))
            return -1;

        return map.GetBlock((int)player.position.x, (int)player.position.z, (int)player.position.y - 1);
    }

    internal int? BlockInHand()
    {
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        return item != null && item.ItemClass == Packet_ItemClassEnum.Block ? item.BlockId : null;
    }

    internal bool IsWearingWeapon() => d_Inventory.RightHand[ActiveMaterial] != null;

    // -------------------------------------------------------------------------
    // Movement speed
    // -------------------------------------------------------------------------

    internal float MoveSpeedNow()
    {
        float speed = movespeed;

        int blockUnder = BlockUnderPlayer();
        if (blockUnder != -1)
        {
            float floorSpeed = d_Data.WalkSpeed()[blockUnder];
            if (floorSpeed != 0) speed *= floorSpeed;
        }

        if (keyboardState[GetKey(Keys.LeftShift)])
            speed *= one * 2 / 10;

        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
        {
            float itemSpeed = DecodeFixedPoint(blocktypes[item.BlockId].WalkSpeedWhenUsedFloat);
            if (itemSpeed != 0) speed *= itemSpeed;

            if (IronSights)
            {
                float ironSpeed = DecodeFixedPoint(blocktypes[item.BlockId].IronSightsMoveSpeedFloat);
                if (ironSpeed != 0) speed *= ironSpeed;
            }
        }

        return speed;
    }

    // -------------------------------------------------------------------------
    // Weapon / aim
    // -------------------------------------------------------------------------

    internal float CurrentFov()
    {
        if (IronSights)
        {
            Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
            if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
            {
                float ironFov = DecodeFixedPoint(blocktypes[item.BlockId].IronSightsFovFloat);
                if (ironFov != 0) return fov * ironFov;
            }
        }
        return fov;
    }

    internal float CurrentRecoil()
    {
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block) return 0;
        return DecodeFixedPoint(blocktypes[item.BlockId].RecoilFloat);
    }

    internal float CurrentAimRadius()
    {
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block) return 0;

        float radius = IronSights
            ? DecodeFixedPoint(blocktypes[item.BlockId].IronSightsAimRadiusFloat) / 800 * Width()
            : DecodeFixedPoint(blocktypes[item.BlockId].AimRadiusFloat) / 800 * Width();

        return radius + RadiusWhenMoving * radius * Math.Min(playervelocity.Length / movespeed, 1);
    }

    public float WeaponAttackStrength() => rnd.Next(2, 4);

    // -------------------------------------------------------------------------
    // Damage
    // -------------------------------------------------------------------------

    internal void ApplyDamageToPlayer(int damage, int damageSource, int sourceId)
    {
        PlayerStats.CurrentHealth -= damage;
        if (PlayerStats.CurrentHealth <= 0)
        {
            PlayerStats.CurrentHealth = 0;
            PlayAudio("death.wav");
            SendPacketClient(ClientPackets.Death(damageSource, sourceId));
        }
        else
        {
            PlayAudio(rnd.Next() % 2 == 0 ? "grunt1.wav" : "grunt2.wav");
        }
        SendPacketClient(ClientPackets.Health(PlayerStats.CurrentHealth));
    }

    // -------------------------------------------------------------------------
    // Player/entity collision
    // -------------------------------------------------------------------------

    internal bool IsAnyPlayerInPos(int blockposX, int blockposY, int blockposZ)
    {
        for (int i = 0; i < entitiesCount; i++)
        {
            Entity e = entities[i];
            if (e?.drawModel == null) continue;
            if (e.networkPosition == null || e.networkPosition.PositionLoaded)
            {
                if (IsPlayerInPos(e.position.x, e.position.y, e.position.z,
                    blockposX, blockposY, blockposZ, e.drawModel.ModelHeight))
                    return true;
            }
        }
        return IsPlayerInPos(player.position.x, player.position.y, player.position.z,
            blockposX, blockposY, blockposZ, player.drawModel.ModelHeight);
    }

    private bool IsPlayerInPos(float playerposX, float playerposY, float playerposZ,
        int blockposX, int blockposY, int blockposZ, float playerHeight)
    {
        for (int i = 0; i < Math.Floor(playerHeight) + 1; i++)
        {
            if (ScriptCharacterPhysics.BoxPointDistance(
                blockposX, blockposZ, blockposY,
                blockposX + 1, blockposZ + 1, blockposY + 1,
                playerposX, playerposY + i + constWallDistance, playerposZ) < constWallDistance)
                return true;
        }
        return false;
    }
}