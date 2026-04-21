using ProtoBuf;
using ManicDigger;
using OpenTK.Mathematics;

//separate class because it's used by server and client.
public class InventoryUtil
{
    public InventoryUtil()
    {
        CellCountX = 12;
        CellCountY = 7 * 6;
    }
    public Inventory d_Inventory;
    public IGameDataItems d_Items;

    internal int CellCountX;
    internal int CellCountY;

    //returns null if area is invalid.
    public Point?[] ItemsAtArea(int pX, int pY, int sizeX, int sizeY, out int retCount)
    {
        Point?[] itemsAtArea = new Point?[256];
        int itemsAtAreaCount = 0;
        for (int xx = 0; xx < sizeX; xx++)
        {
            for (int yy = 0; yy < sizeY; yy++)
            {
                Point cell = new Point(pX + xx, pY + yy);
                if (!IsValidCell(cell))
                {
                    retCount = 0;
                    return null;
                }
                if (ItemAtCell(cell) != null)
                {
                    bool contains = false;
                    for (int i = 0; i < itemsAtAreaCount; i++)
                    {
                        if (itemsAtArea[i] == null)
                        {
                            continue;
                        }
                        if (itemsAtArea[i].Value.X == ItemAtCell(cell).Value.X
                            && itemsAtArea[i].Value.Y == ItemAtCell(cell).Value.Y)
                        {
                            contains = true;
                        }
                    }
                    if (!contains)
                    {
                        itemsAtArea[itemsAtAreaCount++] = ItemAtCell(cell);
                    }
                }
            }
        }
        retCount = itemsAtAreaCount;
        return itemsAtArea;
    }

    public bool IsValidCell(Point p)
    {
        return !(p.X < 0 || p.Y < 0 || p.X >= CellCountX || p.Y >= CellCountY);
    }

    public IEnumerable<Point> ItemCells(Point p)
    {
        Item item = d_Inventory.Items[new ProtoPoint(p.X, p.Y)];
        for (int x = 0; x < d_Items.ItemSizeX(item); x++)
        {
            for (int y = 0; y < d_Items.ItemSizeY(item); y++)
            {
                yield return new Point(p.X + x, p.Y + y);
            }
        }
    }

    public Point? ItemAtCell(Point p)
    {
        foreach (var k in d_Inventory.Items)
        {
            foreach (var pp in ItemCells(new Point(k.Key.X, k.Key.Y)))
            {
                if (p.X == pp.X && p.Y == pp.Y) { return new Point(k.Key.X, k.Key.Y); }
            }
        }
        return null;
    }

    public Item ItemAtWearPlace(int wearPlace, int activeMaterial)
    {
        return wearPlace switch
        {
            (int)WearPlace.RightHand => d_Inventory.RightHand[activeMaterial],
            (int)WearPlace.MainArmor => d_Inventory.MainArmor,
            (int)WearPlace.Boots => d_Inventory.Boots,
            (int)WearPlace.Helmet => d_Inventory.Helmet,
            (int)WearPlace.Gauntlet => d_Inventory.Gauntlet,
            _ => null,
        };
    }

    public void SetItemAtWearPlace(int wearPlace, int activeMaterial, Item item)
    {
        switch (wearPlace)
        {
            //case WearPlace.LeftHand: d_Inventory.LeftHand[activeMaterial] = item; break;
            case (int)WearPlace.RightHand: d_Inventory.RightHand[activeMaterial] = item; break;
            case (int)WearPlace.MainArmor: d_Inventory.MainArmor = item; break;
            case (int)WearPlace.Boots: d_Inventory.Boots = item; break;
            case (int)WearPlace.Helmet: d_Inventory.Helmet = item; break;
            case (int)WearPlace.Gauntlet: d_Inventory.Gauntlet = item; break;
            default: throw new Exception();
        }
    }

