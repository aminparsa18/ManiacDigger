namespace MeinKraft;

/// <summary>
/// Published whenever a block is placed, removed, or replaced.
/// Both old and new IDs are carried so subscribers can decide whether
/// lighting, physics, or audio work is needed without re-reading the world.
///
/// Struct so MessagePipe passes it by value — no heap allocation per publish.
/// </summary>
public readonly struct BlockChangedEvent
{
    public readonly int WorldX;
    public readonly int WorldY;
    public readonly int WorldZ;

    /// <summary>Block type ID before the change. 0 = air.</summary>
    public readonly int OldBlockId;

    /// <summary>Block type ID after the change. 0 = air.</summary>
    public readonly int NewBlockId;

    public BlockChangedEvent(int worldX, int worldY, int worldZ, int oldBlockId, int newBlockId)
    {
        WorldX = worldX;
        WorldY = worldY;
        WorldZ = worldZ;
        OldBlockId = oldBlockId;
        NewBlockId = newBlockId;
    }
}