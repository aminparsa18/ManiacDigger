public class InventoryUtilClient
{
    public InventoryUtilClient()
    {
        CellCountX = 12;
        CellCountY = 7 * 6;
    }

    internal Packet_Inventory d_Inventory;
    internal GameDataItemsClient d_Items;

    internal int CellCountX;
    internal int CellCountY;


    internal Point?[] ItemsAtArea(int pX, int pY, int sizeX, int sizeY, IntRef retCount)
    {
        Point?[] itemsAtArea = new Point?[256];
        int itemsAtAreaCount = 0;
        for (int xx = 0; xx < sizeX; xx++)
        {
            for (int yy = 0; yy < sizeY; yy++)
            {
                Point cell = new(pX + xx, pY + yy);
                if (!IsValidCell(cell))
                {
                    return null;
                }
                Point? itemAtCell = ItemAtCell(cell);
                if (itemAtCell != null)
                {
                    bool contains = false;
                    for (int i = 0; i < itemsAtAreaCount; i++)
                    {
                        if (itemsAtArea[i] == itemAtCell)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        itemsAtArea[itemsAtAreaCount++] = itemAtCell;
                    }
                }
            }
        }
        retCount.value = itemsAtAreaCount;
        return itemsAtArea;
    }

    public bool IsValidCell(Point p)
    {
        return !(p.X < 0 || p.Y < 0 || p.X >= CellCountX || p.Y >= CellCountY);
    }

    internal Packet_Item ItemAtWearPlace(int wearPlace, int activeMaterial)
    {
        return wearPlace switch
        {
            //case WearPlace.LeftHand: return d_Inventory.LeftHand[activeMaterial];
            WearPlace_.RightHand => d_Inventory.RightHand[activeMaterial],
            WearPlace_.MainArmor => d_Inventory.MainArmor,
            WearPlace_.Boots => d_Inventory.Boots,
            WearPlace_.Helmet => d_Inventory.Helmet,
            WearPlace_.Gauntlet => d_Inventory.Gauntlet,
            _ => null,
        };
    }

    internal Point? ItemAtCell(Point? p)
    {
        for (int i = 0; i < d_Inventory.ItemsCount; i++)
        {
            Packet_PositionItem k = d_Inventory.Items[i];
            Packet_Item item = k.Value_;
            for (int x = 0; x < d_Items.ItemSizeX(item); x++)
            {
                for (int y = 0; y < d_Items.ItemSizeY(item); y++)
                {
                    int px = k.X + x;
                    int py = k.Y + y;
                    if (p.Value.X == px && p.Value.Y == py)
                    {
                        return new Point(k.X, k.Y);
                    }
                }
            }
        }
        return null;
    }

    internal IntRef FreeHand(int ActiveMaterial_)
    {
        IntRef freehand = null;
        if (d_Inventory.RightHand[ActiveMaterial_] == null) return IntRef.Create(ActiveMaterial_);
        for (int i = 0; i < 10; i++)
        {
            if (d_Inventory.RightHand[i] == null)
            {
                return freehand;
            }
        }
        return null;
    }
}

public class GameDataItemsClient
{
    internal Game game;

    public string ItemInfo(Packet_Item item)
    {
        if (item.ItemClass == Packet_ItemClassEnum.Block)
        {
            return game.language.Get(StringTools.StringAppend(game.platform, "Block_", game.blocktypes[item.BlockId].Name));
        }
        game.platform.ThrowException("ItemClass");
        return "ItemClass";
    }
    public int ItemSizeX(Packet_Item item)
    {
        if (item.ItemClass == Packet_ItemClassEnum.Block)
        {
            return 1;
        }
        game.platform.ThrowException("ItemClass");
        return 1;
    }
    public int ItemSizeY(Packet_Item item)
    {
        if (item.ItemClass == Packet_ItemClassEnum.Block)
        {
            return 1;
        }
        game.platform.ThrowException("ItemClass");
        return 1;
    }
    public static Packet_Item Stack(Packet_Item itemA, Packet_Item itemB)
    {
        if (itemA.ItemClass == Packet_ItemClassEnum.Block
            && itemB.ItemClass == Packet_ItemClassEnum.Block)
        {
            //int railcountA = MyLinq.Count(DirectionUtils.ToRailDirections(d_Data.Rail[itemA.BlockId]));
            //int railcountB = MyLinq.Count(DirectionUtils.ToRailDirections(d_Data.Rail[itemB.BlockId]));
            //if ((itemA.BlockId != itemB.BlockId) && (!(railcountA > 0 && railcountB > 0)))
            //{
            //    return null;
            //}
            //todo stack size limit
            Packet_Item ret = new()
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
    public static bool CanWear(int selectedWear, Packet_Item item)
    {
        if (item == null) { return true; }
        if (item == null) { return true; }
        switch (selectedWear)
        {
            //case WearPlace.LeftHand: return false;
            case WearPlace_.RightHand: return item.ItemClass == Packet_ItemClassEnum.Block;
            case WearPlace_.MainArmor: return false;
            case WearPlace_.Boots: return false;
            case WearPlace_.Helmet: return false;
            case WearPlace_.Gauntlet: return false;
            default: return false;
        }
    }
    public static string ItemGraphics(Packet_Item item)
    {
        return null;
    }

    public int[] TextureIdForInventory()
    {
        return game.TextureIdForInventory;
    }
}

public class WearPlace_
{
    //LeftHand,
    public const int RightHand = 0;
    public const int MainArmor = 1;
    public const int Boots = 2;
    public const int Helmet = 3;
    public const int Gauntlet = 4;
}

public abstract class IInventoryController
{
    public abstract void InventoryClick(Packet_InventoryPosition pos);
    public abstract void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to);
    public abstract void MoveToInventory(Packet_InventoryPosition from);
}