    public bool GrabItem(Item item, int ActiveMaterial)
    {
        switch (item.ItemClass)
        {
            case ItemClass.Block:
                if (item.BlockId == SpecialBlockId.Empty)
                {
                    return true;
                }
                //stacking
                for (int i = 0; i < 10; i++)
                {
                    if (d_Inventory.RightHand[i] == null)
                    {
                        continue;
                    }
                    Item result = d_Items.Stack(d_Inventory.RightHand[i], item);
                    if (result != null)
                    {
                        d_Inventory.RightHand[i] = result;
                        return true;
                    }
                }
                if (d_Inventory.RightHand[ActiveMaterial] == null)
                {
                    d_Inventory.RightHand[ActiveMaterial] = item;
                    return true;
                }
                //current hand
                if (d_Inventory.RightHand[ActiveMaterial].ItemClass == ItemClass.Block
                    && d_Inventory.RightHand[ActiveMaterial].BlockId == item.BlockId)
                {
                    d_Inventory.RightHand[ActiveMaterial].BlockCount++;
                    return true;
                }
                //any free hand
                for (int i = 0; i < 10; i++)
                {
                    if (d_Inventory.RightHand[i] == null)
                    {
                        d_Inventory.RightHand[i] = item;
                        return true;
                    }
                }
                //grab to main area - stacking
                for (int y = 0; y < CellCountY; y++)
                {
                    for (int x = 0; x < CellCountX; x++)
                    {
                        Point?[] p = ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                        if (p != null && pCount == 1)
                        {
                            var stacked = d_Items.Stack(d_Inventory.Items[new ProtoPoint(p[0].Value.X, p[0].Value.Y)], item);
                            if (stacked != null)
                            {
                                d_Inventory.Items[new ProtoPoint(x, y)] = stacked;
                                return true;
                            }
                        }
                    }
                }
                //grab to main area - adding
                for (int y = 0; y < CellCountY; y++)
                {
                    for (int x = 0; x < CellCountX; x++)
                    {
                        Point?[] p = ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                        if (p != null && pCount == 0)
                        {
                            d_Inventory.Items[new ProtoPoint(x, y)] = item;
                            return true;
                        }
                    }
                }
                return false;
            default:
                throw new NotImplementedException();
        }
    }

    public int? FreeHand(int ActiveMaterial)
    {
        int? freehand = null;
        if (d_Inventory.RightHand[ActiveMaterial] == null) return ActiveMaterial;
        for (int i = 0; i < d_Inventory.RightHand.Length; i++)
        {
            if (d_Inventory.RightHand[i] == null)
            {
                return freehand;
            }
        }
        return null;
    }
}

public enum InventoryPositionType
{
    MainArea,
    Ground,
    MaterialSelector,
    WearPlace,
}

[ProtoContract]
public class InventoryPosition
{
    [ProtoMember(1, IsRequired = false)]
    public InventoryPositionType type;
    [ProtoMember(2, IsRequired = false)]
    public int AreaX;
    [ProtoMember(3, IsRequired = false)]
    public int AreaY;
    [ProtoMember(4, IsRequired = false)]
    public int MaterialId;

    //WearPlace
    [ProtoMember(5, IsRequired = false)]
    public int WearPlace;
    [ProtoMember(6, IsRequired = false)]
    public int ActiveMaterial;
    [ProtoMember(7, IsRequired = false)]
    public int GroundPositionX;
    [ProtoMember(8, IsRequired = false)]
    public int GroundPositionY;
    [ProtoMember(9, IsRequired = false)]
    public int GroundPositionZ;

    public static InventoryPosition MaterialSelector(int materialId)
    {
        InventoryPosition pos = new()
        {
            type = InventoryPositionType.MaterialSelector,
            MaterialId = materialId
        };
        return pos;
    }

    public static InventoryPosition MainArea(Point point)
    {
        InventoryPosition pos = new()
        {
            type = InventoryPositionType.MainArea,
            AreaX = point.X,
            AreaY = point.Y
        };
        return pos;
    }
}

public interface IGameDataItems
{
    int ItemSizeX(Item item);
    int ItemSizeY(Item item);
    /// <summary>
    /// returns null if can't stack.
    /// </summary>
    Item Stack(Item itemA, Item itemB);
    bool CanWear(WearPlace selectedWear, Item item);
    string ItemGraphics(Item item);
}

public interface IDropItem
{
    void DropItem(ref Item item, Vector3i pos);
}

public class InventoryServer : IInventoryController
{
    public IGameDataItems? d_Items;
    public IDropItem? d_DropItem;

    public Inventory? d_Inventory;
    public InventoryUtil? d_InventoryUtil;

