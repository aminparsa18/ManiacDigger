using MemoryPack;

namespace MeinKraft;

/// <summary>
/// Defines all properties of a block type on the server side, including
/// textures, physics, sounds, weapons, and special behaviour.
/// Serialised with MemoryPack for persistence and network transfer.
/// </summary>
[MemoryPackable]
public partial class BlockType
{
    // ── Textures ──────────────────────────────────────────────────────────────

    /// <summary>Texture asset name applied to the top face of this block.</summary>
    public string TextureIdTop { get; set; } = "Unknown";

    /// <summary>Texture asset name applied to the bottom face of this block.</summary>
    public string TextureIdBottom { get; set; } = "Unknown";

    /// <summary>Texture asset name applied to the front face of this block.</summary>
    public string TextureIdFront { get; set; } = "Unknown";

    /// <summary>Texture asset name applied to the back face of this block.</summary>
    public string TextureIdBack { get; set; } = "Unknown";

    /// <summary>Texture asset name applied to the left face of this block.</summary>
    public string TextureIdLeft { get; set; } = "Unknown";

    /// <summary>Texture asset name applied to the right face of this block.</summary>
    public string TextureIdRight { get; set; } = "Unknown";

    /// <summary>Texture asset name used when this block is displayed in the inventory.</summary>
    public string TextureIdForInventory { get; set; } = "Unknown";

    // ── Rendering ─────────────────────────────────────────────────────────────

    /// <summary>Controls how this block is rendered (solid, fluid, plant, ladder, etc.).</summary>
    public DrawType DrawType { get; set; }

    // ── Physics ───────────────────────────────────────────────────────────────

    /// <summary>Determines how the player interacts physically with this block (solid, fluid, or passable).</summary>
    public WalkableType WalkableType { get; set; }

    /// <summary>Rail sub-type for this block (0 = not a rail).</summary>
    public int Rail { get; set; }

    /// <summary>Walk speed multiplier applied while moving on this block. Default is 1 (normal speed).</summary>
    public float WalkSpeed { get; set; } = 1;

    /// <summary>
    /// When <see langword="true"/>, the player slides on this block
    /// with reduced friction (e.g. ice).
    /// </summary>
    public bool IsSlipperyWalk { get; set; }

    /// <summary>Contact damage dealt to the player per tick when standing in or on this block (e.g. lava).</summary>
    public int DamageToPlayer { get; set; } = 0;

    /// <summary>
    /// Block ID this block converts into when placed by a player.
    /// 0 means no conversion.
    /// </summary>
    public int WhenPlayerPlacesGetsConvertedTo { get; set; }

    // ── Lighting ──────────────────────────────────────────────────────────────

    /// <summary>Radius of emitted light in blocks. 0 means this block emits no light.</summary>
    public int LightRadius { get; set; }

    // ── Sounds ────────────────────────────────────────────────────────────────

    /// <summary>Walk, break, build, and clone sound variants for this block.</summary>
    public SoundSet Sounds { get; set; }

    // ── Inventory / interaction ───────────────────────────────────────────────

    /// <summary>Mining strength (time-to-break) for this block.</summary>
    public int Strength { get; set; }

    /// <summary>Internal name of this block type. Used for special-block lookup and debugging.</summary>
    public string Name { get; set; }

    /// <summary>When <see langword="true"/>, players can place this block in the world.</summary>
    public bool IsBuildable { get; set; }

    /// <summary>When <see langword="true"/>, right-clicking this block triggers a use action.</summary>
    public bool IsUsable { get; set; }

    /// <summary>When <see langword="true"/>, this block behaves as a holdable tool item.</summary>
    public bool IsTool { get; set; }

    /// <summary>Asset name of the hand image shown when this block is held.</summary>
    public string handimage { get; set; }

    /// <summary>Maximum interaction/pick distance override when this block is in use. 0 = default distance.</summary>
    public float PickDistanceWhenUsed { get; set; }

    // ── Weapon / firearm ──────────────────────────────────────────────────────

    /// <summary>When <see langword="true"/>, this block acts as a firearm.</summary>
    public bool IsPistol { get; set; }

    /// <summary>Sub-type of firearm behaviour (pistol, rifle, etc.).</summary>
    public PistolType PistolType { get; set; }

    /// <summary>Aim spread radius in world units. Smaller values are more accurate.</summary>
    public int AimRadius { get; set; }

