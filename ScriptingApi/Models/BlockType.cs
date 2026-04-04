using ProtoBuf;

namespace ManicDigger;

[ProtoContract]
public class BlockType
{
    public BlockType() { }
    [ProtoMember(1)]
    public string TextureIdTop = "Unknown";
    [ProtoMember(2)]
    public string TextureIdBottom = "Unknown";
    [ProtoMember(3)]
    public string TextureIdFront = "Unknown";
    [ProtoMember(4)]
    public string TextureIdBack = "Unknown";
    [ProtoMember(5)]
    public string TextureIdLeft = "Unknown";
    [ProtoMember(6)]
    public string TextureIdRight = "Unknown";
    [ProtoMember(7)]
    public string TextureIdForInventory = "Unknown";
    [ProtoMember(8)]
    public DrawType DrawType;
    [ProtoMember(9)]
    public WalkableType WalkableType;
    [ProtoMember(10)]
    public int Rail;
    [ProtoMember(11)]
    public float WalkSpeed = 1;
    [ProtoMember(12)]
    public bool IsSlipperyWalk;
    [ProtoMember(13)]
    public SoundSet Sounds;
    [ProtoMember(14)]
    public int LightRadius;
    [ProtoMember(15)]
    public int StartInventoryAmount;
    [ProtoMember(16)]
    public int Strength;
    [ProtoMember(17)]
    public string Name;
    [ProtoMember(18)]
    public bool IsBuildable;
    [ProtoMember(19)]
    public bool IsUsable;
    [ProtoMember(20)]
    public bool IsTool;
    [ProtoMember(21)]
    public string handimage;
    [ProtoMember(22)]
    public bool IsPistol;
    [ProtoMember(23)]
    public int AimRadius;
    [ProtoMember(24)]
    public float Recoil;
    [ProtoMember(25)]
    public float Delay;
    [ProtoMember(26)]
    public float BulletsPerShot;
    [ProtoMember(27)]
    public float WalkSpeedWhenUsed = 1;
    [ProtoMember(28)]
    public bool IronSightsEnabled;
    [ProtoMember(29)]
    public float IronSightsMoveSpeed = 1;
    [ProtoMember(30)]
    public string IronSightsImage;
    [ProtoMember(31)]
    public float IronSightsAimRadius;
    [ProtoMember(32)]
    public float IronSightsFov;
    [ProtoMember(33)]
    public int AmmoMagazine;
    [ProtoMember(34)]
    public int AmmoTotal;
    [ProtoMember(35)]
    public float ReloadDelay;
    [ProtoMember(36)]
    public float ExplosionRange;
    [ProtoMember(37)]
    public float ExplosionTime;
    [ProtoMember(38)]
    public float ProjectileSpeed; // 0 is infinite
    [ProtoMember(39)]
    public bool ProjectileBounce;
    [ProtoMember(40)]
    public float DamageBody;
    [ProtoMember(41)]
    public float DamageHead;
    [ProtoMember(42)]
    public PistolType PistolType;
    [ProtoMember(43)]
    public int DamageToPlayer = 0;
    [ProtoMember(44)]
    public int WhenPlayerPlacesGetsConvertedTo;
    [ProtoMember(45)]
    public float PickDistanceWhenUsed;

    public string AllTextures
    {
        set
        {
            TextureIdTop = value;
            TextureIdBottom = value;
            TextureIdFront = value;
            TextureIdBack = value;
            TextureIdLeft = value;
            TextureIdRight = value;
            TextureIdForInventory = value;
        }
    }

    public string SideTextures
    {
        set
        {
            TextureIdFront = value;
            TextureIdBack = value;
            TextureIdLeft = value;
            TextureIdRight = value;
        }
    }

    public string TopBottomTextures
    {
        set
        {
            TextureIdTop = value;
            TextureIdBottom = value;
        }
    }

    public bool IsFluid()
    {
        return DrawType == DrawType.Fluid;
    }

    public bool IsEmptyForPhysics()
    {
        return (DrawType == DrawType.Ladder)
            || (WalkableType != WalkableType.Solid && WalkableType != WalkableType.Fluid);
    }
}