    public void InventoryClick(Packet_InventoryPosition pos)
    {
        if (pos.Type == PacketInventoryPositionType.MainArea)
        {
            Point? selected = null;
            foreach (var k in d_Inventory.Items)
            {
                if (pos.AreaX >= k.Key.X && pos.AreaY >= k.Key.Y
                    && pos.AreaX < k.Key.X + d_Items.ItemSizeX(k.Value)
                    && pos.AreaY < k.Key.Y + d_Items.ItemSizeY(k.Value))
                {
                    selected = new Point(k.Key.X, k.Key.Y);
                }
            }
            //drag
            if (selected != null && d_Inventory.DragDropItem == null)
            {
                d_Inventory.DragDropItem = d_Inventory.Items[new ProtoPoint(selected.Value.X, selected.Value.Y)];
                d_Inventory.Items.Remove(new ProtoPoint(selected.Value.X, selected.Value.Y));
                SendInventory();
            }
            //drop
            else if (d_Inventory.DragDropItem != null)
            {
                //make sure there is nothing blocking drop.
                Point?[] itemsAtArea = d_InventoryUtil.ItemsAtArea(pos.AreaX, pos.AreaY,
                    d_Items.ItemSizeX(d_Inventory.DragDropItem), d_Items.ItemSizeY(d_Inventory.DragDropItem), out int itemsAtAreaCount);
                if (itemsAtArea == null || itemsAtAreaCount > 1)
                {
                    //invalid area
                    return;
                }
                if (itemsAtAreaCount == 0)
                {
                    d_Inventory.Items.Add(new ProtoPoint(pos.AreaX, pos.AreaY), d_Inventory.DragDropItem);
                    d_Inventory.DragDropItem = null;
                }
                else //1
                {
                    var swapWith = itemsAtArea[0];
                    //try to stack                        
                    Item stackResult = d_Items.Stack(d_Inventory.Items[new ProtoPoint(swapWith.Value.X, swapWith.Value.Y)], d_Inventory.DragDropItem);
                    if (stackResult != null)
                    {
                        d_Inventory.Items[new ProtoPoint(swapWith.Value.X, swapWith.Value.Y)] = stackResult;
                        d_Inventory.DragDropItem = null;
                    }
                    else
                    {
                        //try to swap
                        //swap (swapWith, dragdropitem)
                        Item z = d_Inventory.Items[new ProtoPoint(swapWith.Value.X, swapWith.Value.Y)];
                        d_Inventory.Items.Remove(new ProtoPoint(swapWith.Value.X, swapWith.Value.Y));
                        d_Inventory.Items[new ProtoPoint(pos.AreaX, pos.AreaY)] = d_Inventory.DragDropItem;
                        d_Inventory.DragDropItem = z;
                    }
                }
                SendInventory();
            }
        }
        else if (pos.Type == PacketInventoryPositionType.Ground)
        {
            /*
            if (d_Inventory.DragDropItem != null)
            {
                d_DropItem.DropItem(ref d_Inventory.DragDropItem,
                    new Vector3i(pos.GroundPositionX, pos.GroundPositionY, pos.GroundPositionZ));
                SendInventory();
            }
            */
        }
        else if (pos.Type == PacketInventoryPositionType.MaterialSelector)
        {
            if (d_Inventory.DragDropItem == null && d_Inventory.RightHand[pos.MaterialId] != null)
            {
                d_Inventory.DragDropItem = d_Inventory.RightHand[pos.MaterialId];
                d_Inventory.RightHand[pos.MaterialId] = null;
            }
            else if (d_Inventory.DragDropItem != null && d_Inventory.RightHand[pos.MaterialId] == null)
            {
                if (d_Items.CanWear(WearPlace.RightHand, d_Inventory.DragDropItem))
                {
                    d_Inventory.RightHand[pos.MaterialId] = d_Inventory.DragDropItem;
                    d_Inventory.DragDropItem = null;
                }
            }
            else if (d_Inventory.DragDropItem != null && d_Inventory.RightHand[pos.MaterialId] != null)
            {
                if (d_Items.CanWear(WearPlace.RightHand, d_Inventory.DragDropItem))
                {
                    Item oldHand = d_Inventory.RightHand[pos.MaterialId];
                    d_Inventory.RightHand[pos.MaterialId] = d_Inventory.DragDropItem;
                    d_Inventory.DragDropItem = oldHand;
                }
            }
            SendInventory();
        }
        else if (pos.Type == PacketInventoryPositionType.WearPlace)
        {
            //just swap.
            Item wear = d_InventoryUtil.ItemAtWearPlace(pos.WearPlace, pos.ActiveMaterial);
            if (d_Items.CanWear((WearPlace)pos.WearPlace, d_Inventory.DragDropItem))
            {
                d_InventoryUtil.SetItemAtWearPlace(pos.WearPlace, pos.ActiveMaterial, d_Inventory.DragDropItem);
                d_Inventory.DragDropItem = wear;
            }
            SendInventory();
        }
        else
        {
            throw new Exception();
        }
    }

