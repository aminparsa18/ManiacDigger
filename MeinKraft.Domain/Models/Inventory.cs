using MemoryPack;

namespace MeinKraft;

/// <summary>
/// Holds all items carried by a player: the hotbar, equipped armour slots,
/// a general item bag, and a drag-drop cursor item.
/// </summary>
/// <remarks>
/// The old ProtoBuf version stored <see cref="RightHand"/> as a
/// <c>Dictionary&lt;int, Item&gt;</c> workaround because ProtoBuf-net could not
/// serialize arrays containing null entries. MemoryPack handles nullable array
/// elements natively, so <see cref="RightHand"/> is a plain <c>Item?[]</c> again.
/// </remarks>
[MemoryPackable]
public partial class Inventory
{
    /// <summary>
    /// The ten hotbar slots. Null entries represent empty slots.
    /// Index 0 is the leftmost slot.
    /// </summary>
    public InventoryItem?[] RightHand { get; set; } = new InventoryItem[10];

    /// <summary>Body armour slot. <see langword="null"/> when empty.</summary>
    public InventoryItem? MainArmor { get; set; }

    /// <summary>Boots armour slot. <see langword="null"/> when empty.</summary>
    public InventoryItem? Boots { get; set; }

    /// <summary>Helmet armour slot. <see langword="null"/> when empty.</summary>
    public InventoryItem? Helmet { get; set; }

    /// <summary>Gauntlet armour slot. <see langword="null"/> when empty.</summary>
    public InventoryItem? Gauntlet { get; set; }

    /// <summary>
    /// General inventory bag keyed by grid position.
    /// Uses <see cref="GridPoint"/> as the key to match the existing save format.
    /// </summary>
    public Dictionary<GridPoint, InventoryItem> Items { get; set; } = [];

    /// <summary>
    /// The item currently held on the mouse cursor during a drag-drop operation.
    /// <see langword="null"/> when nothing is being dragged.
    /// </summary>
    public InventoryItem? DragDropItem { get; set; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Shallow-copies all slots from <paramref name="inventory"/> into this instance.
    /// </summary>
    public void CopyFrom(Inventory inventory)
    {
        RightHand = inventory.RightHand;
        MainArmor = inventory.MainArmor;
        Boots = inventory.Boots;
        Helmet = inventory.Helmet;
        Gauntlet = inventory.Gauntlet;
        Items = inventory.Items;
        DragDropItem = inventory.DragDropItem;
    }

    /// <summary>Creates a new empty <see cref="Inventory"/> with ten blank hotbar slots.</summary>
    public static Inventory Create() => new() { RightHand = new InventoryItem[10] };
}