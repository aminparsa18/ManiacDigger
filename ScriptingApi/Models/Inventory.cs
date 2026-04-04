using ProtoBuf;
using System.Runtime.Serialization;

namespace ManicDigger;

[ProtoContract]
public class Inventory
{
    [OnDeserialized()]
    private void OnDeserialized()
    {
        /*
    LeftHand = new Item[10];
    if (LeftHandProto != null)
    {
        for (int i = 0; i < 10; i++)
        {
            if (LeftHandProto.ContainsKey(i))
            {
                LeftHand[i] = LeftHandProto[i];
            }
        }
    }
         */
        RightHand = new Item[10];
        if (RightHandProto != null)
        {
            for (int i = 0; i < 10; i++)
            {
                if (RightHandProto.ContainsKey(i))
                {
                    RightHand[i] = RightHandProto[i];
                }
            }
        }
    }
    [OnSerializing()]
    private void OnSerializing()
    {
        Dictionary<int, Item> d;// = new Dictionary<int, Item>();
        /*
    for (int i = 0; i < 10; i++)
    {
        if (LeftHand[i] != null)
        {
            d[i] = LeftHand[i];
        }
    }
    LeftHandProto = d;
         */
        d = [];
        for (int i = 0; i < 10; i++)
        {
            if (RightHand[i] != null)
            {
                d[i] = RightHand[i];
            }
        }
        RightHandProto = d;
    }
    //dictionary because protobuf-net can't serialize array of nulls.
    //[ProtoMember(1, IsRequired = false)]
    //public Dictionary<int, Item> LeftHandProto;
    [ProtoMember(2, IsRequired = false)]
    public Dictionary<int, Item> RightHandProto;
    //public Item[] LeftHand = new Item[10];
    public Item[] RightHand = new Item[10];
    [ProtoMember(3, IsRequired = false)]
    public Item MainArmor;
    [ProtoMember(4, IsRequired = false)]
    public Item Boots;
    [ProtoMember(5, IsRequired = false)]
    public Item Helmet;
    [ProtoMember(6, IsRequired = false)]
    public Item Gauntlet;
    [ProtoMember(7, IsRequired = false)]
    public Dictionary<ProtoPoint, Item> Items = [];
    [ProtoMember(8, IsRequired = false)]
    public Item DragDropItem;
    public void CopyFrom(Inventory inventory)
    {
        //this.LeftHand = inventory.LeftHand;
        this.RightHand = inventory.RightHand;
        this.MainArmor = inventory.MainArmor;
        this.Boots = inventory.Boots;
        this.Helmet = inventory.Helmet;
        this.Gauntlet = inventory.Gauntlet;
        this.Items = inventory.Items;
        this.DragDropItem = inventory.DragDropItem;
    }
    public static Inventory Create()
    {
        Inventory i = new()
        {
            //i.LeftHand = new Item[10];
            RightHand = new Item[10]
        };
        return i;
    }
}
