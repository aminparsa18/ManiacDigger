using ManicDigger;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Eyes position
    // -------------------------------------------------------------------------

    public float EyesPosX => Player.position.x;
    public float EyesPosY => Player.position.y + GetCharacterEyesHeight();
    public float EyesPosZ => Player.position.z;

    public float GetCharacterEyesHeight() => Entities[LocalPlayerId].drawModel.eyeHeight;
    public void SetCharacterEyesHeight(float value) => Entities[LocalPlayerId].drawModel.eyeHeight = value;

    public int PlayerEyesBlockX => (int)MathF.Floor(EyesPosX);
    public int PlayerEyesBlockY => (int)MathF.Floor(EyesPosZ);
    public int PlayerEyesBlockZ => (int)MathF.Floor(EyesPosY);

    internal int GetPlayerEyesBlock()
    {
        int bx = (int)MathF.Floor(EyesPosX);
        int by = (int)MathF.Floor(EyesPosZ);
        int bz = (int)MathF.Floor(EyesPosY);

        if (!VoxelMap.IsValidPos(bx, by, bz))
            return Player.position.y < WaterLevel() ? -1 : 0;

        return VoxelMap.GetBlockValid(bx, by, bz);
    }

    // -------------------------------------------------------------------------
    // Swimming state
    // -------------------------------------------------------------------------

    public bool SwimmingEyes()
    {
        int eyesBlock = GetPlayerEyesBlock();
        if (eyesBlock == -1) return true;
        return BlockRegistry.WalkableType[eyesBlock] == WalkableType.Fluid;
    }

    public bool SwimmingBody()
    {
        int block = VoxelMap.GetBlock((int)Player.position.x, (int)Player.position.z, (int)(Player.position.y + 1));
        if (block == -1) return true;
        return BlockRegistry.WalkableType[block] == WalkableType.Fluid;
    }

    public bool WaterSwimmingEyes()
    {
        int block = GetPlayerEyesBlock();
        if (block == -1) return true;
        return IsWater(block);
    }

    // -------------------------------------------------------------------------
    // Block under / in hand
    // -------------------------------------------------------------------------

    public int BlockUnderPlayer()
    {
        if (!VoxelMap.IsValidPos((int)Player.position.x, (int)Player.position.z, (int)Player.position.y - 1))
            return -1;

        return VoxelMap.GetBlock((int)Player.position.x, (int)Player.position.z, (int)Player.position.y - 1);
    }

    public int? BlockInHand()
    {
        Packet_Item item = Inventory.RightHand[ActiveMaterial];
        return item != null && item.ItemClass == ItemClass.Block ? item.BlockId : null;
    }

    internal bool IsWearingWeapon() => Inventory.RightHand[ActiveMaterial] != null;

    // -------------------------------------------------------------------------
    // Movement speed
    // -------------------------------------------------------------------------

    public float MoveSpeedNow()
    {
        float speed = MoveSpeed;

        int blockUnder = BlockUnderPlayer();
        if (blockUnder != -1)
        {
            float floorSpeed = BlockRegistry.WalkSpeed[blockUnder];
            if (floorSpeed != 0) speed *= floorSpeed;
        }

        if (KeyboardState[GetKey(Keys.LeftShift)])
        {
            speed *=  2 / 10;
        }

        Packet_Item item = Inventory.RightHand[ActiveMaterial];
        if (item != null && item.ItemClass == ItemClass.Block)
        {
            float itemSpeed = DecodeFixedPoint(BlockTypes[item.BlockId].WalkSpeedWhenUsedFloat);
            if (itemSpeed != 0) speed *= itemSpeed;

            if (IronSights)
            {
                float ironSpeed = DecodeFixedPoint(BlockTypes[item.BlockId].IronSightsMoveSpeedFloat);
                if (ironSpeed != 0) speed *= ironSpeed;
            }
        }

        return speed;
    }

    // -------------------------------------------------------------------------
    // Weapon / aim
    // -------------------------------------------------------------------------

    public float CurrentFov()
    {
        if (IronSights)
        {
            Packet_Item item = Inventory.RightHand[ActiveMaterial];
            if (item != null && item.ItemClass == ItemClass.Block)
            {
                float ironFov = DecodeFixedPoint(BlockTypes[item.BlockId].IronSightsFovFloat);
                if (ironFov != 0) return fov * ironFov;
            }
        }
        return fov;
    }

    public float CurrentRecoil()
    {
        Packet_Item item = Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block) return 0;
        return DecodeFixedPoint(BlockTypes[item.BlockId].RecoilFloat);
    }

    public float CurrentAimRadius()
    {
        Packet_Item item = Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block) return 0;

        float radius = IronSights
            ? DecodeFixedPoint(BlockTypes[item.BlockId].IronSightsAimRadiusFloat) / 800 * Width()
            : DecodeFixedPoint(BlockTypes[item.BlockId].AimRadiusFloat) / 800 * Width();

        return radius + RadiusWhenMoving * radius * Math.Min(playervelocity.Length / MoveSpeed, 1);
    }

    // -------------------------------------------------------------------------
    // Damage
    // -------------------------------------------------------------------------

    public void ApplyDamageToPlayer(int damage, DeathReason damageSource, int sourceId)
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

    public bool IsAnyPlayerInPos(int blockposX, int blockposY, int blockposZ)
    {
        for (int i = 0; i < Entities.Count; i++)
        {
            Entity e = Entities[i];
            if (e?.drawModel == null) continue;
            if (e.networkPosition == null || e.networkPosition.PositionLoaded)
            {
                if (IsPlayerInPos(e.position.x, e.position.y, e.position.z,
                    blockposX, blockposY, blockposZ, e.drawModel.ModelHeight))
                    return true;
            }
        }
        return IsPlayerInPos(Player.position.x, Player.position.y, Player.position.z,
            blockposX, blockposY, blockposZ, Player.drawModel.ModelHeight);
    }

    private bool IsPlayerInPos(float playerposX, float playerposY, float playerposZ,
        int blockposX, int blockposY, int blockposZ, float playerHeight)
    {
        for (int i = 0; i < Math.Floor(playerHeight) + 1; i++)
        {
            if (ScriptCharacterPhysics.BoxPointDistance(
                blockposX, blockposZ, blockposY,
                blockposX + 1, blockposZ + 1, blockposY + 1,
                playerposX, playerposY + i + WallDistance, playerposZ) < WallDistance)
                return true;
        }
        return false;
    }
}