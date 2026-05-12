using MemoryPack;

namespace MeinKraft;

/// <summary>
/// Represents a single item stack in the player's inventory.
/// An item is either a placeable block, a weapon, a piece of armour, or another object,
/// determined by <see cref="InventoryItemType"/>.
/// </summary>
[MemoryPackable]
public partial class InventoryItem
{
    /// <summary>Broad category of this item (block, weapon, armour slot, etc.).</summary>
    public InventoryItemType InventoryItemType { get; set; }

    /// <summary>
    /// String identifier for non-block items (e.g. weapon or equipment asset name).
    /// <see langword="null"/> for plain block items — use <see cref="BlockId"/> instead.
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// Block type ID when <see cref="InventoryItemType"/> is <see cref="InventoryItemType.Block"/>.
    /// Ignored for non-block items.
    /// </summary>
    public int BlockId { get; set; }

    /// <summary>Number of blocks or items in this stack. Defaults to 1.</summary>
    public int BlockCount { get; set; } = 1;
}