    private static void SendInventory()
    {
    }

    public void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to)
    {
        //todo
        ProtoPoint originPoint = new(from.AreaX, from.AreaY);
        if (from.Type == PacketInventoryPositionType.MainArea
            && to.Type == PacketInventoryPositionType.MaterialSelector
            && d_Inventory.RightHand[to.MaterialId] == null
            && d_Inventory.Items.ContainsKey(originPoint)
            && d_Items.CanWear(WearPlace.RightHand, d_Inventory.Items[originPoint]))
        {
            d_Inventory.RightHand[to.MaterialId] = d_Inventory.Items[originPoint];
            d_Inventory.Items.Remove(originPoint);
        }
    }

    public void MoveToInventory(Packet_InventoryPosition from)
    {
        //todo
        if (from.Type == PacketInventoryPositionType.MaterialSelector)
        {
            //duplicate code with GrabItem().
            Item item = d_Inventory.RightHand[from.MaterialId];
            if (item == null)
            {
                return;
            }
            //grab to main area - stacking
            for (int x = 0; x < d_InventoryUtil.CellCountX; x++)
            {
                for (int y = 0; y < d_InventoryUtil.CellCountY; y++)
                {
                    Point?[] p = d_InventoryUtil.ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                    if (p != null && pCount == 1)
                    {
                        var stacked = d_Items.Stack(d_Inventory.Items[new ProtoPoint(p[0].Value.X, p[0].Value.Y)], item);
                        if (stacked != null)
                        {
                            d_Inventory.Items[new ProtoPoint(x, y)] = stacked;
                            d_Inventory.RightHand[from.MaterialId] = null;
                            return;
                        }
                    }
                }
            }
            //grab to main area - adding
            for (int x = 0; x < d_InventoryUtil.CellCountX; x++)
            {
                for (int y = 0; y < d_InventoryUtil.CellCountY; y++)
                {
                    Point?[] p = d_InventoryUtil.ItemsAtArea(x, y, d_Items.ItemSizeX(item), d_Items.ItemSizeY(item), out int pCount);
                    if (p != null && pCount == 0)
                    {
                        d_Inventory.Items[new ProtoPoint(x, y)] = item;
                        d_Inventory.RightHand[from.MaterialId] = null;
                        return;
                    }
                }
            }
        }
    }
}
public class GameDataItemsBlocks : IGameDataItems
{
    public GameData? d_Data;

    public int ItemSizeX(Item item)
    {
        if (item.ItemClass == ItemClass.Block) { return 1; }
        throw new NotImplementedException();
    }

    public int ItemSizeY(Item item)
    {
        if (item.ItemClass == ItemClass.Block) { return 1; }
        throw new NotImplementedException();
    }

    public Item Stack(Item itemA, Item itemB)
    {
        if (itemA.ItemClass == ItemClass.Block
            && itemB.ItemClass == ItemClass.Block)
        {
            int railcountA = DirectionUtils.RailDirectionFlagsCount(d_Data.Rail()[itemA.BlockId]);
            int railcountB = DirectionUtils.RailDirectionFlagsCount(d_Data.Rail()[itemB.BlockId]);
            if ((itemA.BlockId != itemB.BlockId) && (!(railcountA > 0 && railcountB > 0)))
            {
                return null;
            }
            //todo stack size limit
            Item ret = new()
            {
                ItemClass = itemA.ItemClass,
                BlockId = itemA.BlockId,
                BlockCount = itemA.BlockCount + itemB.BlockCount
            };
            return ret;
        }
        else
        {
            return null;
        }
    }

    public bool CanWear(WearPlace selectedWear, Item item)
    {
        if (item == null) { return true; }
        if (item == null) { return true; }
        return selectedWear switch
        {
            //case WearPlace.LeftHand: return false;
            WearPlace.RightHand => item.ItemClass == ItemClass.Block,
            WearPlace.MainArmor => false,
            WearPlace.Boots => false,
            WearPlace.Helmet => false,
            WearPlace.Gauntlet => false,
            _ => throw new Exception(),
        };
    }

    public string ItemGraphics(Item item)
    {
        throw new NotImplementedException();
    }
}