    /// <summary>Recoil magnitude applied to the camera on each shot.</summary>
    public float Recoil { get; set; }

    /// <summary>Minimum time in seconds between consecutive shots.</summary>
    public float Delay { get; set; }

    /// <summary>Number of projectiles fired per shot (e.g. 1 for a pistol, higher for shotguns).</summary>
    public float BulletsPerShot { get; set; }

    /// <summary>Walk speed multiplier applied while this weapon is being used. Default is 1.</summary>
    public float WalkSpeedWhenUsed { get; set; } = 1;

    // ── Iron sights ───────────────────────────────────────────────────────────

    /// <summary>When <see langword="true"/>, this weapon supports iron-sights aiming mode.</summary>
    public bool IronSightsEnabled { get; set; }

    /// <summary>Walk speed multiplier applied while aiming down sights. Default is 1.</summary>
    public float IronSightsMoveSpeed { get; set; } = 1;

    /// <summary>Asset name of the iron-sights overlay image.</summary>
    public string IronSightsImage { get; set; }

    /// <summary>Aim spread radius while in iron-sights mode.</summary>
    public float IronSightsAimRadius { get; set; }

    /// <summary>Field-of-view override while in iron-sights mode. 0 = no override.</summary>
    public float IronSightsFov { get; set; }

    // ── Ammo ──────────────────────────────────────────────────────────────────

    /// <summary>Maximum rounds held in one magazine before a reload is required.</summary>
    public int AmmoMagazine { get; set; }

    /// <summary>Maximum total ammo the player can carry for this weapon.</summary>
    public int AmmoTotal { get; set; }

    /// <summary>Time in seconds required to reload this weapon.</summary>
    public float ReloadDelay { get; set; }

    // ── Explosives / projectiles ──────────────────────────────────────────────

    /// <summary>Radius of the explosion caused by this block's projectile. 0 = no explosion.</summary>
    public float ExplosionRange { get; set; }

    /// <summary>Duration in seconds before the projectile explodes after being fired.</summary>
    public float ExplosionTime { get; set; }

    /// <summary>
    /// Speed of the projectile in world units per second.
    /// 0 means the projectile travels instantaneously (hitscan).
    /// </summary>
    public float ProjectileSpeed { get; set; }

    /// <summary>When <see langword="true"/>, the projectile bounces off surfaces.</summary>
    public bool ProjectileBounce { get; set; }

    // ── Damage ────────────────────────────────────────────────────────────────

    /// <summary>Damage dealt to the body on a successful hit.</summary>
    public float DamageBody { get; set; }

    /// <summary>Damage dealt to the head on a successful hit (headshot multiplier source).</summary>
    public float DamageHead { get; set; }

    // ── Convenience setters ───────────────────────────────────────────────────

    /// <summary>
    /// Sets the same texture asset on all six faces and the inventory slot simultaneously.
    /// </summary>
    [MemoryPackIgnore]
    public string AllTextures
    {
        set => TextureIdTop = TextureIdBottom = TextureIdFront =
          TextureIdBack = TextureIdLeft = TextureIdRight =
          TextureIdForInventory = value;
    }

    /// <summary>Sets the same texture asset on all four side faces (front, back, left, right).</summary>
    [MemoryPackIgnore]
    public string SideTextures
    { 
        set => TextureIdFront = TextureIdBack = TextureIdLeft = TextureIdRight = value;
    }

    /// <summary>Sets the same texture asset on the top and bottom faces.</summary>
    [MemoryPackIgnore]
    public string TopBottomTextures
    { 
        set => TextureIdTop = TextureIdBottom = value;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when this block's draw type is <see cref="DrawType.Fluid"/>.
    /// </summary>
    public bool IsFluid() => DrawType == DrawType.Fluid;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="block"/> does not
    /// obstruct player movement (ladders and non-solid, non-fluid draw types).
    /// </summary>
    public static bool IsEmptyForPhysics(BlockType block)
        => block.DrawType == DrawType.Ladder
        || (block.WalkableType != WalkableType.Solid
            && block.WalkableType != WalkableType.Fluid);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="b"/> allows light
    /// to pass through it (everything except solid blocks and closed doors).
    /// </summary>
    public static bool IsTransparentForLight(BlockType b)
        => b.DrawType is not DrawType.Solid
        and not DrawType.ClosedDoor;


}